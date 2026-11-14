using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The case of the FIRST letter of a text, the same everywhere (spec/translation-file/casing.md): what a link's
    /// <c>case: upper-first</c> / <c>lower-first</c> asks for (<see cref="LinkReading.UpperFirst"/>).
    ///
    /// 🔴 **Unicode's data, never the machine's culture, never the runtime's <c>ToUpper</c>.** Only the FIRST code point
    /// of the text is looked at, and only the language-independent mapping: UnicodeData's simple one with
    /// SpecialCasing's unconditional multi-letter ones laid over it (« ß » → « Ss »). A capital is Unicode's TITLECASE
    /// (« ǆ » → « ǅ », not « Ǆ »), else the uppercase. Nothing depends on a language (Turkish dotted I, Lithuanian) or
    /// on a context (the final sigma). A text whose first character has no case, or is not a letter, comes back as it
    /// is — the function never skips ahead to the first letter: a script with no case needs no special case.
    ///
    /// The tables (Casing.Tables.g.cs, and corpus/data/first-letter.json for a port in another language) are generated
    /// by tools/generate-casing.py from the Unicode version the whole project pins (<see cref="UnicodeVersion"/>) — the
    /// mod's own tables must carry the same.
    /// </summary>
    public static partial class Casing
    {
        /// <summary>The text with its first letter written as a capital; the very same string when nothing changes.</summary>
        public static string? UpperFirst(string? text) => MapFirst(text, CapitalKeys, CapitalValues);

        /// <summary>The text with its first letter written as a small letter; the very same string when nothing changes.</summary>
        public static string? LowerFirst(string? text) => MapFirst(text, SmallKeys, SmallValues);

        /// <summary>
        /// The WHOLE text written in small letters: every code point replaced by its SIMPLE lower case (UnicodeData's, one code point to
        /// one, the project's Unicode version), never the runtime's and with no rule of any language; the very same string when nothing
        /// changes. Its length in UTF-16 units is the text's (no small letter lies in another plane than its capital).
        ///
        /// 🔴 What « simple » means, written out — the same reading as <see cref="Names"/>, which folds to capitals:
        /// · a capital with no small letter of its own, or one that crosses ASCII, stays as it is: « İ » (U+0130) stays « İ » (its small
        ///   letter is an ASCII « i », and the Turkish pair is not an ASCII one), the Kelvin sign « K » (U+212A) stays itself — so that
        ///   two texts that <see cref="Names"/> keeps apart because of such a pair stay apart here too;
        /// · « Σ » is always « σ »: no final-sigma rule (that one depends on the neighbours — a language's rule), « ΟΔΟΣ » gives « οδοσ »;
        /// · « ẞ » (U+1E9E) gives « ß »; a titlecase digraph « ǅ » gives « ǆ »; « ß » stays « ß » (it is already small);
        /// · a letter the runtime has not heard of yet (the Garay capitals of Unicode 16) is lowered all the same.
        ///
        /// The capital form of a whole text is <see cref="Names.Fold(string)"/>: the same table the names are compared by, so there is no
        /// <c>Casing.Upper</c> — a second door to the same rule.
        /// </summary>
        public static string? Lower(string? text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            char[]? lowered = null;
            for (int i = 0; i < text!.Length; i++)
            {
                char c = text[i];
                int cp = c;
                bool pair = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                if (pair) cp = char.ConvertToUtf32(c, text[i + 1]);
                int small = SimpleLower(cp);
                if (small != cp)
                {
                    lowered ??= text.ToCharArray();
                    if (small > 0xFFFF)
                    {
                        string two = char.ConvertFromUtf32(small);
                        lowered[i] = two[0];
                        lowered[i + 1] = two[1];
                    }
                    else lowered[i] = (char)small;
                }
                if (pair) i++;
            }
            return lowered == null ? text : new string(lowered);
        }

        private static int SimpleLower(int codePoint)
        {
            if (codePoint < 0x80) return codePoint >= 'A' && codePoint <= 'Z' ? codePoint + 32 : codePoint;
            int lo = 0, hi = SimpleLowerFrom.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int at = SimpleLowerFrom[mid];
                if (codePoint < at) hi = mid - 1;
                else if (codePoint > at) lo = mid + 1;
                else return SimpleLowerTo[mid];
            }
            return codePoint;
        }

        /// <summary>Every entry of a table, in order: what the checks hold equal to corpus/data/first-letter.json.</summary>
        internal static System.Collections.Generic.IReadOnlyList<System.Collections.Generic.KeyValuePair<int, string>> Entries(bool capital)
        {
            int[] keys = capital ? CapitalKeys : SmallKeys;
            string[] values = capital ? CapitalValues : SmallValues;
            var list = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<int, string>>(keys.Length);
            for (int i = 0; i < keys.Length; i++) list.Add(new System.Collections.Generic.KeyValuePair<int, string>(keys[i], values[i]));
            return list;
        }

        private static string? MapFirst(string? text, int[] keys, string[] values)
        {
            if (string.IsNullOrEmpty(text)) return text;

            char c = text![0];
            int cp = char.IsHighSurrogate(c) && text.Length > 1 && char.IsLowSurrogate(text[1]) ? char.ConvertToUtf32(c, text[1]) : c;
            int at = Array.BinarySearch(keys, cp);
            if (at < 0) return text;
            return values[at] + text.Substring(cp > 0xFFFF ? 2 : 1);
        }
    }
}
