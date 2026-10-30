namespace BetterJournal;

public readonly record struct WindowRect(double X, double Y, double Width, double Height)
{
    public WindowRect Inset(double scale, double lift) => new(
        X + Width * (1 - scale) / 2, Y + Height * (1 - scale) / 2 + lift, Width * scale, Height * scale);
    public static WindowRect Blend(WindowRect from, WindowRect to, double progress)
    {
        double p = Ease(progress);
        return new(from.X + (to.X - from.X) * p, from.Y + (to.Y - from.Y) * p,
            from.Width + (to.Width - from.Width) * p, from.Height + (to.Height - from.Height) * p);
    }
    public static double Ease(double progress)
    { double p = Math.Clamp(progress, 0, 1); return p * p * p * (p * (p * 6 - 15) + 10); }
}
