using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;

namespace BetterJournal;

// Render each complete native view once. Its controls, text and artwork then
// travel together; layout and hit testing become live only after landing.
public sealed class WindowMotion(ICoreClientAPI api) : GuiElement(api, ElementBounds.Empty)
{
    public sealed class Snapshot(ICoreClientAPI api, FrameBufferRef frame, WindowRect rect) : IDisposable
    {
        public readonly FrameBufferRef Frame = frame;
        public readonly WindowRect Rect = rect;
        private bool disposed;
        private readonly LoadedTexture texture = new(api) { TextureId = frame.ColorTextureIds[0], Width = frame.Width, Height = frame.Height };
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; api.Render.DestroyFrameBuffer(Frame); texture.Dispose();
        }
    }
    private Snapshot? outgoing, incoming;
    private WindowRect from, to;
    private long beganAt;
    private double duration;
    private Action? finished;
    private bool completed;
    private LoadedTexture panel = new(api);
    public bool Active => incoming != null || outgoing != null;

    public Snapshot Capture(WindowRect rect, Action render)
    {
        var r = api.Render;
        var previous = r.CurrentFrameBuffer;
        FrameBufferRef? frame = null;
        try
        {
            frame = r.CreateFrameBuffer(new FramebufferAttrs("betterjournal-transition", r.FrameWidth, r.FrameHeight)
            {
                Attachments = [new FramebufferAttrsAttachment { AttachmentType = EnumFramebufferAttachment.ColorAttachment0,
                    Texture = new RawTexture { Width = r.FrameWidth, Height = r.FrameHeight } }]
            });
            r.CurrentFrameBuffer = frame;
            r.ClearFrameBuffer(frame, [0, 0, 0, 0], false);
            render();
            return new Snapshot(api, frame, rect);
        }
        catch
        {
            if (frame != null) new Snapshot(api, frame, rect).Dispose();
            throw;
        }
        finally
        {
            r.CurrentFrameBuffer = previous;
            r.GlViewport(0, 0, previous?.Width ?? r.FrameWidth, previous?.Height ?? r.FrameHeight);
        }
    }

    public void Begin(Snapshot? oldView, Snapshot? newView, Action? after = null)
    {
        Cancel(); outgoing = oldView; incoming = newView; finished = after; completed = false;
        var rect = (newView ?? oldView)!.Rect;
        from = oldView?.Rect ?? rect.Inset(0.965, 16);
        to = newView?.Rect ?? rect.Inset(0.975, 12);
        duration = oldView == null ? 0.32 : newView == null ? 0.24 : oldView.Rect.Width != newView.Rect.Width ? 0.44 : 0.32;
        beganAt = api.ElapsedMilliseconds;
        if (panel.TextureId == 0)
        {
            using var surface = new ImageSurface(Format.Argb32, 1, 1);
            using var ctx = new Context(surface);
            ctx.SetSourceRGBA(GuiStyle.DialogStrongBgColor); ctx.Paint(); generateTexture(surface, ref panel);
        }
    }

    public void Draw()
    {
        if (!Active) return;
        double t = Math.Clamp((api.ElapsedMilliseconds - beganAt) / (duration * 1000), 0, 1);
        var rect = WindowRect.Blend(from, to, t);
        float visibility = (float)WindowRect.Ease(outgoing == null ? t : incoming == null ? 1 - t : 1);
        api.Render.Render2DTexturePremultipliedAlpha(panel.TextureId, rect.X, rect.Y, rect.Width, rect.Height, 450,
            new Vec4f(visibility, visibility, visibility, visibility));
        // Briefly fade through the leather background, so titles from different
        // views never overlap and the world cannot flash through a resize.
        var clip = ElementBounds.Fixed(rect.X / RuntimeScale, rect.Y / RuntimeScale, rect.Width / RuntimeScale, rect.Height / RuntimeScale)
            .WithParent(ElementBounds.Empty);
        clip.CalcWorldBounds(); api.Render.PushScissor(clip, true);
        try
        {
            if (outgoing != null) DrawSnapshot(outgoing, rect, incoming == null ? visibility : (float)(1 - WindowRect.Ease(t / 0.48)));
            if (incoming != null) DrawSnapshot(incoming, rect, outgoing == null ? visibility : (float)WindowRect.Ease((t - 0.38) / 0.62));
        }
        finally { api.Render.PopScissor(); }
        if (t == 1 && !completed)
        {
            completed = true;
            var after = finished;
            Cancel(); after?.Invoke();
        }
    }
    private static double RuntimeScale => Vintagestory.API.Config.RuntimeEnv.GUIScale;
    private void DrawSnapshot(Snapshot view, WindowRect rect, float alpha)
    {
        if (alpha <= 0) return;
        double sx = rect.Width / view.Rect.Width, sy = rect.Height / view.Rect.Height;
        var r = api.Render;
        r.GlPushMatrix();
        try
        {
            r.GlTranslate(rect.X - view.Rect.X * sx, rect.Y - view.Rect.Y * sy, 0);
            r.GlScale((float)sx, (float)sy, 1);
            // Framebuffer textures have their origin at the lower left.
            r.Render2DTexturePremultipliedAlpha(view.Frame.ColorTextureIds[0], 0d, view.Frame.Height,
                view.Frame.Width, -view.Frame.Height, 451, new Vec4f(alpha, alpha, alpha, alpha));
        }
        finally { r.GlPopMatrix(); }
    }
    public void Cancel()
    {
        outgoing?.Dispose(); incoming?.Dispose(); outgoing = incoming = null; finished = null;
    }
    public override void Dispose() { Cancel(); panel.Dispose(); base.Dispose(); }
}
