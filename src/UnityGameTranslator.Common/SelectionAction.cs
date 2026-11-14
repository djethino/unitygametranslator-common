using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>What a selection in a key can do.</summary>
    public struct SelectionChoice
    {
        /// <summary><see cref="SelectionAction.OpenTerm"/> or <see cref="SelectionAction.NewTerm"/>.</summary>
        public string Action;

        /// <summary>The key: of the term to open, or of the text to make a term of.</summary>
        public string Key;

        /// <summary>For a new term: the line the file already holds under that key, when it does (it will BECOME a term).</summary>
        public TranslationLine? Existing;
    }

    /// <summary>
    /// What a selection in a KEY (the game's text, the source) can do: one question, one answer, so that the grid, the edit window and the
    /// floating button agree. Pure.
    ///
    ///  · the words are already a term — or ARE the citation of one: its key with the first letter in the other case —: <see cref="OpenTerm"/>,
    ///    that window is the one to open;
    ///  · they can become one: <see cref="NewTerm"/> (a line the file holds is marked, a new one is created: <see cref="TermEdit.OpenNew"/>);
    ///  · nothing: a file of the format 1, words that are not a text of the game (a name of the file, a template, a marker), or a text that
    ///    cites terms itself — a path known to end in a refusal is not offered (ux-rules, « jamais d'impasse »).
    ///
    /// No rule of citation is written here beyond <see cref="LinkFinding.CitedWhole"/>: the words are taken as a whole, exactly as selected
    /// (a plural is not looked up as its singular).
    /// </summary>
    public static class SelectionAction
    {
        public const string OpenTerm = "open-term";
        public const string NewTerm = "new-term";

        /// <param name="words">The text selected, as the person selected it (trimmed here, nothing else is done to it).</param>
        /// <param name="lineOf">The line of the file being written, with its pending work, by its key; null when there is none.</param>
        /// <param name="fileFormat">The format of that file.</param>
        /// <param name="terms">The keys of the file's terms, indexed (<see cref="TermIndex.OfKeys"/>); null when it has none.</param>
        public static SelectionChoice? For(string? words, Func<string, TranslationLine?> lineOf, int fileFormat, TermIndex? terms)
        {
            if (lineOf == null) throw new ArgumentNullException(nameof(lineOf));
            if (fileFormat < 2) return null;

            string key = (words ?? string.Empty).TrimSpaces();
            if (key.Length == 0) return null;
            if (TranslationFileKeys.IsMetadataKey(key) || LineMarkers.ClosedSlots(key).Count > 0 || key.IndexOf("[!", StringComparison.Ordinal) >= 0) return null;

            TranslationLine? line = lineOf(key);
            if (line.HasValue && line.Value.Term) return new SelectionChoice { Action = OpenTerm, Key = key };

            // The words ARE the citation of a term (its key with the first letter in the other case, say): that term
            if (terms != null)
            {
                string? cited = LinkFinding.CitedWhole(terms, key, line.HasValue ? line.Value.Unlinked : null);
                if (cited != null) return new SelectionChoice { Action = OpenTerm, Key = cited };
            }

            if (line.HasValue && ((line.Value.Links != null && line.Value.Links.Count > 0)
                                  || (line.Value.Value ?? string.Empty).IndexOf("[!g*", StringComparison.Ordinal) >= 0)) return null;

            return new SelectionChoice { Action = NewTerm, Key = key, Existing = line };
        }
    }
}
