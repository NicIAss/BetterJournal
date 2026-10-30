using Vintagestory.GameContent;

namespace BetterJournal;

public sealed record TapestryPiece(string Type, string Texture, int Column, int Row);
public sealed record TapestryDesign(string Key, string NameKey, string LoreCode, int ChapterId,
    int Columns, int Rows, IReadOnlyList<TapestryPiece> Pieces, bool InferFromLore = true, string? LoreSource = null);
public sealed record TapestryView(TapestryDesign Design, IReadOnlyList<bool> Seen, IReadOnlyList<string> Lore)
{
    public bool Discovered => Seen.Any(s => s);
    public int SeenCount => Seen.Count(s => s);
    public bool Complete => Design.Pieces.Count > 0 && Seen.Count == Design.Pieces.Count && Seen.All(s => s);
    public string Revision => Design.Key + ":" + string.Join("", Seen.Select(s => s ? "1" : "0")) + ":" + string.Join("\0", Lore);
}

// Pure projection: never unlocks journal chapters or reads undiscovered lore text.
public static class TapestryCollection
{
    public static bool ObservePiece(ISet<string> seen, string? piece, bool rotten) =>
        !rotten && piece != null && seen.Add(piece);

    public static IReadOnlyList<TapestryPiece> Pieces(string domain, string name, int columns, int rows)
    {
        var result = new List<TapestryPiece>();
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                string suffix = columns == 1 && rows == 1 ? "" : columns == 1 ? (row + 1).ToString()
                    : rows == 1 ? (col + 1).ToString() : $"{row + 1}{col + 1}";
                string type = name + suffix;
                result.Add(new(type, domain + ":textures/block/cloth/tapestry/" + type + ".png", col, row));
            }
        return result;
    }

    public static List<TapestryView> Read(IReadOnlyList<TapestryDesign> designs, IEnumerable<JournalEntry> journal,
        ISet<string> seen, Func<string, string> localize)
    {
        var chapters = journal.SelectMany(e => (e.Chapters ?? []).Select(c => (Code: e.LoreCode, Chapter: c))).ToArray();
        var bySource = chapters.ToLookup(c => (c.Code, c.Chapter.Text));
        var byId = chapters.ToLookup(c => (c.Code, c.Chapter.ChapterId));
        var knownSources = KnownSources(designs);
        return designs.Select(d =>
        {
            // IDs can be stale, missing or reordered in a server's saved journal. Prefer the
            // received source identity; never fill missing lore from the asset catalogue.
            var matching = d.LoreSource != null ? bySource[(d.LoreCode, d.LoreSource)]
                : byId[(d.LoreCode, d.ChapterId)].Where(c => !knownSources.Contains((c.Code, c.Chapter.Text)));
            var received = d.ChapterId < 0 ? [] : matching.Select(c => c.Chapter.Text ?? "")
                .Distinct(StringComparer.Ordinal).Select(localize).Distinct(StringComparer.Ordinal).ToArray();
            // Shared chapter IDs do not prove that an alternate design was seen.
            bool infer = d.InferFromLore && received.Length > 0;
            bool[] visible = d.Pieces.Select(p => infer || seen.Contains(PieceKey(d, p.Type))).ToArray();
            return new TapestryView(d, visible, visible.Any(s => s) ? received : []);
        }).ToList();
    }

    public static string PieceKey(TapestryDesign design, string type) => design.Key + "/" + type;

    public static IEnumerable<JournalEntry> BookEntries(IEnumerable<JournalEntry> entries, IReadOnlyList<TapestryDesign> designs)
    {
        var knownSources = KnownSources(designs);
        var mappedIds = designs.Where(d => d.ChapterId >= 0 && d.LoreSource == null)
            .Select(d => (d.LoreCode, d.ChapterId)).ToHashSet();
        foreach (var entry in entries)
        {
            if ((entry.Chapters?.Count ?? 0) == 0 && designs.Any(d => d.LoreCode == entry.LoreCode)) continue;
            var remaining = (entry.Chapters ?? []).Where(c => !knownSources.Contains((entry.LoreCode, c.Text)) &&
                !mappedIds.Contains((entry.LoreCode, c.ChapterId))).ToList();
            if (remaining.Count == (entry.Chapters?.Count ?? 0)) yield return entry;
            else if (remaining.Count > 0)
                yield return new JournalEntry { EntryId = entry.EntryId, LoreCode = entry.LoreCode, Title = entry.Title, Chapters = remaining };
        }
    }

    private static HashSet<(string, string?)> KnownSources(IEnumerable<TapestryDesign> designs) =>
        designs.Where(d => d.ChapterId >= 0 && d.LoreSource != null).Select(d => (d.LoreCode, d.LoreSource)).ToHashSet();
}

public sealed class TapestryDiscoveries
{
    public int Version { get; set; } = 1;
    public Dictionary<string, HashSet<string>> Worlds { get; set; } = [];

    public HashSet<string> ForWorld(string world, string player)
    {
        Worlds ??= [];
        string key = world.Length + ":" + world + player;
        if (!Worlds.TryGetValue(key, out var seen) || seen == null) Worlds[key] = seen = [];
        return seen;
    }
}
