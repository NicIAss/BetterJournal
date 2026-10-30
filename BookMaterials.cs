using System.Runtime.InteropServices;
using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BetterJournal;

// Read installed assets once per dialog; release native surfaces on leaving the world.
public sealed class BookMaterials(ICoreClientAPI api) : IDisposable
{
    private static readonly string[] Colors = ["normal-darkgreen", "normal-cherryred", "normal-darkbeige",
        "normal-orangebrown", "normal-purple", "normal-teal", "normal-darkolive"];
    private readonly Dictionary<string, ImageSurface> surfaces = [];
    public string Cover(JournalBook book) => Colors[JournalBooks.CoverSeed(book.Code, book.Title) % Colors.Length];
    private ImageSurface Surface(string path)
    {
        if (!surfaces.TryGetValue(path, out var surface))
            surfaces[path] = surface = GuiElement.getImageSurfaceFromAsset(api, new AssetLocation("game:textures/" + path + ".png"));
        return surface;
    }
    public void Texture(Context c, string name, double u0, double v0, double u1, double v1,
        double x, double y, double w, double h)
    {
        var surface = Surface("item/lore/" + name);
        string key = FormattableString.Invariant($"uv:{name}:{u0}:{v0}:{u1}:{v1}");
        if (!surfaces.TryGetValue(key, out var cropped))
        {
            double sw = (u1 - u0) * surface.Width / 16, sh = (v1 - v0) * surface.Height / 16;
            cropped = new ImageSurface(Format.Argb32, Math.Max(1, (int)Math.Ceiling(sw)), Math.Max(1, (int)Math.Ceiling(sh)));
            using var paint = new Context(cropped);
            Draw(paint, surface, u0 * surface.Width / 16, v0 * surface.Height / 16, sw, sh, 0, 0, cropped.Width, cropped.Height);
            surfaces[key] = cropped;
        }
        // Clamp within the selected face, never into a neighboring atlas tile.
        Draw(c, cropped, 0, 0, cropped.Width, cropped.Height, x, y, w, h);
    }
    // Tall-book faces use opposite UV directions on opposite normals. In this
    // reader, gold fittings belong to the free edge, never the bound edge.
    public void Board(Context c, string cover, bool front, bool inside,
        double x, double y, double w, double h)
    {
        c.Save();
        if (!inside) { c.Translate(x + w, y); c.Scale(-1, 1); x = y = 0; }
        Texture(c, cover, front ? 5 : 10.5, 7, front ? 10.5 : 16, 16, x, y, w, h);
        c.Restore();
    }
    private static void Draw(Context c, ImageSurface surface, double sx, double sy, double sw, double sh,
        double x, double y, double w, double h)
    {
        if (w <= 0 || h <= 0) return;
        c.Save(); c.Rectangle(x, y, w, h); c.Clip();
        using var pattern = new SurfacePattern(surface) { Filter = Filter.Nearest, Extend = Extend.Pad };
        pattern.Matrix = new Matrix(sw / w, 0, 0, sh / h, sx - x * sw / w, sy - y * sh / h);
        c.SetSource(pattern); c.Paint(); c.Restore();
    }
    public void Background(Context c, double width, double height)
    {
        c.SetSourceRGBA(GuiStyle.DialogStrongBgColor); c.Rectangle(0, 0, width, height); c.Fill();
        c.Save(); c.Rectangle(0, 0, width, height); c.Clip();
        using var pattern = new SurfacePattern(Surface("gui/backgrounds/soil")) { Filter = Filter.Nearest, Extend = Extend.Repeat };
        pattern.Matrix = new Matrix(0.125, 0, 0, 0.125, 0, 0);
        c.SetSource(pattern); c.PaintWithAlpha(64 / 255d); c.Restore();
    }
    public void Paper(Context c, double x, double y, double w, double h)
    {
        Texture(c, "pages", 1.5, 1.5, 13.5, 15.5, x, y, w, h);
        BookArtwork.Rect(c, x, y, w, h, 238 / 255d, 225 / 255d, 189 / 255d, 0.7);
    }
    private (double R, double G, double B) Sample(ImageSurface source, int x, int y, double factor)
    {
        // Coordinates are atlas texels at the vanilla 32px resolution. Texture packs may upscale it.
        x = Math.Clamp(x * source.Width / 32, 0, source.Width - 1);
        y = Math.Clamp(y * source.Height / 32, 0, source.Height - 1);
        uint color = unchecked((uint)Marshal.ReadInt32(source.DataPtr, y * source.Stride + x * 4));
        return (((color >> 16) & 255) / 255d * factor, ((color >> 8) & 255) / 255d * factor, (color & 255) / 255d * factor);
    }
    public void Spine(Context c, string cover, double x, double y, double w, double h)
    {
        string key = "spine:" + cover;
        if (!surfaces.TryGetValue(key, out var material))
        {
            var source = Surface("item/lore/" + cover);
            material = new ImageSurface(Format.Argb32, 4, 18);
            using var paint = new Context(material);
            Texture(paint, cover, 1, 7, 3, 16, 0, 0, 4, 18);
            void Pixel(int px, int py, int ax, int ay, double factor)
            {
                var (r, g, b) = Sample(source, ax, ay, factor);
                BookArtwork.Rect(paint, px, py, 1, 1, r, g, b);
            }
            for (int row = 3; row <= 14; row++)
            {
                for (int col = 1; col <= 2; col++) Pixel(col, row, 15 + (row * 3 + col) % 6, (row + col) % 12, .62);
                Pixel(0, row, 2, 14 + row, .72); Pixel(3, row, 5, 14 + row, .72);
            }
            foreach (int row in new[] { 2, 15 }) { Pixel(1, row, 10, 14, .62); Pixel(2, row, 10, 14, .77); }
            surfaces[key] = material;
        }
        Draw(c, material, 0, 0, 4, 18, x, y, w, h);
    }
    public void Dispose() { foreach (var surface in surfaces.Values) surface.Dispose(); surfaces.Clear(); }
}
