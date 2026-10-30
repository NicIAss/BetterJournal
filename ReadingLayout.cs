namespace BetterJournal;

// Shared by the resting reader, its captured pages and both reading toolbars.
public static class ReadingLayout
{
    public const double PaperTop = 112;
    public const double ToolbarTop = 14;
    public const double Inset = 20;
    public const double NavigationBottom = 82;
    public static double PaperHeight(double height) => height - 220;
    public static int Spread(double availableWidth, double height) => availableWidth >= PaperHeight(height) * 0.66 * 2 + 56 ? 2 : 1;
    public static double ReaderWidth(double availableWidth, double height) =>
        Math.Min(availableWidth, PaperHeight(height) * 0.66 * Spread(availableWidth, height) + 56);
}
