using System.Reflection;
using BetterJournal;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

static class TapestryRoutingChecks
{
    public static void Run(Action<bool, string> check)
    {
        string game = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "GamePath").Value!;
        var blockAsset = JsonObject.FromJson(File.ReadAllText(Path.Combine(game, "assets/survival/blocktypes/cloth/tapestry.json")));
        var loreAsset = JsonObject.FromJson(File.ReadAllText(Path.Combine(game, "assets/survival/config/lore/tapestry.json")));
        string[] sources = loreAsset["pieces"].AsObject<string[]>()!;
        var assets = new Dictionary<string, string[]> { ["tapestry"] = sources };
        BlockTapestry Block(string side, JsonObject attributes) => new()
            { Code = new AssetLocation("game:tapestry-" + side), Attributes = attributes };
        var blocks = new[] { "north", "east", "south", "west" }.Select(s => Block(s, blockAsset["attributes"]));
        var designs = LoreCatalogue.BuildTapestries(blocks, assets);
        check(designs.Count == 31 && designs.Select(d => d.Key).Distinct().Count() == 31,
            "Installed vanilla metadata yields all 31 designs once across four block orientations");
        check(designs.Where(d => d.ChapterId >= 0).All(d => d.LoreSource == sources[d.ChapterId]) &&
            designs.Where(d => d.ChapterId < 0).All(d => d.LoreSource == null),
            "Every installed vanilla tapestry matches its actual lore source, including the shared bloody schematic");
        var english = JsonObject.FromJson(File.ReadAllText(Path.Combine(game, "assets/game/lang/en.json")))
            .AsObject<Dictionary<string, string>>()!.ToDictionary(n => n.Key.Contains(':') ? n.Key : "game:" + n.Key, n => n.Value);
        check(LoreCatalogue.FindTapestryName("tapestry-rotbeast11", english) == "Rotbeast" &&
            LoreCatalogue.FindTapestryName("tapestry-rot1", english) == "Rot" &&
            designs.All(d => LoreCatalogue.FindTapestryName(d.NameKey, english) != null),
            "All vanilla designs resolve names and Rotbeast prefers its specific translation over the earlier Rot wildcard");
        var customNames = new Dictionary<string, string> { ["example:tapestry-art*"] = "Broad", ["example:tapestry-art-special*"] = "Specific",
            ["example:tapestry-art-special1"] = "Exact", ["other:tapestry-art*"] = "Other domain" };
        check(LoreCatalogue.FindTapestryName("example:tapestry-art-special1", customNames) == "Exact" &&
            LoreCatalogue.FindTapestryName("example:tapestry-art-special2", customNames) == "Specific" &&
            LoreCatalogue.FindTapestryName("other:tapestry-art1", customNames) == "Other domain" &&
            LoreCatalogue.FindTapestryName("missing:tapestry-art1", customNames) == null,
            "Tapestry names prefer exact then specific wildcard translations and preserve mod domains");

        JournalChapter Chapter(int id, string text) => new() { EntryId = 0, ChapterId = id, Text = text };
        JournalEntry Entry(IEnumerable<JournalChapter> chapters) => new()
            { EntryId = 0, LoreCode = "tapestry", Title = "Tapestries", Chapters = chapters.ToList() };
        Journal Network(params JournalEntry[] entries) => SerializerUtil.Deserialize<Journal>(
            SerializerUtil.Serialize(new Journal { Entries = entries.ToList() }));
        List<TapestryView> Read(IReadOnlyList<TapestryDesign> catalogue, Journal journal, ISet<string>? seen = null,
            System.Func<string, string>? localize = null) => TapestryCollection.Read(catalogue, journal.Entries, seen ?? new HashSet<string>(), localize ?? (s => s));
        var all = Network(Entry(sources.Select((s, i) => Chapter(i, s))));
        var allViews = Read(designs, all);
        check(allViews.Where(v => v.Design.InferFromLore).All(v => v.Lore.SequenceEqual([sources[v.Design.ChapterId]])) &&
            allViews.Where(v => v.Design.ChapterId < 0).All(v => !v.Discovered && v.Lore.Count == 0),
            "All received vanilla lore routes to the correct artwork after the game's protobuf serialization");
        check(allViews.Single(v => v.Design.Key.EndsWith("/schematic-c")).Complete &&
            !allViews.Single(v => v.Design.Key.EndsWith("/schematic-c-bloody")).Discovered,
            "Shared lore reveals only the canonical schematic, preserving hidden alternate artwork");

