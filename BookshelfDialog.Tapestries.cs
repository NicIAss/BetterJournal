using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace BetterJournal;

public sealed partial class BookshelfDialog
{
    private string TapestryTitle(TapestryView tapestry) => tapestry.Discovered
        ? catalogue.TapestryNames.GetValueOrDefault(tapestry.Design.NameKey) ?? Lang.GetMatching(tapestry.Design.NameKey) : "???";
    private static string TapestryStatus(TapestryView tapestry) => !tapestry.Discovered ? L("undiscovered")
        : L("tapestry-pieces", tapestry.SeenCount, tapestry.Design.Pieces.Count);

    private void GalleryBackground(string subtitle)
    {
        Paint(ctx =>
        {
            BookArtwork.CollectionBackground(ctx, width, height);
            BookArtwork.Text(ctx, subtitle, width / 2, 105, 13, maxWidth: width - 65, center: true);
            BookArtwork.Rect(ctx, 42, 119, width - 84, 1, 0.67, 0.48, 0.28, 0.5);
        });
        CollectionTabs();
    }

    private void ComposeGallery()
    {
        GalleryBackground(L("gallery-count", tapestries.Count(t => t.Discovered), tapestries.Count));
        int columns = Math.Max(1, (int)((width - 72) / 220));
        int rows = height >= 600 ? 2 : 1;
        perGallery = columns * rows;
        int count = Math.Max(1, (tapestries.Count + perGallery - 1) / perGallery);
        galleryPage = Math.Clamp(galleryPage, 0, count - 1);
        double cellWidth = (width - 72) / columns, cellHeight = (height - 212) / rows;
        for (int slot = 0; slot < perGallery && galleryPage * perGallery + slot < tapestries.Count; slot++)
        {
            var tapestry = tapestries[galleryPage * perGallery + slot];
            void OpenTapestry() { selectedTapestry = tapestry; lorePage = 0; ScheduleCompose(); }
            double x = 36 + slot % columns * cellWidth, y = 134 + slot / columns * cellHeight;
            SingleComposer.AddInteractiveElement(new TapestryImageElement(capi,
                ElementBounds.Fixed(x + 6, y, cellWidth - 12, cellHeight - 74), tapestry, OpenTapestry), "tapestry-image-" + slot);
            Button(TapestryTitle(tapestry), () => { OpenTapestry(); return true; }, x + 6, y + cellHeight - 65, cellWidth - 12);
            bool unread = reading.UnreadTapestry(tapestry);
            double badgeWidth = Math.Min(64, (cellWidth - 20) / 3);
            if (unread)
                Paint(ctx => BookArtwork.NewBadge(ctx, x + 8, y + cellHeight - 30, badgeWidth));
            double statusX = unread ? x + badgeWidth + 16 : x + 4;
            double statusWidth = unread ? cellWidth - badgeWidth - 24 : cellWidth - 8;
            SingleComposer.AddStaticText(TapestryStatus(tapestry),
                Font(11).WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(statusX, y + cellHeight - 27, statusWidth, 20));
        }
        if (tapestries.Count == 0)
            SingleComposer.AddStaticText(L("gallery-empty"), Font(16).WithOrientation(EnumTextOrientation.Center),
                ElementBounds.Fixed(45, 175, width - 90, 120));
        double footer = height - 51;
        Button("‹ " + L("previous"), () => Move(-1), 32, footer, 112, "previous");
        Button(L("next") + " ›", () => Move(1), width - 144, footer, 112, "next");
        SingleComposer.AddStaticText(L("gallery-page", galleryPage + 1, count), Font(13).WithOrientation(EnumTextOrientation.Center),
            ElementBounds.Fixed(width / 2 - 80, footer + 6, 160, 23));
        SingleComposer.GetButton("previous").Enabled = galleryPage > 0;
        SingleComposer.GetButton("next").Enabled = galleryPage < count - 1;
    }

    private void ComposeTapestry()
    {
        var tapestry = selectedTapestry!;
        Paint(ctx =>
        {
            BookArtwork.Text(ctx, TapestryTitle(tapestry), width / 2, 73, 23, maxWidth: width - 76, center: true);
            BookArtwork.Text(ctx, TapestryStatus(tapestry), width / 2, 95, 13, maxWidth: width - 70, center: true);
        });
        ReadingToolbar(L("gallery-back"), () => { CommitVisibleReading(); selectedTapestry = null; ScheduleCompose(); return true; });
        bool stacked = width < 720;
        double imageX = 36, imageY = 145, imageW = stacked ? width - 72 : (width - 100) * 0.44;
        double imageH = stacked ? Math.Min(175, (height - 235) * 0.37) : height - 253;
        SingleComposer.AddInteractiveElement(new TapestryImageElement(capi, ElementBounds.Fixed(imageX, imageY, imageW, imageH), tapestry), "tapestry-large");
        double panelX = stacked ? 36 : imageX + imageW + 28, panelY = stacked ? imageY + imageH + 16 : imageY;
        double panelW = stacked ? width - 72 : width - panelX - 36, panelH = height - panelY - 108;
        Paint(ctx =>
        {
            BookArtwork.Rect(ctx, panelX + 3, panelY + 5, panelW, panelH, 0, 0, 0, 0.3);
            materials.Paper(ctx, panelX, panelY, panelW, panelH);
        });
        double textW = panelW - 48, textH = Math.Max(30, panelH - 88);
        LayoutTapestryText(textW, textH);
        SingleComposer.AddStaticText(L("tapestry-notes"), Font(11, true), ElementBounds.Fixed(panelX + 24, panelY + 15, textW, 20));
        SingleComposer.AddInteractiveElement(new BookPageElement(capi, tapestryPages[lorePage], Font(fontSize, true),
            ElementBounds.Fixed(panelX + 24, panelY + 40, textW, textH), textH), "tapestry-lore");
        ReadingNavigation(L("page", lorePage + 1, tapestryPages.Count), lorePage > 0, lorePage < tapestryPages.Count - 1);
    }
    private void LayoutTapestryText(double textW, double textH)
    {
        var tapestry = selectedTapestry!;
        string text = tapestry.Lore.Count > 0 ? string.Join("\n\n", tapestry.Lore)
            : !tapestry.Discovered ? L("tapestry-unknown-text")
            : tapestry.Design.ChapterId < 0 ? L("tapestry-no-lore") : L("tapestry-lore-pending");
        bool FitsLore(string fragment)
        {
            using var rich = new GuiElementRichtext(capi, VtmlUtil.Richtextify(capi, fragment, Font(fontSize, true)),
                ElementBounds.Fixed(0, 0, textW, textH));
            rich.CalcHeightAndPositions();
            return rich.TotalHeight <= (textH - 3) * RuntimeEnv.GUIScale && rich.MaxLineWidth <= textW * RuntimeEnv.GUIScale + 0.5;
        }
        var source = JournalBooks.SplitPages(text).Select((part, i) => new RecoveredPage(0, i, part)).ToArray();
        var book = new JournalBook(-1, tapestry.Design.Key, "", 1, source);
        tapestryPages = pageLayouts.Get(book, textW, textH, lastScale, fontSize,
            () => BookPagination.Layout(book, FitsLore)).Select(p => p.Text).ToList();
        lorePage = Math.Clamp(lorePage, 0, Math.Max(0, tapestryPages.Count - 1));
    }
}
