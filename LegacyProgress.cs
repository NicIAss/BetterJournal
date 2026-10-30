namespace BetterJournal;

// Prefer the latest development save location over its older ModConfig predecessor.
// SavedDataFile keeps any existing BetterJournal save authoritative and imports atomically.
public static class LegacyProgress
{
    public static (string Path, string ArchiveFolder) Find(string currentFolder, string configFolder, string file)
    {
        string previous = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(currentFolder)!, "lorebookshelf", file);
        return File.Exists(previous) || File.Exists(previous + ".bak")
            ? (previous, "legacy-lorebookshelf")
            : (System.IO.Path.Combine(configFolder, "lorebookshelf-" + file), "legacy-modconfig");
    }
}
