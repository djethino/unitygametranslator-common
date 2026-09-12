using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What one scrolling list asks of the surface it is drawn on: everything it holds, and the
    /// least it can be shown in.
    /// </summary>
    public struct ListRoom
    {
        /// <summary>
        /// Everything it holds — its rows and its own heading, padding and verb. **A ceiling, not a
        /// wish**: a list given more than this draws a gap under its last row, which is the whole
        /// complaint ("la liste au dessus grandit en montrant du vide plutôt que de se bloquer").
        /// </summary>
        public double Whole;

        /// <summary>
        /// The smallest box still worth showing — <see cref="ListRooms.LeastRows"/> rows and the
        /// chrome around them, or <see cref="Whole"/> when the list is shorter than that.
        ///
        /// 🔴 **A floor, and it is what a window's own minimum is built from.** Without one, a
        /// proportional share hands a short list a few pixels: its heading survives, its rows do
        /// not, and a list that is there while showing nothing reads as a defect rather than as a
        /// small window.
        /// </summary>
        public double Least;
    }

    /// <summary>
    /// The three facts a list states about itself, so that the layout — and not us — divides the
    /// height.
    ///
    /// 🔴 **This replaced an arithmetic that did the dividing itself, and that was the mistake**
    /// (2026-09-12, on "je pense qu'il faut que tu reréfléchisse le problème dans son ensemble […]
    /// là tu patch sur patch on dirait"). Three shapes were tried in turn — a height written in the
    /// panel, then a share of the room in proportion to the rows, then that share computed from an
    /// ESTIMATE of how much room there was. Each patched the one before, and the last could not
    /// work: it settled pixel heights ONCE, at draw time, from a guess, inside layout engines that
    /// know the real sizes and re-run on every resize. A stretched window showed a gap under one
    /// list while the other was still incomplete.
    ///
    /// So nothing here divides anything. A list says:
    ///
    ///   <see cref="ListRoom.Whole"/>  — its content, and never more
    ///   <see cref="ListRoom.Least"/>  — never squeezed below this
    ///   its weight, when there is not enough for everyone — which IS <see cref="ListRoom.Whole"/>
    ///
    /// and both engines already arbitrate exactly that: uGUI reads a preferred and a minimum height
    /// off a LayoutElement and shrinks between them; Avalonia reads a weighted star row clamped by
    /// MinHeight and MaxHeight and does the same. **Spare room is nobody's**: neither takes a
    /// flexible share, so what is left over falls to a spacer below them all.
    ///
    /// ⚠ Rendering stays per product, and so does the arbitration. What is shared is only the three
    /// numbers, because they are the same promise in two windows over the same folder — and because
    /// <see cref="LeastSurface"/> is the size that promise costs, which a window has to declare or
    /// break.
    /// </summary>
    public static class ListRooms
    {
        /// <summary>
        /// How many rows a list must still be able to show when the surface is at its smallest.
        ///
        /// ⚠ Two, not one: one row shows that a list exists, two show that it is a list — and the
        /// second one is what says whether the first is the newest or the oldest.
        /// </summary>
        public const int LeastRows = 2;

        /// <summary>
        /// What a list of <paramref name="rows"/> rows asks for.
        /// </summary>
        /// <param name="rowSpace">One row and the gap under it, in the units this surface uses.</param>
        /// <param name="chrome">
        /// What the list carries besides its rows — a heading, the padding around it, a verb under
        /// it. **In pixels, and the caller works it out**: a row is not the same height in a game's
        /// overlay as in a desktop window, and neither is what surrounds it. It was expressed in
        /// rows once, and a floor of "one and a half rows" did not even cover the chrome, so the
        /// squeezed list showed its title and nothing else.
        /// </param>
        public static ListRoom For(int rows, double rowSpace, double chrome = 0)
        {
            var around = Math.Max(0, chrome);
            var each = Math.Max(0, rowSpace);

            // ⚠ A list with nothing in it is a sentence, not a scroll area. It is answered rather
            // than refused — a list being refilled holds nothing for a frame — but it asks for its
            // chrome and not one pixel of row.
            if (rows <= 0) return new ListRoom { Whole = around, Least = around };

            return Of(around + each * rows, rows, each);
        }

        /// <summary>
        /// The same three facts for a caller that can MEASURE what the whole list comes to instead
        /// of adding it up.
        ///
        /// 🔴 **Measured beats declared, wherever it is available.** The chrome around a list — a
        /// heading, an introduction that wraps onto two lines at one window width and one at
        /// another, a verb under it — is not one number: the two cards of the backups window differ
        /// by thirty pixels, and a single figure for both either cuts the taller list short (it
        /// scrolls with room to spare beside it) or hands the shorter one a band of empty card. A
        /// desktop toolkit will answer what a control comes to for the asking; a game overlay will
        /// not, which is why <see cref="For"/> still exists beside this.
        /// </summary>
        /// <param name="whole">What the list and everything around it comes to, measured.</param>
        /// <param name="rows">How many rows are in it.</param>
        /// <param name="rowSpace">One row and the gap under it — measured too, off the first row.</param>
        public static ListRoom Of(double whole, int rows, double rowSpace)
        {
            var total = Math.Max(0, whole);
            var each = Math.Max(0, rowSpace);

            // Rows the floor gives up, and nothing else: what is left is the chrome, which stays
            // whatever it happens to be. Never below zero, and never above its own content — a
            // floor larger than what the list holds IS the gap under the last row, arrived at from
            // the other side.
            var given = each * Math.Max(0, rows - LeastRows);

            return new ListRoom { Whole = total, Least = Math.Max(0, total - given) };
        }

        /// <summary>
        /// The height a surface must be able to reach for every one of these lists to keep showing
        /// <see cref="LeastRows"/> rows — that is, the minimum size the window or panel holding
        /// them has to declare.
        ///
        /// 🔴 **A surface that can be made smaller than this breaks its own promise, silently.**
        /// Measured on the Manager: shrinking the window kept squeezing past the floors, so the
        /// second list went under the docked bar and covered the Close button — the way out of the
        /// screen, hidden by the screen. The floors were right; nothing had worked out what they
        /// cost together.
        /// </summary>
        /// <param name="around">
        /// What the surface carries besides the lists — a heading, its margins, a button bar.
        /// </param>
        public static double LeastSurface(IReadOnlyList<ListRoom> lists, double around = 0)
        {
            var total = Math.Max(0, around);
            if (lists == null) return total;

            for (var i = 0; i < lists.Count; i++) total += Math.Max(0, lists[i].Least);

            return total;
        }
    }
}
