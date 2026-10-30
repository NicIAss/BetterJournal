using BetterJournal;

static class BrandingMigrationChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterJournal-renaming-" + Guid.NewGuid().ToString("N"));
        string current = Path.Combine(root, "ModData", "betterjournal");
        string config = Path.Combine(root, "ModConfig");
        string previous = Path.Combine(root, "ModData", "lorebookshelf");
        Directory.CreateDirectory(previous); Directory.CreateDirectory(config);
        var warnings = new List<string>();
        try
        {
            var book = new JournalBook(1, "test:story", "Story", 1, [new RecoveredPage(0, 0, "one two three four")], 2);
            var reading = new ReadingProgressData();
            reading.ForWorld("save", "player").Mark(book, [new ReadingPage(0, 1, "one two", 0, 2)]);
            string oldReading = Path.Combine(previous, "reading.json");
            new ReadingProgressFile(oldReading, warnings.Add).Save(reading);
            new ReadingProgressFile(oldReading, warnings.Add).Save(reading);
            string original = File.ReadAllText(oldReading), originalBackup = File.ReadAllText(oldReading + ".bak");
            File.WriteAllText(Path.Combine(config, "lorebookshelf-reading.json"), "stale and invalid");
            var legacy = LegacyProgress.Find(current, config, "reading.json");
            var migrated = new ReadingProgressFile(Path.Combine(current, "reading.json"), warnings.Add, legacy.Path, legacy.ArchiveFolder).Load();
            string archive = Path.Combine(current, "legacy-lorebookshelf");
            check(legacy.Path == oldReading && migrated.ForWorld("save", "player").UnreadFragments(book) == 1 &&
                migrated.ForWorld("save", "player").Bookmark(book) && File.Exists(Path.Combine(current, "reading.json.bak")) &&
                File.ReadAllText(Path.Combine(archive, "reading.json")) == original &&
                File.ReadAllText(Path.Combine(archive, "reading.json.bak")) == originalBackup && !File.Exists(oldReading),
                "BetterJournal renaming preserves development reading ranges/bookmarks and archives original primary/backup before preferring stale ModConfig");

            var discoveries = new TapestryDiscoveries();
            discoveries.ForWorld("save", "player").Add("game:tapestry/forlorn/forlorn1");
            discoveries.ForWorld("other-save", "player").Add("game:tapestry/rot/rot1");
            string oldTapestries = Path.Combine(previous, "tapestries.json");
            new SavedDataFile<TapestryDiscoveries>(oldTapestries, warnings.Add).Save(discoveries);
            File.Move(oldTapestries, oldTapestries + ".bak");
            legacy = LegacyProgress.Find(current, config, "tapestries.json");
            var seen = new SavedDataFile<TapestryDiscoveries>(Path.Combine(current, "tapestries.json"), warnings.Add, legacy.Path, legacy.ArchiveFolder).Load();
            check(seen.ForWorld("save", "player").SetEquals(["game:tapestry/forlorn/forlorn1"]) &&
                seen.ForWorld("other-save", "player").SetEquals(["game:tapestry/rot/rot1"]) &&
                seen.ForWorld("save", "other-player").Count == 0 && File.Exists(Path.Combine(archive, "tapestries.json.bak")),
                "Renaming imports backup-only development tapestry discoveries while preserving world/player isolation");

            var newer = new ReadingProgressData();
            new ReadingProgressFile(Path.Combine(current, "reading.json"), warnings.Add).Save(newer);
            File.WriteAllText(oldReading, original);
            legacy = LegacyProgress.Find(current, config, "reading.json");
            check(new ReadingProgressFile(Path.Combine(current, "reading.json"), warnings.Add, legacy.Path, legacy.ArchiveFolder).Load().Worlds.Count == 0,
                "Existing BetterJournal progress remains authoritative when development files reappear");

            File.WriteAllText(Path.Combine(config, "lorebookshelf-reading.json"), original);
            legacy = LegacyProgress.Find(current, config, "reading.json");
            check(legacy.Path == Path.Combine(config, "lorebookshelf-reading.json") && legacy.ArchiveFolder == "legacy-modconfig",
                "The renamed mod still finds the oldest ModConfig history when no development ModData remains");
        }
        finally { Directory.Delete(root, true); }
    }
}
