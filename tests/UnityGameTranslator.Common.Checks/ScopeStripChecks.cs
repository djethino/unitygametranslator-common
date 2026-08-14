using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Which form of the scope strip fits, and that it cannot flap between two of them.
    ///
    /// ⚠ The oscillation cases are the point. A tier chosen without a dead band flips on every
    /// recalculation at the threshold, and the flipping is invisible in code review — it only shows
    /// up as a window that flickers while somebody drags its corner.
    /// </summary>
    internal static class ScopeStripChecks
    {
        // A strip that would take 300 in full, 160 in medium, 60 in mini.
        private const double Full = 300, Medium = 160, Mini = 60;

        private static StripTier At(double available, StripTier current) =>
            ScopeStrip.Fits(available, Full, Medium, Mini, current);

        public static void Run(Action<bool, string, string> check)
        {
            check(At(400, StripTier.Full) == StripTier.Full,
                "room to spare keeps every word", "the words are what teach the pictures");

            check(At(200, StripTier.Full) == StripTier.Medium,
                "too tight for all three, the chosen one keeps its words",
                "which position is aimed at survives longest, because it is the answer");

            check(At(80, StripTier.Full) == StripTier.Mini,
                "tighter still, pictures alone", "somebody has seen the full form elsewhere");

            // 🔴 The floor. Returned even when it does not fit, because a screen unable to say
            // where it writes is worse than a cramped badge.
            check(At(10, StripTier.Full) == StripTier.Mini,
                "and mini is the floor, fitting or not",
                "there is no fourth form and showing nothing answers nothing");

            // ── The dead band ─────────────────────────────────────────────────
            check(At(Full, StripTier.Full) == StripTier.Full,
                "exactly enough keeps the form it already has", "no reason to drop");

            check(At(Full, StripTier.Medium) == StripTier.Medium,
                "but exactly enough does NOT win it back",
                "climbing at the same threshold it fell from is what makes it flap");

            check(At(Full + ScopeStrip.ClimbBack, StripTier.Medium) == StripTier.Full,
                "a clear gain does win it back", "the band is a delay, not a wall");

            check(At(Medium, StripTier.Mini) == StripTier.Mini
                  && At(Medium + ScopeStrip.ClimbBack, StripTier.Mini) == StripTier.Medium,
                "and the same holds one tier down", "one rule, applied at every step");

            // ── Which positions keep their words ──────────────────────────────
            check(ScopeStrip.ShowsWords(StripTier.Full, chosen: false),
                "at full even the positions not aimed at are named", "that is what teaches them");

            check(ScopeStrip.ShowsWords(StripTier.Medium, chosen: true)
                  && !ScopeStrip.ShowsWords(StripTier.Medium, chosen: false),
                "at medium only the chosen one is named",
                "the others are recognised from having been read at full");

            check(!ScopeStrip.ShowsWords(StripTier.Mini, chosen: true),
                "at mini nothing is named", "three pictures, and the room the title needed");
        }
    }
}
