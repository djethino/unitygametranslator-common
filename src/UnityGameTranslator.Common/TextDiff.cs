using System;

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
    /// How two SUCCESSIVE texts differ, in three lines (spec/translation-file/editing.md §2): the longest common
    /// prefix, then the longest common suffix of what is left of both texts, and what lies between is ONE hunk —
    /// the old range replaced by the new. No table, no search for an alignment: linear in time and memory, and
    /// trivial to port to the letter. A surrogate pair is never cut (the prefix and the suffix back off one unit
    /// rather than end or begin between its halves); offsets are UTF-16 units.
    /// </summary>
    public static class TextDiff
    {
        /// <summary>The one hunk between the two texts, or null when they are equal.</summary>
        public static TextHunk? Hunk(string oldText, string newText)
        {
            if (oldText == null) throw new ArgumentNullException(nameof(oldText));
            if (newText == null) throw new ArgumentNullException(nameof(newText));
            if (string.Equals(oldText, newText, StringComparison.Ordinal)) return null;

            int shortest = Math.Min(oldText.Length, newText.Length);

            int prefix = 0;
            while (prefix < shortest && oldText[prefix] == newText[prefix]) prefix++;
            if (prefix > 0 && char.IsHighSurrogate(oldText[prefix - 1])) prefix--;

            int room = shortest - prefix;
            int suffix = 0;
            while (suffix < room && oldText[oldText.Length - 1 - suffix] == newText[newText.Length - 1 - suffix]) suffix++;
            if (suffix > 0 && char.IsLowSurrogate(oldText[oldText.Length - suffix])) suffix--;

            return new TextHunk
            {
                OldStart = prefix,
                OldEnd = oldText.Length - suffix,
                NewStart = prefix,
                NewEnd = newText.Length - suffix,
            };
        }
    }

    /// <summary>
    /// THE border rule and its companions, in one place so they change together (spec/translation-file/editing.md §4, §8):
    /// which differences touch a linked passage, which reach outside it, whether it stays open for the next
    /// keystroke, and which writing of a term a changed passage is widened to.
    ///
    /// **The border rule (default of 2026-11-13, the user's opinion pending)**: a difference stuck to the edge of a
    /// passage is OUTSIDE it. **Open** is its one exception and follows from the gesture: a passage whose end was
    /// just replaced or erased stays open, so the next characters typed at its end are its own — retyping a passage
    /// letter by letter is retyping the passage. **Absorption**: words typed against a changed passage are taken
    /// into a writing the term already has when that writing, found in the text, contains them.
    /// To change any of it, change these methods and rules/passage_border.json — nothing else knows where an edge is.
    /// </summary>
    public static class PassageBorder
    {
        /// <summary>
        /// Does the hunk <c>[hunkStart, hunkEnd)</c> of the old text touch the passage <c>[passageStart, passageEnd)</c>?
        /// An insertion (empty old range) touches strictly inside, or at the very end of an OPEN passage; a deletion
        /// or replacement touches when it overlaps.
        /// </summary>
        public static bool Touches(int hunkStart, int hunkEnd, int passageStart, int passageEnd, bool open) =>
            hunkStart == hunkEnd
                ? (passageStart < hunkStart && hunkStart < passageEnd) || (open && hunkStart == passageEnd)
                : hunkStart < passageEnd && hunkEnd > passageStart;

        /// <summary>A touching hunk that also reaches outside the passage: it covers words around it.</summary>
        public static bool Straddles(int hunkStart, int hunkEnd, int passageStart, int passageEnd, bool open) =>
            Touches(hunkStart, hunkEnd, passageStart, passageEnd, open)
            && hunkStart < hunkEnd && (hunkStart < passageStart || hunkEnd > passageEnd);

        /// <summary>Is the passage open for the next change after this hunk: it was touched, and the hunk reached its end.</summary>
        public static bool StaysOpen(int hunkStart, int hunkEnd, int passageStart, int passageEnd, bool open) =>
            Touches(hunkStart, hunkEnd, passageStart, passageEnd, open) && hunkEnd >= passageEnd;

        /// <summary>
        /// Where a writing found in the text contains the changed words <c>[start, end)</c>: the leftmost
        /// occurrence of <paramref name="writing"/> that starts at or before <paramref name="start"/> and ends at or
        /// after <paramref name="end"/>; -1 when there is none.
        /// </summary>
        public static int AbsorbedAt(string text, int start, int end, string writing)
        {
            if (string.IsNullOrEmpty(writing) || end - start > writing.Length || end > text.Length) return -1;
            int from = Math.Max(0, end - writing.Length);
            int to = Math.Min(start, text.Length - writing.Length);
            for (int at = from; at <= to; at++)
                if (string.CompareOrdinal(text, at, writing, 0, writing.Length) == 0) return at;
            return -1;
        }
    }
}
