using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What a scrolling list states about itself, and what the surface holding several of them
    /// must be able to reach.
    ///
    /// 🔴 **Every case here was reported by somebody looking at the screen**, and each one had
    /// shipped: a list drawing a gap under its last row while another scrolled, a list squeezed to
    /// its heading alone, and a window shrunk until the second list covered the Close button.
    /// </summary>
    internal static class ListRoomChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            const double row = 56;
            const double chrome = 90;

            // ── What a list asks for ────────────────────────────────────────────────────────────
            var three = ListRooms.For(3, row, chrome);

            check(Math.Abs(three.Whole - (chrome + 3 * row)) < 0.001,
                "a list asks for its rows and its own chrome",
                "🔴 the reported defect: a list handed more than it holds draws a gap under its last row");

            check(Math.Abs(three.Least - (chrome + ListRooms.LeastRows * row)) < 0.001,
                "and can be squeezed to two rows, chrome included",
                "a floor counted in rows alone did not cover the heading, so the squeezed list showed a title and nothing");

            // ── Shorter than the floor ──────────────────────────────────────────────────────────
            var one = ListRooms.For(1, row, chrome);

            check(Math.Abs(one.Least - one.Whole) < 0.001,
                "a list of one row is never given room for two",
                "a floor that overshoots the content is the gap under the last row, arrived at from the other side");

            // ── Nothing in it ───────────────────────────────────────────────────────────────────
            var empty = ListRooms.For(0, row, chrome);

            check(Math.Abs(empty.Whole - chrome) < 0.001 && Math.Abs(empty.Least - chrome) < 0.001,
                "an empty list asks for its chrome and no row",
                "a list being refilled holds nothing for a frame, and must not size itself on that");

            // ── Measured rather than added up ───────────────────────────────────────────────────
            // 🔴 The two cards of one window do not carry the same chrome — one has a two-line
            // introduction and a verb under its list, the other does not — so a single declared
            // figure for both cut the taller list short or padded the shorter one.
            var measured = ListRooms.Of(three.Whole, 3, row);

            check(Math.Abs(measured.Least - three.Least) < 0.001,
                "a measured whole answers the same floor as a declared one",
                "the chrome is whatever it turned out to be, and the floor never gives any of it up");

            check(Math.Abs(ListRooms.Of(146, 1, row).Least - 146) < 0.001,
                "a list already shorter than the floor is at its floor",
                "taking a row off it would be taking the list");

            // ── Who gets cut first ──────────────────────────────────────────────────────────────
            // 🔴 Whole is also the weight: the two engines shrink between minimum and preferred in
            // proportion to what was asked, so the long list keeps the larger part of a short
            // window. Two lists declaring the same thing is what gave a list of three rows half of
            // a tall window while the eight beside it were still scrolling.
            var eight = ListRooms.For(8, row, chrome);

            check(eight.Whole > three.Whole,
                "the longer list asks for more, so it is cut less",
                "🔴 both asking equally is what gave a list of three half of a tall window");

            // ── What the surface costs ──────────────────────────────────────────────────────────
            const double around = 200;
            var surface = ListRooms.LeastSurface(new List<ListRoom> { three, eight }, around);

            check(Math.Abs(surface - (around + three.Least + eight.Least)) < 0.001,
                "a surface must be able to reach both floors plus what it carries itself",
                "🔴 the window could be shrunk past them, so the second list went under the docked bar and covered the Close button");

            check(ListRooms.LeastSurface(null, around) == around,
                "a surface with no list still carries its own chrome",
                "a screen whose lists are all empty holds sentences, and still has a heading and a bar");
        }
    }
}
