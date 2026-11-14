using System;
using System.Collections.Generic;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// How the entries of a game's list are JOINED when no text holds them: a name the game draws (<c>劳</c> + <c>土</c> + <c>全</c>)
    /// is shown « Lao Tuquan », not « Lao TuQuan » — written in the file as <c>_lists.lists.NAME.join</c> (the text between two
    /// entries) and <c>join_case</c> (the case of the first letter of each entry after the first), spec/translation-file/lists.md.
    ///
    /// The case is <see cref="Casing"/>'s — the first code point only, Unicode's data, never the runtime's — so the mod, the Manager
    /// and a port all write the same name. Pure, with no JSON library: the caller reads the two fields and hands them in.
    /// </summary>
    public static class ListJoin
    {
        /// <summary>Each entry after the first starts with a small letter (the vocabulary of a link's <c>case</c>).</summary>
        public const string LowerFirst = "lower-first";

        /// <summary>Each entry after the first starts with a capital.</summary>
        public const string UpperFirst = "upper-first";

        /// <summary>Whether a text is a value <c>join_case</c> may hold — what a WRITER owes. A reader never refuses: see <see cref="Join"/>.</summary>
        public static bool IsCase(string? joinCase) => joinCase == LowerFirst || joinCase == UpperFirst;

        /// <summary>
        /// The entries as a name: the first as it is, then for each following one <paramref name="between"/> and the entry with the
        /// case of its first letter set by <paramref name="joinCase"/>.
        /// <list type="bullet">
        /// <item>An EMPTY (or absent) entry gives nothing, neither text nor separator, and does not count as the first: a name never
        /// starts with a separator nor holds two in a row, and the first entry that has words is the one kept as written.</item>
        /// <item><paramref name="between"/> absent is no text at all.</item>
        /// <item><paramref name="joinCase"/> absent — or a value this version does not know, which a READER meets in a file written by
        /// a newer one — keeps every entry's own first letter: a wrong capital, never a file refused.</item>
        /// <item>Only the first letter moves (a first character with no case is left as it is); the rest of the entry is untouched.</item>
        /// </list>
        /// </summary>
        public static string Join(IReadOnlyList<string?> entries, string? between, string? joinCase)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));
            var joined = new StringBuilder();
            foreach (string? entry in entries)
            {
                if (string.IsNullOrEmpty(entry)) continue;
                if (joined.Length == 0) { joined.Append(entry); continue; }

                joined.Append(between);
                joined.Append(joinCase == LowerFirst ? Casing.LowerFirst(entry)
                            : joinCase == UpperFirst ? Casing.UpperFirst(entry)
                            : entry);
            }
            return joined.ToString();
        }
    }
}
