using System.Text.Json;

namespace BetterJournal;

// Both client progress records use version 1 with a Worlds dictionary.
// A new-format file always takes precedence over a leftover legacy file.
public class SavedDataFile<T>(string path, Action<string> warning, string? legacyPath = null,
    string legacyArchiveFolder = "legacy-modconfig") where T : class, new()
{
    public bool Writable { get; private set; } = true;
    private static bool Exists(string file) => File.Exists(file) || File.Exists(file + ".bak");
    private static T Read(string file)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(file));
        if (!json.RootElement.TryGetProperty("Version", out var version) || version.GetInt32() != 1)
            throw new NotSupportedException("Unrecognized saved-progress version");
        if (!json.RootElement.TryGetProperty("Worlds", out var worlds) || worlds.ValueKind != JsonValueKind.Object)
            throw new JsonException("Missing world records");
        return JsonSerializer.Deserialize<T>(json.RootElement) ?? throw new JsonException("Empty progress file");
    }
    private T LoadCurrent()
    {
        try { return Read(path); }
        catch (NotSupportedException ex) { Writable = false; warning(ex.Message + "; keeping files unchanged."); return new(); }
        catch (Exception ex) { warning("Could not read " + Path.GetFileName(path) + ": " + ex.Message); }
        try { var data = Read(path + ".bak"); warning("Recovered " + Path.GetFileName(path) + " from backup."); return data; }
        catch (Exception ex) { Writable = false; warning("Could not recover progress; keeping files unchanged: " + ex.Message); return new(); }
    }
    public T Load()
    {
        if (Exists(path))
        {
            var current = LoadCurrent();
            if (Writable && legacyPath != null && Exists(legacyPath))
            {
                // Finish an interrupted first migration before removing old files.
                if (!File.Exists(path + ".bak") && !Save(current)) return current;
                ArchiveLegacy();
            }
            return current;
        }
        if (legacyPath == null || !Exists(legacyPath)) return new();
        var legacy = new SavedDataFile<T>(legacyPath, warning);
        var imported = legacy.Load();
        if (!legacy.Writable)
        {
            Writable = false;
            warning("Legacy progress could not be migrated safely; keeping original files unchanged.");
            return imported;
        }
        // Commit and read back the complete snapshot before moving either old file.
        if (Save(imported) && Save(imported))
        {
            try { Read(path); Read(path + ".bak"); ArchiveLegacy(); }
            catch (Exception ex) { Writable = false; warning("Migration verification failed; retaining legacy files: " + ex.Message); }
        }
        return imported;
    }
    private void ArchiveLegacy()
    {
        if (legacyPath == null) return;
        foreach (var original in new[] { legacyPath, legacyPath + ".bak" })
        {
            if (!File.Exists(original)) continue;
            try
            {
                string folder = Path.Combine(Path.GetDirectoryName(path)!, legacyArchiveFolder);
                Directory.CreateDirectory(folder);
                string archived = Path.Combine(folder, Path.GetFileName(original));
                // Preserve an existing archive too, including a different old revision.
                if (File.Exists(archived)) archived += "." + Guid.NewGuid().ToString("N");
                File.Move(original, archived);
            }
            catch (Exception ex) { warning("Could not archive legacy progress; original retained: " + ex.Message); }
        }
    }
    public bool Save(T data)
    {
        if (!Writable) return false;
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, data); stream.Flush(true); }
            bool validPrimary = false;
            if (File.Exists(path))
                try { Read(path); validPrimary = true; }
                catch (NotSupportedException) { Writable = false; warning("Progress version changed; keeping file unchanged."); return false; }
                catch { }
            if (validPrimary) File.Replace(temporary, path, path + ".bak");
            else File.Move(temporary, path, true);
            return true;
        }
        catch (Exception ex) { warning("Could not save " + Path.GetFileName(path) + "; will retry: " + ex.Message); return false; }
    }
}
