using System.Text.Json;
using BetterJournal;

static class SavedDataChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterJournal-storage-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var warnings = new List<string>();
        (string Old, string New) Paths(string name)
        {
            string folder = Path.Combine(root, name);
            string old = Path.Combine(folder, "ModConfig", "lorebookshelf-reading.json");
            Directory.CreateDirectory(Path.GetDirectoryName(old)!);
            return (old, Path.Combine(folder, "ModData", "lorebookshelf", "reading.json"));
        }
        var book = new JournalBook(1, "mod:story", "Story", 1, [new RecoveredPage(0, 0, "one two three four")], 2);
        var data = new ReadingProgressData();
        data.ForWorld("save", "player").Mark(book, [new ReadingPage(0, 1, "one two", 0, 2)]);
        var design = new TapestryDesign("mod:art", "art", "tapestry", -1, 1, 1,
            TapestryCollection.Pieces("mod", "art", 1, 1));
        var tapestry = new TapestryView(design, [true], []);
        data.ForWorld("save", "player").ViewTapestry(tapestry);
        try
        {
            var p = Paths("normal");
            var oldFile = new ReadingProgressFile(p.Old, warnings.Add);
            oldFile.Save(data); oldFile.Save(data);
            string original = File.ReadAllText(p.Old), backup = File.ReadAllText(p.Old + ".bak");
            var migrated = new ReadingProgressFile(p.New, warnings.Add, p.Old).Load();
            var memory = migrated.ForWorld("save", "player");
            string archive = Path.Combine(Path.GetDirectoryName(p.New)!, "legacy-modconfig");
            check(memory.UnreadFragments(book) == 1 && memory.Bookmark(book) && !memory.UnreadTapestry(tapestry) &&
                migrated.Worlds.Values.Single()[ReadingProgress.BookKey(book)].LastOffset == 0 &&
                migrated.Worlds.Values.Single()[ReadingProgress.BookKey(book)].Fragments.Values.Single().Single().End == 2 &&
                migrated.ForWorld("save", "other-player").UnreadTapestry(tapestry) &&
                File.Exists(p.New) && File.Exists(p.New + ".bak") && !File.Exists(p.Old) && !File.Exists(p.Old + ".bak") &&
                File.ReadAllText(Path.Combine(archive, Path.GetFileName(p.Old))) == original &&
                File.ReadAllText(Path.Combine(archive, Path.GetFileName(p.Old) + ".bak")) == backup,
                "ModConfig migration preserves book ranges, bookmarks, tapestry acknowledgements, player isolation and original backups");

            string legacyTapestry = Path.Combine(Path.GetDirectoryName(p.Old)!, "lorebookshelf-tapestries.json");
            string currentTapestry = Path.Combine(Path.GetDirectoryName(p.New)!, "tapestries.json");
            var discoveries = new TapestryDiscoveries();
            discoveries.ForWorld("save", "player").Add("mod:art/art");
            discoveries.ForWorld("other-save", "player").Add("mod:other/part1");
            string tapestryJson = JsonSerializer.Serialize(discoveries, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(legacyTapestry, tapestryJson);
            var discoveryFile = new SavedDataFile<TapestryDiscoveries>(currentTapestry, warnings.Add, legacyTapestry);
            var migratedDiscoveries = discoveryFile.Load();
            check(migratedDiscoveries.ForWorld("save", "player").SetEquals(["mod:art/art"]) &&
                migratedDiscoveries.ForWorld("other-save", "player").SetEquals(["mod:other/part1"]) &&
                migratedDiscoveries.ForWorld("save", "other-player").Count == 0 &&
                !File.Exists(legacyTapestry) && File.ReadAllText(Path.Combine(archive, Path.GetFileName(legacyTapestry))) == tapestryJson,
                "Tapestry discovery migration preserves all world/player scopes and clears its legacy config file");

            memory.Mark(book, [new ReadingPage(0, 1, "three four", 2, 4)]);
            new ReadingProgressFile(p.New, warnings.Add).Save(migrated);
            File.WriteAllText(p.Old, original);
            var reloaded = new ReadingProgressFile(p.New, warnings.Add, p.Old).Load();
            check(reloaded.ForWorld("save", "player").UnreadFragments(book) == 0 && !File.Exists(p.Old) &&
                Directory.GetFiles(archive, Path.GetFileName(p.Old) + "*").Length == 3 &&
                File.ReadAllText(Path.Combine(archive, Path.GetFileName(p.Old))) == original,
                "Restarted migration never reimports stale history or overwrites an earlier migration archive");

            p = Paths("cleanup-retry");
            File.WriteAllText(p.Old, original);
            string blockedArchive = Path.Combine(Path.GetDirectoryName(p.New)!, "legacy-modconfig");
            Directory.CreateDirectory(Path.GetDirectoryName(blockedArchive)!);
            File.WriteAllText(blockedArchive, "blocked");
            var cleanupData = new ReadingProgressFile(p.New, warnings.Add, p.Old).Load();
            bool retained = File.Exists(p.Old) && File.Exists(p.New);
            cleanupData.ForWorld("save", "player").Mark(book, [new ReadingPage(0, 1, "", 2, 4)]);
            new ReadingProgressFile(p.New, warnings.Add).Save(cleanupData);
            File.Delete(blockedArchive);
            var cleanupReloaded = new ReadingProgressFile(p.New, warnings.Add, p.Old).Load();
            check(retained && !File.Exists(p.Old) && cleanupReloaded.ForWorld("save", "player").UnreadFragments(book) == 0,
                "Interrupted legacy cleanup retries without losing newer ModData progress");

            p = Paths("legacy-recovery");
            File.WriteAllText(p.Old, "broken primary"); File.WriteAllText(p.Old + ".bak", original);
            var recovered = new ReadingProgressFile(p.New, warnings.Add, p.Old).Load();
            check(!recovered.ForWorld("save", "player").UnreadTapestry(tapestry) &&
                File.ReadAllText(Path.Combine(Path.GetDirectoryName(p.New)!, "legacy-modconfig", Path.GetFileName(p.Old))) == "broken primary" &&
                !File.Exists(p.Old + ".bak"),
                "A damaged legacy primary migrates from its valid backup and retains the damaged original for recovery");

            p = Paths("backup-only"); File.WriteAllText(p.Old + ".bak", original);
            check(!new ReadingProgressFile(p.New, warnings.Add, p.Old).Load().ForWorld("save", "player").UnreadTapestry(tapestry) &&
                File.Exists(p.New) && !File.Exists(p.Old + ".bak"), "A legacy backup alone can restore and migrate saved progress");

            p = Paths("bad-legacy"); File.WriteAllText(p.Old, "bad"); File.WriteAllText(p.Old + ".bak", "also bad");
            var invalid = new ReadingProgressFile(p.New, warnings.Add, p.Old); invalid.Load();
            check(!invalid.Writable && !File.Exists(p.New) && File.ReadAllText(p.Old) == "bad" && File.ReadAllText(p.Old + ".bak") == "also bad",
                "Unreadable legacy files stay untouched and cannot be replaced by empty ModData history");

            p = Paths("future-legacy"); File.WriteAllText(p.Old, "{\"Version\":99,\"Worlds\":{}}"); File.WriteAllText(p.Old + ".bak", original);
            var future = new ReadingProgressFile(p.New, warnings.Add, p.Old); future.Load();
            check(!future.Writable && !File.Exists(p.New) && File.ReadAllText(p.Old).Contains("99") && File.Exists(p.Old + ".bak"),
                "An unknown legacy schema is preserved without silently importing an older backup");

            p = Paths("bad-tapestry"); File.WriteAllText(p.Old, "{\"Version\":1,\"Worlds\":null}");
            var badDiscoveries = new SavedDataFile<TapestryDiscoveries>(p.New, warnings.Add, p.Old); badDiscoveries.Load();
            check(!badDiscoveries.Writable && File.Exists(p.Old) && !File.Exists(p.New),
                "Malformed tapestry discoveries cannot silently migrate as an empty collection");

            p = Paths("future-current"); Directory.CreateDirectory(Path.GetDirectoryName(p.New)!);
            File.WriteAllText(p.New, "{\"Version\":99,\"Worlds\":{}}"); File.WriteAllText(p.Old, original);
            var futureCurrent = new ReadingProgressFile(p.New, warnings.Add, p.Old); futureCurrent.Load();
            check(!futureCurrent.Writable && File.ReadAllText(p.New).Contains("99") && File.Exists(p.Old),
                "Unknown ModData schemas block migration and retain both locations unchanged");

            File.WriteAllText(p.New, "bad current"); File.WriteAllText(p.New + ".bak", "bad current backup");
            var badCurrent = new ReadingProgressFile(p.New, warnings.Add, p.Old); badCurrent.Load();
            check(!badCurrent.Writable && File.ReadAllText(p.New) == "bad current" && File.Exists(p.Old),
                "Damaged ModData does not silently fall back to stale legacy progress");

            p = Paths("write-retry"); File.WriteAllText(p.Old, original);
            string blockedFolder = Path.GetDirectoryName(p.New)!;
            Directory.CreateDirectory(Path.GetDirectoryName(blockedFolder)!); File.WriteAllText(blockedFolder, "blocked");
            var writeFailure = new ReadingProgressFile(p.New, warnings.Add, p.Old);
            var inMemory = writeFailure.Load();
            bool readable = !inMemory.ForWorld("save", "player").UnreadTapestry(tapestry) && File.Exists(p.Old) && !File.Exists(p.New);
            File.Delete(blockedFolder);
            check(readable && !new ReadingProgressFile(p.New, warnings.Add, p.Old).Load().ForWorld("save", "player").UnreadTapestry(tapestry) && !File.Exists(p.Old),
                "Failed destination writes retain legacy progress in memory and migrate safely on retry");

            p = Paths("empty-current"); new ReadingProgressFile(p.New, warnings.Add).Save(new()); File.WriteAllText(p.Old, original);
            check(new ReadingProgressFile(p.New, warnings.Add, p.Old).Load().Worlds.Count == 0 && !File.Exists(p.Old) &&
                File.Exists(Path.Combine(Path.GetDirectoryName(p.New)!, "legacy-modconfig", Path.GetFileName(p.Old))),
                "An intentionally empty current history stays authoritative while old history is archived");

            discoveryFile.Save(migratedDiscoveries); discoveryFile.Save(migratedDiscoveries);
            File.WriteAllText(currentTapestry, "broken");
            var recoveredDiscoveries = new SavedDataFile<TapestryDiscoveries>(currentTapestry, warnings.Add).Load();
            check(File.Exists(currentTapestry + ".bak") && recoveredDiscoveries.ForWorld("save", "player").Contains("mod:art/art"),
                "Tapestry discoveries now share atomic backup writes and damaged-primary recovery");
        }
        finally { Directory.Delete(root, true); }
    }
}
