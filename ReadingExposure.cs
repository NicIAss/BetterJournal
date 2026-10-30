namespace BetterJournal;

// Only the live, settled reader calls Observe. Snapshot/animation rendering
// cannot make content eligible. An intentional departure acknowledges a
// displayed page immediately, including a very short final page.
public sealed class ReadingExposure
{
    private long? since;
    public void Reset() => since = null;
    public void Observe(long now, bool visible)
    {
        if (!visible) Reset();
        else since ??= now;
    }
    public bool Ready(long now, bool leaving = false) => since.HasValue && (leaving || now - since.Value >= 1500);
}
