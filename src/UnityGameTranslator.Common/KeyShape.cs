using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The two character facts the key of a text needs, from Unicode's data at the project's pinned version
    /// (KeyChars.Tables.g.cs, tools/generate-key-chars.py): which UTF-16 units are decimal digits, and which are white
    /// space — the Basic Multilingual Plane only, as the runtime's regular expression and <c>Trim()</c> read them.
    /// Nothing here depends on the game's runtime or on a culture.
    /// </summary>
    public static partial class KeyChars
    {
        /// <summary>A decimal digit of any script (Nd), of the Basic Multilingual Plane. A surrogate is none.</summary>
        public static bool IsDigit(char c) =>
            // Below U+0660, the first digit that is not an ASCII one, the answer is the ten ASCII digits: no table walk
            // for the characters of almost every text.
            c < FirstNonAsciiDigit ? (c >= '0' && c <= '9') : InRanges(DigitRanges, c);

        private const char FirstNonAsciiDigit = '\u0660';

        /// <summary>White_Space, of the Basic Multilingual Plane (what <c>Trim()</c> removes on .NET 8, checked equal).</summary>
        public static bool IsSpace(char c) => InRanges(SpaceRanges, c);

        /// <summary>The text without the white space at its two ends; the very same string when it has none.</summary>
        public static string? Trim(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            int from = 0, to = text!.Length;
            while (from < to && IsSpace(text[from])) from++;
            while (to > from && IsSpace(text[to - 1])) to--;
            return from == 0 && to == text.Length ? text : text.Substring(from, to - from);
        }

        private static bool InRanges(int[] ranges, int c)
        {
            int lo = 0, hi = ranges.Length / 2 - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (c < ranges[mid * 2]) hi = mid - 1;
                else if (c > ranges[mid * 2 + 1]) lo = mid + 1;
                else return true;
            }
            return false;
        }
    }

    /// <summary>
    /// The shape a text's KEY is stored in (spec/translation-file/key.md): the one definition the mod, the Manager and
    /// every file written from a game's texts share — otherwise a file extracted cold never meets the text the game shows.
    ///
    ///  1. line endings made LF (<see cref="LineEndings"/>);
    ///  2. (the mod only) the game's live variables lifted out as <c>[!STR*N]</c> — state of a running game, not here;
    ///  3. numbers lifted out as <c>[!v*N]</c>, when the setting says so.
    ///
    /// <see cref="Stored"/> is 1 and 3, then trimmed — the form a cold translation writes (the mod's lookup tries the
    /// text as it is, then its key, then its trimmed key).
    ///
    /// 🔴 **A text with nothing to change comes back as the very same string, allocating nothing**: this runs on every text
    /// of every frame.
    ///
    /// ⚠ **The facts of the key are a contract with the files already written**, reproduced exactly, never corrected without
    /// a migration: « 1,000,000 » is two slots (<c>1,000</c> and <c>000</c>); a sign is taken with its number (« 2-5 » is
    /// <c>2</c> and <c>-5</c>); a digit outside the Basic Multilingual Plane is no number; the decimal and thousands marks
    /// are the ASCII « . » and « , ».
    /// </summary>
    public static class KeyShape
    {
        /// <summary>The slot of an extracted number: <c>[!v*N]</c>.</summary>
        public const string PlaceholderPrefix = "[!v*";

        /// <summary>The end of a slot.</summary>
        public const string PlaceholderSuffix = "]";

        private static readonly string[] NoNumbers = new string[0];

        /// <summary>The key of a text: line endings, then — when <paramref name="normalizeNumbers"/> — its numbers.</summary>
        public static string? Of(string? text, bool normalizeNumbers, out IReadOnlyList<string> numbers)
        {
            numbers = NoNumbers;
            string? lf = LineEndings.ToLf(text);
            if (!normalizeNumbers || string.IsNullOrEmpty(lf)) return lf;
            return ExtractNumbers(lf!, out numbers);
        }

        /// <summary>The key as a cold translation writes it: numbers lifted out, trimmed.</summary>
        public static string? Stored(string? text)
        {
            string? key = Of(text, true, out _);
            return KeyChars.Trim(key);
        }

        /// <summary>
        /// The numbers of a text lifted into slots, in order of appearance (<c>[!v*0]</c>, <c>[!v*1]</c>…), and their
        /// values. A number is <c>-?</c> digits, then optionally <c>.</c> or <c>,</c> and digits, then optionally
        /// <c>%</c>; not one right after <c>[!v*</c>, nor one that is the tail of a hexadecimal colour (<c>#FF00</c>), nor
        /// one inside a <c>[!v*N]</c> or <c>[!STR*N]</c> slot already there.
        /// </summary>
        private static string ExtractNumbers(string text, out IReadOnlyList<string> numbers)
        {
            numbers = NoNumbers;
            if (string.IsNullOrEmpty(text)) return text;

            // First, is there anything: the common case allocates nothing.
            int at = 0;
            bool any = false;
            while (NextMatch(text, at, out int s, out int e))
            {
                at = e;
                if (!IsPartOfHexColor(text, s) && !IsInsidePlaceholder(text, s)) { any = true; break; }
            }
            if (!any) return text;

            var found = new List<string>();
            var result = new StringBuilder(text.Length + 8);
            int copied = 0;
            at = 0;
            while (NextMatch(text, at, out int start, out int end))
            {
                at = end;
                if (IsPartOfHexColor(text, start) || IsInsidePlaceholder(text, start)) continue;
                result.Append(text, copied, start - copied)
                      .Append(PlaceholderPrefix).Append(found.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(PlaceholderSuffix);
                found.Add(text.Substring(start, end - start));
                copied = end;
            }
            result.Append(text, copied, text.Length - copied);
            numbers = found;
            return result.ToString();
        }

        /// <summary>The slots of a text replaced by the numbers seen, in slot order.</summary>
        public static string? Restore(string? text, IReadOnlyList<string>? numbers)
        {
            if (string.IsNullOrEmpty(text) || numbers == null || numbers.Count == 0) return text;
            string result = text!;
            for (int i = 0; i < numbers.Count; i++)
                result = result.Replace(PlaceholderPrefix + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + PlaceholderSuffix, numbers[i]);
            return result;
        }

        /// <summary>The same, for a caller that holds the numbers by slot.</summary>
        public static string? Restore(string? text, IDictionary<int, string>? numbersByIndex)
        {
            if (string.IsNullOrEmpty(text) || numbersByIndex == null || numbersByIndex.Count == 0) return text;
            string result = text!;
            foreach (var pair in numbersByIndex)
                result = result.Replace(PlaceholderPrefix + pair.Key.ToString(System.Globalization.CultureInfo.InvariantCulture) + PlaceholderSuffix, pair.Value);
            return result;
        }

        // ── braced slots ─────────────────────────────────────────────────────────

        private static readonly Regex BracedSlot = new Regex(@"-?\{(\[!v\*[0-9]+\])\}%?", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex BracedNumber = new Regex(@"(-?)\{\[!v\*([0-9]+)\]\}(%?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>
        /// A key with its braced number slots unbraced (<c>{[!v*0]}</c> → <c>[!v*0]</c>): the shape of the same text once
        /// the game has formatted it — a <c>%</c> glued to the hole goes with the number, and so does a sign before it
        /// (<c>-{0}</c> shown <c>-3</c>), as the number pass takes them.
        /// </summary>
        public static string Unbrace(string key) => BracedSlot.Replace(key, "$1");

        /// <summary>
        /// The translation of a key's formatted shape given the braces its written shape has (<paramref name="bracedKey"/>,
        /// <see cref="Unbrace"/>'s inverse): each slot braced there is braced again, its sign before it and its <c>%</c>
        /// after it. Null when one of those slots is not once in the translation.
        /// </summary>
        public static string? Rebrace(string translation, string bracedKey)
        {
            foreach (Match m in BracedNumber.Matches(bracedKey))
            {
                string slot = PlaceholderPrefix + m.Groups[2].Value + PlaceholderSuffix;
                int at = translation.IndexOf(slot, StringComparison.Ordinal);
                if (at < 0 || translation.IndexOf(slot, at + 1, StringComparison.Ordinal) >= 0) return null;
                if (at > 0 && translation[at - 1] == '{') continue;
                translation = translation.Substring(0, at) + m.Groups[1].Value + "{" + slot + "}" + m.Groups[3].Value + translation.Substring(at + slot.Length);
            }
            return translation;
        }

        // ── the scan: the regular expression of the mod, written out ─────────────

        /// <summary>
        /// The next number at or after <paramref name="from"/>: what <c>(?&lt;!\[!v\*)(-?\d+(?:[.,]\d+)?%?)</c> finds, with
        /// <c>\d</c> being <see cref="KeyChars.IsDigit"/>. Leftmost, greedy, never overlapping the one before.
        /// </summary>
        private static bool NextMatch(string text, int from, out int start, out int end)
        {
            int n = text.Length;
            for (int i = from; i < n; i++)
            {
                // (?<!\[!v\*) — not right after a slot's opening
                if (i >= 4 && text[i - 4] == '[' && text[i - 3] == '!' && text[i - 2] == 'v' && text[i - 1] == '*') continue;

                int k = i;
                if (text[k] == '-')
                {
                    k++;
                    if (k >= n || !KeyChars.IsDigit(text[k])) continue;
                }
                else if (!KeyChars.IsDigit(text[k])) continue;

                int m = k;
                while (m < n && KeyChars.IsDigit(text[m])) m++;

                if (m < n && (text[m] == '.' || text[m] == ',') && m + 1 < n && KeyChars.IsDigit(text[m + 1]))
                {
                    m += 1;
                    while (m < n && KeyChars.IsDigit(text[m])) m++;
                }

                if (m < n && text[m] == '%') m++;

                start = i;
                end = m;
                return true;
            }
            start = end = n;
            return false;
        }

        /// <summary>A hexadecimal colour's tail: up to eight hexadecimal characters, then « # », just before the number.</summary>
        private static bool IsPartOfHexColor(string text, int index)
        {
            for (int i = index - 1; i >= 0 && i >= index - 8; i--)
            {
                char c = text[i];
                if (c == '#') return true;
                if (!IsHexChar(c)) break;
            }
            return false;
        }

        private static bool IsHexChar(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        /// <summary>
        /// Is the number at <paramref name="index"/> the digits of a <c>[!v*N]</c> or <c>[!STR*N]</c> slot already there:
        /// the opening within eight characters before it, then up to four digits and a « ] ».
        /// </summary>
        private static bool IsInsidePlaceholder(string text, int index)
        {
            for (int i = index - 1; i >= Math.Max(0, index - 8); i--)
            {
                if (i >= 3 && text[i] == '*' && text[i - 1] == 'v' && text[i - 2] == '!' && text[i - 3] == '[' && ClosesAfterDigits(text, index))
                    return true;
                if (i >= 5 && text[i] == '*' && text[i - 1] == 'R' && text[i - 2] == 'T' && text[i - 3] == 'S'
                    && text[i - 4] == '!' && text[i - 5] == '[' && ClosesAfterDigits(text, index))
                    return true;
            }
            return false;
        }

        private static bool ClosesAfterDigits(string text, int index)
        {
            for (int j = index; j < Math.Min(text.Length, index + 4); j++)
            {
                if (text[j] == ']') return true;
                if (!KeyChars.IsDigit(text[j])) break;
            }
            return false;
        }
    }
}
