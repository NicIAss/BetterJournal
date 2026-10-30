using Cairo;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace BetterJournal;

public enum BookMotionKind { None, Turn, Open, Close }

// Four immutable leaves form a turn. Opening/closing use the same complete
// book scene in opposite directions. Composition changes only after landing.
public sealed class BookMotion(ICoreClientAPI api, BookMaterials materials) : GuiElement(api, ElementBounds.Empty)
{
    private string currentCover = "normal-darkgreen";
    private readonly LoadedTexture[] leaves = Enumerable.Range(0, 4).Select(_ => new LoadedTexture(api)).ToArray();
    private LoadedTexture cover = new(api), insideCover = new(api), rearBoard = new(api), backing = new(api), mask = new(api), shadow = new(api);
    private MeshRef? leafMesh;
    private readonly MeshData movingMesh = new(50, 144, false, true, false, false);
    private sealed record SceneKey(double Width, double Height, double Scale, bool Spread, string Cover, string Title, string Caption);
    private sealed record LeafKey(double Width, double Height, double Scale, bool Spread, int Side, string Cover,
        string Text, string Heading, string Number, double FontSize, string FontName);
    private SceneKey? sceneKey;
    private readonly LeafKey?[] leafKeys = new LeafKey?[4];
    private long beganAt;
    private int direction;
    private bool completionRequested;
    private Action? finished;
    public BookMotionKind Kind { get; private set; }
    public bool Active => Kind != BookMotionKind.None;
    private double Duration => Kind == BookMotionKind.Turn ? 0.64 : 0.76;

    public void CaptureScene(double width, double height, bool spread, JournalBook book, string caption)
    {
        currentCover = materials.Cover(book);
        var key = new SceneKey(width, height, RuntimeEnv.GUIScale, spread, currentCover, book.Title, caption);
        if (sceneKey == key) return;
        double paperWidth = width - 56, h = ReadingLayout.PaperHeight(height), w = paperWidth / (spread ? 2 : 1);
        Capture(ref backing, paperWidth + 24, h + 24, ctx =>
        {
            ctx.Translate(-16, -100);
            BookArtwork.OpenBook(ctx, materials, currentCover, width, height, spread);
        });
        double boardWidth = BookPose.BoardWidth(w, spread), boardHeight = h + 2 * BookPose.CoverVerticalOverhang;
        // A single physical back board stays full-sized behind its page block.
        // Its fittings cannot drift into the binding as the front board opens.
        Capture(ref rearBoard, boardWidth, boardHeight, ctx =>
            materials.Board(ctx, currentCover, front: false, inside: true, 0, 0,
                Math.Ceiling(boardWidth * RuntimeEnv.GUIScale) / RuntimeEnv.GUIScale,
                Math.Ceiling(boardHeight * RuntimeEnv.GUIScale) / RuntimeEnv.GUIScale));
        Capture(ref mask, paperWidth + 26, h + 31, ctx =>
        { ctx.Translate(-15, -140); materials.Background(ctx, width, height + 42); });
        Capture(ref cover, boardWidth, boardHeight, ctx =>
        {
            // Fill the whole pixel-rounded surface, including its final row
            // and column. A translucent rounding fringe exposes white paper.
            double scale = RuntimeEnv.GUIScale;
            materials.Board(ctx, currentCover, front: true, inside: false, 0, 0,
                Math.Ceiling(boardWidth * scale) / scale, Math.Ceiling(boardHeight * scale) / scale);
            BookArtwork.Ornament(ctx, boardWidth / 2, boardHeight / 2 - 45);
            using var titleText = new GuiElementStaticText(api, book.Title, EnumTextOrientation.Center,
                ElementBounds.Fixed(26, boardHeight / 2 - 13, boardWidth - 52, 100).WithParent(ElementBounds.Empty),
                new CairoFont(20, GuiStyle.DecorativeFontName, [0.87, 0.79, 0.62, 1]));
            ctx.Save(); ctx.Scale(1 / RuntimeEnv.GUIScale, 1 / RuntimeEnv.GUIScale);
            titleText.ComposeTextElements(ctx, null!); ctx.Restore();
            BookArtwork.Text(ctx, caption, boardWidth / 2, boardHeight - 57, 12, maxWidth: boardWidth - 48, center: true);
        });
        if (shadow.TextureId == 0)
            Capture(ref shadow, 64, 1, ctx =>
            {
                using var gradient = new LinearGradient(0, 0, 64, 0);
                gradient.AddColorStop(0, new Color(0, 0, 0, 0.30));
                gradient.AddColorStop(1, new Color(0, 0, 0, 0));
                ctx.SetSource(gradient); ctx.Paint();
            });
        sceneKey = key;
    }