        // Reproduce mismatched server block numbering and colliding saved chapter IDs.
        var serverAttributes = JsonObject.FromJson(blockAsset["attributes"].ToString()!);
        serverAttributes["loreChapterIds"].Token!["rot"] = 10;
        serverAttributes["loreChapterIds"].Token!["forlorn"] = 12;
        serverAttributes["sizes"].Token!["rot"] = "2x2";
        var serverDesigns = LoreCatalogue.BuildTapestries([Block("north", serverAttributes)], assets);
        var received = Network(Entry([
            Chapter(0, "tapestrylore-ambush"),
            Chapter(1, "tapestrylore-nightfall"), Chapter(1, "tapestrylore-nightfall"),
            Chapter(10, "tapestrylore-schematic-d"), Chapter(10, "tapestrylore-rotbeast"),
            Chapter(12, "tapestrylore-blackguard"), Chapter(12, "tapestrylore-blackguard"),
            Chapter(4, "tapestrylore-rot"), Chapter(13, "tapestrylore-forlorn"),
            Chapter(12, "mod:unmapped-story")
        ]));
        var views = Read(serverDesigns, received);
        TapestryView View(string name) => views.Single(v => v.Design.Key == "game:tapestry/" + name);
        check(View("ambush").Lore.SequenceEqual(["tapestrylore-ambush"]) &&
            View("nightfall").Lore.SequenceEqual(["tapestrylore-nightfall"]) &&
            View("blackguard").Lore.SequenceEqual(["tapestrylore-blackguard"]),
            "Duplicate Nightfall and Blackguard journal records display once while Ambush remains unchanged");
        check(View("rot").Lore.SequenceEqual(["tapestrylore-rot"]) && View("rot").SeenCount == 4 &&
            View("schematic-d").Lore.SequenceEqual(["tapestrylore-schematic-d"]) &&
            View("rotbeast").Lore.SequenceEqual(["tapestrylore-rotbeast"]),
            "Rot cannot absorb Schematic D or Rot beast despite colliding IDs and server-specific mosaic dimensions");
        check(View("forlorn").Lore.SequenceEqual(["tapestrylore-forlorn"]) && View("forlorn").Complete && View("blackguard").Complete,
            "Forlorn Hope and Blackguard retain separate lore and canonical inference even when server IDs collide");
        check(TapestryCollection.BookEntries(received.Entries, serverDesigns).Single().Chapters.Single().Text == "mod:unmapped-story" &&
            received.Entries.Single().Chapters.Count == 10,
            "Unknown mod lore with a colliding ID stays readable and the received server journal is never edited");
        received.Entries[0].Chapters.Add(SerializerUtil.Deserialize<JournalChapter>(
            SerializerUtil.Serialize(Chapter(99, "tapestrylore-nightfall"))));
        check(Read(serverDesigns, received).Select(v => v.Revision).SequenceEqual(views.Select(v => v.Revision)),
            "A repeated incremental chapter packet with a different ID cannot duplicate lore or retrigger a completed tapestry prompt");
        check(Read(serverDesigns, Network(received.Entries.ToArray())).Select(v => v.Revision).SequenceEqual(views.Select(v => v.Revision)),
            "Rejoining with the same duplicated full-journal snapshot leaves displayed content and acknowledgement revisions unchanged");

        var incomplete = Network(Entry([Chapter(10, "tapestrylore-schematic-d"), Chapter(10, "tapestrylore-rotbeast"),
            Chapter(12, "tapestrylore-blackguard")]));
        var observed = new HashSet<string>(serverDesigns.Single(d => d.Key.EndsWith("/forlorn")).Pieces
            .Select(p => "game:tapestry/forlorn/" + p.Type));
        var notReceived = Read(serverDesigns, incomplete, observed);
        check(!notReceived.Single(v => v.Design.Key.EndsWith("/rot")).Discovered &&
            notReceived.Single(v => v.Design.Key.EndsWith("/forlorn")).Complete &&
            notReceived.Single(v => v.Design.Key.EndsWith("/forlorn")).Lore.Count == 0,
            "Wrong-numbered chapters reveal no unrelated artwork and missing lore is never filled from assets");
        var missingIds = Network(Entry(sources.Select(s => Chapter(0, s))));
        check(Read(designs, missingIds).Where(v => v.Design.InferFromLore).All(v => v.Complete && v.Lore.Count == 1),
            "Legacy journals and vanilla discover-everything records with default chapter IDs route by source identity");
        var shuffled = Network(Entry(sources.Reverse().Select((s, i) => Chapter(100 + i, s))));
        check(Read(designs, shuffled).Select(v => v.Revision).SequenceEqual(allViews.Select(v => v.Revision)),
            "Reordering entries and changing every numeric chapter ID leaves tapestry content and acknowledgement revisions stable");

        var customBlock = new BlockTapestry { Code = new AssetLocation("example:tapestry-north"), Attributes = JsonObject.FromJson(
            "{sizes:{custom:'1x2'},loreChapterIds:{custom:2}}") };
        var custom = LoreCatalogue.BuildTapestries([customBlock], new Dictionary<string, string[]> { ["tapestry"] = ["x", "y", "example:custom-lore"] });
        var customReceived = Network(Entry([Chapter(8, "example:custom-lore"), Chapter(2, "example:unknown")]));
        check(Read(custom, customReceived).Single().Lore.SequenceEqual(["example:custom-lore"]) &&
            TapestryCollection.BookEntries(customReceived.Entries, custom).Single().Chapters.Single().Text == "example:unknown",
            "Modded BlockTapestry definitions use their loaded lore source without depending on vanilla naming");
        var fallback = custom.Single() with { LoreSource = null };
        check(Read([fallback], Network(Entry([Chapter(2, "Direct custom text"), Chapter(2, "Direct custom text")]))).Single().Lore
            .SequenceEqual(["Direct custom text"]),
            "Direct mod chapters retain deduplicated numeric fallback when source metadata is unavailable");
        var alias = custom.Single() with { Key = "example:tapestry/alias", ChapterId = 8, LoreSource = null };
        check(!Read([custom.Single(), alias], customReceived)[1].Discovered,
            "A numeric fallback cannot steal an identified source belonging to a different design");
        var aliases = Network(Entry([Chapter(2, "example:one"), Chapter(2, "example:two")]));
        check(Read([fallback], aliases, localize: _ => "Same resolved passage").Single().Lore.Count == 1,
            "Different source aliases resolving to identical lore do not duplicate displayed notes");
    }
}
