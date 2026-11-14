namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The general category of a code point (Lu, Ll, Nd, Mn, Zs …), from the project's Unicode (<c>UNICODE_VERSION</c>), all
    /// planes — never from the runtime, whose Unicode is the one of whatever runs the code (the Manager's .NET 8 does not know
    /// 22 068 letters Unicode 18 does; a game's Mono and IL2CPP know fewer). The table is generated:
    /// tools/generate-general-category.py.
    ///
    /// A surrogate is not a code point of a text: read a pair as one (<see cref="Of(string, int)"/>); a lone surrogate, and
    /// every code point nobody has assigned, is <c>Cn</c>.
    /// </summary>
    public static partial class UnicodeCategories
    {
        /// <summary>The two-letter category of a code point; <c>Cn</c> when it has none.</summary>
        public static string Of(int codePoint)
        {
            if (codePoint < 0 || codePoint > 0x10FFFF) return "Cn";
            int lo = 0, hi = Firsts.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                if (codePoint < Firsts[mid]) hi = mid - 1;
                else if (codePoint > Lasts[mid]) lo = mid + 1;
                else return Names[Kinds[mid]];
            }
            return "Cn";
        }

        /// <summary>The category of the code point that starts at <paramref name="index"/> of the text (a surrogate pair read as one).</summary>
        public static string Of(string text, int index)
        {
            char c = text[index];
            if (char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
                return Of(char.ConvertToUtf32(c, text[index + 1]));
            return char.IsSurrogate(c) ? "Cn" : Of(c);
        }

        /// <summary>Whether the code point is a letter (Lu Ll Lt Lm Lo).</summary>
        public static bool IsLetter(int codePoint) => Of(codePoint)[0] == 'L';

        /// <summary>Whether the code point is a mark (Mn Mc Me).</summary>
        public static bool IsMark(int codePoint) => Of(codePoint)[0] == 'M';

        /// <summary>Whether the code point is a number (Nd Nl No).</summary>
        public static bool IsNumber(int codePoint) => Of(codePoint)[0] == 'N';
    }
}
