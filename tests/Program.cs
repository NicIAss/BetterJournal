using System.Reflection;
using BetterJournal;
using Vintagestory.GameContent;

int checks = 0;
void Check(bool condition, string message)
{ if (!condition) throw new Exception(message); checks++; Console.WriteLine("PASS: " + message); }
string Localize(string text) => text switch
{ "vanilla-title" => "Recovered letters", "vanilla-piece" => "A letter from home.___NEWPAGE___", _ => text };

var vanilla = new JournalEntry { EntryId = 0, LoreCode = "letters", Title = "vanilla-title", Chapters =
    [new JournalChapter { EntryId = 0, ChapterId = 7, Text = "vanilla-piece" }] };
var modded = new JournalEntry { EntryId = 1, LoreCode = "some-mod:unregistered-book", Title = "A modded diary", Chapters =
    [new JournalChapter { EntryId = 1, ChapterId = 8, Text = "<i>First page.___NEWPAGE___Second page.</i>" },
     new JournalChapter { EntryId = 1, ChapterId = 2, Text = "<a href=\"https://example.org\">A link</a>" }] };
var source = new List<JournalEntry> { vanilla, modded };
var books = JournalBooks.Read(source, Localize);
Check(books.Count == 2 && books[1].Title == modded.Title, "Arbitrary mod-added entries survive without catalogue lookup");
Check(books[0].Title == "Recovered letters" && books[0].Pages[0].Text.StartsWith("A letter"), "Vanilla localization keys resolve");
Check(books[0].Pages.Count == 1, "Vanilla trailing page-break marker creates no phantom page");
Check(books[1].FragmentCount == 2 && books[1].Pages.Count == 3, "Recovered fragment count stays separate from explicit reading pages");
Check(books[1].Pages[0].Text == "<i>First page.</i>" && books[1].Pages[1].Text == "<i>Second page.</i>", "Formatting survives a page break inside a tag");
Check(books[1].Pages[0].ChapterId == 8 && books[1].Pages[2].ChapterId == 2, "Journal chapter order and IDs are preserved");
Check(books[1].Pages[2].Text == modded.Chapters[1].Text, "Modded VTML links are retained verbatim");
Check(source[0].Chapters.Count == 1 && modded.Chapters[0].Text.Contains("___NEWPAGE___"), "Projection never mutates the game journal");
Check(JournalBooks.SameContent(books, JournalBooks.Read(source, Localize)), "Unchanged journal does not trigger redraw");
modded.Chapters.Add(new JournalChapter { EntryId = 1, ChapterId = 11, Text = "New discovery" });
Check(!JournalBooks.SameContent(books, JournalBooks.Read(source, Localize)), "New fragment is detected while the reader is open");
source[1] = new JournalEntry { EntryId = 1, LoreCode = modded.LoreCode, Title = "Updated by another mod", Chapters = [] };
var updated = JournalBooks.Read(source, Localize);
Check(updated[1].Pages.Count == 0 && !JournalBooks.SameContent(books, updated), "Entry replacement and entries without chapters are supported");
Check(JournalBooks.Read([], Localize).Count == 0 && JournalBooks.SplitPages("").Count == 1, "Empty journal and empty recovered chapter are safe");
Check(JournalBooks.CoverSeed("some-mod:book", "x") == JournalBooks.CoverSeed("some-mod:book", "translated title"), "Cover appearance is stable across languages");
Check(typeof(ModJournal).GetField("ownJournal", BindingFlags.Instance | BindingFlags.NonPublic)?.FieldType == typeof(Journal), "Private journal integration matches installed game assembly");
string Plain(string text) => System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "");
string Words(string text) => System.Text.RegularExpressions.Regex.Replace(Plain(text), @"\s+", " ").Trim();
bool FitsSmall(string text) => Plain(text).Trim().Length <= 70;
string longPiece = "<i>" + string.Join(" ", Enumerable.Range(0, 100).Select(i => "word" + i)) + "</i>";
var leaves = BookPagination.Split(longPiece, FitsSmall);
Check(leaves.Count > 1 && leaves.All(FitsSmall), "Long fragments are split into fitted leaves without scrolling");
Check(Words(string.Join(" ", leaves)) == Words(longPiece), "Pagination retains every word in its original order");
Check(leaves.All(t => t.StartsWith("<i>") && t.EndsWith("</i>")), "Automatic page boundaries preserve italic styling");
string linked = "<font color=\"#aabbcc\"><a href=\"https://example.org/story\">" + string.Join(" ", Enumerable.Repeat("Linked text", 25)) + "</a></font>";
var linkedPages = BookPagination.Split(linked, FitsSmall);
Check(linkedPages.All(t => t.StartsWith("<font color=\"#aabbcc\"><a href=\"https://example.org/story\">") && t.EndsWith("</a></font>")), "Nested formatting and link destinations survive automatic splits");
Check(BookPagination.Split("<strong>" + new string('x', 300) + "</strong>", FitsSmall).Count == 1, "An oversized indivisible item advances without losing content");
Check(BookPagination.Split("", FitsSmall).SequenceEqual([""]), "Empty recovered pages remain safe during layout");
var physical = BookPagination.Layout(books[1], FitsSmall);
Check(physical.Select(p => p.Fragment).SequenceEqual([1, 1, 2]) && books[1].FragmentCount == 2, "Explicit and fitted leaves do not inflate discovery counts");
Check(BookPose.Cosine(0) == 1 && BookPose.Cosine(1) == -1, "Leaf rotation starts and finishes flat on opposite sides");
Check(Math.Abs(BookPose.Cosine(0.5)) < 1e-12, "Front/back faces meet only when the sheet is edge-on");
Check(Enumerable.Range(0, 50).All(i => BookPose.TurnFace(1, i / 100d) == 1) &&
      Enumerable.Range(50, 51).All(i => BookPose.TurnFace(1, i / 100d) == 2), "Forward turn keeps outgoing right and incoming left text on fixed faces");
