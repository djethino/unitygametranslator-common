using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>One difference between two texts: the old range is replaced by the new one. UTF-16 offsets.</summary>
    public struct TextHunk
    {
        public int OldStart;
        public int OldEnd;
        public int NewStart;
        public int NewEnd;
    }

    /// <summary>
    /// How two texts differ, DEFINED here to the letter so that every implementation cuts the same
    /// way (a port that answered another cut for the same pair of texts would move a link somewhere
    /// else in a sentence). It is part of the specification, not an optimisation:
    ///
    ///  1. the unit is the CODE POINT (a surrogate pair is one, a lone surrogate is one), never the
    ///     UTF-16 unit; offsets are given back in UTF-16 units;
    ///  2. the longest common PREFIX comes first, then the longest common SUFFIX of what is left
    ///     of both texts — so with repeated text the earlier copy is the one kept
    ///     (<c>aa</c> → <c>a</c> deletes the second, <c>abab</c> → <c>ab</c> deletes the last two);
    ///  3. what remains in the middle is compared by the longest common subsequence, and where two
    ///     ways are equally long the walk from the start takes a match whenever there is one and
    ///     otherwise INSERTS before it deletes — which keeps the earliest characters of the old
    ///     text matched;
    ///  4. a hunk is a maximal run of characters that are not matched.
    ///
    /// No table of any language, no constant. The middle is quadratic in its length in the worst
    /// case; after rule 2 it is the size of the edit, which is what a person types.
    /// </summary>
    public static class TextDiff
    {
        private static readonly List<TextHunk> None = new List<TextHunk>();

        public static List<TextHunk> Hunks(string oldText, string newText)
        {
            if (oldText == null) throw new ArgumentNullException(nameof(oldText));
            if (newText == null) throw new ArgumentNullException(nameof(newText));
            if (string.Equals(oldText, newText, StringComparison.Ordinal)) return None;

            int[] a = CodePoints(oldText, out int[] offA);
            int[] b = CodePoints(newText, out int[] offB);

            int prefix = 0;
            while (prefix < a.Length && prefix < b.Length && a[prefix] == b[prefix]) prefix++;
            int suffix = 0;
            while (suffix < a.Length - prefix && suffix < b.Length - prefix
                   && a[a.Length - 1 - suffix] == b[b.Length - 1 - suffix]) suffix++;

            int n = a.Length - prefix - suffix;
            int m = b.Length - prefix - suffix;
            var hunks = new List<TextHunk>();

            if (n == 0 || m == 0)
            {
                hunks.Add(Hunk(offA, offB, prefix, prefix + n, prefix, prefix + m));
                return hunks;
            }

            // Longest common subsequence of the middle, by suffixes: lcs[i, j] for a[prefix+i..], b[prefix+j..].
            var lcs = new int[n + 1, m + 1];
            for (int i = n - 1; i >= 0; i--)
                for (int j = m - 1; j >= 0; j--)
                    lcs[i, j] = a[prefix + i] == b[prefix + j]
                        ? lcs[i + 1, j + 1] + 1
                        : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

            int x = 0, y = 0;
            int runOldFrom = -1, runNewFrom = -1;
            while (x < n || y < m)
            {
                bool match = x < n && y < m && a[prefix + x] == b[prefix + y];
                if (match)
                {
                    if (runOldFrom >= 0)
                    {
                        hunks.Add(Hunk(offA, offB, prefix + runOldFrom, prefix + x, prefix + runNewFrom, prefix + y));
                        runOldFrom = runNewFrom = -1;
                    }
                    x++; y++;
                    continue;
                }

                if (runOldFrom < 0) { runOldFrom = x; runNewFrom = y; }

                if (x >= n) y++;
                else if (y >= m) x++;
                else if (lcs[x + 1, y] > lcs[x, y + 1]) x++;
                else y++;   // a tie inserts first: the old characters stay available to match later
            }

            if (runOldFrom >= 0)
                hunks.Add(Hunk(offA, offB, prefix + runOldFrom, prefix + n, prefix + runNewFrom, prefix + m));
            return hunks;
        }

        private static TextHunk Hunk(int[] offA, int[] offB, int oldFrom, int oldTo, int newFrom, int newTo) =>
            new TextHunk { OldStart = offA[oldFrom], OldEnd = offA[oldTo], NewStart = offB[newFrom], NewEnd = offB[newTo] };

        /// <summary>The code points of a text, and the UTF-16 offset of each (plus the end).</summary>
        private static int[] CodePoints(string text, out int[] offsets)
        {
            var points = new List<int>(text.Length);
            var at = new List<int>(text.Length + 1);
            for (int i = 0; i < text.Length; i++)
            {
                at.Add(i);
                char c = text[i];
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    points.Add(char.ConvertToUtf32(c, text[i + 1]));
                    i++;
                }
                else points.Add(c);
            }
            at.Add(text.Length);
            offsets = at.ToArray();
            return points.ToArray();
        }
    }

    /// <summary>
    /// THE border rule — written here and nowhere else (user decision pending, 2026-11-13): which
    /// differences touch a linked passage. A difference stuck to the edge of a passage is OUTSIDE
    /// it: text typed right after « pièces d'étoile » is not part of the term. To change the rule,
    /// change <see cref="Touches"/> and the cases of corpus/rules/passage_border.json — nothing else
    /// in the editing rules knows where an edge is.
    ///
    /// The border rule has three companions that move with it, all defined from the same notion of « stuck
    /// to the edge », and all in the corpus (rules/passage_border.json, rules/link_editing.json):
    /// <see cref="Straddles"/> (a change that reaches outside the passage drops the link), the SLIDING of a
    /// hunk to the placement that straddles fewest passages (LinkEditing.Slide — a case that fails without
    /// it: k5-last-word-deleted), and the ABSORPTION of typed words stuck to a changed passage when they
    /// complete a known writing (<see cref="Adjacent"/> — a case that fails without it: c2-a-known-writing).
    /// </summary>
    public static class PassageBorder
    {
        /// <summary>
        /// Does the hunk <c>[hunkStart, hunkEnd)</c> of the old text touch the passage
        /// <c>[passageStart, passageEnd)</c>? An insertion (empty old range) touches only strictly
        /// inside; a deletion or replacement touches when it overlaps.
        /// </summary>
        public static bool Touches(int hunkStart, int hunkEnd, int passageStart, int passageEnd) =>
            hunkStart == hunkEnd
                ? passageStart < hunkStart && hunkStart < passageEnd
                : hunkStart < passageEnd && hunkEnd > passageStart;

        /// <summary>
        /// On which side of a passage a hunk sits stuck to it: -1 when it ends exactly where the passage
        /// begins, +1 when it begins exactly where the passage ends, 0 otherwise (a hunk that touches the
        /// passage is never adjacent to it). These are the words the <b>absorption</b> rule may take into a
        /// CHANGED passage when they complete a writing the term already has (LinkEditing.Reconcile): it lives
        /// here because it is the other half of « stuck to the edge ».
        /// </summary>
        public static int Adjacent(int hunkStart, int hunkEnd, int passageStart, int passageEnd)
        {
            if (Touches(hunkStart, hunkEnd, passageStart, passageEnd)) return 0;
            if (hunkEnd == passageStart) return -1;
            if (hunkStart == passageEnd) return 1;
            return 0;
        }

        /// <summary>A touching hunk that also reaches outside the passage: it covers words around it.</summary>
        public static bool Straddles(int hunkStart, int hunkEnd, int passageStart, int passageEnd) =>
            Touches(hunkStart, hunkEnd, passageStart, passageEnd)
            && hunkStart < hunkEnd && (hunkStart < passageStart || hunkEnd > passageEnd);
    }
}
