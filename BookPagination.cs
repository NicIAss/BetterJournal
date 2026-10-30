using System.Text.RegularExpressions;

namespace BetterJournal;

public sealed record ReadingPage(int ChapterId, int Fragment, string Text, int Start = 0, int End = 0);

public static class BookPagination
{
    private static readonly Regex Tokens = new("<(?:[^>\"']|\"[^\"]*\"|'[^']*')+>|&[\\w#]+;|[^<\\s&]+|\\s+|[<&]", RegexOptions.CultureInvariant);
    private static readonly Regex Tag = new(@"^<(/?)([\w:-]+)\b", RegexOptions.CultureInvariant);

    public static List<ReadingPage> Layout(JournalBook book, Func<string, bool> fits)
    {
        var result = new List<ReadingPage>();
        int fragment = 0, offset = 0;
        foreach (var recovered in book.Pages)
        {
            if (recovered.Part == 0) { fragment++; offset = 0; }
            foreach (string text in Split(recovered.Text, fits))
            {
                int end = offset + Math.Max(1, Units(text));
                result.Add(new ReadingPage(recovered.ChapterId, fragment, text, offset, end));
                offset = end;
            }
        }
        return result;
    }

    // Count the same content tokens used by Split. Carried formatting tags and
    // whitespace do not shift anchors; illustrations each occupy one unit.
    public static int Units(string text) => Tokens.Matches(text).Count(m =>
        !string.IsNullOrWhiteSpace(m.Value) && (!m.Value.StartsWith('<') ||
            Regex.IsMatch(m.Value, @"^<(img|icon|itemstack)\b", RegexOptions.IgnoreCase)));

    // Measure balanced VTML candidates with the native renderer. Split only at
    // text/markup boundaries, carrying open styles and links onto the next leaf.
    public static IReadOnlyList<string> Split(string text, Func<string, bool> fits)
    {
        if (text.Length == 0 || (text.Length <= 2048 && fits(text))) return [text];
        var tokens = Tokens.Matches(text).Select(m => m.Value).ToArray();
        var pages = new List<string>();
        var carried = new List<(string Name, string Markup)>();
        int start = 0;
        while (start < tokens.Length)
        {
            while (start < tokens.Length && string.IsNullOrWhiteSpace(tokens[start])) start++;
            if (start == tokens.Length) break;
            string Candidate(int end)
            {
                var stack = new List<(string Name, string Markup)>(carried);
                for (int i = start; i < end; i++) Track(tokens[i], stack);
                return string.Concat(carried.Select(t => t.Markup)) +
                    string.Concat(tokens[start..end]).TrimEnd() +
                    string.Concat(stack.AsEnumerable().Reverse().Select(t => "</" + t.Name + ">"));
            }
            // Find a page-sized upper bound before binary search. Measuring
            // half of every remaining chapter made long books quadratic work
            // for the native text renderer, even though only one leaf can fit.
            int low = start + 1, high = Math.Min(tokens.Length, start + 32), best = start;
            while (fits(Candidate(high)))
            {
                best = high;
                if (high == tokens.Length) break;
                low = high + 1;
                high = start + (int)Math.Min(tokens.Length - (long)start, (high - (long)start) * 2);
            }
            if (best == tokens.Length) { low = tokens.Length + 1; }
            else high--;
            while (low <= high)
            {
                int middle = low + (high - low) / 2;
                if (fits(Candidate(middle))) { best = middle; low = middle + 1; }
                else high = middle - 1;
            }
            // An oversized indivisible item must still advance. The reader
            // scales such a leaf to fit; it never crops or silently drops it.
            if (best == start) best = start + 1;
            while (best < tokens.Length && !HasContent(tokens[start..best])) best++;
            while (best < tokens.Length && (tokens[best].StartsWith("</", StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(tokens[best]))) best++;
            string leaf = Candidate(best);
            if (HasContent(tokens[start..best])) pages.Add(leaf);
            for (int i = start; i < best; i++) Track(tokens[i], carried);
            start = best;
        }
        return pages.Count == 0 ? [text] : pages;
    }

    private static bool HasContent(IEnumerable<string> tokens) => tokens.Any(t =>
        (!t.StartsWith('<') && !string.IsNullOrWhiteSpace(t)) || Regex.IsMatch(t, @"^<(img|icon|itemstack)\b", RegexOptions.IgnoreCase));

    private static void Track(string token, List<(string Name, string Markup)> stack)
    {
        var match = Tag.Match(token);
        if (!match.Success) return;
        string name = match.Groups[2].Value.ToLowerInvariant();
        if (match.Groups[1].Value == "/")
        {
            int at = stack.FindLastIndex(t => t.Name == name);
            if (at >= 0) stack.RemoveRange(at, stack.Count - at);
        }
        else if (!token.EndsWith("/>", StringComparison.Ordinal) && name is not ("br" or "img" or "icon" or "hr" or "itemstack"))
            stack.Add((name, token));
    }
}
