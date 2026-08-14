namespace UnityGameTranslator.Common
{
    /// <summary>How much of the scope strip there is room for.</summary>
    public enum StripTier
    {
        /// <summary>Every position with its picture and its words.</summary>
        Full,

        /// <summary>Pictures only, except the chosen position which keeps its words.</summary>
        Medium,

        /// <summary>Three pictures.</summary>
        Mini,
    }

    /// <summary>
    /// Which form of the strip fits beside a title.
    ///
    /// 🔴 **The strip is never prioritary over the title.** It takes what is left AFTER the title
    /// has what it needs, and gives up its words rather than push the title onto a second line or
    /// out of sight. That single rule is what makes the degradation automatic: there is no
    /// arbitration to make, only an order.
    ///
    /// ⚠ **Chosen by measuring, not by noticing a wrap.** Building, letting a layout pass happen,
    /// seeing two lines and rebuilding costs a flicker and can oscillate — the taller form makes
    /// the content wider, which forces the smaller form, which frees the room for the taller one.
    /// All three widths are known before anything is built, so the choice is made once.
    ///
    /// ⚠ **And it answers the real question, which is not the window's width.** Room runs out
    /// because a window is narrow, because a title is long, because a font is bigger, or because a
    /// translated label is twice the length. Measuring what is left covers every one of those
    /// without naming any of them.
    /// </summary>
    public static class ScopeStrip
    {
        /// <summary>
        /// How much MORE room a tier needs to be climbed back to than it needed to be left.
        ///
        /// 🔴 Without it the mechanism is unstable by construction, not by accident: a window
        /// resting exactly on a threshold flips between two forms on every recalculation, and a
        /// form that changes the content's width can drive the flip itself. The dead band means a
        /// size that just lost its words has to gain a visible amount before getting them back.
        /// </summary>
        public const double ClimbBack = 20;

        /// <param name="available">Room left for the strip once the title has what it needs.</param>
        /// <param name="full">Width the full form would take.</param>
        /// <param name="medium">Width of pictures only, the chosen one keeping its words.</param>
        /// <param name="mini">Width of three pictures.</param>
        /// <param name="current">
        /// What is on screen now, so climbing back costs <see cref="ClimbBack"/> more than falling
        /// did. Pass <see cref="StripTier.Mini"/> when building for the first time: the strip then
        /// only grows into room it certainly has, rather than starting wide and collapsing.
        /// </param>
        public static StripTier Fits(double available, double full, double medium, double mini,
                                     StripTier current)
        {
            // Climbing costs more than falling. Whichever tier is on screen keeps its place until
            // the room clearly justifies the next one up.
            if (available >= full + (current == StripTier.Full ? 0 : ClimbBack))
                return StripTier.Full;

            if (available >= medium + (current == StripTier.Medium || current == StripTier.Full
                                           ? 0 : ClimbBack))
                return StripTier.Medium;

            // ⚠ Mini is the floor, returned even when it does not fit. There is no fourth form, and
            // showing nothing would leave a screen unable to say where it writes — which is the one
            // thing this control exists for.
            return StripTier.Mini;
        }

        /// <summary>Does this position keep its words at this tier?</summary>
        public static bool ShowsWords(StripTier tier, bool chosen)
        {
            if (tier == StripTier.Full) return true;
            if (tier == StripTier.Medium) return chosen;

            return false;
        }
    }
}
