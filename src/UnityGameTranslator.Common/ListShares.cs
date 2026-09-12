using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>How much room one list asks for, and whether it takes any that is left over.</summary>
    public struct ListShare
    {
        /// <summary>
        /// The height it asks for. Never more than it has content to fill, and never less than one
        /// row — see <see cref="ListShares.Floor"/>.
        /// </summary>
        public double Preferred;

        /// <summary>
        /// Its share of whatever room remains — zero for a list that already shows everything.
        ///
        /// ⚠ Zero is the important value. A list handed room it has nothing to put in draws a gap
        /// under its last row, which is what "elle grandit en montrant du vide plutôt que de se
        /// bloquer" was.
        /// </summary>
        public double Weight;
    }

    /// <summary>
    /// How several lists sharing one panel divide its height.
    ///
    /// 🔴 **Three cases, and getting any of them wrong is visible immediately.**
    ///
    ///   - **Alone.** It takes everything. There is nobody to give the spare room to, and a panel
    ///     enlarged to show more of one list that then draws it small is a window doing nothing.
    ///   - **Together, and everything fits.** Each asks for exactly its own content and no more;
    ///     what is left goes below them both. A list that stops at its last row is finished, and
    ///     says so.
    ///   - **Together, and it does not fit.** They divide the room in proportion to what they hold
    ///     — but a list is never given more than it can fill, so a short one beside a long one
    ///     takes its few rows and leaves the rest to the long one.
    ///
    /// ⚠ **Two lists of equal weight was the first shape and it was wrong twice over**: the mod
    /// gave both `9999`, the Manager gave both `*`, so a list of three rows took half a tall window
    /// and drew a gap while the list of eight beside it was still scrolling.
    ///
    /// ⚠ Rendering stays per product — uGUI reads preferred and flexible heights on a LayoutElement,
    /// Avalonia reads star and auto rows — but which list deserves what is one question, asked of
    /// the same two lists in two windows.
    /// </summary>
    public static class ListShares
    {
        /// <summary>
        /// Divides <paramref name="available"/> between lists whose whole contents would need
        /// <paramref name="natural"/> pixels each.
        ///
        /// <paramref name="floor"/> is the least any of them may be squeezed to, in the same units
        /// as <paramref name="natural"/>. Zero asks for no floor.
        ///
        /// 🔴 **Without a floor a list can be squeezed to nothing.** Shrink the window with a short
        /// list above a long one and the proportional share hands the short one a few pixels: its
        /// heading survives, its rows do not, and a list that is there while showing nothing reads
        /// as a bug rather than as a small window.
        ///
        /// ⚠ **In pixels, and the caller works it out** — it was a number of rows here, and that
        /// was wrong: what a list asks for includes its heading and its padding, so one and a half
        /// rows did not even cover the chrome and the floor bought no visible row at all. A row is
        /// not the same height in a game's overlay as in a desktop window, and neither is what
        /// surrounds it.
        ///
        /// ⚠ A list with nothing in it is not a list here: pass only the ones that have rows. An
        /// empty one is a sentence, not a scroll area, and sizing it as one is how a panel ends up
        /// holding room for something that is not there.
        /// </summary>
        public static List<ListShare> Split(IReadOnlyList<double> natural, double available,
                                            double floor = 0)
        {
            var shares = new List<ListShare>();
            if (natural == null || natural.Count == 0) return shares;

            // Alone: take the room, whatever the content comes to.
            if (natural.Count == 1)
            {
                shares.Add(new ListShare { Preferred = Math.Max(0, natural[0]), Weight = 1 });
                return shares;
            }

            double total = 0;
            foreach (var one in natural) total += Math.Max(0, one);

            // Everything fits: each asks for its own content, nobody takes the remainder.
            if (total <= available || available <= 0)
            {
                foreach (var one in natural)
                    shares.Add(new ListShare { Preferred = Math.Max(0, one), Weight = 0 });

                return shares;
            }

            // 🔴 **It does not fit, so the ones that nearly do are served first.** Dividing the room
            // in one pass, in proportion to what each holds, takes a slice off a list that was
            // three pixels from fitting — and a list cut just short of its content scrolls for one
            // row, which is the most irritating amount there is. So: anyone whose whole content is
            // no larger than their share gets exactly it and leaves the table; what they did not
            // take is shared again between those still over. Repeated until only the over remain.
            var wants = new double[natural.Count];
            var settled = new bool[natural.Count];

            for (var i = 0; i < natural.Count; i++) wants[i] = Math.Max(0, natural[i]);

            var room = available;
            var asking = total;
            var pending = wants.Length;
            bool served;

            do
            {
                served = false;

                for (var i = 0; i < wants.Length; i++)
                {
                    if (settled[i] || pending <= 0) continue;

                    // 🔴 An EQUAL part of what is left, not a proportional one — and that
                    // distinction decides who gets cut. Judged proportionally, a short list is
                    // refused its content precisely because it is short: three rows beside eight
                    // were held to three elevenths of the room and missed fitting by four pixels,
                    // so BOTH lists scrolled where one could have been complete. Judged equally, it
                    // takes its three rows, and the long one gets everything it left.
                    var portion = room / pending;
                    if (wants[i] > portion) continue;

                    settled[i] = true;
                    room -= wants[i];
                    asking -= wants[i];
                    pending--;
                    served = true;
                }
            }
            while (served);

            for (var i = 0; i < wants.Length; i++)
            {
                if (settled[i])
                {
                    shares.Add(new ListShare { Preferred = wants[i], Weight = 0 });
                    continue;
                }

                // Still over: it takes its share of what the others left, and the spare room too.
                var portion = asking > 0 ? room * (wants[i] / asking) : room;

                // ⚠ Never squeezed below the floor, whatever the arithmetic says — and never above
                // its own content either, or the floor becomes the gap under the last row.
                var least = Math.Min(floor, wants[i]);

                shares.Add(new ListShare { Preferred = Math.Max(portion, least), Weight = wants[i] });
            }

            return shares;
        }
    }
}
