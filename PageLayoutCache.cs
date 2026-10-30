namespace BetterJournal;

// Keep a few recent native fits, not an unbounded copy of every discovered book.
public sealed class PageLayoutCache(int capacity = 12, int characterBudget = 2_000_000)
{
    private sealed record Entry(string[] Text, int[] Chapters, int[] Parts, double Width, double Height, double Scale,
        int Font, List<ReadingPage> Pages, int Characters);
    private readonly List<Entry> recent = [];
    private int characters;
    public List<ReadingPage> Get(JournalBook book, double width, double height, double scale, int font,
        Func<List<ReadingPage>> layout)
    {
        for (int i = 0; i < recent.Count; i++)
        {
            var entry = recent[i];
            if (entry.Width != width || entry.Height != height || entry.Scale != scale || entry.Font != font || entry.Text.Length != book.Pages.Count) continue;
            bool same = true;
            for (int p = 0; p < entry.Text.Length && same; p++)
                same = entry.Text[p] == book.Pages[p].Text && entry.Chapters[p] == book.Pages[p].ChapterId && entry.Parts[p] == book.Pages[p].Part;
            if (!same) continue;
            recent.RemoveAt(i); recent.Add(entry); return entry.Pages;
        }
        var pages = layout();
        long size = book.Pages.Sum(p => (long)p.Text.Length) + pages.Sum(p => (long)p.Text.Length);
        if (capacity <= 0 || size > characterBudget) return pages;
        while (recent.Count > 0 && (recent.Count >= capacity || characters + size > characterBudget))
        { characters -= recent[0].Characters; recent.RemoveAt(0); }
        recent.Add(new(book.Pages.Select(p => p.Text).ToArray(), book.Pages.Select(p => p.ChapterId).ToArray(),
            book.Pages.Select(p => p.Part).ToArray(), width, height, scale, font, pages, (int)size));
        characters += (int)size;
        return pages;
    }
    public void Clear() { recent.Clear(); characters = 0; }
}