    private void Capture(ref LoadedTexture texture, double w, double h, Action<Context> paint)
    {
        double scale = RuntimeEnv.GUIScale;
        using var surface = new ImageSurface(Format.Argb32, Math.Max(1, (int)Math.Ceiling(w * scale)), Math.Max(1, (int)Math.Ceiling(h * scale)));
        using var ctx = new Context(surface);
        ctx.Scale(scale, scale); paint(ctx); generateTexture(surface, ref texture);
    }

    public void CaptureLeaf(int slot, int side, string text, string heading, string number,
        CairoFont font, double width, double height, bool spread)
    {
        double scale = RuntimeEnv.GUIScale, w = (width - 56) / (spread ? 2 : 1), h = ReadingLayout.PaperHeight(height);
        var key = new LeafKey(width, height, scale, spread, side, currentCover, text, heading, number, font.UnscaledFontsize, font.Fontname);
        if (leafKeys[slot] == key) return;
        using var surface = new ImageSurface(Format.Argb32, (int)Math.Ceiling(w * scale), (int)Math.Ceiling(h * scale));
        using var ctx = new Context(surface);
        ctx.Save(); ctx.Scale(scale, scale); ctx.Translate(-28 - side * w, -112);
        BookArtwork.OpenBook(ctx, materials, currentCover, width, height, spread); ctx.Restore();
        using var header = new GuiElementStaticText(api, heading, EnumTextOrientation.Left,
            ElementBounds.Fixed(32, 14, w - 64, 18).WithParent(ElementBounds.Empty), font.Clone().WithFontSize(10));
        using var folio = new GuiElementStaticText(api, number, EnumTextOrientation.Center,
            ElementBounds.Fixed(w / 2 - 25, h - 30, 50, 20).WithParent(ElementBounds.Empty), font.Clone().WithFontSize(12));
        header.ComposeTextElements(ctx, surface); folio.ComposeTextElements(ctx, surface);
        using var rich = new GuiElementRichtext(api, VtmlUtil.Richtextify(api, text, font), ElementBounds.Fixed(0, 0, w - 64, h - 88));
        rich.CalcHeightAndPositions();
        double fit = Math.Min(1, Math.Min((h - 88) * scale / Math.Max(1, rich.TotalHeight), (w - 64) * scale / Math.Max(1, rich.MaxLineWidth)));
        ctx.Save(); ctx.Translate(32 * scale, 37 * scale); ctx.Scale(fit, fit);
        rich.ComposeFor(rich.Bounds.CopyOnlySize(), ctx, surface); ctx.Restore();
        generateTexture(surface, ref leaves[slot]);
        if (slot == 0)
        {
            double boardWidth = BookPose.BoardWidth(w, spread), boardHeight = h + 2 * BookPose.CoverVerticalOverhang;
            Capture(ref insideCover, boardWidth, boardHeight, board =>
            {
                materials.Board(board, currentCover, front: true, inside: true, 0, 0, boardWidth, boardHeight);
                board.Save(); board.Translate(BookPose.CoverOverhang, BookPose.CoverVerticalOverhang);
                board.Scale(1 / scale, 1 / scale);
                board.SetSourceSurface(surface, 0, 0);
                board.Paint(); board.Restore();
            });
        }
        leafKeys[slot] = key;
    }

    public void Begin(BookMotionKind kind, int direction = 1, Action? finished = null)
    { Kind = kind; this.direction = direction; this.finished = finished; beganAt = api.ElapsedMilliseconds; completionRequested = false; }
    public void Cancel() { Kind = BookMotionKind.None; finished = null; }

    private void Texture(LoadedTexture texture, double x, double y, double w, double h, float z, float alpha = 1)
    {
        double scale = RuntimeEnv.GUIScale;
        api.Render.Render2DTexturePremultipliedAlpha(texture.TextureId, x * scale, y * scale, w * scale, h * scale, z,
            alpha == 1 ? null : new Vec4f(alpha, alpha, alpha, alpha));
    }

    private void MovingLeaf(LoadedTexture texture, double progress, double hinge, double top, double w, double h, bool right, bool hardcover = false, bool spread = true)
    {
        if (Math.Abs(BookPose.Cosine(progress)) < 0.002) return;
        // The sheet bows slightly and its free edge lifts; the binding is fixed.
        const int strips = 24;
        var mesh = movingMesh;
        mesh.VerticesCount = mesh.IndicesCount = 0;
        for (int i = 0; i <= strips; i++)
        {
            double across = (double)i / strips;
            for (int row = 0; row < 2; row++)
            {
                var (x, y) = hardcover
                    ? BookPose.CoverVertex(progress, across, row == 1, right, w, h, spread)
                    : BookPose.Vertex(progress, across, row == 1, right, w, h);
                // Texture rectangles and the mesh rasterize fractional edges
                // differently. Overdraw one screen pixel at the closed-side
                // binding so the stationary white page cannot peek through.
                if (hardcover && right) x -= (1 - across) * Math.Abs(BookPose.Cosine(progress)) / RuntimeEnv.GUIScale;
                mesh.AddVertex((float)(2 * x / w - 1), (float)(2 * y / h - 1), 0,
                    (float)(right ? across : 1 - across), row);
            }
        }
        for (int i = 0; i < strips; i++)
        {
            int a = i * 2;
            mesh.AddIndex(a); mesh.AddIndex(right ? a + 2 : a + 1); mesh.AddIndex(right ? a + 1 : a + 2);
            mesh.AddIndex(a + 2); mesh.AddIndex(right ? a + 3 : a + 1); mesh.AddIndex(right ? a + 1 : a + 3);
        }
        if (leafMesh == null) leafMesh = api.Render.UploadMesh(mesh); else api.Render.UpdateMesh(leafMesh, mesh);
        double scale = RuntimeEnv.GUIScale;
        api.Render.Render2DTexture(leafMesh, texture.TextureId, (float)(hinge * scale), (float)(top * scale), (float)(w * scale), (float)(h * scale), 420);
    }

