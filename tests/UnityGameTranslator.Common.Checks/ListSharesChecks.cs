using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How several lists sharing a panel divide its height.
    ///
    /// 🔴 **Every case here was reported by somebody looking at the screen**, and each one had
    /// shipped: two lists of equal weight, a list drawing a gap under its last row while another
    /// scrolled, and a panel enlarged for nothing.
    /// </summary>
    internal static class ListSharesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── Alone ───────────────────────────────────────────────────────────────────────────
            var alone = ListShares.Split(new List<double> { 200 }, 800);

            check(alone.Count == 1 && alone[0].Weight > 0,
                "a list on its own takes the room",
                "there is nobody to leave it to, and a panel enlarged to show more that then draws small does nothing");

            // ── Together, and it all fits ───────────────────────────────────────────────────────
            var fits = ListShares.Split(new List<double> { 120, 200 }, 800);

            check(Math.Abs(fits[0].Preferred - 120) < 0.001 && Math.Abs(fits[1].Preferred - 200) < 0.001,
                "when everything fits, each asks for exactly its own content",
                "a list that stops at its last row is finished, and says so");

            check(fits[0].Weight == 0 && fits[1].Weight == 0,
                "and neither takes the room that is left",
                "🔴 the reported defect: a list handed room it cannot fill draws a gap under its last row");

            // ── Together, and it does not fit ───────────────────────────────────────────────────
            // Three rows beside eight, in a window too small for both: the short one keeps its
            // three, the long one takes everything else.
            var tight = ListShares.Split(new List<double> { 150, 400 }, 300);

            check(tight[0].Preferred <= 150 && tight[1].Preferred <= 400,
                "a squeezed list is never given more than it can fill",
                "the short list beside a long one takes its few rows and leaves the rest");

            check(tight[0].Preferred + tight[1].Preferred <= 300.001,
                "and the two together do not ask for more than there is",
                "asking for more than the panel has is how a second scrollbar appears beside the first");

            check(tight[1].Weight > tight[0].Weight,
                "what is left over goes to the one still scrolling",
                "🔴 both asking equally is what gave a list of three half of a tall window");

            // ── The shape that made the rule ────────────────────────────────────────────────────
            // The backups screen as it was reported: three saved copies, eight automatic ones.
            var real = ListShares.Split(new List<double> { 3 * 56, 8 * 56 }, 600);

            check(Math.Abs(real[0].Preferred - 168) < 0.001 && real[0].Weight == 0,
                "three saved copies ask for three rows and stop",
                "this is the list that was drawing a gap while the other scrolled");

            check(real[1].Weight > 0,
                "and the eight automatic ones take what is left",
                "the room freed by the first has somewhere useful to go");

            // ── Squeezed to nothing ─────────────────────────────────────────────────────────────
            // 🔴 A short list above a long one, in a window shrunk hard. Proportionally the short
            // one would get a handful of pixels: its heading there, its rows gone, which reads as
            // a bug rather than as a small window.
            // ⚠ The floor is in pixels and covers the chrome too — a heading and padding around
            // the rows — because a floor that only counts rows buys no visible row at all.
            const double row = 56;
            const double chrome = 90;
            const double floor = chrome + row;

            var crushed = ListShares.Split(new List<double> { chrome + 2 * row, chrome + 20 * row },
                                           300, floor);

            check(crushed[0].Preferred >= floor - 0.001,
                "a squeezed list keeps its heading AND a row",
                "a list showing its heading and none of its rows is there while showing nothing");

            // ⚠ And the floor never hands a list room it has nothing to fill.
            var tiny = ListShares.Split(new List<double> { chrome, chrome + 20 * row }, 300, floor);

            check(tiny[0].Preferred <= chrome + 0.001,
                "but never more than it holds",
                "a floor that overshoots the content is the gap under the last row, again");

            // ── Degenerate ──────────────────────────────────────────────────────────────────────
            check(ListShares.Split(new List<double>(), 800).Count == 0,
                "no lists, no shares",
                "a panel whose lists are all empty holds sentences, not scroll areas");

            var unmeasured = ListShares.Split(new List<double> { 100, 100 }, 0);

            check(unmeasured.Count == 2 && unmeasured[0].Weight == 0,
                "a panel that has not been measured yet asks for content heights",
                "a window reports no size before its first layout, and a list sized on that opens at nothing");
        }
    }
}
