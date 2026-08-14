namespace UnityGameTranslator.Common
{
    /// <summary>Whether a translation has ever left this machine.</summary>
    public enum Publication
    {
        /// <summary>It exists here and nothing of its lineage is on the site.</summary>
        NeverPublished,

        /// <summary>It exists here and it is published.</summary>
        Published,

        /// <summary>It is published and there is nothing here.</summary>
        NotDownloaded,
    }

    /// <summary>
    /// The one thing somebody needs to know before doing anything with a translation: has this
    /// ever been published, or does it only exist on this machine?
    ///
    /// ⚠ **It decides what the actions mean.** Publishing something never published CREATES a
    /// lineage; publishing something already published UPDATES it — two different acts behind one
    /// button. Nothing else on a card answers that, and guessing it from a sync verdict means
    /// reading a sentence about something else.
    ///
    /// ⚠ **"Published" here is the same word as <see cref="EditScope"/>'s Server side, and that is
    /// deliberate.** An earlier version invented a second vocabulary to keep the two apart, which
    /// was the wrong fix: the project's own rule is one act, one term. What keeps them apart is
    /// their FORM — this is a flat tag that states a fact, that one is a three-mark switch that
    /// aims an action — and their grammar: this says what a translation IS, that one says where a
    /// save GOES.
    /// </summary>
    public static class Publications
    {
        /// <param name="hereOnDisk">A translation file for this lineage sits on this machine.</param>
        /// <param name="onTheSite">Something of this lineage is published.</param>
        public static Publication Of(bool hereOnDisk, bool onTheSite)
        {
            if (!hereOnDisk) return Publication.NotDownloaded;
            return onTheSite ? Publication.Published : Publication.NeverPublished;
        }

        /// <summary>The tag itself, identical in every product.</summary>
        public static string Name(Publication publication)
        {
            switch (publication)
            {
                case Publication.Published: return "Published";
                case Publication.NotDownloaded: return "Not downloaded";
                default: return "Never published";
            }
        }

        /// <summary>What follows from it, for whoever is about to act.</summary>
        public static string Effect(Publication publication)
        {
            switch (publication)
            {
                case Publication.Published:
                    return "This translation is on the site. Publishing again updates it.";
                case Publication.NotDownloaded:
                    return "Published by somebody, and nothing for this game is on this machine yet.";
                default:
                    return "This exists on this machine only. Publishing it would put it on the "
                         + "site under your name for the first time.";
            }
        }
    }
}
