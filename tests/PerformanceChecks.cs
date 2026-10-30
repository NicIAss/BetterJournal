using BetterJournal;

static class PerformanceChecks
{
    public static void Run(Action<bool, string> check)
    {
        var cache = new PageLayoutCache();
        var book = new JournalBook(1, "story", "Title", 1, [new RecoveredPage(3, 0, "one two three four")]);
        int builds = 0;
        List<ReadingPage> Layout(JournalBook b) { builds++; return BookPagination.Layout(b, s => BookPagination.Units(s) <= 2); }
        var first = cache.Get(book, 250, 400, 1.25, 17, () => Layout(book));
        var projectedAgain = book with { EntryId = 99, Title = "Translated title", Pages = book.Pages.ToArray() };
        var second = cache.Get(projectedAgain, 250, 400, 1.25, 17, () => Layout(projectedAgain));
        check(builds == 1 && ReferenceEquals(first, second), "Reopening unchanged content reuses fitted pages across new journal projections");
        cache.Get(book, 250, 400, 1.25, 18, () => Layout(book));
        cache.Get(book, 250, 400, 1.25, 17, () => Layout(book));
        check(builds == 2, "Returning to a recently used text size performs no native fitting");
        cache.Get(book, 251, 400, 1.25, 17, () => Layout(book));
        cache.Get(book, 250, 401, 1.25, 17, () => Layout(book));
        cache.Get(book, 250, 400, 1.5, 17, () => Layout(book));
        check(builds == 5, "Width, height and GUI-scale changes invalidate cached pagination");
        var edited = book with { Pages = [new RecoveredPage(3, 0, "changed text")] };
        var rekeyed = book with { Pages = [new RecoveredPage(7, 0, "one two three four")] };
        var reparted = book with { Pages = [new RecoveredPage(3, 1, "one two three four")] };
        cache.Get(edited, 250, 400, 1.25, 17, () => Layout(edited));
        var rekeyedPages = cache.Get(rekeyed, 250, 400, 1.25, 17, () => Layout(rekeyed));
        cache.Get(reparted, 250, 400, 1.25, 17, () => Layout(reparted));
        check(builds == 8 && rekeyedPages.All(p => p.ChapterId == 7), "Text edits, chapter IDs and explicit fragment parts cannot reuse stale fitted pages");
        cache.Clear(); cache.Get(book, 250, 400, 1.25, 17, () => Layout(book));
        check(builds == 9, "Leaving the world can release all retained fitted layouts");
        var small = new PageLayoutCache(2);
        int before = builds;
        foreach (int font in new[] { 17, 18, 17, 19, 18 }) small.Get(book, 250, 400, 1.25, font, () => Layout(book));
        check(builds - before == 4, "The layout cache evicts the least recently used size instead of growing indefinitely");
        var tiny = new PageLayoutCache(characterBudget: 2);
        before = builds;
        tiny.Get(book, 250, 400, 1.25, 17, () => Layout(book)); tiny.Get(book, 250, 400, 1.25, 17, () => Layout(book));
        check(builds - before == 2, "Oversized layouts are returned normally without exceeding the cache character budget");
        string longText = string.Join(" ", Enumerable.Range(0, 4000).Select(i => "word" + i));
        int largest = 0;
        long measured = 0;
        var pages = BookPagination.Split(longText, s =>
        { largest = Math.Max(largest, s.Length); measured += s.Length; return BookPagination.Units(s) <= 40; });
        check(pages.Count == 100 && string.Join(" ", pages) == longText && largest < longText.Length / 8 && measured < longText.Length * 30L,
            "Long chapters retain every word while native fitting probes remain close to one page instead of half the remaining chapter");
    }
}