Check(Enumerable.Range(0, 50).All(i => BookPose.TurnFace(-1, i / 100d) == 0) &&
      Enumerable.Range(50, 51).All(i => BookPose.TurnFace(-1, i / 100d) == 3), "Backward turn keeps outgoing left and incoming right text on fixed faces");
Check(BookPose.OpeningProgress(BookMotionKind.Open, 0) == 0 && BookPose.OpeningProgress(BookMotionKind.Close, 1) == 0,
    "Opening starts and closing ends at the same fully closed pose");
Check(BookPose.OpeningProgress(BookMotionKind.Open, 1) == 1 && BookPose.OpeningProgress(BookMotionKind.Close, 0) == 1,
    "Opening ends and closing starts at the same fully open pose");
Check(Enumerable.Range(0, 101).All(i => BookPose.OpeningProgress(BookMotionKind.Close, i / 100d) ==
    1 - BookPose.OpeningProgress(BookMotionKind.Open, i / 100d)), "Closing exactly reverses the full opening scene");
Check(Enumerable.Range(0, 101).All(i => BookPose.Vertex(i / 100d, 0, false, true, 360, 550) == (0d, 0d)),
    "The binding stays fixed while the free edge lifts");
Check(Enumerable.Range(0, 25).All(i => Math.Abs(BookPose.Vertex(0.5, i / 24d, false, true, 360, 550).X) < 1e-10),
    "Every strip is edge-on when the visible face changes");
Check(BookPose.Vertex(0, 1, false, true, 360, 550) == (360d, 0d) &&
    Math.Abs(BookPose.Vertex(1, 1, false, false, 360, 550).X + 360) < 1e-10,
    "Animation endpoints align with the native page rectangles");
var counted = new JournalEntry { EntryId = 30, LoreCode = "catalogued", Title = "A recovered title", Chapters =
    [new JournalChapter { ChapterId = 0, Text = "First" }, new JournalChapter { ChapterId = 3, Text = "Fourth" }] };
var totals = new Dictionary<string, int> { ["catalogued"] = 5 };
var countedBook = JournalBooks.Read([counted], Localize, totals).Single();
Check(countedBook.TotalFragments == 5 && countedBook.FragmentCount == 2 && countedBook.SpineProgress == "2/5",
    "Catalogue counts expose missing fragments without exposing their text or title");
Check(JournalBooks.Read([modded], Localize, totals).Single().TotalFragments == null,
    "Direct mod entries keep an unknown total instead of implying completion");
counted.Chapters.Add(new JournalChapter { ChapterId = 9, Text = "Mod-inserted chapter outside the catalogue" });
Check(JournalBooks.Read([counted], Localize, totals).Single().TotalFragments == null,
    "Out-of-catalogue chapters remain readable without a misleading total");
var quad = TapestryCollection.Pieces("game", "salvation", 2, 2);
Check(quad.Select(p => p.Type).SequenceEqual(["salvation11", "salvation12", "salvation21", "salvation22"]) &&
    quad[2].Column == 0 && quad[2].Row == 1, "Tapestry quarters assemble in the game's row/column order");
Check(TapestryCollection.Pieces("game", "ambush", 3, 1).Select(p => p.Type).SequenceEqual(["ambush1", "ambush2", "ambush3"]) &&
    TapestryCollection.Pieces("game", "notice-blackguard", 1, 1).Single().Type == "notice-blackguard",
    "Horizontal panels and unsuffixed single tapestries use the actual texture codes");
