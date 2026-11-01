using System.Reflection;
using BetterJournal;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

public static class CatalogueAssetChecks
{
    public static void Run(Action<bool, string> check)
    {
        var warnings = new List<string>();
        var reads = new List<string>();
        var data = new Dictionary<string, object?>
        {
            ["broken:config/lore/first.json"] = new IOException("Missing backing file"),
            ["broken:config/lore/invalid.json"] = new FormatException("Malformed JSON"),
            ["broken:config/lore/null.json"] = null,
            ["game:config/lore/letters.json"] = new LoreCatalogue.CountAsset { Code = "letters", Pieces = ["a", "b"] },
            ["stablesurface:lang/en.json"] = new IOException("Requested asset [stablesurface:lang/en.json] could not be found"),
            ["game:lang/en.json"] = new Dictionary<string, string> { ["tapestry-rot*"] = "Rot", ["tapestry-rotbeast*"] = "Rotbeast" },
            ["game:lang/en.json.backup"] = new FormatException("Not a language asset"),
            ["example:lang/en.json"] = new Dictionary<string, string> { ["tapestry-art*"] = "Mod artwork" },
            ["game:lang/de.json"] = new IOException("Missing locale file"),
            ["example:lang/de.json"] = new Dictionary<string, string> { ["tapestry-art*"] = "Kunst" }
        };
        var assets = Fake<IAssetManager>((method, args) =>
        {
            if (method.Name == "GetLocations") return data.Keys.Select(k => new AssetLocation(k))
                .Where(k => k.Path.StartsWith((string)args![0]!)).ToList();
            if (method.Name == "Get" && method.IsGenericMethod)
            {
                string key = args![0]!.ToString()!;
                reads.Add(key);
                if (data[key] is Exception ex) throw ex;
                return data[key];
            }
            throw new InvalidOperationException("Unexpected asset API: " + method.Name);
        });
        var counts = CatalogueAssets.Read<LoreCatalogue.CountAsset>(assets, "config/lore/", warnings.Add).ToList();
        check(counts.Count == 1 && counts[0].Value.Pieces.Length == 2 && warnings.Count == 3,
            "Missing, malformed and null lore metadata cannot suppress later valid catalogues");

        var names = CatalogueAssets.Read<Dictionary<string, string>>(assets, "lang/en.json", warnings.Add,
            new HashSet<string> { "game", "example" }, exactPath: true).ToList();
        check(names.Count == 2 && !reads.Contains("stablesurface:lang/en.json") && !reads.Contains("game:lang/en.json.backup"),
            "Unrelated server-only mods and prefix-matching backup files are never loaded for tapestry names");
        data["game:lang/en.json"] = new IOException("Missing vanilla language origin");
        names = CatalogueAssets.Read<Dictionary<string, string>>(assets, "lang/en.json", warnings.Add,
            new HashSet<string> { "game", "example" }, exactPath: true).ToList();
        check(names.Count == 1 && names[0].Key.Domain == "example",
            "A failed relevant language origin leaves other tapestry mods available");
        var unavailable = Fake<IAssetManager>((_, _) => throw new IOException("Unavailable asset index"));
        check(!CatalogueAssets.Read<LoreCatalogue.CountAsset>(unavailable, "config/lore/", warnings.Add).Any(),
            "Unavailable optional asset index degrades to an empty catalogue");

        data["game:lang/en.json"] = new Dictionary<string, string> { ["tapestry-rot*"] = "Rot", ["tapestry-rotbeast*"] = "Rotbeast" };
        var blocks = new Block[]
        {
            new BlockTapestry { Code = new AssetLocation("game:tapestry-north"), Attributes = JsonObject.FromJson("{\"sizes\":{\"rotbeast\":\"2x2\"},\"noLoreEvent\":true}") },
            new BlockTapestry { Code = new AssetLocation("example:tapestry-north"), Attributes = JsonObject.FromJson("{\"sizes\":{\"art\":\"1x1\"},\"noLoreEvent\":true}") }
        };
        var world = Fake<IClientWorldAccessor>((method, _) => method.Name == "get_Blocks" ? blocks : throw new InvalidOperationException(method.Name));
        var logger = Fake<ILogger>((_, _) => null);
        var api = Fake<ICoreClientAPI>((method, _) => method.Name switch
        {
            "get_Assets" => assets, "get_World" => world, "get_Logger" => logger,
            _ => throw new InvalidOperationException(method.Name)
        });
        string originalLocale = Lang.CurrentLocale;
        try
        {
            Lang.ChangeLanguage("de");
            var catalogue = new LoreCatalogue(api);
            check(catalogue.Totals["letters"] == 2 && catalogue.Tapestries.Count == 2 &&
                catalogue.TapestryNames["tapestry-rotbeast11"] == "Rotbeast" && catalogue.TapestryNames["example:tapestry-art"] == "Kunst",
                "Full catalogue survives the reported missing asset, keeps English fallback and prefers the current locale");
        }
        finally { Lang.ChangeLanguage(originalLocale); }
        var empty = new LoreCatalogue();
        check(empty.Totals.Count == 0 && empty.Tapestries.Count == 0 &&
            JournalBooks.Read([new JournalEntry { LoreCode = "unknown", Title = "Recovered", Chapters = [new JournalChapter { Text = "Readable" }] }], s => s, empty.Totals).Count == 1,
            "Emergency catalogue fallback still displays recovered uncatalogued journal entries");
    }

    static T Fake<T>(System.Func<MethodInfo, object?[]?, object?> invoke) where T : class
    {
        T proxy = DispatchProxy.Create<T, Stub>();
        ((Stub)(object)proxy).Call = invoke;
        return proxy;
    }

    public class Stub : DispatchProxy
    {
        public System.Func<MethodInfo, object?[]?, object?> Call = null!;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => Call(method!, args);
    }
}
