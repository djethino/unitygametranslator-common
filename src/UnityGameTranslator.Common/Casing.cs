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