var canonical = new TapestryDesign("game:tapestry/schematic-c", "hidden-name", "tapestry", 8, 1, 2,
    TapestryCollection.Pieces("game", "schematic-c", 1, 2));
var alternate = new TapestryDesign("game:tapestry/schematic-c-bloody", "hidden-alternate", "tapestry", 8, 1, 2,
    TapestryCollection.Pieces("game", "schematic-c-bloody", 1, 2), false);
var noLore = new TapestryDesign("mod:tapestry/aid", "mod:hidden-name", "tapestry", -1, 1, 2,
    TapestryCollection.Pieces("mod", "aid", 1, 2), false);
var designs = new[] { canonical, alternate, noLore };
var discoveries = new TapestryDiscoveries();
var observationSeen = new HashSet<string>();
string observedPiece = TapestryCollection.PieceKey(noLore, "aid1");
Check(!TapestryCollection.ObservePiece(observationSeen, observedPiece, rotten: true) &&
    observationSeen.Count == 0 && !TapestryCollection.Read([noLore], [], observationSeen, Localize).Single().Discovered,
    "Rotten tapestry observations add no progress and reveal no artwork or title");
Check(TapestryCollection.ObservePiece(observationSeen, observedPiece, rotten: false) &&
    TapestryCollection.Read([noLore], [], observationSeen, Localize).Single().Seen.SequenceEqual([true, false]),
    "An intact copy of the same piece still reveals exactly that piece");
Check(!TapestryCollection.ObservePiece(observationSeen, observedPiece, rotten: true) &&
    !TapestryCollection.ObservePiece(observationSeen, observedPiece, rotten: false) &&
    !TapestryCollection.ObservePiece(observationSeen, null, rotten: false) && observationSeen.SetEquals([observedPiece]),
    "Rotten, repeated and unknown observations preserve existing valid discoveries without new progress");
var seen = discoveries.ForWorld("world-one", "player-one");
var hidden = TapestryCollection.Read(designs, [], seen, Localize);
Check(hidden.All(v => !v.Discovered && v.SeenCount == 0 && v.Lore.Count == 0),
    "Unseen tapestries reveal neither images nor lore");
seen.Add(TapestryCollection.PieceKey(noLore, "aid1"));
var partial = TapestryCollection.Read(designs, [], seen, Localize)[2];
Check(partial.Discovered && partial.Seen.SequenceEqual([true, false]) && partial.Lore.Count == 0,
    "Looking at a piece reveals only that piece, including designs without lore");
var tapestryEntry = new JournalEntry { EntryId = 31, LoreCode = "tapestry", Title = "Tapestry", Chapters =
    [new JournalChapter { ChapterId = 8, Text = "Received, possibly mod-patched lore" }, new JournalChapter { ChapterId = 99, Text = "Unmapped mod chapter" }] };
var legacy = TapestryCollection.Read(designs, [tapestryEntry], seen, Localize);
Check(legacy[0].SeenCount == 2 && legacy[0].Lore.Single() == "Received, possibly mod-patched lore" && !legacy[1].Discovered,
    "Existing journal unlocks the canonical tapestry without spoiling an alternate sharing its chapter ID");
var remaining = TapestryCollection.BookEntries([vanilla, tapestryEntry, modded], designs).ToList();
Check(remaining.Count == 3 && remaining[1].Chapters.Single().ChapterId == 99 && tapestryEntry.Chapters.Count == 2,
    "Gallery routes mapped lore while preserving unknown mod chapters and leaving the journal untouched");
Check(!TapestryCollection.BookEntries([new JournalEntry { LoreCode = "tapestry", Chapters = [] },
    new JournalEntry { LoreCode = "tapestry", Chapters = [tapestryEntry.Chapters[0]] }], designs).Any(),
    "Tapestry-only entries, including empty ones, never create a bookshelf book");
Check(discoveries.ForWorld("world-two", "player-one").Count == 0 && discoveries.ForWorld("world-one", "player-two").Count == 0 &&
    discoveries.ForWorld("world-one", "player-one").SetEquals(seen), "Tapestry memories stay separate across worlds and players");
var shelfRect = new WindowRect(120, 70, 1040, 760);
var readerRect = new WindowRect(245, 70, 790, 760);
Check(WindowRect.Blend(shelfRect, readerRect, 0) == shelfRect && WindowRect.Blend(shelfRect, readerRect, 1) == readerRect,
    "Window resize lands exactly on the native layouts without a final size jump");
