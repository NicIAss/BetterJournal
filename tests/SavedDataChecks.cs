using System.Text.Json;
using BetterJournal;

static class SavedDataChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterJournal-storage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var warnings = new List<string>();
        string path = Path.Combine(root, "ModData", "betterjournal", "tapestries.json");
        var discoveries = new TapestryDiscoveries();
        discoveries.ForWorld("save", "player").Add("game:tapestry/art/part1");
        discoveries.ForWorld("other-save", "player").Add("game:tapestry/other/part2");
        try
        {
            var file = new SavedDataFile<TapestryDiscoveries>(path, warnings.Add);
            check(file.Load().Worlds.Count == 0 && file.Writable && !File.Exists(path),
                "A first installation starts with empty local discovery history and creates no save until needed");
            check(file.Save(discoveries) && file.Save(discoveries) && File.Exists(path + ".bak") &&
                new SavedDataFile<TapestryDiscoveries>(path, warnings.Add).Load().ForWorld("save", "player")
                    .SetEquals(["game:tapestry/art/part1"]) &&
                new SavedDataFile<TapestryDiscoveries>(path, warnings.Add).Load().ForWorld("other-save", "player")
                    .SetEquals(["game:tapestry/other/part2"]),
                "Discovery storage preserves all world/player scopes and retains a valid previous snapshot");
            string valid = File.ReadAllText(path);
            File.WriteAllText(path, "broken primary");
            var recovered = new SavedDataFile<TapestryDiscoveries>(path, warnings.Add);
            var loaded = recovered.Load();
            check(loaded.ForWorld("save", "player").Contains("game:tapestry/art/part1") && recovered.Writable &&
                recovered.Save(loaded) && File.ReadAllText(path + ".bak") == valid,
                "A damaged discovery primary recovers from backup without overwriting the valid backup with damaged data");

            File.Delete(path);
            check(new SavedDataFile<TapestryDiscoveries>(path, warnings.Add).Load().ForWorld("save", "player")
                .Contains("game:tapestry/art/part1"), "A backup-only discovery save remains recoverable");
            File.WriteAllText(path, "{\"Version\":1,\"Worlds\":null}");
            File.WriteAllText(path + ".bak", "broken backup");
            var malformed = new SavedDataFile<TapestryDiscoveries>(path, warnings.Add);
            check(malformed.Load().Worlds.Count == 0 && !malformed.Writable && !malformed.Save(discoveries) &&
                File.ReadAllText(path).Contains("null") && File.ReadAllText(path + ".bak") == "broken backup",
                "Malformed discovery files stay untouched and cannot be replaced with empty history");
            File.WriteAllText(path, "{\"Version\":99,\"Worlds\":{}}");
            File.WriteAllText(path + ".bak", valid);
            var unsupported = new SavedDataFile<TapestryDiscoveries>(path, warnings.Add);
            check(unsupported.Load().Worlds.Count == 0 && !unsupported.Writable && !unsupported.Save(discoveries) &&
                File.ReadAllText(path).Contains("99") && File.ReadAllText(path + ".bak") == valid,
                "Unsupported progress formats are preserved without silently replacing them from backup");

            string blocked = Path.Combine(root, "blocked");
            File.WriteAllText(blocked, "a file occupies the required directory");
            var retry = new SavedDataFile<TapestryDiscoveries>(Path.Combine(blocked, "tapestries.json"), warnings.Add);
            bool failed = !retry.Save(discoveries);
            File.Delete(blocked);
            check(failed && retry.Save(discoveries) && retry.Load().ForWorld("save", "player")
                .Contains("game:tapestry/art/part1"), "Failed discovery writes can retry without losing in-memory progress");

            string unrelated = Path.Combine(root, "unrelated", "tapestries.json");
            Directory.CreateDirectory(Path.GetDirectoryName(unrelated)!);
            File.WriteAllText(unrelated, JsonSerializer.Serialize(discoveries));
            string fresh = Path.Combine(root, "fresh", "tapestries.json");
            check(new SavedDataFile<TapestryDiscoveries>(fresh, warnings.Add).Load().Worlds.Count == 0 &&
                File.Exists(unrelated) && !Directory.Exists(Path.GetDirectoryName(fresh)),
                "Loading a fresh save reads only its designated path and leaves unrelated files untouched");
        }
        finally { Directory.Delete(root, true); }
    }
}
