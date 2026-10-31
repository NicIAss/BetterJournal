using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace BetterJournal;

public sealed class LoreCatalogue
{
    public Dictionary<string, int> Totals { get; } = [];
    public Dictionary<string, string[]> Sources { get; } = [];
    public List<TapestryDesign> Tapestries { get; } = [];
    public Dictionary<string, string> TapestryNames { get; } = [];
    private readonly Dictionary<(string Domain, string Code, string Type), (TapestryDesign Design, string Piece)> pieces = [];

    // Sources identify received chapters and supply counts; never use them as reading text.
    public sealed class CountAsset
    {
        public string Code { get; set; } = "";
        public string[] Pieces { get; set; } = [];
    }

    public LoreCatalogue(ICoreClientAPI api)
    {
        var sources = Sources;
        foreach (var asset in api.Assets.GetMany<CountAsset>(api.Logger, "config/lore/").Values)
            if (!string.IsNullOrEmpty(asset.Code))
            {
                sources[asset.Code] = asset.Pieces ?? [];
                Totals[asset.Code] = sources[asset.Code].Length;
            }
        Tapestries.AddRange(BuildTapestries(api.World.Blocks.OfType<BlockTapestry>(), sources));
        // GetMatching returns the first matching wildcard: Rot* also catches Rotbeast*.
        // Resolve the most specific loaded name once, outside UI rendering/composition.
        foreach (string locale in new[] { Lang.DefaultLocale, Lang.CurrentLocale }.Distinct())
        {
            var names = new Dictionary<string, string>();
            foreach (var (location, translations) in api.Assets.GetMany<Dictionary<string, string>>(api.Logger, "lang/" + locale + ".json"))
                foreach (var (key, value) in translations)
                    names[key.Contains(':') ? key : location.Domain + ":" + key] = value;
            foreach (var design in Tapestries)
                if (FindTapestryName(design.NameKey, names) is { } title) TapestryNames[design.NameKey] = title;
        }
        foreach (var design in Tapestries)
            foreach (var tile in design.Pieces)
                pieces[(design.Key[..design.Key.IndexOf(':')], design.LoreCode, tile.Type)] = (design, tile.Type);
    }

    public static string? FindTapestryName(string key, IReadOnlyDictionary<string, string> names)
    {
        if (!key.Contains(':')) key = "game:" + key;
        if (names.TryGetValue(key, out string? exact)) return exact;
        return names.Where(n => n.Key.EndsWith('*') && n.Key.IndexOf('*') == n.Key.Length - 1 &&
                key.StartsWith(n.Key[..^1], StringComparison.Ordinal))
            .OrderByDescending(n => n.Key.Length).Select(n => n.Value).FirstOrDefault();
    }

    public static List<TapestryDesign> BuildTapestries(IEnumerable<BlockTapestry> blocks, IReadOnlyDictionary<string, string[]> sources)
    {
        var result = new List<TapestryDesign>();
        var keys = new HashSet<string>();
        var inferred = new HashSet<(string, string)>();
        foreach (var block in blocks)
        {
            // Wall carvings use the same game class, but belong to their own journal entries.
            if (block.FirstCodePart() == "wallcarving") continue;
            var sizes = block.Attributes?["sizes"].AsObject<Dictionary<string, string>>();
            if (sizes == null) continue;
            foreach (var (name, size) in sizes)
            {
                string key = block.Code.Domain + ":" + block.LoreCode + "/" + name;
                if (!keys.Add(key)) continue;
                string[] dimensions = size.Split('x');
                if (dimensions.Length != 2 || !int.TryParse(dimensions[0], out int cols) || !int.TryParse(dimensions[1], out int rows) ||
                    cols < 1 || rows < 1 || cols > 8 || rows > 8) continue;
                int chapter = block.Attributes?["noLoreEvent"].AsBool() == true ? -1 : block.GetLoreChapterId(name);
                sources.TryGetValue(block.LoreCode, out var assetPieces);
                string? source = chapter >= 0 ? LoreSource(block.Code.Domain, block.LoreCode, name, chapter, assetPieces ?? []) : null;
                var tiles = TapestryCollection.Pieces(block.Code.Domain, name, cols, rows);
                var design = new TapestryDesign(key, (block.Code.Domain == "game" ? "" : block.Code.Domain + ":") + block.LoreCode + "-" + tiles[0].Type,
                    block.LoreCode, chapter, cols, rows, tiles,
                    chapter >= 0 && inferred.Add((block.LoreCode, source ?? "id:" + chapter)), source);
                result.Add(design);
            }
        }
        return result;
    }

    private static string? LoreSource(string domain, string code, string name, int chapter, string[] sources)
    {
        // Vanilla names have stable lore keys, even when server block metadata and the
        // client's lore array use different numbering. The bloody schematic shares C's lore.
        string canonical = name == "schematic-c-bloody" ? "schematic-c" : name;
        string named = code + "lore-" + canonical;
        if (domain == "game" && code == "tapestry" && sources.Contains(named, StringComparer.Ordinal)) return named;
        return chapter < sources.Length ? sources[chapter] : null;
    }

    public string? ObservedPiece(BlockTapestry block, string type) =>
        pieces.TryGetValue((block.Code.Domain, block.LoreCode, type), out var piece)
            ? TapestryCollection.PieceKey(piece.Design, piece.Piece) : null;
}
