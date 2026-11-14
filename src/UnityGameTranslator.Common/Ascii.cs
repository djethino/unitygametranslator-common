using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What a text DECLARED ASCII is compared with: a file extension, a hexadecimal digest, a protocol keyword, a language code,
    /// a uuid. Only the 26 letters fold — A-Z onto a-z — and nothing else does, on every runtime.
    ///
    /// 🔴 **Why not <c>OrdinalIgnoreCase</c> / <c>ToLowerInvariant</c>**: they fold with the Unicode of the machine that runs
    /// them (the Manager's .NET 8, a game's Mono, an IL2CPP table frozen years ago) and answer differently on the letters one
    /// of them does not know. For a text that is ASCII by contract that is a risk with nothing to gain; a text that is NOT
    /// (a game's name, a font, a sprite) does not belong here — it waits for the folding table generated from the project's
    /// Unicode (<c>UNICODE_VERSION</c>), see the plan in analyse/smart-suivi-socle.md.
    ///
    /// ⚠ So « É » is not « é » here, « ı » is not « I », and the Kelvin sign is not « k ». Where that would matter the text is
    /// not ASCII and this is the wrong class.
    /// </summary>
    public static class Ascii
    {
        /// <summary>The character, with A-Z folded to a-z and everything else left as it is.</summary>
        public static char Lower(char c) => c >= 'A' && c <= 'Z' ? (char)(c + 32) : c;

        /// <summary>The text with A-Z folded to a-z; the same instance when it holds no capital.</summary>
        public static string Lower(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int first = 0;
            while (first < text.Length && !(text[first] >= 'A' && text[first] <= 'Z')) first++;
            if (first == text.Length) return text;

            var chars = text.ToCharArray();
            for (int i = first; i < chars.Length; i++) chars[i] = Lower(chars[i]);
            return new string(chars);
        }

        /// <summary>Whether the two are the same text but for the case of A-Z. Two nulls are equal; a null is no text.</summary>
        public static bool EqualsIgnoreCase(string? a, string? b)
        {
            if (ReferenceEquals(a, b)) return true;
            if (a == null || b == null || a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
                if (Lower(a[i]) != Lower(b[i])) return false;
            return true;
        }

        /// <summary>Whether the text begins with <paramref name="prefix"/>, but for the case of A-Z.</summary>
        public static bool StartsWithIgnoreCase(string? text, string prefix) => MatchesAt(text, 0, prefix);

        /// <summary>Whether the text ends with <paramref name="suffix"/>, but for the case of A-Z.</summary>
        public static bool EndsWithIgnoreCase(string? text, string suffix) =>
            text != null && text.Length >= suffix.Length && MatchesAt(text, text.Length - suffix.Length, suffix);

        /// <summary>The first place <paramref name="needle"/> is found, but for the case of A-Z; -1 when it is not. The empty needle is at 0.</summary>
        public static int IndexOfIgnoreCase(string? text, string needle)
        {
            if (text == null) return -1;
            for (int at = 0; at + needle.Length <= text.Length; at++)
                if (MatchesAt(text, at, needle)) return at;
            return -1;
        }

        private static bool MatchesAt(string? text, int at, string part)
        {
            if (text == null || at < 0 || at + part.Length > text.Length) return false;
            for (int i = 0; i < part.Length; i++)
                if (Lower(text[at + i]) != Lower(part[i])) return false;
            return true;
        }
    }
}
