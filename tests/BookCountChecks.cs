using System.Reflection;
using BetterJournal;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

static class BookCountChecks
{
    public static void Run(Action<bool, string> check)
    {
        string game = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(a => a.Key == "GamePath").Value!;
        var definitions = Directory.EnumerateFiles(Path.Combine(game, "assets/survival/config/lore"), "*.json")
            .Select(p => JsonObject.FromJson(File.ReadAllText(p)).AsObject<LoreCatalogue.CountAsset>()!)
            .ToDictionary(a => a.Code, a => a.Pieces);
        var totals = definitions.ToDictionary(a => a.Key, a => a.Value.Length);
        string[] letters = definitions["letters"];
        JournalEntry Entry(string code, IEnumerable<string> sources) => new()
            { EntryId = 0, LoreCode = code, Title = code, Chapters = sources.Select(s => new JournalChapter
                { EntryId = 0, ChapterId = 0, Text = s }).ToList() };
        JournalBook Read(JournalEntry entry, IReadOnlyDictionary<string, string[]>? sources = null) =>
            JournalBooks.Read([entry], s => s, totals, sources ?? definitions).Single();
        var partial = SerializerUtil.Deserialize<JournalEntry>(SerializerUtil.Serialize(Entry("letters", letters.Take(5))));
        var wireBefore = SerializerUtil.Serialize(partial);
        var book = Read(partial);
        check(letters.Length == 8 && book.FragmentCount == 5 && book.TotalFragments == 8 && book.SpineProgress == "5/8" &&
            book.Pages.Count == 5 && !book.Pages.Any(p => letters.Skip(5).Contains(p.Text)),
            "Five received Letters fragments with repeated default IDs show 5/8 without revealing the other three");
        check(definitions.All(d => { var b = Read(Entry(d.Key, d.Value)); return b.FragmentCount == d.Value.Length &&
                b.TotalFragments == d.Value.Length && b.SpineProgress == d.Value.Length + "/" + d.Value.Length; }),
            "Every installed vanilla lore definition retains its full catalogue total even when all received chapter IDs are zero");

        for (int i = 0; i < partial.Chapters.Count; i++) partial.Chapters[i].ChapterId = i == 0 ? -3 : 100 + i;
        check(Read(partial).SpineProgress == "5/8" && Read(partial).Pages.Select(p => p.ChapterId)
            .SequenceEqual(partial.Chapters.Select(c => c.ChapterId)),
            "Known fragment sources recover totals despite out-of-range IDs while preserving received reading identities");
        partial = SerializerUtil.Deserialize<JournalEntry>(wireBefore);
        partial.Chapters.Add(new JournalChapter { ChapterId = 77, Text = letters[0] });
        partial.Chapters.Add(new JournalChapter { ChapterId = 0, Text = letters[0] });
        var unchanged = SerializerUtil.Serialize(partial);
        var deduplicated = Read(partial);
        check(deduplicated.SpineProgress == "5/8" && deduplicated.Pages.Count == 5 &&
            unchanged.SequenceEqual(SerializerUtil.Serialize(partial)),
            "Repeated packets for a uniquely identified fragment do not inflate found counts or modify the server journal");
        var memory = new ReadingProgressData().ForWorld("world", "player");
        memory.Mark(book, BookPagination.Layout(book, _ => true));
        check(memory.UnreadFragments(deduplicated) == 0,
            "Removing repeated records preserves the viewing history of the retained source fragments");

        var complete = Read(Entry("letters", letters));
        memory.Mark(complete, BookPagination.Layout(complete, _ => true));
        check(complete.SpineProgress == "8/8" && memory.UnreadFragments(complete) == 0 && !memory.Bookmark(complete),
            "A fully recovered and viewed Letters book is complete at 8/8 and clears its bookmark");
        check(Read(Entry("letters", [])).SpineProgress == "0/8",
            "An empty received entry can show its known catalogue total without granting fragments");
        var qualified = Entry("letters", letters.Take(5).Select(s => "game:" + s));
        check(Read(qualified).SpineProgress == "5/8",
            "Default-domain qualified lore keys match the same vanilla fragment catalogue");

        var customDefinitions = new Dictionary<string, string[]> { ["example:story"] = ["example:first", "example:second", "example:third"] };
        check(Read(Entry("example:story", ["example:first", "example:third"]), customDefinitions).SpineProgress == "2/3",
            "Modded lore catalogues provide found/total counts without relying on chapter numbering");
        var outside = Entry("letters", letters.Take(5));
        outside.Chapters.Add(new JournalChapter { ChapterId = 1, Text = "A directly inserted uncatalogued fragment" });
        check(Read(outside).TotalFragments == null && Read(outside).SpineProgress == "6/?" && Read(outside).FragmentCount == 6 &&
            Read(Entry("unregistered:story", ["Only received text"])).SpineProgress == "1/?",
            "Truly uncatalogued content remains readable with an honest unknown denominator instead of false completion");
        var repeatedDefinition = new Dictionary<string, string[]> { ["repeated"] = ["same-source", "same-source"] };
        var repeated = Entry("repeated", ["same-source", "same-source"]);
        repeated.Chapters[1].ChapterId = 1;
        check(Read(repeated, repeatedDefinition).SpineProgress == "2/2" && Read(repeated, repeatedDefinition).Pages.Count == 2,
            "A definition intentionally assigning the same source to multiple fragments retains those distinct records");
    }
}
