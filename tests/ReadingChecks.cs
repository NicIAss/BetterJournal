using System.Text.Json;
using BetterJournal;
using Vintagestory.GameContent;

static class ReadingChecks
{
    public static void Run(Action<bool, string> check)
    {
        JournalBook Book((int Id, string Text)[] chapters, int? total = null, int entryId = 1, string code = "test:story", string title = "Story") =>
            JournalBooks.Read([new JournalEntry { EntryId = entryId, LoreCode = code, Title = title,
                Chapters = chapters.Select(c => new JournalChapter { ChapterId = c.Id, Text = c.Text }).ToList() }], s => s,
                total.HasValue ? new Dictionary<string, int> { [code] = total.Value } : null).Single();
        var data = new ReadingProgressData();
        var progress = data.ForWorld("save", "reader");
        var book = Book([(0, "one two three four five six seven eight nine ten"), (1, "second fragment")], 3);
        bool Fits(string s) => BookPagination.Units(s) <= 3;
        var pages = BookPagination.Layout(book, Fits);
        check(progress.UnreadFragments(book) == 2 && !progress.HasRead(book) && !progress.Bookmark(book),
            "Newly received fragments start unread without a fabricated reading position");
        progress.Mark(book, pages.Take(1));
        check(progress.UnreadFragments(book) == 2 && progress.Bookmark(book) && progress.Resume(book, pages) == 1,
            "One viewed leaf leaves the rest of a long fragment unread and resumes at its next text");
        progress.Mark(book, [pages[3]]);
        check(progress.Resume(book, pages) == 1 && progress.UnreadFragments(book) == 2,
            "Jumping to a fragment's end does not count its skipped middle as read");
        var reflow = BookPagination.Layout(book, s => BookPagination.Units(s) <= 2);
        int resumed = progress.Resume(book, reflow);
        check(reflow[resumed].Start <= 3 && reflow[resumed].End > 3 && progress.UnreadFragments(book) == 2,
            "Font/window reflow finds the same unread text even when its page number changes");
        progress.Mark(book, pages);
        check(progress.UnreadFragments(book) == 0 && progress.Bookmark(book),
            "A started incomplete story retains its bookmark after all currently found fragments are viewed");
        var complete = Book([(0, "one two three four five six seven eight nine ten"), (1, "second fragment")], 2);
        check(!progress.Bookmark(complete) && progress.UnreadFragments(complete) == 0,
            "A complete fully viewed story has neither unread fragments nor a bookmark");
        var added = Book([(0, "one two three four five six seven eight nine ten"), (1, "second fragment"), (2, "newly found text")], 3);
        var addedPages = BookPagination.Layout(added, Fits);
        check(progress.UnreadFragments(added) == 1 && addedPages[progress.Resume(added, addedPages)].ChapterId == 2,
            "New discoveries reopen at the new fragment while old fragments remain read");
        var reordered = Book([(1, "second fragment"), (0, "one two three four five six seven eight nine ten")], 2, 77, title: "Translated title");
        check(progress.UnreadFragments(reordered) == 0,
            "Entry ID, title and chapter ordering changes preserve progress for stable lore codes and content");
        var removed = Book([(1, "second fragment")]);
        check(progress.UnreadFragments(removed) == 0 && progress.UnreadFragments(book) == 0,
            "Removed and restored chapters retain dormant progress without inventing received text");
        var changed = Book([(0, "one two three four five six seven eight nine ten eleven"), (1, "second fragment")]);
        check(progress.UnreadFragments(changed) == 1 && progress.UnreadFragments(book) == 0,
            "Edited content becomes unread conservatively while its prior revision remains recoverable");
        var reused = Book([(0, "Completely different story")], entryId: 1, code: "other:story");
        check(progress.UnreadFragments(reused) == 1 && !progress.HasRead(reused),
            "A reused numeric entry ID cannot inherit another lore code's reading progress");
        check(data.ForWorld("save", "other-reader").UnreadFragments(book) == 2 &&
            data.ForWorld("other-save", "reader").UnreadFragments(book) == 2 &&
            data.ForWorld("sa", "vereader").UnreadFragments(book) == 2,
            "Reading memories are isolated by save and player with unambiguous scope keys");
        string text = "<i>alpha &amp; beta gamma___NEWPAGE___delta <img src=\"sample\"/> epsilon zeta</i>";
        var formatted = Book([(4, text)]);
        var small = BookPagination.Layout(formatted, s => BookPagination.Units(s) <= 2);
        var large = BookPagination.Layout(formatted, _ => true);
        var formattedProgress = new ReadingProgress([]);
        formattedProgress.Mark(formatted, small);
        check(small[^1].End == large[^1].End && small[^1].End == ReadingProgress.Fragments(formatted).Single().Units &&
            formattedProgress.UnreadFragments(formatted) == 0 && large.All(p => !formattedProgress.Unread(formatted, p)),
            "Explicit breaks, VTML styles, entities and images retain consistent content ranges across layouts");
        var roundtrip = JsonSerializer.Deserialize<ReadingProgressData>(JsonSerializer.Serialize(data))!;
        check(roundtrip.ForWorld("save", "reader").UnreadFragments(added) == 1 &&
            roundtrip.ForWorld("save", "reader").Resume(book, pages) == progress.Resume(book, pages),
            "Serialized progress restores unread status and the saved text anchor");
        var lastGone = Book([(0, "one two three four five six seven eight nine ten")]);
        var lastGonePages = BookPagination.Layout(lastGone, Fits);
        check(progress.Resume(lastGone, lastGonePages) >= 0 && progress.Resume(lastGone, lastGonePages) < lastGonePages.Count,
            "A deleted bookmark target falls back to a valid current page");
        var translated = JournalBooks.Read([new JournalEntry { LoreCode = "test:translated", Title = "title-key",
            Chapters = [new JournalChapter { ChapterId = 1, Text = "text-key" }] }], s => s == "text-key" ? "original wording" : s).Single();
        var translationProgress = new ReadingProgress([]);
        translationProgress.Mark(translated, BookPagination.Layout(translated, _ => true));
        var translationEdit = JournalBooks.Read([new JournalEntry { LoreCode = "test:translated", Title = "title-key",
            Chapters = [new JournalChapter { ChapterId = 1, Text = "text-key" }] }], s => s == "text-key" ? "changed wording" : s).Single();
        check(translationProgress.UnreadFragments(translationEdit) == 1 && translationProgress.UnreadFragments(translated) == 0,
            "A changed translation under the same source key is treated as a new revision");
        var unkeyed = Book([(2, "unkeyed text")], code: "", title: "mod:title");
        var unkeyedProgress = new ReadingProgress([]);
        unkeyedProgress.Mark(unkeyed, BookPagination.Layout(unkeyed, _ => true));
        check(unkeyedProgress.UnreadFragments(Book([(2, "unkeyed text")], entryId: 99, code: "", title: "mod:title")) == 0,
            "Entries without lore codes use their source title rather than unstable numeric IDs");
        var exposure = new ReadingExposure();
        check(!exposure.Ready(5000, leaving: true), "Closing a never-displayed reader cannot mark animation snapshots as viewed");
        exposure.Observe(100, true);
        check(!exposure.Ready(200) && exposure.Ready(200, leaving: true),
            "An intentional departure records a fully displayed short page before the idle timer expires");
        exposure.Observe(300, false);
        check(!exposure.Ready(5000, leaving: true), "Losing live visibility cancels the page's pending exposure");
        exposure.Observe(500, true);
        check(!exposure.Ready(1000) && exposure.Ready(2200), "A settled reader still records progress while the player stays on a page");
        exposure.Reset();
        check(!exposure.Ready(5000, leaving: true), "A newly composed page cannot inherit the outgoing spread's exposure");
        var tapestryDesign = new TapestryDesign("test:tapestry", "test:name", "tapestry", 2, 2, 1,
            TapestryCollection.Pieces("test", "design", 2, 1));
        var partialTapestry = new TapestryView(tapestryDesign, [true, false], ["Recovered lore"]);
        var completeTapestry = new TapestryView(tapestryDesign, [true, true], ["Recovered lore"]);
        var tapestryProgress = new ReadingProgress([]);
        check(!tapestryProgress.UnreadTapestry(partialTapestry) && !tapestryProgress.ViewTapestry(partialTapestry) &&
            tapestryProgress.UnreadTapestry(completeTapestry),
            "Partial tapestry views never prompt NEW or acknowledge the later completed tapestry");
        check(tapestryProgress.ViewTapestry(completeTapestry) && !tapestryProgress.UnreadTapestry(completeTapestry) &&
            !tapestryProgress.ViewTapestry(completeTapestry),
            "Viewing a completed tapestry clears NEW idempotently");
        check(tapestryProgress.UnreadTapestry(completeTapestry with { Lore = ["Changed recovered lore"] }) &&
            !tapestryProgress.UnreadTapestry(completeTapestry),
            "New tapestry lore prompts again while retaining the acknowledged previous revision");
        var noLoreTapestry = completeTapestry with { Design = tapestryDesign with { Key = "test:no-lore", ChapterId = -1 }, Lore = [] };
        check(tapestryProgress.UnreadTapestry(noLoreTapestry) && tapestryProgress.ViewTapestry(noLoreTapestry) &&
            !tapestryProgress.UnreadTapestry(noLoreTapestry), "Complete artwork without journal lore can also be acknowledged");
        var tapestryData = new ReadingProgressData();
        tapestryData.ForWorld("tapestry-world", "viewer").ViewTapestry(completeTapestry);
        var restoredTapestryData = JsonSerializer.Deserialize<ReadingProgressData>(JsonSerializer.Serialize(tapestryData))!;
        check(!restoredTapestryData.ForWorld("tapestry-world", "viewer").UnreadTapestry(completeTapestry) &&
            restoredTapestryData.ForWorld("tapestry-world", "other-viewer").UnreadTapestry(completeTapestry),
            "Tapestry acknowledgements persist in the existing schema and stay isolated by player");
        var location = Path.Combine(Path.GetTempPath(), "BetterJournal-reading-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(location);
        try
        {
            string path = Path.Combine(location, "progress.json");
            var warnings = new List<string>();
            var file = new ReadingProgressFile(path, warnings.Add);
            check(file.Load().Worlds.Count == 0 && file.Save(data) && file.Save(data) && File.Exists(path + ".bak"),
                "Reading progress saves atomically with a previous valid backup");
            File.WriteAllText(path, "truncated{");
            var recoveredFile = new ReadingProgressFile(path, warnings.Add);
            var recovered = recoveredFile.Load();
            check(recovered.ForWorld("save", "reader").UnreadFragments(book) == 0 && recoveredFile.Save(recovered) &&
                new ReadingProgressFile(path, warnings.Add).Load().ForWorld("save", "reader").UnreadFragments(book) == 0,
                "A truncated primary recovers from backup and can save safely again");
            File.WriteAllText(path, "{\"Version\":99,\"Worlds\":{}}");
            var futureFile = new ReadingProgressFile(path, warnings.Add);
            futureFile.Load();
            check(!futureFile.Writable && !futureFile.Save(data) && File.ReadAllText(path).Contains("99"),
                "Unknown future schemas are preserved without overwriting or importing an older backup");
            File.WriteAllText(path, "broken"); File.WriteAllText(path + ".bak", "also broken");
            var brokenFile = new ReadingProgressFile(path, warnings.Add);
            check(brokenFile.Load().Worlds.Count == 0 && !brokenFile.Writable && !brokenFile.Save(data) && File.ReadAllText(path) == "broken",
                "If both progress files are unreadable the reader remains usable and preserves both originals");
        }
        finally { Directory.Delete(location, true); }
    }
}
