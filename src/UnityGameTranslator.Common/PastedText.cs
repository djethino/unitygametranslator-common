using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What a box of one line keeps of a text pasted into it.
    ///
    /// 🔴 **The text, without its line breaks — never only what comes before the first one.** A
    /// title copied from a page or a chat often brings a line break before or after it. The
    /// Manager's text box kept what preceded the first break, so a copy that began with one pasted
    /// NOTHING, with no sign of why (2026-10-05, a game title in Chinese copied with the line above
    /// it). Each line is trimmed, empty ones dropped, and the rest joined by one space — what the
    /// person meant to give is the words, and the breaks only came with the copy.
    ///
    /// ⚠ The breaks are the mandatory ones of Unicode's line breaking (UAX #14: BK, CR, LF, NL) —
    /// what any text can end a line with, whatever its script.
    /// </summary>
    public static class PastedText
    {
        private static readonly char[] Breaks = { '\r', '\n', '\u000B', '\u000C', '\u0085', (char)0x2028, (char)0x2029 };

        /// <summary>The words of <paramref name="pasted"/> on one line; empty when it holds none.</summary>
        public static string OneLine(string pasted)
        {
            if (string.IsNullOrEmpty(pasted)) return "";

            var kept = new List<string>();
            foreach (var line in pasted.Split(Breaks))
            {
                var words = line.Trim();
                if (words.Length > 0) kept.Add(words);
            }

            return string.Join(" ", kept);
        }

        /// <summary>Whether a box of one line would lose something of <paramref name="pasted"/> taken as it is.</summary>
        public static bool NeedsOneLine(string pasted) =>
            !string.IsNullOrEmpty(pasted) && pasted.IndexOfAny(Breaks) >= 0;
    }
}
