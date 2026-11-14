using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The white space at the ends of a text, and a text made of nothing else — answered by Unicode's White_Space
    /// (<see cref="KeyChars.IsSpace"/>, generated from the project's Unicode version), never by the runtime's
    /// <c>Trim()</c> / <c>IsNullOrWhiteSpace</c>: those answer with the Unicode of the machine that runs them.
    ///
    /// Measured on .NET 8.0.34 over the whole plane: the same 25 characters, so nothing a Manager or a test says changes;
    /// what changes is the game's own runtime, which now agrees with it.
    ///
    /// 🔴 **No allocation unless something is taken off**: a text with no white space at its ends comes back as the very
    /// same string, as <c>Trim()</c> returns it. Not the same names as the instance methods on purpose: an instance method
    /// would win over an extension of the same name, and the old call would silently stay.
    /// </summary>
    public static class Spaces
    {
        /// <summary>The text without its white space at both ends.</summary>
        public static string TrimSpaces(this string text) => TrimSpacesEnd(TrimSpacesStart(text));

        /// <summary>The text without its white space at its start.</summary>
        public static string TrimSpacesStart(this string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int from = 0;
            while (from < text.Length && KeyChars.IsSpace(text[from])) from++;
            return from == 0 ? text : text.Substring(from);
        }

        /// <summary>The text without its white space at its end.</summary>
        public static string TrimSpacesEnd(this string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            int to = text.Length;
            while (to > 0 && KeyChars.IsSpace(text[to - 1])) to--;
            return to == text.Length ? text : text.Substring(0, to);
        }

        /// <summary>Null, empty, or nothing but white space.</summary>
        public static bool IsBlank(string? text)
        {
            if (text == null) return true;
            for (int i = 0; i < text.Length; i++)
                if (!KeyChars.IsSpace(text[i])) return false;
            return true;
        }
    }
}
