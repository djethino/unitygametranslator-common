using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How tall a dropdown's list is, and when it earns a search field.
    ///
    /// 🔴 **The stake is that two products stop answering the same question differently.** A list of
    /// four answers had a search box in one and not the other; a list of a hundred and eighty could
    /// be handed two hundred pixels by a call site that had not thought about it. Nothing threw,
    /// nothing looked broken, and the two just drifted.
    /// </summary>
    internal static class DropdownFitChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            const double row = 28;
            const double window = 700;          // an ordinary window; share gives 315px

            // ── A short list: fits, so no search ────────────────────────────────────────────────
            var four = DropdownFit.Height(4, row, window);

            check(Math.Abs(four - 4 * row) < 0.001,
                "a list of four is exactly four rows tall",
                "empty space under the last row says an entry is missing");

            check(!DropdownFit.NeedsSearch(4, row, window),
                "and it gets no search field",
                "a search over a list already entirely visible is furniture");

            // ── A long list: capped, so searchable ──────────────────────────────────────────────
            var many = DropdownFit.Height(180, row, window);

            check(many < 180 * row && many <= window * DropdownFit.Share + 0.001,
                "a list of a hundred and eighty is capped at its share of the room",
                "a dropdown that covers its window stops reading as something in front of a screen");

            check(DropdownFit.NeedsSearch(180, row, window),
                "and it does get a search field",
                "at this length the search field is the only way through, not a convenience");

            // 🔴 The boundary, which is the whole rule: a search appears exactly when the list stops
            // fitting. Eleven rows fit in 315px, thirteen do not.
            check(!DropdownFit.NeedsSearch(11, row, window) && DropdownFit.NeedsSearch(13, row, window),
                "the search appears exactly when the list stops fitting",
                "any other trigger is a judgement made per call site, which is what this replaces");

            // ── A short window: the floor wins ──────────────────────────────────────────────────
            // ⚠ A panel 200px tall would give 90px — three rows — and a list cut to three rows turns
            // choosing into scrolling.
            var cramped = DropdownFit.Height(50, row, 200);

            check(cramped >= DropdownFit.LeastRows * row,
                "a cramped window still shows four rows",
                "a list cut to one or two rows is worse than a tall one");

            // ── A list whose height somebody else settled ───────────────────────────────────────
            // ⚠ The mod's popup opens downwards from its button with nothing to flip it, so it keeps
            // its own height for now — but the search question is still answered here.
            check(!DropdownFit.Overflows(7, row, 200) && DropdownFit.Overflows(9, row, 200),
                "a fixed 200px list overflows between seven rows and nine",
                "the same rule has to hold for a caller whose geometry is not ours to decide");

            // ── Degenerate inputs, because both callers measure rather than declare ─────────────
            check(DropdownFit.Height(0, row, window) == 0 && !DropdownFit.NeedsSearch(0, row, window),
                "an empty list is no rows tall and needs nothing",
                "a picker being refilled is empty for a frame, and must not size itself on that");

            check(DropdownFit.Height(10, 0, window) == 0 && !DropdownFit.NeedsSearch(10, 0, window),
                "a row height of nothing answers nothing rather than dividing by it",
                "the height is read from a laid-out row, which does not exist before the first pass");

            // ⚠ Room of zero is what a control that is not on screen yet reports. It must fall back
            // to the floor rather than to nothing, or the list opens at zero height.
            check(DropdownFit.Height(50, row, 0) == DropdownFit.LeastRows * row,
                "and a window that has not been measured yet still opens four rows",
                "a list that opens at zero height reads as a dropdown that is broken");
        }
    }
}
