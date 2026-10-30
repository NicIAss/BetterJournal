using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace BetterJournal;

public sealed class BetterJournalSystem : ModSystem
{
    private ICoreClientAPI? api;
    private ModJournal? journalSystem;
    private FieldInfo? journalField;
    private ActionConsumable<KeyCombination>? previousHandler;
    private ActionConsumable<KeyCombination>? handler;
    private BookshelfDialog? dialog;
    private long tick;
    private LoreCatalogue? catalogue;
    private TapestryDiscoveries discoveries = new();
    private HashSet<string> seen = [];
    private bool dirty, persistent;
    private long lastSave, lastRefresh;
    private SavedDataFile<TapestryDiscoveries>? discoveryFile;
    private ReadingProgressData readingData = new();
    private ReadingProgress reading = new([]);
    private ReadingProgressFile? readingFile;
    private bool readingDirty;
    private long lastReadingSave;

    public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

    public override void StartClientSide(ICoreClientAPI api)
    {
        this.api = api;
        // Finalize after the survival mod registers its existing, remappable journal hotkey.
        api.Event.LevelFinalize += Install;
        api.Event.LeaveWorld += LeaveWorld;
    }

    private void Install()
    {
        if (api == null || handler != null) return;
        journalSystem = api.ModLoader.GetModSystem<ModJournal>();
        journalField = typeof(ModJournal).GetField("ownJournal", BindingFlags.Instance | BindingFlags.NonPublic);
        var hotkey = api.Input.GetHotKeyByCode("journal");
        if (journalSystem == null || journalField?.FieldType != typeof(Journal) || hotkey == null)
        {
            api.Logger.Error("[BetterJournal] Journal integration unavailable; keeping the existing journal.");
            return;
        }
        previousHandler = hotkey.Handler;
        catalogue = new LoreCatalogue(api);
        persistent = !string.IsNullOrEmpty(api.World.SavegameIdentifier) && !string.IsNullOrEmpty(api.World.Player?.PlayerUID);
        try
        {
            string folder = api.GetOrCreateDataPath(Path.Combine("ModData", "betterjournal"));
            void Warn(string message) => api.Logger.Warning("[BetterJournal] {0}", message);
            var oldDiscoveries = LegacyProgress.Find(folder, GamePaths.ModConfig, "tapestries.json");
            var oldReading = LegacyProgress.Find(folder, GamePaths.ModConfig, "reading.json");
            discoveryFile = new SavedDataFile<TapestryDiscoveries>(Path.Combine(folder, "tapestries.json"), Warn,
                oldDiscoveries.Path, oldDiscoveries.ArchiveFolder);
            readingFile = new ReadingProgressFile(Path.Combine(folder, "reading.json"), Warn,
                oldReading.Path, oldReading.ArchiveFolder);
            discoveries = discoveryFile.Load();
            readingData = readingFile.Load();
        }
        catch (Exception ex)
        {
            api.Logger.Warning("[BetterJournal] Saved data unavailable; using temporary progress: {0}", ex.Message);
            discoveries = new(); readingData = new(); persistent = false;
        }
        seen = persistent ? discoveries.ForWorld(api.World.SavegameIdentifier, api.World.Player!.PlayerUID) : [];
        reading = persistent ? readingData.ForWorld(api.World.SavegameIdentifier, api.World.Player!.PlayerUID) : new([]);
        handler = Toggle;
        api.Input.SetHotKeyHandler("journal", handler);
        tick = api.Event.RegisterGameTickListener(_ => Tick(), 200);
        api.Logger.Notification("[BetterJournal] Journal reader enabled. Vanilla discovery and networking remain active.");
    }

    private List<JournalEntry> ReadEntries() =>
        (journalField?.GetValue(journalSystem) as Journal)?.Entries ?? [];

    private bool Toggle(KeyCombination combination)
    {
        if (api == null) return false;
        try
        {
            dialog ??= new BookshelfDialog(api, ReadEntries, catalogue!, () => seen, reading,
                () => readingDirty = true, SaveReading);
            if (dialog.IsOpened()) dialog.CloseAnimated();
            else dialog.TryOpen();
            return true;
        }
        catch (Exception ex)
        {
            api.Logger.Error("[BetterJournal] Could not open the reader: {0}", ex);
            dialog?.TryClose();
            return previousHandler?.Invoke(combination) ?? false;
        }
    }

    private void Tick()
    {
        if (api == null) return;
        if (readingDirty && api.ElapsedMilliseconds - lastReadingSave > 2000) SaveReading();
        var selection = api.World.Player?.CurrentBlockSelection;
        if (selection != null && !api.Gui.LoadedGuis.Any(g => g.IsOpened() && g.DisableMouseGrab) &&
            api.World.BlockAccessor.GetBlockEntity(selection.Position) is BlockEntityTapestry tapestry &&
            tapestry.Block is BlockTapestry block && tapestry.Type != null)
        {
            string? piece = catalogue?.ObservedPiece(block, tapestry.Type);
            if (TapestryCollection.ObservePiece(seen, piece, tapestry.Rotten)) dirty = true;
        }
        if (dirty && api.ElapsedMilliseconds - lastSave > 2000) SaveDiscoveries();
        if (dialog?.IsOpened() != true || api.ElapsedMilliseconds - lastRefresh < 750) return;
        lastRefresh = api.ElapsedMilliseconds;
        try { dialog.Refresh(); }
        catch (Exception ex) { api?.Logger.Error("[BetterJournal] Refresh failed: {0}", ex); dialog.TryClose(); }
    }

    private void SaveDiscoveries()
    {
        if (api == null || !dirty || !persistent || discoveryFile?.Writable != true) return;
        lastSave = api.ElapsedMilliseconds;
        if (discoveryFile.Save(discoveries)) dirty = false;
    }

    private void LeaveWorld()
    {
        if (api != null)
        {
            SaveDiscoveries();
            SaveReading();
            if (tick != 0) api.Event.UnregisterGameTickListener(tick);
            var hotkey = api.Input.GetHotKeyByCode("journal");
            if (handler != null && hotkey?.Handler == handler)
                hotkey.Handler = previousHandler;
        }
        tick = 0;
        handler = null;
        previousHandler = null;
        dialog?.TryClose();
        dialog?.Dispose();
        dialog = null;
        journalSystem = null;
        catalogue = null;
        seen = []; discoveries = new(); discoveryFile = null; dirty = persistent = false;
        readingData = new(); reading = new([]); readingFile = null; readingDirty = false; lastReadingSave = 0;
        lastSave = lastRefresh = 0;
    }

    private void SaveReading()
    {
        if (api == null || !readingDirty || !persistent || readingFile?.Writable != true) return;
        lastReadingSave = api.ElapsedMilliseconds;
        if (readingFile.Save(readingData)) readingDirty = false;
    }

    public override void Dispose()
    {
        LeaveWorld();
        if (api != null)
        {
            api.Event.LevelFinalize -= Install;
            api.Event.LeaveWorld -= LeaveWorld;
        }
        base.Dispose();
    }
}