var rects = Enumerable.Range(0, 101).Select(i => WindowRect.Blend(shelfRect, readerRect, i / 100d)).ToList();
Check(rects.All(r => Math.Abs(r.X + r.Width / 2 - 640) < 1e-9 && r.Width >= 790 && r.Width <= 1040) &&
    rects.Zip(rects.Skip(1)).All(p => p.First.Width >= p.Second.Width),
    "Resizing stays centered and never overshoots or reverses direction");
Check(WindowRect.Blend(shelfRect, readerRect, -1) == shelfRect && WindowRect.Blend(shelfRect, readerRect, 2) == readerRect &&
    WindowRect.Blend(readerRect, readerRect, 0.4) == readerRect,
    "Late frames and same-size view changes keep valid window geometry");
Check(Enumerable.Range(0, 101).All(i => Math.Abs(WindowRect.Ease(i / 100d) + WindowRect.Ease(1 - i / 100d) - 1) < 1e-9) &&
    WindowRect.Ease(0.001) < 1e-7 && 1 - WindowRect.Ease(0.999) < 1e-7,
    "The transition accelerates and settles gently in both directions");
Check(Enumerable.Range(0, 25).All(i =>
    BookPose.CoverVertex(0, i / 24d, false, true, 367.37, 556.21).Y < 0 &&
    BookPose.CoverVertex(0, i / 24d, true, true, 367.37, 556.21).Y > 556.21) &&
    BookPose.CoverVertex(0, 1, false, true, 367.37, 556.21).X > 367.37,
    "Closed hardcover conceals paper at top, bottom and free edge with fractional dimensions");
Check(Enumerable.Range(0, 25).All(i =>
    Math.Abs(BookPose.CoverVertex(0.5, i / 24d, false, true, 367, 556).X) < 1e-10),
    "Hardcover overhang also becomes edge-on before changing to the paper reverse");
Check(Enumerable.Range(0, 51).All(i => Enumerable.Range(0, 25).All(j =>
    BookPose.CoverVertex(i / 100d, j / 24d, true, true, 367.37, 556.21).Y > 556.21)),
    "The rigid cover's lower rim conceals the page block throughout its visible exterior rotation");
Check(Enumerable.Range(0, 101).All(i =>
{
    double p = i / 100d;
    var a = BookPose.CoverVertex(p, 0, false, true, 367.37, 556.21);
    var b = BookPose.CoverVertex(p, 1, false, true, 367.37, 556.21);
    var middle = BookPose.CoverVertex(p, 0.5, false, true, 367.37, 556.21);
    var bottom = BookPose.CoverVertex(p, 1, true, true, 367.37, 556.21);
    return Math.Abs(middle.X - (a.X + b.X) / 2) < 1e-9 &&
        Math.Abs(middle.Y - (a.Y + b.Y) / 2) < 1e-9 &&
        Math.Abs(bottom.Y - b.Y - 572.21) < 1e-9;
}), "Hardcover stays rigid with straight edges and constant height throughout rotation");
double boardLeaf = 367.37, bookLeft = 125.13;
Check(Math.Abs(BookPose.CoverHinge(bookLeft, boardLeaf, true, 0) + BookPose.BoardWidth(boardLeaf, true) / 2 -
    (bookLeft + boardLeaf)) < 1e-9 &&
    Math.Abs(BookPose.CoverHinge(bookLeft, boardLeaf, true, 1) - (bookLeft + boardLeaf)) < 1e-9 &&
    Math.Abs(BookPose.CoverHinge(bookLeft, boardLeaf, true, 1) +
        BookPose.CoverVertex(1, 1, false, false, boardLeaf, 556.21).X - (bookLeft - BookPose.CoverOverhang)) < 1e-9,
    "Closed board is centered and its opened reverse lands exactly on the native left board");
Check(Enumerable.Range(0, 101).All(i => BookPose.CoverHinge(bookLeft, boardLeaf, false, i / 100d) ==
    bookLeft - BookPose.CoverOverhang) &&
    Math.Abs(BookPose.BoardWidth(boardLeaf, false) + BookPose.CoverHinge(bookLeft, boardLeaf, false, 0) -
        (bookLeft + boardLeaf + BookPose.CoverOverhang)) < 1e-9,
    "Single-leaf hardcover shares the native outer board bounds without sliding its binding");
ReadingChecks.Run(Check);
SavedDataChecks.Run(Check);
PerformanceChecks.Run(Check);
TapestryRoutingChecks.Run(Check);
BrandingMigrationChecks.Run(Check);
ExistingWorldChecks.Run(Check);
Console.WriteLine($"{checks} checks passed against installed game types.");
