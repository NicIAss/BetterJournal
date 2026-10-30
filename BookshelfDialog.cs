using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace BetterJournal;

public sealed partial class BookshelfDialog : GuiDialog
{
    private readonly Func<List<JournalEntry>> readEntries;
    private readonly BookMotion motion;
    private readonly WindowMotion windowMotion;
    private readonly PageLayoutCache pageLayouts = new();
    private readonly BookMaterials materials;
    private bool entering, composeRequested, closingWindow, closedIncoming, animateCompose;
    private Action? composeAfter;
    private int composeGeneration;
    private bool Busy => motion.Active || windowMotion.Active || pendingCompose || textUpdateRequested || closingWindow || entering;
    private bool textUpdateRequested;
    private readonly LoreCatalogue catalogue;
    private readonly Func<HashSet<string>> readSeen;
    private readonly ReadingProgress reading;
    private readonly Action readingChanged, saveReading;
    private bool restoreReadingPosition, readingMarked;
    private readonly ReadingExposure readingExposure = new();
    private List<TapestryView> tapestries = [];
    private TapestryView? selectedTapestry;
    private bool gallery;
    private int galleryPage, perGallery = 1, lorePage;
    private List<string> tapestryPages = [];
    private List<JournalBook> books = [];
    private JournalBook? selected, laidOutBook;
    private List<ReadingPage> readingPages = [];
    private int shelf, page, perShelf = 1, perSpread = 2, fontSize = 17;
    private int layoutFont, lastFrameWidth, lastFrameHeight;
    private double width, height, lastScale, layoutWidth, layoutHeight, layoutScale;
    private bool pendingCompose, swipe;
    private int swipeX, swipeY;
    private const double PaperY = ReadingLayout.PaperTop;
    private const double HeaderHeight = 42;
    private double PaperHeight => ReadingLayout.PaperHeight(height);
    private double LeafWidth => (width - 56) / perSpread;
    private double TextWidth => LeafWidth - 64;
    private double TextHeight => PaperHeight - 88;

    public BookshelfDialog(ICoreClientAPI api, Func<List<JournalEntry>> readEntries, LoreCatalogue catalogue, Func<HashSet<string>> readSeen,
        ReadingProgress reading, Action readingChanged, Action saveReading) : base(api)
    { this.readEntries = readEntries; this.catalogue = catalogue; this.readSeen = readSeen; this.reading = reading;
        this.readingChanged = readingChanged; this.saveReading = saveReading;
        materials = new BookMaterials(api); motion = new BookMotion(api, materials); windowMotion = new WindowMotion(api); }
    public override string ToggleKeyCombinationCode => "journal";
    public override bool PrefersUngrabbedMouse => true;
    public override bool DisableMouseGrab => true;
    private static string L(string key, params object[] args) => Lang.Get("betterjournal:" + key, args);
    private static string Fragments(int count) => count == 1 ? L("fragment-one") : L("fragments", count);
    private static string Progress(JournalBook book) => book.TotalFragments is int total
        ? L("progress", book.FragmentCount, total) + " · " + (book.FragmentCount == total ? L("complete") : L("missing", total - book.FragmentCount))
        : Fragments(book.FragmentCount) + " · " + L("total-unknown");
    private List<JournalBook> ReadBooks() => JournalBooks.Read(TapestryCollection.BookEntries(readEntries(), catalogue.Tapestries),
        text => text.Length == 0 ? "" : Lang.Get(text), catalogue.Totals);
    private List<TapestryView> ReadTapestries() => TapestryCollection.Read(catalogue.Tapestries, readEntries(), readSeen(),
        text => text.Length == 0 ? "" : Lang.Get(text)).OrderByDescending(v => v.Discovered).ToList();
    private CairoFont Font(float size, bool paper = false) =>
        new CairoFont(size, GuiStyle.DecorativeFontName, paper ? [0.20, 0.145, 0.095, 1] : [0.88, 0.81, 0.68, 1])
            .WithLineHeightMultiplier(1.15);

