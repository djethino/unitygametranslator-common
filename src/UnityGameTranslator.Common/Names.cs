using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// How the NAMES of things (a game, a font, a sprite, a language, a file, a tag) are compared and ordered without caring
    /// about case: every code point is written as its simple capital, from the project's Unicode (<c>UNICODE_VERSION</c>,
    /// tools/generate-case-mapping.py) — never the runtime's, which is the Unicode of whatever runs the code.
    ///
    /// 🔴 **The same answers as <c>OrdinalIgnoreCase</c>, and the same ORDER, wherever the runtime knows the letters** — which
    /// is why it is the CAPITAL and not the small letter: in capitals « _ » sorts after « Z » and before « a »-« z » as it
    /// always did, so a list written in that order (a pack's references) does not move. Held by NamesChecks over every code
    /// point against .NET 8, with the exceptions listed and explained there.
    ///
    /// 🔴 **No bridge between ASCII and the rest**: « ı » (dotless i) is not « I », « ſ » is not « S ». ASCII letters fold
    /// with ASCII letters only (as <see cref="Ascii"/>); a text DECLARED ASCII (an extension, a digest, a code) goes through
    /// <see cref="Ascii"/>, a name through here.
    ///
    /// ⚠ **This is not the order a game's FILES are walked in** (the Manager's <c>GameFileOrder</c> writes capitals by
    /// <see cref="Casing"/> — « ß » as « SS », « ı » as « I », a digraph in title case — and differs from this on 109 code points):
    /// another question with its own rule, never to be joined to this one. And it is not the file system's opinion either: two
    /// names that differ only by a letter whose capital Unicode gave after the running runtime's table (the 75 pairs of
    /// NamesChecks) are the same name here and two files there.
    ///
    /// ⚠ One code point to one: « ß » stays « ß » (its capital is two letters). Never culture, never a normal form: « é »
    /// written as one code point and as « e » + accent are two names, as they are to the file system.
    /// </summary>
    public static partial class Names
    {
        /// <summary>The code point, written as its capital; itself when it has none.</summary>
        public static int Fold(int codePoint)
        {
            if (codePoint < 0x80) return codePoint >= 'a' && codePoint <= 'z' ? codePoint - 32 : codePoint;
            int lo = 0, hi = Lower.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int at = Lower[mid];
                if (codePoint < at) hi = mid - 1;
                else if (codePoint > at) lo = mid + 1;
                else return Upper[mid];
            }
            return codePoint;
        }

        /// <summary>
        /// The text with every code point written as its capital; the very same string when none changes. Its length in UTF-16
        /// units is the text's (no capital lies in another plane than its small letter), so a position in one is a position in the other.
        /// </summary>
        public static string Fold(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            char[]? folded = null;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                int cp = c;
                bool pair = char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]);
                if (pair) cp = char.ConvertToUtf32(c, text[i + 1]);
                int capital = Fold(cp);
                if (capital != cp)
                {
                    folded ??= text.ToCharArray();
                    if (capital > 0xFFFF)
                    {
                        string two = char.ConvertFromUtf32(capital);
                        folded[i] = two[0];
                        folded[i + 1] = two[1];
                    }
                    else folded[i] = (char)capital;
                }
                if (pair) i++;
            }
            return folded == null ? text : new string(folded);
        }

        /// <summary>Whether the two names are the same but for case. Two nulls are equal; a null is no name.</summary>
        public static bool Equals(string? a, string? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                char x = a[i], y = b[i];
                int cx = x, cy = y;
                bool pairX = char.IsHighSurrogate(x) && i + 1 < a.Length && char.IsLowSurrogate(a[i + 1]);
                bool pairY = char.IsHighSurrogate(y) && i + 1 < b.Length && char.IsLowSurrogate(b[i + 1]);
                if (pairX) cx = char.ConvertToUtf32(x, a[i + 1]);
                if (pairY) cy = char.ConvertToUtf32(y, b[i + 1]);
                if (pairX != pairY || Fold(cx) != Fold(cy)) return false;
                if (pairX) i++;
            }
            return true;
        }

        /// <summary>The first place <paramref name="part"/> is found in <paramref name="text"/> but for case; -1 when it is not. The empty part is at 0.</summary>
        public static int IndexOf(string? text, string part)
        {
            if (text == null) return -1;
            return Fold(text).IndexOf(Fold(part), StringComparison.Ordinal);
        }

        /// <summary>
        /// The order of two names: that of their capitals, CODE POINT by code point — the order .NET 8's <c>OrdinalIgnoreCase</c>
        /// gives (a letter beyond the plane sorts after U+FFFD, not between U+D7FF and U+E000 as UTF-16 units would put it).
        /// ⚠ Not the order of the content hash's keys, which is UTF-16 code units (schema.json, x-hash-key-order).
        /// </summary>
        public static int Compare(string? a, string? b)
        {
            if (ReferenceEquals(a, b)) return 0;
            if (a == null) return -1;
            if (b == null) return 1;
            int i = 0, j = 0;
            while (i < a.Length && j < b.Length)
            {
                int x = CodePointAt(a, i), y = CodePointAt(b, j);
                int fx = Fold(x), fy = Fold(y);
                if (fx != fy) return fx < fy ? -1 : 1;
                i += x > 0xFFFF ? 2 : 1;
                j += y > 0xFFFF ? 2 : 1;
            }
            return (a.Length - i) == (b.Length - j) ? 0 : (i < a.Length ? 1 : -1);
        }

        private static int CodePointAt(string text, int index)
        {
            char c = text[index];
            return char.IsHighSurrogate(c) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1])
                ? char.ConvertToUtf32(c, text[index + 1])
                : c;
        }

        /// <summary>The comparer for a dictionary, a set or a sort of names: <see cref="Equals(string, string)"/> and <see cref="Compare"/>.</summary>
        public static readonly NameComparer Comparer = new NameComparer();
    }

    /// <summary>A <see cref="StringComparer"/> by <see cref="Names"/>: what replaces <c>StringComparer.OrdinalIgnoreCase</c> where the strings are names.</summary>
    public sealed class NameComparer : StringComparer
    {
        internal NameComparer() { }

        /// <inheritdoc />
        public override int Compare(string? x, string? y) => Names.Compare(x, y);

        /// <inheritdoc />
        public override bool Equals(string? x, string? y) => Names.Equals(x, y);

        /// <inheritdoc />
        public override int GetHashCode(string obj)
        {
            if (obj == null) throw new ArgumentNullException(nameof(obj));
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < obj.Length; i++)
                {
                    char c = obj[i];
                    int cp = c;
                    if (char.IsHighSurrogate(c) && i + 1 < obj.Length && char.IsLowSurrogate(obj[i + 1])) { cp = char.ConvertToUtf32(c, obj[i + 1]); i++; }
                    hash = (hash ^ Names.Fold(cp)) * 16777619;
                }
                return hash;
            }
        }
    }
}
