using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

namespace BetterJournal;

// Native UI surrounds materials loaded from the installed game at runtime.
public static class BookArtwork
{
    public static void NewBadge(Context ctx, double x, double y, double width)
    {
        Rect(ctx, x, y, width, 24, .17, .12, .075);
        Rect(ctx, x, y, width, 2, .70, .49, .23);
        Text(ctx, Lang.Get("betterjournal:new"), x + width / 2, y + 17, 12,
            .97, .82, .45, maxWidth: width - 4, center: true);
    }

    public static void Rect(Context c, double x, double y, double w, double h, double r, double g, double b, double a = 1)
    { c.SetSourceRGBA(r, g, b, a); c.Rectangle(x, y, w, h); c.Fill(); }

    public static void Gradient(Context c, double x, double y, double w, double h,
        double r, double g, double b, double factor = 0.55)
    {
        using var p = new LinearGradient(x, y, x + w, y);
        p.AddColorStop(0, new Color(r * factor, g * factor, b * factor));
        p.AddColorStop(0.35, new Color(r, g, b));
        p.AddColorStop(1, new Color(r * factor, g * factor, b * factor));
        c.SetSource(p); c.Rectangle(x, y, w, h); c.Fill();
    }

    public static void Text(Context c, string text, double x, double y, double size,
        double r = 0.87, double g = 0.79, double b = 0.62, double maxWidth = double.MaxValue, bool center = false)
    {
        c.SelectFontFace(GuiStyle.DecorativeFontName, FontSlant.Normal, FontWeight.Normal);
        c.SetFontSize(size);
        string fitted = text.Replace('\n', ' ').Replace('\r', ' ');
        if (c.TextExtents(fitted).XAdvance > maxWidth)
        {
            var indices = System.Globalization.StringInfo.ParseCombiningCharacters(fitted);
            int count = indices.Length;
            while (count > 0 && c.TextExtents(fitted[..indices[--count]] + "…").XAdvance > maxWidth) { }
            fitted = (count > 0 ? fitted[..indices[count]] : "") + "…";
        }
        c.SetSourceRGB(r, g, b);
        c.MoveTo(center ? x - c.TextExtents(fitted).XAdvance / 2 : x, y);
        c.ShowText(fitted);
    }

    public static void CollectionBackground(Context c, double width, double height)
    { } // The composer supplies the game's shaded dialog background.

    public static void Cabinet(Context c, double width, double height, int rows, double rowHeight)
    {
        CollectionBackground(c, width, height);
        for (int row = 0; row < rows; row++)
        {
            double top = 128 + row * rowHeight;
            Rect(c, 31, top, width - 62, rowHeight - 14, 0, 0, 0, .15);
            Rect(c, 31, top, width - 62, 2, 0, 0, 0, .4);
            Rect(c, 31, top, 2, rowHeight - 14, 0, 0, 0, .3);
            Gradient(c, 24, top + rowHeight - 16, width - 48, 13, .40, .32, .24, .8);
            Rect(c, 24, top + rowHeight - 16, width - 48, 1.5, 1, 1, 1, .12);
            Rect(c, 26, top + rowHeight - 3, width - 52, 5, 0, 0, 0, 0.3);
        }
        Rect(c, 42, 99, width - 84, 1, 0.67, 0.48, 0.28, 0.5);
        Ornament(c, width / 2, 108);
    }

    public static void Ornament(Context c, double x, double y)
    {
        c.SetSourceRGB(0.69, 0.51, 0.30); c.MoveTo(x, y - 4);
        c.LineTo(x + 4, y); c.LineTo(x, y + 4); c.LineTo(x - 4, y); c.ClosePath(); c.Fill();
    }

    public static void Spine(Context c, BookMaterials materials, JournalBook book, double width, double height)
    {
        Rect(c, 5, 4, width - 3, height - 3, 0, 0, 0, 0.4);
        double w = width - 6, h = height - 4;
        materials.Spine(c, materials.Cover(book), 1, 0, w, h);
        c.Save(); c.Translate(1 + w / 2, h / 2); c.Rotate(-Math.PI / 2);
        Text(c, book.Title, 0, 5, 17, .94, .886, .78, maxWidth: Math.Max(20, h * 12 / 18 - 24), center: true);
        c.Restore();
        Rect(c, 1 + w * .1, h - 30, w * .8, 24, .075, .055, .035, .7);
        Text(c, book.SpineProgress, 1 + w / 2, h - 11, 12, .94, .886, .78, maxWidth: width - 12, center: true);
    }

    public static void OpenBook(Context c, BookMaterials materials, string cover, double width, double height, bool spread)
    {
        double leaf = (width - 56) / (spread ? 2 : 1);
        for (int side = 0; side < (spread ? 2 : 1); side++)
            materials.Board(c, cover, front: spread && side == 0, inside: true,
                18 + side * (leaf + 10), ReadingLayout.PaperTop - 8, leaf + (spread ? 10 : 20), ReadingLayout.PaperHeight(height) + 16);
        double x = 28, y = ReadingLayout.PaperTop, h = ReadingLayout.PaperHeight(height);
        for (int side = 0; side < (spread ? 2 : 1); side++) materials.Paper(c, x + side * leaf, y, leaf, h);
        if (spread)
        {
            using var seam = new LinearGradient(width / 2 - 17, 0, width / 2 + 17, 0);
            seam.AddColorStop(0, new Color(0.25, 0.16, 0.08, 0));
            seam.AddColorStop(0.5, new Color(0.25, 0.16, 0.08, 0.3));
            seam.AddColorStop(1, new Color(0.25, 0.16, 0.08, 0));
            c.SetSource(seam); c.Rectangle(width / 2 - 17, y, 34, h); c.Fill();
            Rect(c, width / 2 - 0.5, y, 1, h, 0.33, 0.24, 0.13, 0.23);
        }
    }
}