    public override void OnGuiOpened()
    {
        composeGeneration++;
        motion.Cancel(); windowMotion.Cancel(); textUpdateRequested = false;
        books = ReadBooks(); tapestries = ReadTapestries();
        ResolveSelection(); restoreReadingPosition = true; ComposeView();
        entering = true;
        closedIncoming = selected != null;
        if (closedIncoming) { CaptureBook(); composeAfter = BeginBookOpen; }
    }
    public void Refresh()
    {
        if (Busy || capi.Render.FrameWidth == 0 || capi.Render.FrameHeight == 0) return;
        var current = ReadBooks(); var currentTapestries = ReadTapestries();
        if (!JournalBooks.SameContent(books, current) || !tapestries.Select(t => t.Revision).SequenceEqual(currentTapestries.Select(t => t.Revision)))
        { books = current; tapestries = currentTapestries; ResolveSelection(); ScheduleCompose(); }
        else if (lastFrameWidth != capi.Render.FrameWidth || lastFrameHeight != capi.Render.FrameHeight || lastScale != RuntimeEnv.GUIScale)
            ScheduleCompose(animate: false);
    }
    private void ResolveSelection()
    {
        if (selected != null)
            selected = books.FirstOrDefault(b => ReadingProgress.BookKey(b) == ReadingProgress.BookKey(selected));
        if (selectedTapestry != null) selectedTapestry = tapestries.FirstOrDefault(t => t.Design.Key == selectedTapestry.Design.Key);
    }
    // Defer composition: input dispatch is iterating the current elements.
    private void ScheduleCompose(Action? after = null, bool animate = true)
    {
        if (pendingCompose) return;
        pendingCompose = true;
        int generation = composeGeneration;
        capi.Event.EnqueueMainThreadTask(() =>
        {
            if (generation != composeGeneration) return;
            if (!IsOpened()) { pendingCompose = false; return; }
            // Snapshot drawing needs the game's GUI shader and projection,
            // which are active in OnRenderGUI, not in the task queue.
            composeRequested = true; composeAfter = after; animateCompose = animate;
        }, "betterjournal-compose");
    }
    private void ComposeView()
    {
        readingExposure.Reset(); readingMarked = false;
        lastFrameWidth = capi.Render.FrameWidth; lastFrameHeight = capi.Render.FrameHeight; lastScale = RuntimeEnv.GUIScale;
        height = Math.Max(320, Math.Min(lastFrameHeight / lastScale - 48 - HeaderHeight, 760));
        double availableWidth = Math.Max(280, lastFrameWidth / lastScale - 48);
        if (selected == null) width = Math.Min(availableWidth, 1040);
        else
        {
            perSpread = ReadingLayout.Spread(availableWidth, height);
            width = ReadingLayout.ReaderWidth(availableWidth, height);
        }
        ClearComposers();
        SingleComposer = capi.Gui.CreateCompo("betterjournal",
            ElementBounds.Fixed(0, 0, width, height + HeaderHeight).WithAlignment(EnumDialogArea.CenterMiddle));
        SingleComposer.AddShadedDialogBG(ElementBounds.Fixed(0, 0, width, height + HeaderHeight), true)
            .AddDialogTitleBar(Lang.Get("Journal"), () => CloseAnimated(), CairoFont.WhiteSmallText(),
                ElementBounds.Fixed(0, 0, width, GuiStyle.TitleBarHeight))
            .BeginChildElements(ElementBounds.Fixed(0, HeaderHeight, width, height));
        if (selected != null) ComposeReader();
        else if (gallery) { if (selectedTapestry == null) ComposeGallery(); else ComposeTapestry(); }
        else ComposeShelf();
        SingleComposer.EndChildElements().Compose(false);
    }
    private void Paint(Action<Context> draw) => SingleComposer.AddStaticCustomDraw(
        ElementBounds.Fixed(0, 0, width, height), (ctx, surface, bounds) =>
        { ctx.Save(); ctx.Translate(bounds.drawX, bounds.drawY); ctx.Scale(RuntimeEnv.GUIScale, RuntimeEnv.GUIScale); draw(ctx); ctx.Restore(); });
    private void Button(string text, Func<bool> action, double x, double y, double buttonWidth, string? key = null)
        => SingleComposer.AddButton(text, () => !Busy && action(), ElementBounds.Fixed(x, y, buttonWidth, 32),
            new CairoFont(14, GuiStyle.StandardFontName, GuiStyle.ButtonTextColor).WithOrientation(EnumTextOrientation.Center), EnumButtonStyle.Normal, key);
    private void ReadingToolbar(string backLabel, Func<bool> back)
    {
        double left = ReadingControlsLeft, right = width - left;
        double sizeX = right - 86;
        Button("‹ " + backLabel, back, left, ReadingLayout.ToolbarTop,
            Math.Min(145, sizeX - left - 12), "back");
        Button("A−", () => FontChange(-1), sizeX, ReadingLayout.ToolbarTop, 38, "smaller");
        Button("A+", () => FontChange(1), sizeX + 48, ReadingLayout.ToolbarTop, 38, "larger");
        SingleComposer.GetButton("smaller").Enabled = fontSize > 14;
        SingleComposer.GetButton("larger").Enabled = fontSize < 24;
    }
    // Center the same control span in both dialogs. Their backgrounds can
    // change width without moving habitual back, size or navigation targets.
    private double ReadingControlsLeft => (width - Math.Min(width,
        ReadingLayout.ReaderWidth(Math.Max(280, lastFrameWidth / lastScale - 48), height))) / 2 + ReadingLayout.Inset;
    private void ReadingNavigation(string label, bool previous, bool next)
    {
        double y = height - ReadingLayout.NavigationBottom;
        double left = ReadingControlsLeft;
        double span = width - 2 * left;
        double buttonWidth = Math.Min(112, Math.Max(72, (span - 108) / 2));
        Button("‹ " + L("previous"), () => Move(-1), left, y, buttonWidth, "previous");
        Button(L("next") + " ›", () => Move(1), width - left - buttonWidth, y, buttonWidth, "next");
        double labelWidth = Math.Max(40, Math.Min(180, span - 2 * buttonWidth - 16));
        SingleComposer.AddDynamicText(label, Font(12).WithOrientation(EnumTextOrientation.Center),
            ElementBounds.Fixed(width / 2 - labelWidth / 2, y + 7, labelWidth, 23), "page-counter");
        SingleComposer.GetButton("previous").Enabled = previous;
        SingleComposer.GetButton("next").Enabled = next;
    }
    public bool CloseAnimated()
    {
        if (Busy) return true;
        CommitVisibleReading();
        if (selected == null) { closingWindow = true; return true; }
        StartClose(() => { if (IsOpened()) closingWindow = true; }); return true;
    }
    private bool Back()
    {
        if (Busy) return true;
        StartClose(() => { selected = null; ScheduleCompose(motion.Cancel); }); return true;
    }
    private bool SwitchView(bool tapestry)
    {
        gallery = tapestry; selectedTapestry = null; lorePage = 0; ScheduleCompose(); return true;
    }
    private void CollectionTabs()
    {
        double tabWidth = Math.Min(132, (width - 100) / 2), start = Math.Max(20, width / 2 - tabWidth - 6);
        if (width < 400) start = 20;
        Button(L("books-tab"), () => SwitchView(false), start, 15, tabWidth, "books-tab");
        Button(L("tapestries-tab"), () => SwitchView(true), start + tabWidth + 12, 15, tabWidth, "tapestries-tab");
        SingleComposer.GetButton(gallery ? "tapestries-tab" : "books-tab").Enabled = false;
    }
    private void StartClose(Action finished)
    {
        CommitVisibleReading();
        CaptureBook();
        motion.Begin(BookMotionKind.Close, finished: () => capi.Event.EnqueueMainThreadTask(finished, "betterjournal-close"));
        PlayTurn();
    }
    private void Open(JournalBook book)
    {
        if (Busy) return;
        selected = book; page = 0; restoreReadingPosition = true;
        closedIncoming = true;
        ScheduleCompose(BeginBookOpen);
    }
    private void BeginBookOpen() { motion.Begin(BookMotionKind.Open, finished: motion.Cancel); PlayTurn(); }
    private void ComposeShelf()
    {
        const int rows = 1;
        double rowHeight = height - 240;
        int columns = Math.Max(1, (int)((width - 80) / 78));
        perShelf = columns;
        int shelfCount = Math.Max(1, (books.Count + perShelf - 1) / perShelf);
        shelf = Math.Clamp(shelf, 0, shelfCount - 1);
        Paint(ctx =>
        {
            BookArtwork.Cabinet(ctx, width, height, rows, rowHeight);
            BookArtwork.Text(ctx, L("collection", books.Count, books.Sum(b => b.FragmentCount)), width / 2, height - 66,
                13, maxWidth: width - 65, center: true);
            if (books.Count == 0) BookArtwork.Text(ctx, L("empty-title"), width / 2, 164, 20, maxWidth: width - 75, center: true);
        });
        CollectionTabs();
        if (books.Count == 0)
            SingleComposer.AddRichtext(L("empty-text"), Font(16), ElementBounds.Fixed(55, 185, width - 110, 90));
        double cell = (width - 80) / columns;
        for (int slot = 0; slot < perShelf && shelf * perShelf + slot < books.Count; slot++)
        {
            var book = books[shelf * perShelf + slot];
            double bookHeight = rowHeight - 32 - JournalBooks.CoverSeed(book.Code, book.Title) % 24;
            var bounds = ElementBounds.Fixed(42 + slot * cell, 128 + rowHeight - 17 - bookHeight,
                Math.Min(73, cell - 5), bookHeight);
            int unread = reading.UnreadFragments(book);
            SingleComposer.AddInteractiveElement(new BookSpineElement(capi, bounds, materials, book, unread > 0, reading.Bookmark(book), () => Open(book), title =>
            { SingleComposer.GetDynamicText("hover").SetNewText(title + " · " + Progress(book) + " · " + L("unread-fragments", unread)); }), "book-" + slot);
        }
        SingleComposer.AddDynamicText(L("hover"), Font(14).WithOrientation(EnumTextOrientation.Center),
            ElementBounds.Fixed(36, height - 103, width - 72, 31), "hover");
        double footer = height - 47;
        Button("‹ " + L("previous"), () => Move(-1), 32, footer, 112, "previous");
        Button(L("next") + " ›", () => Move(1), width - 144, footer, 112, "next");
        SingleComposer.AddStaticText(L("shelf-page", shelf + 1, shelfCount), Font(13).WithOrientation(EnumTextOrientation.Center),
            ElementBounds.Fixed(width / 2 - 65, footer + 6, 130, 23));
        SingleComposer.GetButton("previous").Enabled = shelf > 0;
        SingleComposer.GetButton("next").Enabled = shelf < shelfCount - 1;
    }
    private bool Fits(string text)
    {
        using var rich = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, text, Font(fontSize, true)),
            ElementBounds.Fixed(0, 0, TextWidth, TextHeight));
        rich.CalcHeightAndPositions();
        return rich.TotalHeight <= (TextHeight - 3) * RuntimeEnv.GUIScale && rich.MaxLineWidth <= TextWidth * RuntimeEnv.GUIScale + 0.5;
    }
    private void LayoutPages()
    {
        if (laidOutBook == selected && layoutFont == fontSize && layoutWidth == width && layoutHeight == height && layoutScale == lastScale) return;
        string? anchorKey = null;
        int anchorOffset = 0;
        if (laidOutBook != null && ReadingProgress.BookKey(laidOutBook) == ReadingProgress.BookKey(selected!) && page < readingPages.Count)
        {
            anchorKey = ReadingProgress.Fragments(laidOutBook).ElementAtOrDefault(readingPages[page].Fragment - 1)?.Key;
            anchorOffset = readingPages[page].Start;
        }
        readingPages = pageLayouts.Get(selected!, TextWidth, TextHeight, lastScale, fontSize,
            () => BookPagination.Layout(selected!, Fits));
        var anchor = ReadingProgress.Fragments(selected!).FirstOrDefault(f => f.Key == anchorKey);
        if (restoreReadingPosition || anchor == null) page = reading.Resume(selected!, readingPages);
        else
        {
            for (int i = 0; i < readingPages.Count; i++)
            {
                if (readingPages[i].Fragment == anchor.Ordinal && readingPages[i].End > anchorOffset) { page = i; break; }
            }
        }
        restoreReadingPosition = false;
        laidOutBook = selected; layoutFont = fontSize; layoutWidth = width; layoutHeight = height; layoutScale = lastScale;
        capi.Logger.Debug("[BetterJournal] Laid out {0} fragments as {1} fitted pages.", selected!.FragmentCount, readingPages.Count);
    }
    private void ComposeReader()
    {
        var book = selected!;
        if (restoreReadingPosition && laidOutBook == selected && layoutFont == fontSize && layoutWidth == width && layoutHeight == height && layoutScale == lastScale)
        { page = reading.Resume(book, readingPages); restoreReadingPosition = false; }
        LayoutPages();
        page = Math.Clamp(page / perSpread * perSpread, 0, Math.Max(0, (readingPages.Count - 1) / perSpread * perSpread));
        Paint(ctx =>
        {
            BookArtwork.OpenBook(ctx, materials, materials.Cover(book), width, height, perSpread == 2);
            BookArtwork.Text(ctx, book.Title, width / 2, 73, 23, maxWidth: width - 76, center: true);
            BookArtwork.Text(ctx, Progress(book), width / 2, 95, 13, maxWidth: width - 70, center: true);
        });
        ReadingToolbar(L("back"), Back);
        for (int side = 0; side < perSpread; side++)
        {
            double x = 28 + side * LeafWidth;
            int index = page + side;
            string text = index < readingPages.Count ? readingPages[index].Text : (readingPages.Count == 0 ? L("no-pages") : L("end"));
            if (text.Length == 0) text = L("empty-page");
            SingleComposer.AddDynamicText(index < readingPages.Count ? L("recovered-fragment", readingPages[index].Fragment) : "",
                Font(10, true), ElementBounds.Fixed(x + 32, PaperY + 14, TextWidth, 18), "heading-" + side);
            var textBounds = ElementBounds.Fixed(x + 32, PaperY + 37, TextWidth, TextHeight);
            SingleComposer.AddInteractiveElement(new BookPageElement(capi, text, Font(fontSize, true), textBounds, TextHeight), "page-" + side);
            SingleComposer.AddDynamicText(index < readingPages.Count ? (index + 1).ToString() : "",
                Font(12, true).WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(x + LeafWidth / 2 - 25, PaperY + PaperHeight - 30, 50, 20), "folio-" + side);
        }
        ReadingNavigation(PageCounter(), page > 0, page + perSpread < readingPages.Count);
        Button(L("next-unread"), JumpUnread, width / 2 - 80, height - 43, 160, "unread");
        SingleComposer.GetButton("unread").Enabled = reading.UnreadFragments(book) > 0;
    }
    private string PageCounter() => readingPages.Count == 0 ? "" : perSpread == 1 || readingPages.Count == 1
        ? L("page", page + 1, readingPages.Count)
        : L("pages", page + 1, Math.Min(page + 2, readingPages.Count), readingPages.Count);
    private void UpdateReadingText(bool fade = true)
    {
        textUpdateRequested = false;
        readingExposure.Reset(); readingMarked = false;
        if (selected != null)
        {
            LayoutPages();
            page = Math.Clamp(page / perSpread * perSpread, 0, Math.Max(0, (readingPages.Count - 1) / perSpread * perSpread));
            for (int side = 0; side < perSpread; side++)
            {
                int index = page + side;
                string text = index < readingPages.Count ? readingPages[index].Text : (readingPages.Count == 0 ? L("no-pages") : L("end"));
                ((BookPageElement)SingleComposer.GetElement("page-" + side)).UpdateText(text.Length == 0 ? L("empty-page") : text, Font(fontSize, true), fade);
                SingleComposer.GetDynamicText("heading-" + side).SetNewText(index < readingPages.Count ? L("recovered-fragment", readingPages[index].Fragment) : "");
                SingleComposer.GetDynamicText("folio-" + side).SetNewText(index < readingPages.Count ? (index + 1).ToString() : "");
            }
            SingleComposer.GetDynamicText("page-counter").SetNewText(PageCounter());
            SingleComposer.GetButton("previous").Enabled = page > 0;
            SingleComposer.GetButton("next").Enabled = page + perSpread < readingPages.Count;
            SingleComposer.GetButton("unread").Enabled = reading.UnreadFragments(selected) > 0;
        }
        else if (selectedTapestry != null)
        {
            var text = (BookPageElement)SingleComposer.GetElement("tapestry-lore");
            LayoutTapestryText(text.Bounds.fixedWidth, text.AvailableHeight);
            text.UpdateText(tapestryPages[lorePage], Font(fontSize, true), fade);
            SingleComposer.GetDynamicText("page-counter").SetNewText(L("page", lorePage + 1, tapestryPages.Count));
            SingleComposer.GetButton("previous").Enabled = lorePage > 0;
            SingleComposer.GetButton("next").Enabled = lorePage < tapestryPages.Count - 1;
        }
        SingleComposer.GetButton("smaller").Enabled = fontSize > 14;
        SingleComposer.GetButton("larger").Enabled = fontSize < 24;
    }
    private bool JumpUnread()
    {
        CommitVisibleReading();
        int next = reading.NextUnread(selected!, readingPages, page + perSpread - 1);
        if (next >= 0) TurnTo(next / perSpread * perSpread);
        return true;
    }
    private void TrackReading()
    {
        readingExposure.Observe(capi.ElapsedMilliseconds, ReadingVisible);
        if (readingExposure.Ready(capi.ElapsedMilliseconds)) RecordVisibleReading();
    }
    private bool ReadingVisible => IsOpened() && Focused && !capi.IsGamePaused && !Busy &&
        (selected != null || selectedTapestry != null);
    private void CommitVisibleReading()
    {
        if (ReadingVisible && readingExposure.Ready(capi.ElapsedMilliseconds, leaving: true)) RecordVisibleReading();
    }
    private void RecordVisibleReading()
    {
        if (readingMarked) return;
        bool changed = selected != null ? reading.Mark(selected, readingPages.Skip(page).Take(perSpread))
            : reading.ViewTapestry(selectedTapestry!);
        if (changed) readingChanged();
        readingMarked = true;
        if (selected != null) SingleComposer.GetButton("unread").Enabled = reading.UnreadFragments(selected) > 0;
    }
    private bool FontChange(int amount)
    {
        int next = Math.Clamp(fontSize + amount, 14, 24);
        if (next == fontSize) return true;
        CommitVisibleReading();
        fontSize = next; textUpdateRequested = true;
        return true;
    }
    private void PlayTurn() => capi.Gui.PlaySound(new AssetLocation("game:sounds/held/bookturn1"), true, 0.35f);
    private void CaptureBook()
    {
        motion.CaptureScene(width, height, perSpread == 2, selected!,
            selected!.TotalFragments is int total ? L("progress", selected.FragmentCount, total) : Fragments(selected.FragmentCount));
        CaptureLeaf(0, 0, page);
        if (perSpread == 2) CaptureLeaf(1, 1, page + 1);
    }
    private void CaptureLeaf(int slot, int side, int index)
    {
        bool exists = index < readingPages.Count;
        string text = exists ? readingPages[index].Text : (readingPages.Count == 0 ? L("no-pages") : L("end"));
        if (text.Length == 0) text = L("empty-page");
        motion.CaptureLeaf(slot, side, text, exists ? L("recovered-fragment", readingPages[index].Fragment) : "",
            exists ? (index + 1).ToString() : "", Font(fontSize, true), width, height, perSpread == 2);
    }
    private bool Move(int direction)
    {
        if (Busy) return true;
        if (gallery && selected == null)
        {
            CommitVisibleReading();
            if (selectedTapestry == null) galleryPage = Math.Clamp(galleryPage + direction, 0, Math.Max(0, (tapestries.Count - 1) / perGallery));
            else lorePage = Math.Clamp(lorePage + direction, 0, Math.Max(0, tapestryPages.Count - 1));
            ScheduleCompose();
        }
        else if (selected == null)
        {
            int next = Math.Clamp(shelf + direction, 0, Math.Max(0, (books.Count - 1) / perShelf));
            if (next != shelf) { shelf = next; ScheduleCompose(); }
        }
        else
        {
            int next = Math.Clamp(page + direction * perSpread, 0, Math.Max(0, (readingPages.Count - 1) / perSpread * perSpread));
            if (next == page) return true;
            TurnTo(next);
        }
        return true;
    }
    private void TurnTo(int next)
    {
        if (next == page) return;
        CommitVisibleReading();
        int direction = next > page ? 1 : -1;
        CaptureBook();
        CaptureLeaf(2, 0, next);
        if (perSpread == 2) CaptureLeaf(3, 1, next + 1);
        motion.Begin(BookMotionKind.Turn, direction, finished: () =>
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (!IsOpened()) { motion.Cancel(); return; }
                page = next; UpdateReadingText(fade: false);
                motion.Cancel();
            }, "betterjournal-turn");
        });
        PlayTurn();
    }
    public override void OnRenderGUI(float dt)
    {
        if (textUpdateRequested) UpdateReadingText();
        if (entering || closingWindow || composeRequested)
        {
            bool close = closingWindow, entrance = entering;
            bool animate = entrance || close || animateCompose;
            WindowMotion.Snapshot? before = null, after = null;
            try
            {
                if (animate && !entrance) before = windowMotion.Capture(CurrentRect(), () => RenderView(0));
                if (!entrance) motion.Cancel();
                var finished = composeAfter;
                entering = closingWindow = composeRequested = pendingCompose = false; composeAfter = null;
                if (!entrance && !close) ComposeView();
                if (closedIncoming && !entrance) CaptureBook();
                if (animate && !close) after = windowMotion.Capture(CurrentRect(), () => RenderView(0, closedIncoming));
                closedIncoming = false;
                if (animate) windowMotion.Begin(before, after, close ? () => { motion.Cancel(); TryClose(); } : finished);
                else finished?.Invoke();
            }
            catch { before?.Dispose(); after?.Dispose(); throw; }
        }
        if (windowMotion.Active) { readingExposure.Reset(); windowMotion.Draw(); return; }
        RenderView(dt);
        TrackReading();
    }
    private WindowRect CurrentRect() => new(SingleComposer.Bounds.renderX, SingleComposer.Bounds.renderY,
        width * lastScale, (height + HeaderHeight) * lastScale);
    private void RenderView(float dt, bool closed = false)
    {
        base.OnRenderGUI(dt);
        if (closed || motion.Active)
        {
            var root = SingleComposer.Bounds;
            if (closed) motion.DrawClosed(root.renderX / lastScale + 28, root.renderY / lastScale + HeaderHeight + PaperY, width - 56, PaperHeight, perSpread == 2);
            else motion.Draw(root.renderX / lastScale + 28, root.renderY / lastScale + HeaderHeight + PaperY, width - 56, PaperHeight, perSpread == 2);
        }
    }
    public override void OnKeyDown(KeyEvent args)
    {
        if (args.KeyCode == (int)GlKeys.Left) { Move(-1); args.Handled = true; return; }
        if (args.KeyCode == (int)GlKeys.Right) { Move(1); args.Handled = true; return; }
        var hotkey = capi.Input.GetHotKeyByCode("journal");
        if (hotkey?.DidPress(args, capi.World, capi.World.Player, true) == true)
        { CloseAnimated(); args.Handled = true; return; }
        if (Busy) { args.Handled = true; return; }
        base.OnKeyDown(args);
    }
    public override bool OnEscapePressed()
    {
        if (Busy) return false;
        if (selected == null && selectedTapestry != null) { CommitVisibleReading(); selectedTapestry = null; ScheduleCompose(); return false; }
        if (selected == null) { CloseAnimated(); return false; }
        Back(); return false;
    }
    public override void OnMouseDown(MouseEvent args)
    {
        if (Busy) { args.Handled = true; return; }
        swipe = selected != null && args.Button == EnumMouseButton.Left && InsidePaper(args.X, args.Y);
        swipeX = args.X; swipeY = args.Y;
        base.OnMouseDown(args);
    }
    public override void OnMouseUp(MouseEvent args)
    {
        if (Busy) { swipe = false; args.Handled = true; return; }
        int dx = args.X - swipeX, dy = args.Y - swipeY;
        bool turn = swipe && args.Button == EnumMouseButton.Left && Math.Abs(dx) > 60 * lastScale && Math.Abs(dx) > Math.Abs(dy) * 1.5;
        swipe = false;
        if (turn) { Move(dx < 0 ? 1 : -1); args.Handled = true; return; }
        base.OnMouseUp(args);
    }
    private bool InsidePaper(int x, int y)
    {
        var root = SingleComposer.Bounds;
        double localX = (x - root.renderX) / lastScale, localY = (y - root.renderY) / lastScale - HeaderHeight;
        return localX >= 28 && localX <= width - 28 && localY >= PaperY && localY <= PaperY + PaperHeight;
    }
    public override void OnMouseWheel(MouseWheelEventArgs args) { args.SetHandled(true); }
    public override void OnGuiClosed()
    {
        windowMotion.Cancel(); motion.Cancel(); swipe = textUpdateRequested = false;
        composeGeneration++;
        entering = closingWindow = composeRequested = pendingCompose = closedIncoming = false; composeAfter = null;
        readingExposure.Reset(); saveReading();
        base.OnGuiClosed();
    }
    public override void OnMouseMove(MouseEvent args) { if (Busy) { args.Handled = true; return; } base.OnMouseMove(args); }
    public override void Dispose() { pageLayouts.Clear(); windowMotion.Dispose(); motion.Dispose(); base.Dispose(); materials.Dispose(); }
}
