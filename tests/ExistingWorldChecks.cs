using BetterJournal;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

static class ExistingWorldChecks
{
    public static void Run(Action<bool, string> check)
    {
        string root = Path.Combine(Path.GetTempPath(), "BetterJournal-existing-world-" + Guid.NewGuid().ToString("N"));
        var story = new JournalEntry { EntryId = 0, LoreCode = "letters", Title = "Letters", Chapters =
            [new JournalChapter { EntryId = 0, ChapterId = 0, Text = "Previously recovered words" }] };
        var tapestryEntry = new JournalEntry { EntryId = 1, LoreCode = "tapestry", Title = "Tapestries", Chapters =
            [new JournalChapter { EntryId = 1, ChapterId = 4, Text = "existing-tapestry-lore" }] };
        var journal = new Journal { Entries = [story, tapestryEntry] };
        var priorJournal = SerializerUtil.Serialize(journal);
        var canonical = new TapestryDesign("game:tapestry/known", "known", "tapestry", 4, 2, 1,
            TapestryCollection.Pieces("game", "known", 2, 1), LoreSource: "existing-tapestry-lore");
        var alternate = canonical with { Key = "game:tapestry/alternate", InferFromLore = false,
            Pieces = TapestryCollection.Pieces("game", "alternate", 2, 1) };
        var partial = new TapestryDesign("game:tapestry/partial", "partial", "tapestry", -1, 2, 1,
            TapestryCollection.Pieces("game", "partial", 2, 1), false);
        var designs = new[] { canonical, alternate, partial };
        var seenData = new TapestryDiscoveries();
        var seen = seenData.ForWorld("world", "player");
        List<TapestryView> Gallery() => TapestryCollection.Read(designs, journal.Entries, seen, s => s);
        JournalBook Book() => JournalBooks.Read(TapestryCollection.BookEntries(journal.Entries, designs), s => s,
            new Dictionary<string, int> { ["letters"] = 3 }).Single();
        var readingData = new ReadingProgressData();
        var reading = readingData.ForWorld("world", "player");
        var first = Gallery();
        check(Book().FragmentCount == 1 && Book().TotalFragments == 3 && reading.UnreadFragments(Book()) == 1 &&
            first[0].Complete && first[0].Lore.SequenceEqual(["existing-tapestry-lore"]) &&
            !first[1].Discovered && !first[2].Discovered && priorJournal.SequenceEqual(SerializerUtil.Serialize(journal)),
            "Installing in an existing world restores received stories/canonical lore, starts reading history as unread and invents no old partial or alternate observations");

        TapestryCollection.ObservePiece(seen, TapestryCollection.PieceKey(partial, "partial1"), false);
        TapestryCollection.ObservePiece(seen, TapestryCollection.PieceKey(partial, "partial2"), true);
        check(Gallery()[2].Seen.SequenceEqual([true, false]) && Gallery()[2].Lore.Count == 0,
            "New intact observations remember individual pieces while rotten observations grant neither pieces nor lore");
        TapestryCollection.ObservePiece(seen, TapestryCollection.PieceKey(partial, "partial2"), false);
        check(Gallery()[2].Complete && Gallery()[2].Lore.Count == 0 &&
            priorJournal.SequenceEqual(SerializerUtil.Serialize(journal)),
            "Remembering every piece can complete the gallery image without granting any new vanilla journal lore");
        reading.Mark(Book(), BookPagination.Layout(Book(), _ => true));
        reading.ViewTapestry(first[0]);
        try
        {
            var readingFile = new ReadingProgressFile(Path.Combine(root, "reading.json"), _ => { });
            var discoveryFile = new SavedDataFile<TapestryDiscoveries>(Path.Combine(root, "tapestries.json"), _ => { });
            readingFile.Save(readingData); discoveryFile.Save(seenData);
            // While uninstalled, only the normal journal receives new lore. There is no
            // BetterJournal observer to remember any additional looked-at tapestry pieces.
            story.Chapters.Add(new JournalChapter { EntryId = 0, ChapterId = 1, Text = "Recovered while the mod was absent" });
            var serverJournalAfterAbsence = SerializerUtil.Serialize(journal);
            var restoredReading = new ReadingProgressFile(Path.Combine(root, "reading.json"), _ => { }).Load().ForWorld("world", "player");
            seen = new SavedDataFile<TapestryDiscoveries>(Path.Combine(root, "tapestries.json"), _ => { }).Load().ForWorld("world", "player");
            check(restoredReading.UnreadFragments(Book()) == 1 && restoredReading.Bookmark(Book()) &&
                !restoredReading.UnreadTapestry(Gallery()[0]) && Gallery()[2].Complete && !Gallery()[1].Discovered &&
                serverJournalAfterAbsence.SequenceEqual(SerializerUtil.Serialize(journal)),
                "Reinstalling restores saved exposure/pieces, marks newly received lore unread and leaves server discoveries unchanged");

            var rememberedBook = Book();
            check(!JournalBooks.Read(TapestryCollection.BookEntries([tapestryEntry], designs), s => s).Any() &&
                restoredReading.UnreadFragments(rememberedBook) == 1 && !restoredReading.Unread(rememberedBook,
                    BookPagination.Layout(rememberedBook, _ => true)[0]),
                "Temporarily missing journal entries leave reading memories dormant for unchanged restored fragments");
            var cleanReading = new ReadingProgressData().ForWorld("world", "player");
            var cleanGallery = TapestryCollection.Read(designs, journal.Entries, new HashSet<string>(), s => s);
            check(cleanReading.UnreadFragments(Book()) == 2 && cleanGallery[0].Complete && !cleanGallery[2].Discovered &&
                serverJournalAfterAbsence.SequenceEqual(SerializerUtil.Serialize(journal)),
                "Resetting only local mod history resets reading/individual observations but still reconstructs existing vanilla tapestry lore");
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
