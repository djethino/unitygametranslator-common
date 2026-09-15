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
        /// Divides a MEASURED height between lists, for a layout that cannot be told a ceiling.
        ///
        /// 🔴 **Only one of the two engines needs this, and that is the whole reason it exists.**
        /// Avalonia takes the three facts and arbitrates: a star row weighted by
        /// <see cref="ListRoom.Whole"/>, clamped by MinHeight and MaxHeight, and the spare falls to
        /// a spacer. uGUI has a minimum, a preferred and a flexible height — and **no maximum**: a
        /// flexible child grows without bound, and a preferred height large enough to hold the whole
        /// content makes the panel's own scroll area grow to the sum of them, which is a scrollbar
        /// around the screen on top of the one inside each list. So on that side the answer is
        /// worked out and written as fixed heights.
        ///
        /// 🔴 **And it is the SAME arbitration a star row makes, so the two windows divide alike**
        /// (2026-09-15). Each list is handed its share of the room in proportion to what it holds;
        /// whoever is handed more than they hold stops at their content and what they did not take
        /// is shared again; whoever would fall under their floor is held at it, and the rest is
        /// shared again among those still at the table. It replaced a rule that first served whole
        /// any list fitting in an EQUAL part of the room — which read well and was not continuous:
        /// one pixel of resize across that threshold moved both lists by a whole row, reported as
        /// the window not growing or shrinking "de façon proportionnelle et agréable". A share that
        /// follows a handle must move by the pixel it was given, never by a jump.
        ///
        /// ⚠ **This is NOT the arithmetic that was removed on 2026-09-12.** That one took a GUESS at
        /// the available room, at draw time, and settled the heights once. This takes a height that
        /// has been measured off a laid-out viewport, and is asked again whenever that height
        /// changes. The estimate was the defect, never the division.
        ///
        /// ⚠ A list ALONE is not divided with anybody: it takes the height it is given, whatever its
        /// content comes to. There is nobody to leave the spare room to, and a window enlarged to
        /// show more that then draws small does nothing.
        ///
        /// ⚠ Room under the sum of the floors is not made to fit: everybody is at their floor and the
        /// total overflows, which is what <see cref="LeastSurface"/> exists to keep the surface from
        /// asking. Room over the sum of the wholes is nobody's: each stops at its last row.
        /// </summary>
        public static List<double> Share(IReadOnlyList<ListRoom> lists, double available)
        {
            var heights = new List<double>();
            if (lists == null || lists.Count == 0) return heights;

            var room = Math.Max(0, available);

            if (lists.Count == 1)
            {
                heights.Add(room);
                return heights;
            }

            var settled = new double[lists.Count];
            var done = new bool[lists.Count];
            var left = room;

            bool changed;
            do
            {
                changed = false;

                // A proportional share for everybody still at the table.
                double weight = 0;
                var pending = 0;
                for (var i = 0; i < lists.Count; i++)
                {
                    if (done[i]) continue;
                    weight += Math.Max(0, lists[i].Whole);
                    pending++;
                }
                if (pending == 0) break;

                for (var i = 0; i < lists.Count; i++)
                {
                    if (done[i]) continue;
                    settled[i] = weight > 0 ? left * Math.Max(0, lists[i].Whole) / weight : left / pending;
                }

                // Whoever is handed more than they hold stops at their content, and what they did
                // not take goes back in the pot. Ceilings before floors: freeing room can only lift
                // the others, so nobody is held at a floor they would have cleared.
                for (var i = 0; i < lists.Count; i++)
                {
                    if (done[i] || settled[i] <= lists[i].Whole) continue;
                    settled[i] = Math.Max(0, lists[i].Whole);
                    done[i] = true;
                    left -= settled[i];
                    changed = true;
                }
                if (changed) continue;

                // Whoever would fall under their floor is held at it — a heading over no rows reads
                // as a defect rather than as a small window — and drops out, so the others share
                // what is left rather than what was there.
                for (var i = 0; i < lists.Count; i++)
                {
                    if (done[i] || settled[i] >= lists[i].Least) continue;
                    settled[i] = Math.Max(0, lists[i].Least);
                    done[i] = true;
                    left -= settled[i];
                    changed = true;
                }
            }
            while (changed);

            for (var i = 0; i < lists.Count; i++) heights.Add(settled[i]);

            return heights;
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