    public void Draw(double left, double top, double paperWidth, double paperHeight, bool spread)
    {
        if (!Active) return;
        double t = Math.Clamp((api.ElapsedMilliseconds - beganAt) / (Duration * 1000), 0, 1);
        DrawPose(left, top, paperWidth, paperHeight, spread, Kind, t);
        Complete(t);
    }

    public void DrawClosed(double left, double top, double paperWidth, double paperHeight, bool spread)
        => DrawPose(left, top, paperWidth, paperHeight, spread, BookMotionKind.Open, 0);

    private void DrawPose(double left, double top, double paperWidth, double paperHeight, bool spread, BookMotionKind kind, double t)
    {
        double leaf = paperWidth / (spread ? 2 : 1), hinge = spread ? left + leaf : left;
        // Hide the entire original book: both pages, covers, and paper edges.
        Texture(mask, left - 13, top - 14, paperWidth + 26, paperHeight + 31, 400);
        if (kind == BookMotionKind.Turn)
        {
            Texture(backing, left - 12, top - 12, paperWidth + 24, paperHeight + 24, 402);
            bool front = BookPose.FrontVisible(t), right = direction > 0 ? front : !front;
            if (spread)
            {
                Texture(leaves[direction > 0 ? 0 : 2], left, top, leaf, paperHeight, 405);
                Texture(leaves[direction > 0 ? 3 : 1], hinge, top, leaf, paperHeight, 405);
            }
            else
            {
                Texture(leaves[2], left, top, leaf, paperHeight, 405);
                right = direction > 0;
                hinge = right ? left : left + leaf;
            }
            double leafProgress = spread ? t : t / 2;
            double edge = hinge + (right ? 1 : -1) * leaf * Math.Abs(BookPose.Cosine(leafProgress));
            Texture(shadow, edge, top, right ? 36 : -36, paperHeight, 410, (float)Math.Sin(Math.PI * t));
            int moving = spread ? BookPose.TurnFace(direction, t) : 0;
            MovingLeaf(leaves[moving], leafProgress, hinge, top, leaf, paperHeight, right);
        }
        else
        {
            double p = BookPose.OpeningProgress(kind, t);
            hinge = BookPose.CoverHinge(left, leaf, spread, p);
            Texture(rearBoard, hinge, top - BookPose.CoverVerticalOverhang,
                BookPose.BoardWidth(leaf, spread), paperHeight + 2 * BookPose.CoverVerticalOverhang, 402);
            Texture(leaves[spread ? 1 : 0], spread ? hinge : left, top, leaf, paperHeight, 405);
            double coverProgress = spread ? p : p / 2;
            bool front = BookPose.FrontVisible(coverProgress);
            if (front || spread)
            {
                bool right = front;
                double edge = hinge + (right ? 1 : -1) * BookPose.BoardWidth(leaf, spread) * Math.Abs(BookPose.Cosine(coverProgress));
                Texture(shadow, edge, top, right ? 36 : -36, paperHeight, 410, (float)Math.Sin(Math.PI * p));
                // Both immutable faces belong to this same rigid board. The
                // inner face includes the fitted page and its outer fittings.
                MovingLeaf(front ? cover : insideCover, coverProgress, hinge, top, leaf, paperHeight, right, hardcover: true, spread: spread);
            }
        }
    }

    private void Complete(double t)
    {
        if (t < 1 || completionRequested) return;
        // Latch the final pose until the queued composer update commits, so an
        // old spread cannot flash between the animation and the new layout.
        completionRequested = true; finished?.Invoke();
    }
    public override void Dispose()
    {
        Cancel(); foreach (var leaf in leaves) leaf.Dispose();
        cover.Dispose(); insideCover.Dispose(); rearBoard.Dispose(); backing.Dispose(); mask.Dispose(); shadow.Dispose(); leafMesh?.Dispose(); base.Dispose();
    }
}
