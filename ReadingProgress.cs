using System.Security.Cryptography;
using System.Text;
using System.Runtime.CompilerServices;

namespace BetterJournal;

public sealed record ReadRange(int Start, int End);
public sealed record ReadingFragment(int Ordinal, string Key, int Units);
public sealed class BookReadingMemory
{
    public Dictionary<string, List<ReadRange>> Fragments { get; set; } = [];
    public string? LastFragment { get; set; }
    public int LastOffset { get; set; }
}

public sealed class ReadingProgressData
{
    public int Version { get; set; } = 1;
    public Dictionary<string, Dictionary<string, BookReadingMemory>> Worlds { get; set; } = [];
    public ReadingProgress ForWorld(string world, string player)
    {
        string key = world.Length + ":" + world + player;
        if (!Worlds.TryGetValue(key, out var books) || books == null) Worlds[key] = books = [];
        return new ReadingProgress(books);
    }
}

// Only received fragments enter this projection. Missing mods/chapters leave
// dormant records; they never reveal artwork, text, or undiscovered chapters.
public sealed class ReadingProgress(Dictionary<string, BookReadingMemory> books)
{
    private static readonly ConditionalWeakTable<JournalBook, List<ReadingFragment>> FragmentCache = new();
    public static string Fingerprint(params string[] values)
    {
        string packed = string.Concat(values.Select(v => v.Length + ":" + v));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(packed)));
    }
    public static string BookKey(JournalBook book) => book.ProgressKey ??
        (book.Code.Length > 0 ? "code:" + book.Code : "title:" + Fingerprint(book.Title));
    public static IReadOnlyList<ReadingFragment> Fragments(JournalBook book) => FragmentCache.GetValue(book, BuildFragments);
    private static List<ReadingFragment> BuildFragments(JournalBook book)
    {
        var result = new List<ReadingFragment>();
        foreach (var part in book.Pages)
        {
            int units = Math.Max(1, BookPagination.Units(part.Text));
            if (part.Part == 0 || result.Count == 0)
                result.Add(new(result.Count + 1, part.ChapterId + ":" + (part.Revision.Length > 0
                    ? part.Revision : Fingerprint(string.Join("\0", book.Pages.Where(p => p.ChapterId == part.ChapterId).Select(p => p.Text)))), units));
            else result[^1] = result[^1] with { Units = result[^1].Units + units };
        }
        return result;
    }
    private BookReadingMemory? Memory(JournalBook book) => books.GetValueOrDefault(BookKey(book));
    private static List<ReadRange> Ranges(BookReadingMemory? memory, ReadingFragment fragment) =>
        (memory?.Fragments?.GetValueOrDefault(fragment.Key) ?? [])
        .Where(r => r != null && r.Start >= 0 && r.End > r.Start && r.Start < fragment.Units)
        .Select(r => new ReadRange(r.Start, Math.Min(r.End, fragment.Units))).OrderBy(r => r.Start).ToList();
    private static int Gap(IEnumerable<ReadRange> ranges, int start, int end)
    {
        int at = start;
        foreach (var r in ranges)
        {
            if (r.Start > at) break;
            at = Math.Max(at, r.End);
            if (at >= end) return end;
        }
        return at;
    }
    public int UnreadFragments(JournalBook book)
    {
        var memory = Memory(book);
        return Fragments(book).Count(f => Gap(Ranges(memory, f), 0, f.Units) < f.Units);
    }
    public bool HasRead(JournalBook book) => Fragments(book).Any(f => Ranges(Memory(book), f).Count > 0);
    public bool Bookmark(JournalBook book) => HasRead(book) &&
        !(UnreadFragments(book) == 0 && book.TotalFragments is int total && book.FragmentCount == total);
    private static JournalBook TapestryMemory(TapestryView tapestry) => new(-1, tapestry.Design.Key, "", 1,
        [new RecoveredPage(0, 0, "", Fingerprint(tapestry.Design.Key,
            string.Join("\0", tapestry.Design.Pieces.Select(p => p.Type + ":" + p.Texture)),
            string.Join("\0", tapestry.Lore)))], 1, "tapestry:" + tapestry.Design.Key);
    public bool UnreadTapestry(TapestryView tapestry) => tapestry.Complete && UnreadFragments(TapestryMemory(tapestry)) > 0;
    public bool ViewTapestry(TapestryView tapestry) => tapestry.Complete &&
        Mark(TapestryMemory(tapestry), [new ReadingPage(0, 1, "", 0, 1)]);
    public bool Unread(JournalBook book, ReadingPage page)
    {
        var fragment = Fragments(book).ElementAtOrDefault(page.Fragment - 1);
        return fragment != null && Gap(Ranges(Memory(book), fragment), page.Start, page.End) < page.End;
    }
    public int Resume(JournalBook book, IReadOnlyList<ReadingPage> pages)
    {
        for (int i = 0; i < pages.Count; i++) if (Unread(book, pages[i])) return i;
        var memory = Memory(book);
        var fragment = Fragments(book).FirstOrDefault(f => f.Key == memory?.LastFragment);
        if (fragment != null)
            for (int i = 0; i < pages.Count; i++)
                if (pages[i].Fragment == fragment.Ordinal && pages[i].End > memory!.LastOffset) return i;
        return 0;
    }
    public int NextUnread(JournalBook book, IReadOnlyList<ReadingPage> pages, int after)
    {
        for (int n = 1; n <= pages.Count; n++)
        {
            int i = (after + n) % pages.Count;
            if (Unread(book, pages[i])) return i;
        }
        return -1;
    }
    public bool Mark(JournalBook book, IEnumerable<ReadingPage> pages)
    {
        var fragments = Fragments(book);
        var visible = pages.Where(p => p.End > p.Start && p.Fragment > 0 && p.Fragment <= fragments.Count).ToList();
        if (visible.Count == 0) return false;
        string key = BookKey(book);
        if (!books.TryGetValue(key, out var memory) || memory == null) books[key] = memory = new();
        memory.Fragments ??= [];
        bool changed = false;
        foreach (var page in visible)
        {
            var fragment = fragments[page.Fragment - 1];
            var ranges = Ranges(memory, fragment);
            int end = Math.Min(page.End, fragment.Units);
            if (Gap(ranges, page.Start, end) >= end) continue;
            ranges.Add(new(page.Start, end));
            var merged = new List<ReadRange>();
            foreach (var r in ranges.OrderBy(r => r.Start))
                if (merged.Count == 0 || merged[^1].End < r.Start) merged.Add(r);
                else merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, r.End) };
            memory.Fragments[fragment.Key] = merged; changed = true;
        }
        string anchor = fragments[visible[0].Fragment - 1].Key;
        if (memory.LastFragment != anchor || memory.LastOffset != visible[0].Start)
        { memory.LastFragment = anchor; memory.LastOffset = visible[0].Start; changed = true; }
        return changed;
    }
}

public sealed class ReadingProgressFile(string path, Action<string> warning)
    : SavedDataFile<ReadingProgressData>(path, warning);
