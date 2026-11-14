namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Where a word ends, for finding a term in a text (spec/translation-file/finding.md §5): an edge cuts a word when the
    /// character just inside it and the character just outside it are BOTH written stuck together into words — a CONSTITUENT OF
    /// A WORD (Unicode's Word_Break, UAX #29: letters, digits, the marks that belong to a letter, the low line) in a script the
    /// project writes with spaces (<see cref="Classes"/>: Unicode's line breaking classes, UAX #14, which leave out ideographs,
    /// kana, Thai, Lao, Khmer and Myanmar). Everything else is an edge: spaces (the no-break one too), punctuation, the hyphen,
    /// the apostrophe, the full stop, and every sign and symbol a game draws round a name (<c>#</c> <c>*</c> <c>&lt;</c> <c>☆</c>)
    /// — « Coin » is in « Coin-op », « d'Coin », « *Coin* » and « &lt;b&gt;Coin&lt;/b&gt; », not in « Coins », « Coin2 » or « _Coin_ ».
    ///
    /// 🔴 It was the line breaking classes ALONE until 2026-11-14, and that was the wrong question: UAX #14 says where a LINE may
    /// break and files every symbol it has no other use for with the letters, so that a name between `☆…☆` or `*…*` was never found.
    /// UAX #29 says where a WORD ends; the classes stay as the project's decisions over it.
    ///
    /// 🔴 No script and no range is named here: the table (WordGlue.Tables.g.cs, tools/generate-word-glue.py) is
    /// Unicode's, at the project's pinned version (<see cref="UnicodeVersion"/>, equal to the mod's), and it is
    /// nothing a caller can replace — a function handed in by the caller is what made the case rule diverge.
    /// </summary>
    public static partial class WordGlue
    {
        /// <summary>Is this code point written stuck into words?</summary>
        public static bool IsGlued(int codePoint)
        {
            int lo = 0, hi = GluedRanges.Length / 2 - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (codePoint < GluedRanges[mid * 2]) hi = mid - 1;
                else if (codePoint > GluedRanges[mid * 2 + 1]) lo = mid + 1;
                else return true;
            }
            return false;
        }

        /// <summary>May a word begin at <paramref name="index"/>: not between a glued character before it and one at it?</summary>
        public static bool EdgeBefore(string text, int index)
        {
            if (index <= 0 || index >= text.Length) return true;
            return !(IsGlued(CodePointAt(text, index)) && IsGlued(CodePointBefore(text, index)));
        }

        /// <summary>May a word end just before <paramref name="index"/> (an exclusive end)?</summary>
        public static bool EdgeAfter(string text, int index)
        {
            if (index <= 0 || index >= text.Length) return true;
            return !(IsGlued(CodePointBefore(text, index)) && IsGlued(CodePointAt(text, index)));
        }

        private static int CodePointAt(string s, int i)
        {
            char c = s[i];
            return char.IsHighSurrogate(c) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]) ? char.ConvertToUtf32(c, s[i + 1]) : c;
        }

        private static int CodePointBefore(string s, int i)
        {
            char c = s[i - 1];
            return char.IsLowSurrogate(c) && i >= 2 && char.IsHighSurrogate(s[i - 2]) ? char.ConvertToUtf32(s[i - 2], c) : c;
        }
    }
}
