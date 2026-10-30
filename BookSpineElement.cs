using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace BetterJournal;

public sealed class BookSpineElement : GuiElement
{
    private readonly JournalBook book;
    private readonly BookMaterials materials;
    private readonly Action open;
    private readonly Action<string> hover;
    private readonly bool unread, bookmark;
    private bool pressed;
    private bool over;
    private LoadedTexture highlight;

    public BookSpineElement(ICoreClientAPI api, ElementBounds bounds, BookMaterials materials, JournalBook book, bool unread, bool bookmark, Action open, Action<string> hover)
        : base(api, bounds)
    { this.materials = materials; this.book = book; this.unread = unread; this.bookmark = bookmark; this.open = open; this.hover = hover; highlight = new LoadedTexture(api); }

    public override bool Focusable => true;

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        ctx.Save(); ctx.Translate(Bounds.drawX, Bounds.drawY); ctx.Scale(RuntimeEnv.GUIScale, RuntimeEnv.GUIScale);
        BookArtwork.Spine(ctx, materials, book, Bounds.fixedWidth, Bounds.fixedHeight);
        double w = Bounds.fixedWidth - 6;
        if (bookmark)
        {
            BookArtwork.Rect(ctx, w - 14, 0, 10, 34, .40, .18, .12);
            BookArtwork.Rect(ctx, w - 12, 0, 2, 30, .63, .34, .19);
            BookArtwork.Rect(ctx, w - 14, 34, 4, 4, .40, .18, .12);
            BookArtwork.Rect(ctx, w - 8, 34, 4, 4, .40, .18, .12);
        }
        if (unread)
            BookArtwork.NewBadge(ctx, 4, 42, w - 6);
        ctx.Restore();
        using var glow = new ImageSurface(Format.Argb32, Bounds.OuterWidthInt, Bounds.OuterHeightInt);
        using var gc = new Context(glow);
        gc.SetSourceRGBA(0.95, 0.82, 0.55, 0.18); gc.Paint();
        gc.LineWidth = scaled(1.5); gc.SetSourceRGBA(0.95, 0.82, 0.55, 0.8);
        gc.Rectangle(1, 1, glow.Width - 2, glow.Height - 2); gc.Stroke();
        generateTexture(glow, ref highlight);
    }

    public override void RenderInteractiveElements(float deltaTime)
    {
        if (over || HasFocus) api.Render.Render2DTexture(highlight.TextureId, Bounds);
    }

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    {
        bool now = Bounds.PointInside(args.X, args.Y);
        if (now && !over) hover(book.Title);
        over = now;
    }

    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (args.Button != EnumMouseButton.Left) return;
        pressed = true; args.Handled = true;
    }

    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (pressed && args.Button == EnumMouseButton.Left && Bounds.PointInside(args.X, args.Y))
        { args.Handled = true; open(); }
        pressed = false;
    }

    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (HasFocus && (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.Space))
        { args.Handled = true; open(); }
    }

    public override void Dispose() { highlight.Dispose(); base.Dispose(); }
}
