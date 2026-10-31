using Vintagestory.GameContent;
using System.Text.RegularExpressions;

namespace BetterJournal;

public sealed record RecoveredPage(int ChapterId, int Part, string Text, string Revision = "");
public sealed record JournalBook(int EntryId, string Code, string Title, int FragmentCount, IReadOnlyList<RecoveredPage> Pages,
    int? TotalFragments = null, string? ProgressKey = null)
{
    public string SpineProgress => TotalFragments is int total ? $"{FragmentCount}/{total}" : $"{FragmentCount}/?";
}

public static class JournalBooks
{
    // Text and titles always come from received entries. The optional catalogue
    // supplies counts only; arbitrary entries inserted by mods still work.
    public static List<JournalBook> Read(IEnumerable<JournalEntry> entries, Func<string, string> localize,
        IReadOnlyDictionary<string, int>? totals = null, IReadOnlyDictionary<string, string[]>? catalogueSources = null)
    {
        var books = new List<JournalBook>();
        foreach (var entry in entries)
        {
            var recovered = (entry.Chapters ?? []).ToList();
            string[]? definition = null;
            bool sourceMatch = false;
            if (catalogueSources?.TryGetValue(entry.LoreCode ?? "", out definition) == true && definition.Length > 0)
            {
                var occurrences = definition.GroupBy(SourceKey).ToDictionary(g => g.Key, g => g.Count());
                var found = new HashSet<string>();
                // A catalogue source occurring once identifies one fragment, even when
                // received IDs repeat/change. Retain the first record's ID and text so
                // existing reading identities survive; never edit the server journal.
                recovered = recovered.Where(c => !occurrences.TryGetValue(SourceKey(c.Text), out int copies) ||
                    copies != 1 || found.Add(SourceKey(c.Text))).ToList();
                sourceMatch = recovered.All(c => occurrences.ContainsKey(SourceKey(c.Text))) &&
                    recovered.GroupBy(c => SourceKey(c.Text)).All(g => g.Count() <= occurrences[g.Key]);
            }
            var pages = new List<RecoveredPage>();
            foreach (var chapter in recovered)
            {
                var text = localize(chapter.Text ?? "");
                string revision = ReadingProgress.Fingerprint(chapter.Text ?? "", text);
                var parts = SplitPages(text);
                for (int i = 0; i < parts.Count; i++)
                    pages.Add(new RecoveredPage(chapter.ChapterId, i, parts[i], revision));
            }
            int? total = null;
            if (sourceMatch) total = definition!.Length;
            else if (definition == null && totals?.TryGetValue(entry.LoreCode ?? "", out int count) == true && count > 0 &&
                recovered.All(c => c.ChapterId >= 0 && c.ChapterId < count) &&
                recovered.Select(c => c.ChapterId).Distinct().Count() == recovered.Count) total = count;
            books.Add(new JournalBook(entry.EntryId, entry.LoreCode ?? "", localize(entry.Title ?? ""),
                recovered.Count, pages, total, string.IsNullOrEmpty(entry.LoreCode)
                    ? "title:" + ReadingProgress.Fingerprint(entry.Title ?? "") : "code:" + entry.LoreCode));
        }
        return books;
    }

    private static string SourceKey(string? source) => source?.StartsWith("game:", StringComparison.Ordinal) == true
        ? source[5..] : source ?? "";

    public static IReadOnlyList<string> SplitPages(string text)
    {
        if (!text.Contains("___NEWPAGE___", StringComparison.Ordinal)) return [text];
        var result = new List<string>();
        var open = new List<(string Name, string Tag)>();
        foreach (var part in text.Split("___NEWPAGE___", StringSplitOptions.None))
        {
            string prefix = string.Concat(open.Select(t => t.Tag));
            foreach (Match tag in Regex.Matches(part, @"<(/?)([\w:-]+)\b[^>]*>", RegexOptions.CultureInvariant))
            {
                string name = tag.Groups[2].Value.ToLowerInvariant();
                if (tag.Groups[1].Value == "/")
                {
                    int index = open.FindLastIndex(t => t.Name == name);
                    if (index >= 0) open.RemoveRange(index, open.Count - index);
                }
                else if (!tag.Value.EndsWith("/>", StringComparison.Ordinal) &&
                    name is not ("br" or "img" or "icon" or "hr")) open.Add((name, tag.Value));
            }
            // Vanilla lore often ends a fragment with NEWPAGE. That must not
            // create a second, empty recovered page or inflate discovery counts.
            if (!string.IsNullOrWhiteSpace(part))
                result.Add(prefix + part + string.Concat(open.AsEnumerable().Reverse().Select(t => "</" + t.Name + ">")));
        }
        return result.Count > 0 ? result : [""];
    }

    public static bool SameContent(IReadOnlyList<JournalBook> left, IReadOnlyList<JournalBook> right)
    {
        if (left.Count != right.Count) return false;
        for (int i = 0; i < left.Count; i++)
        {
            var a = left[i]; var b = right[i];
            if (a.EntryId != b.EntryId || a.Code != b.Code || a.Title != b.Title || a.ProgressKey != b.ProgressKey || a.FragmentCount != b.FragmentCount || a.TotalFragments != b.TotalFragments ||
                !a.Pages.SequenceEqual(b.Pages)) return false;
        }
        return true;
    }

    public static int CoverSeed(string code, string title)
    {
        uint hash = 2166136261;
        foreach (char c in code.Length > 0 ? code : title) hash = (hash ^ c) * 16777619;
        return (int)(hash & 0x7fffffff);
    }
}
