namespace UnityGameTranslator.Common
{
    /// <summary>Where a set of figures about a translation was measured.</summary>
    public enum Origin
    {
        /// <summary>The file on this machine, counted from disk.</summary>
        YourFile,

        /// <summary>The published version, and a copy of it exists on this machine.</summary>
        PublishedWithCopy,

        /// <summary>The published version, with nothing on this machine.</summary>
        PublishedOnly,
    }

    /// <summary>
    /// Whose figures are being shown — the file on this machine, or the published version.
    ///
    /// ⚠ **Written because the SAME quality bar is drawn over two different things.** The manager
    /// puts it over the file on disk on a game's page and over a published translation in the
    /// community list; the site puts it over published translations. Nothing on the bar itself says
    /// which, and the two diverge the moment somebody plays — borrowing published figures to
    /// describe a local file would report a stranger's work as yours.
    ///
    /// 🔴 **This is NOT <see cref="EditScope"/>, and it must never look like it.** That switch says
    /// where a save will LAND — a destination, chosen, acted on. This says where a measurement CAME
    /// FROM — an origin, observed, never chosen. Two controls with the same shape and the same words
    /// for two different questions is how somebody publishes thinking they are counting. Hence:
    ///
    /// - different words — "your file" against "published", never the switch's "On this machine";
    /// - no icons, where the switch is built on three;
    /// - one flat tag, where the switch is three segments;
    /// - nothing to click, ever.
    ///
    /// ⚠ **"Not downloaded" is a state of the translation, not of the figures.** There is always
    /// something being measured, so it cannot label a bar on its own — it only means anything
    /// alongside "published", which is why it is a shade of <see cref="Origin.PublishedOnly"/>
    /// rather than a third thing.
    /// </summary>
    public static class Provenance
    {
        /// <summary>
        /// Which of the three a set of figures is.
        /// </summary>
        /// <param name="countedFromDisk">
        /// The figures were counted from the file on this machine. False when they come from a
        /// published translation, whether or not a copy of it also sits on this machine.
        /// </param>
        /// <param name="haveCopyHere">A file for this lineage exists on this machine.</param>
        public static Origin Of(bool countedFromDisk, bool haveCopyHere)
        {
            if (countedFromDisk) return Origin.YourFile;
            return haveCopyHere ? Origin.PublishedWithCopy : Origin.PublishedOnly;
        }

        /// <summary>The tag itself, identical in every product.</summary>
        public static string Name(Origin origin)
        {
            switch (origin)
            {
                case Origin.PublishedWithCopy: return "Published — you have a copy";
                case Origin.PublishedOnly: return "Published — not downloaded";
                default: return "Your file";
            }
        }

        /// <summary>
        /// What that means for the numbers beside it, for whoever wonders why two bars for the same
        /// game disagree.
        /// </summary>
        public static string Effect(Origin origin)
        {
            switch (origin)
            {
                case Origin.PublishedWithCopy:
                    return "Counted from the published version. Your own copy has moved on wherever "
                         + "you have played since.";
                case Origin.PublishedOnly:
                    return "Counted from the published version. Nothing for this game is on this "
                         + "machine yet.";
                default:
                    return "Counted from the file on this machine, not from anything published.";
            }
        }
    }
}
