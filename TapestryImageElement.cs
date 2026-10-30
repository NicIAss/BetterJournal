using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace BetterJournal;

// References live game/mod textures. The package contains no copied tapestry art.
public sealed class TapestryImageElement(ICoreClientAPI api, ElementBounds bounds, TapestryView tapestry, Action? open = null)
    : GuiElement(api, bounds)
{
    private readonly int[] textures = new int[tapestry.Design.Pieces.Count];
    private double imageX, imageY, tileSize;
    private bool pressed;
    public override bool Focusable => open != null;

    public override void ComposeElements(Context ctx, ImageSurface surface)
    {
        Bounds.CalcWorldBounds();
        double w = Bounds.fixedWidth, h = Bounds.fixedHeight;
        tileSize = Math.Min((w - 20) / tapestry.Design.Columns, (h - 20) / tapestry.Design.Rows);
        imageX = (w - tileSize * tapestry.Design.Columns) / 2;
        imageY = (h - tileSize * tapestry.Design.Rows) / 2;
        ctx.Save(); ctx.Translate(Bounds.drawX, Bounds.drawY); ctx.Scale(RuntimeEnv.GUIScale, RuntimeEnv.GUIScale);
        BookArtwork.Gradient(ctx, 0, 0, w, h, 0.20, 0.135, 0.09, 0.55);
        ctx.SetSourceRGBA(0.63, 0.46, 0.28, 0.6); ctx.LineWidth = 1; ctx.Rectangle(1, 1, w - 2, h - 2); ctx.Stroke();
        for (int i = 0; i < textures.Length; i++)
        {
            var piece = tapestry.Design.Pieces[i];
            double x = imageX + piece.Column * tileSize, y = imageY + piece.Row * tileSize;
            BookArtwork.Rect(ctx, x, y, tileSize, tileSize, 0.13, 0.10, 0.075);
            if (tapestry.Seen[i])
            {
                var path = new AssetLocation(piece.Texture);
                if (!api.Assets.Exists(path)) path = new AssetLocation("game", path.Path);
                if (api.Assets.Exists(path)) textures[i] = api.Render.GetOrLoadTexture(path);
            }
            if (textures[i] == 0)
            {
                ctx.SetSourceRGBA(0.58, 0.44, 0.29, 0.28); ctx.LineWidth = 1;
                ctx.Rectangle(x + 2, y + 2, Math.Max(1, tileSize - 4), Math.Max(1, tileSize - 4)); ctx.Stroke();
                BookArtwork.Text(ctx, tapestry.Seen[i] ? "?" : "???", x + tileSize / 2, y + tileSize / 2 + 5,
                    Math.Min(20, tileSize / 5), maxWidth: tileSize - 8, center: true);
            }
        }
        ctx.Restore();
    }
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    {
        if (open == null || args.Button != EnumMouseButton.Left) return;
        pressed = true; args.Handled = true;
    }
    public override void OnMouseUp(ICoreClientAPI api, MouseEvent args)
    {
        if (pressed && args.Button == EnumMouseButton.Left && Bounds.PointInside(args.X, args.Y))
        { args.Handled = true; open?.Invoke(); }
        pressed = false;
    }
    public override void OnKeyDown(ICoreClientAPI api, KeyEvent args)
    {
        if (open != null && HasFocus && (args.KeyCode == (int)GlKeys.Enter || args.KeyCode == (int)GlKeys.Space))
        { args.Handled = true; open(); }
    }

    public override void RenderInteractiveElements(float dt)
    {
        double scale = RuntimeEnv.GUIScale;
        for (int i = 0; i < textures.Length; i++)
        {
            if (textures[i] == 0) continue;
            var piece = tapestry.Design.Pieces[i];
            double x = Math.Round(Bounds.renderX + (imageX + piece.Column * tileSize) * scale);
            double y = Math.Round(Bounds.renderY + (imageY + piece.Row * tileSize) * scale);
            double nextX = Math.Round(Bounds.renderX + (imageX + (piece.Column + 1) * tileSize) * scale);
            double nextY = Math.Round(Bounds.renderY + (imageY + (piece.Row + 1) * tileSize) * scale);
            api.Render.Render2DTexturePremultipliedAlpha(textures[i],
                x, y, nextX - x, nextY - y, 60);
        }
    }
    // Textures belong to the game's shared cache and must not be disposed here.
}
