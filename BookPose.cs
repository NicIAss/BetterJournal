namespace BetterJournal;

// Fixed front/back faces meet only at an edge-on pose.
public static class BookPose
{
    public const double CoverOverhang = 10;
    public const double CoverVerticalOverhang = 8;
    public static double BoardWidth(double leafWidth, bool spread) => leafWidth + CoverOverhang * (spread ? 1 : 2);
    public static double CoverHinge(double left, double leafWidth, bool spread, double progress) => spread
        ? left + (leafWidth - CoverOverhang) / 2 + BoardWidth(leafWidth, true) / 2 * Ease(progress)
        : left - CoverOverhang;
    public static (double X, double Y) CoverVertex(double progress, double across, bool bottom,
        bool right, double leafWidth, double leafHeight, bool spread = true)
    {
        double angle = Math.PI * Ease(progress);
        double x = BoardWidth(leafWidth, spread) * across * Math.Abs(Math.Cos(angle));
        // A thick board lifts less than a flexible sheet. Its lower rim must
        // still conceal the paper while the outside face is visible.
        double lift = 4 * Math.Sin(angle) * across;
        return ((right ? 1 : -1) * x,
            (bottom ? leafHeight + CoverVerticalOverhang : -CoverVerticalOverhang) - lift);
    }
    public static double Ease(double progress)
    {
        double p = Math.Clamp(progress, 0, 1);
        return p * p * (3 - 2 * p);
    }
    public static double Cosine(double progress) => Math.Cos(Math.PI * Ease(progress));
    public static bool FrontVisible(double progress) => progress < 0.5;
    // Slots: old left/right = 0/1, new left/right = 2/3.
    public static int TurnFace(int direction, double progress) =>
        FrontVisible(progress) ? (direction > 0 ? 1 : 0) : (direction > 0 ? 2 : 3);
    public static double OpeningProgress(BookMotionKind kind, double progress) =>
        kind == BookMotionKind.Close ? 1 - Math.Clamp(progress, 0, 1) : Math.Clamp(progress, 0, 1);
    public static (double X, double Y) Vertex(double progress, double across, bool bottom,
        bool right, double leafWidth, double leafHeight)
    {
        double angle = Math.PI * Ease(progress), bend = Math.Sin(angle);
        double distance = leafWidth * Math.Abs(Math.Cos(angle)) *
            (across + 0.10 * bend * Math.Sin(Math.PI * across));
        double lift = 12 * bend * across * across;
        return ((right ? 1 : -1) * distance, bottom ? leafHeight + lift * 0.35 : -lift);
    }
}
