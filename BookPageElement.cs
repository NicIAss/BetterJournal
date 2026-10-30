using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BetterJournal;

// Native VTML interaction, with a fit fallback for indivisible oversized modded
// illustrations or words. Ordinary text is paginated at the chosen font size.
public sealed class BookPageElement(ICoreClientAPI api, string text, CairoFont font, ElementBounds bounds, double availableHeight, bool fadeIn = false)
    : GuiElementRichtext(api, VtmlUtil.Richtextify(api, text, font), bounds)
{
    private long? fadeStartedAt;
    private bool fading = fadeIn;
    public double AvailableHeight => availableHeight;
    public void UpdateText(string text, CairoFont font, bool fade = true)
    {
        SetNewTextWithoutRecompose(text, font);
        RecomposeText();
        fading = fade; fadeStartedAt = null;
    }
    private double FitScale => Math.Min(1, Math.Min(
        availableHeight * RuntimeEnv.GUIScale / Math.Max(1, TotalHeight),
        Bounds.InnerWidth / Math.Max(1, MaxLineWidth)));

    public override void RenderInteractiveElements(float dt)
    {
        // Only this rich-text element fades after reflow. Paper, artwork,
        // controls and window remain live, including during repeated clicks.
        var original = RenderColor;
        if (fading)
        {
            fadeStartedAt ??= api.ElapsedMilliseconds;
            float alpha = (float)WindowRect.Ease((api.ElapsedMilliseconds - fadeStartedAt.Value) / 140d);
            if (alpha < 1) RenderColor = new Vec4f((original?.X ?? 1) * alpha, (original?.Y ?? 1) * alpha,
                (original?.Z ?? 1) * alpha, (original?.W ?? 1) * alpha);
            else fading = false;
        }
        try
        {
            double fit = FitScale;
            if (fit >= 0.999) { base.RenderInteractiveElements(dt); return; }
            api.Render.Render2DTexturePremultipliedAlpha(richtTextTexture.TextureId,
                Bounds.renderX, Bounds.renderY, richtTextTexture.Width * fit, richtTextTexture.Height * fit, zPos, RenderColor);
        }
        finally { RenderColor = original; }
    }

    private MouseEvent Unscale(MouseEvent args) => new(
        (int)(Bounds.absX + (args.X - Bounds.absX) / FitScale),
        (int)(Bounds.absY + (args.Y - Bounds.absY) / FitScale), args.Button, 0);

    public override void OnMouseMove(ICoreClientAPI api, MouseEvent args)
    { var local = Unscale(args); base.OnMouseMove(api, local); args.Handled = local.Handled; }
    public override void OnMouseDownOnElement(ICoreClientAPI api, MouseEvent args)
    { var local = Unscale(args); base.OnMouseDownOnElement(api, local); args.Handled = local.Handled; }
}
