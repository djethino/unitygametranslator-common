using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How the end of a scroll gives, and how it comes back.
    ///
    /// 🔴 **Every defect these hold cost a round trip, and none could be seen in a still picture.**
    /// Each position taken alone was defensible in all three: a moment only exists in a sequence,
    /// so every case here is a sequence.
    ///
    /// ⚠ Time is handed in rather than read, which is the whole reason this rule is in the socle: a
    /// frame is a number here, so a wheel turned for half a second is a loop and not a stopwatch —
    /// and the same numbers answer for the site, the Manager and the mod.
    /// </summary>
    internal static class EdgeGiveChecks
    {
        /// <summary>One frame at 60 Hz, which is what every sequence below is counted in.</summary>
        private const double Frame = 1.0 / 60;

        public static void Run(Action<bool, string, string> check)
        {
            HowFarOneNotchLeans(check);
            TheWheelHoldsTheEdgeWhileItTurns(check);
            WhetherItTrembles(check);
            HowTheEdgeComesBack(check);
            WhatAStalledWindowHandsBack(check);
        }

        /// <summary>
        /// ⚠ These read `Want`, not `Offset`: the resistance and the ceiling are about where the
        /// wheel ASKS the edge to be. What is drawn follows it through a second spring.
        /// </summary>
        private static void HowFarOneNotchLeans(Action<bool, string, string> check)
        {
            var give = new EdgeGive();
            check(give.AtRest && give.Offset == 0,
                "a scroller that has not been pushed leans by nothing",
                "a view that leans while nobody is at the end answers a question nobody asked");

            give.Push(1);
            check(Math.Abs(give.Want - EdgeGive.PerNotch) < 0.001,
                "one notch from rest asks for exactly PerNotch",
                "changing this figure silently changes the feel of every scroller at once");

            // 🔴 The resistance, and the reason it is squared: the edge has to firm up under the
            // hand rather than arrive at a second wall.
            var first = give.Want;
            give.Push(1);
            var second = give.Want - first;

            check(second < EdgeGive.PerNotch && second > 0,
                "the second notch is worth less than the first, and still worth something",
                "a give with no resistance opens like a drawer; one that stops dead is a second wall");

            for (var i = 0; i < 500; i++) give.Push(1);
            check(give.Want < EdgeGive.MaxPull && give.Want > EdgeGive.MaxPull * 0.9,
                "five hundred notches approach the ceiling without reaching it",
                "an edge that can be opened indefinitely stops reading as an edge");

            // ⚠ The case a trackpad produces and a detent never does: one event worth far more than
            // a notch. The give is read from where the edge sat BEFORE the push, so the bound is
            // what stops a single large delta spending the whole of it at full give.
            var flick = new EdgeGive();
            flick.Push(9000);
            check(flick.Want <= EdgeGive.MaxPull,
                "one enormous delta cannot push past the ceiling",
                "a free-spinning wheel sends deltas in the hundreds; overshooting is what gets yanked back");

            var upward = new EdgeGive();
            upward.Push(-1);
            check(Math.Abs(upward.Want + EdgeGive.PerNotch) < 0.001,
                "the other end gives exactly as much, the other way",
                "an edge that behaves differently at the top than at the bottom reads as a fault");
        }

        private static void TheWheelHoldsTheEdgeWhileItTurns(Action<bool, string, string> check)
        {
            // 🔴 THE first regression. A wheel spun steadily delivers something every frame; the
            // edge must stay out for as long as that lasts, and never travel back through zero.
            var give = new EdgeGive();
            var lowest = double.MaxValue;
            var reversals = 0;
            var previous = 0.0;

            // ⚠ The first frames are the edge OPENING, which is a climb and not a state. Mixing the
            // two would read the lowest point of a rising curve as a fall.
            for (var frame = 0; frame < 60; frame++)
            {
                give.Push(1);
                give.Advance(Frame);

                if (frame > 15)
                {
                    lowest = Math.Min(lowest, give.Offset);
                    if (give.Offset < previous - 0.001) reversals++;
                }

                previous = give.Offset;
            }

            check(lowest > EdgeGive.PerNotch * 0.9,
                "a wheel turning every frame keeps the edge out the whole time",
                "this is the ping-pong: the edge used to travel back to zero between two notches");

            check(reversals == 0,
                "and it never travels backwards while the wheel is still pushing",
                "a push and a pull netted against each other is what made a free wheel tremble");

            // 🔴 **A real mouse, which is the gesture that was reported.** A detent every three
            // frames — an ordinary steady turn — is the case the first shape failed: each notch
            // restarted a whole out-and-back, so the edge slammed home between two of them.
            var detents = new EdgeGive();
            var floor = double.MaxValue;
            var ceiling = 0.0;

            for (var notch = 0; notch < 40; notch++)
            {
                detents.Push(1);
                detents.Advance(Frame);
                detents.Advance(Frame);
                detents.Advance(Frame);

                if (notch < 12) continue;   // again the steady turn, not the climb into it

                floor = Math.Min(floor, detents.Offset);
                ceiling = Math.Max(ceiling, detents.Offset);
            }

            check(floor > EdgeGive.PerNotch / 2,
                "a detent every three frames never lets the edge slam back home",
                "this is what 'it bounces and bounces until the wheel stops' was");

            // 🔴 **The band is the measurement, not the height.** What makes it read as a held edge
            // rather than a bounce is that the band is narrow; the first shape swung the full
            // height on every notch, so the same reading would have been 0 to 8.
            check(ceiling - floor < EdgeGive.PerNotch / 4 && ceiling < EdgeGive.MaxPull / 2,
                $"and it holds within a couple of pixels (low {floor:0.0}, high {ceiling:0.0})",
                "a wide band IS the ping-pong; a narrow one is an edge being held open");

            // ⚠ A notched wheel is the other half of the same line, and it needs no constant of its
            // own: the gaps between detents are longer than a frame, so the spring gets its turn.
            var notched = new EdgeGive();
            notched.Push(1);
            for (var frame = 0; frame < 20; frame++) notched.Advance(Frame);

            check(notched.Offset < EdgeGive.PerNotch,
                "a single notch is on its way back a third of a second later",
                "waiting for the wheel to be declared stopped is what made the view look stuck");
        }

        /// <summary>
        /// 🔴 **Not "does it bounce" but "is it smooth", and they are different questions.**
        ///
        /// Reported after the ping-pong was fixed: *"it still trembles, less, but it is not smooth
        /// — and the site does it too"*. A real wheel does not deliver one notch every N frames on
        /// the dot; the gaps are uneven. If pushing and springing are two regimes that take turns,
        /// the edge climbs on the frames a notch lands and falls on the frames none does — a
        /// sawtooth measured at 6.9px on an edge that only opens 8.
        /// </summary>
        private static void WhetherItTrembles(Action<bool, string, string> check)
        {
            // The gaps a hand actually produces. Fixed rather than random so a failure is the same
            // failure tomorrow.
            var gaps = new[] { 2, 1, 3, 2, 1, 2, 4, 1, 2, 3, 1, 2, 2, 3, 1, 2, 1, 3, 2, 2 };

            var give = new EdgeGive();
            var worstFall = 0.0;
            var previous = 0.0;
            var falling = 0.0;
            var first = true;

            foreach (var gap in gaps)
            {
                give.Push(1);
                for (var f = 0; f < gap; f++)
                {
                    give.Advance(Frame);

                    // A run of falls counts once and at full size.
                    if (!first)
                    {
                        if (give.Offset < previous) falling += previous - give.Offset;
                        else { worstFall = Math.Max(worstFall, falling); falling = 0; }
                    }

                    previous = give.Offset;
                    first = false;
                }
            }

            worstFall = Math.Max(worstFall, falling);

            check(worstFall < EdgeGive.PerNotch / 5,
                $"an uneven wheel does not saw the edge up and down (worst fall {worstFall:0.0}px)",
                "pushing and springing taking turns is a tremble, however small each turn is");

            // 🔴 **And the second kind, which the first measurement cannot see.** Reported after the
            // filter went in: *"it still trembles a little, very fast"*. Not a fall — a jolt. What
            // the eye reads as one is the change in SPEED: an edge moving at a steady rate looks
            // calm however fast it goes.
            var steady = new EdgeGive();
            var previousStep = 0.0;
            var at = 0.0;
            var worstJolt = 0.0;

            for (var notch = 0; notch < 40; notch++)
            {
                steady.Push(1);
                for (var f = 0; f < 3; f++)
                {
                    steady.Advance(Frame);
                    var step = steady.Offset - at;
                    at = steady.Offset;

                    if (notch >= 12) worstJolt = Math.Max(worstJolt, Math.Abs(step - previousStep));
                    previousStep = step;
                }
            }

            check(worstJolt < EdgeGive.PerNotch / 20,
                $"and it does not jolt on each notch either ({worstJolt:0.00}px/frame)",
                "a step in the asked-for edge comes through the filter as a jolt at the wheel's rate");

            // 🔴 **The condition the two above quietly cheat on: frames are not even.** A window
            // hands back 12ms, then 20, then 14 — scheduling, not frame rate. This is the one thing
            // a simulation on a perfect clock can never show.
            var jitter = new[] { 0.012, 0.020, 0.014, 0.018, 0.011, 0.023, 0.016, 0.013, 0.021, 0.015 };

            var uneven = new EdgeGive();
            var unevenStep = 0.0;
            var unevenAt = 0.0;
            var unevenJolt = 0.0;
            var tick = 0;

            for (var notch = 0; notch < 40; notch++)
            {
                uneven.Push(1);
                for (var f = 0; f < 3; f++)
                {
                    uneven.Advance(jitter[tick++ % jitter.Length]);

                    // 🔴 Measured per FRAME, not per second. A screen shows its frames on its own
                    // even beat; the uneven number above is when the callback happened to RUN, which
                    // nobody sees. Dividing by it would measure the clock rather than the motion.
                    var step = uneven.Offset - unevenAt;
                    unevenAt = uneven.Offset;

                    if (notch >= 12) unevenJolt = Math.Max(unevenJolt, Math.Abs(step - unevenStep));
                    unevenStep = step;
                }
            }

            // 🔴 Compared with the perfect clock rather than against a number of its own, because
            // that is the actual claim: uneven frames must not make the motion any less smooth than
            // even ones. An absolute bar loose enough to be safe would pass either way.
            check(unevenJolt <= worstJolt * 1.15,
                $"and uneven frames do not shake it ({unevenJolt:0.00} against {worstJolt:0.00}px/frame)",
                "scheduling jitter turns a smooth spring into an uneven one, and that is the fast tremble");
        }

        private static void HowTheEdgeComesBack(Action<bool, string, string> check)
        {
            var give = new EdgeGive();
            give.Push(1);

            // The frame that consumes the push. From the next one the spring has it.
            give.Advance(Frame);
            var held = give.Want;

            var frames = 0;
            var crossings = 0;
            var sign = Math.Sign(give.Offset);

            while (give.Advance(Frame))
            {
                if (Math.Sign(give.Offset) != 0 && Math.Sign(give.Offset) != sign) crossings++;
                sign = Math.Sign(give.Offset);
                if (++frames > 600) break;
            }

            check(Math.Abs(held - EdgeGive.PerNotch) < 0.001,
                "the frame that takes the push leaves the asked-for edge alone",
                "the wheel decides where the edge sits for that frame; the spring may not net against it");

            check(crossings == 0,
                "it comes back without ever overshooting",
                "critically damped on purpose: a wobble reads as a bug, a single soft return as a material");

            // ⚠ **A ceiling, not a target.** The edge is meant to read as damped — the run-out of a
            // free-spinning wheel — so a long settle is what was asked for rather than a cost to be
            // tuned away. What this refuses is an edge that HANGS: past a second, somebody has
            // started scrolling again before it closed.
            check(frames > 0 && frames * Frame < 0.8,
                $"and it is home inside a second (took {frames * Frame:0.00}s)",
                "an edge that hangs open is the shape that made a page feel stuck");

            check(give.AtRest && give.Offset == 0,
                "and it stops exactly at zero rather than near it",
                "a sub-pixel left behind keeps a transform alive on every scroller for ever");
        }

        private static void WhatAStalledWindowHandsBack(Action<bool, string, string> check)
        {
            // ⚠ A window that was not drawing — dragged, occluded, the machine asleep — hands back a
            // step of seconds. The integrator is explicit in the stiffness term, so an unclamped
            // step of that size does not merely look wrong, it diverges.
            var give = new EdgeGive();
            give.Push(1);
            give.Advance(Frame);
            give.Advance(4.0);

            check(!double.IsNaN(give.Offset) && !double.IsInfinity(give.Offset)
                  && Math.Abs(give.Offset) <= EdgeGive.MaxPull,
                "a four-second step leaves the edge somewhere real",
                "an unclamped step through this spring returns NaN, and a NaN transform kills the panel");

            var settled = new EdgeGive();
            settled.Push(1);
            settled.Advance(Frame);
            while (settled.Advance(1.0)) { }

            check(settled.AtRest,
                "and a run of long steps still arrives",
                "a spring that cannot settle keeps a frame callback alive for the life of the window");
        }
    }
}
