namespace UnityGameTranslator.Common
{
    /// <summary>Whether a translation has ever left this machine.</summary>
    public enum Publication
    {
        /// <summary>It exists here and nothing of its lineage is on the site.</summary>
        NeverPublished,

        /// <summary>It exists here and it is published — by the account reading this.</summary>
        Published,

        /// <summary>
        /// It exists here, its lineage IS published, and somebody else leads it. Nothing of this
        /// account's has ever been sent.
        ///
        /// 🔴 **The third act, and the one that used to be invisible.** Publishing something never
        /// published CREATES a lineage; publishing something already yours UPDATES it; publishing
        /// into somebody else's lineage makes a BRANCH for its Main to review — three acts behind
        /// one button, and only two had a state. Somebody holding a community translation was told
        /// either "Never published" (the Manager, so publishing read as creating their own) or
        /// "Published" (the mod, so it read as updating a file that is not theirs to update).
        ///
        /// ⚠ **Not a Branch, and that distinction is the whole point**: nothing has been sent yet.
        /// One becomes a Branch by uploading. The mod named this state NOT YOURS on 2026-08-14 for
        /// exactly that reason, and the word never reached the strip nor the Manager.
        ///
        /// ⚠ Applies to somebody with no account too. They can hold a community translation and
        /// diverge from it exactly like a Branch would, and they are the person most likely to
        /// press a button expecting it to create something of their own.
        /// </summary>
        NotYours,

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
        /// <param name="onTheSite">
        /// Something of this LINEAGE is published — by anybody, not necessarily by the reader.
        ///
        /// ⚠ The Manager passed "does this account hold a row in it" here, which is a different
        /// question and false for the ordinary case of a downloaded translation: it announced
        /// "Never published" over somebody else's published work.
        /// </param>
        /// <param name="yours">
        /// Whether the published one is this account's. False when somebody else leads the lineage
        /// — see <see cref="Publication.NotYours"/>.
        ///
        /// ⚠ Null means the caller did not ask, and keeps the older behaviour of treating a
        /// published lineage as the reader's own. Anything that CAN tell should say so: it is the
        /// difference between a button that updates and a button that contributes.
        /// </param>
        public static Publication Of(bool hereOnDisk, bool onTheSite, bool? yours = null)
        {
            if (!hereOnDisk) return Publication.NotDownloaded;
            if (!onTheSite) return Publication.NeverPublished;

            return yours == false ? Publication.NotYours : Publication.Published;
        }

        /// <summary>The tag itself, identical in every product.</summary>
        public static string Name(Publication publication)
        {
            switch (publication)
            {
                case Publication.Published: return "Published";
                case Publication.NotYours: return "Not yours";
                case Publication.NotDownloaded: return "Not downloaded";
                default: return "Never published";
            }
        }

        /// <summary>
        /// What follows from it, for whoever is about to act.
        /// </summary>
        /// <param name="owner">
        /// Who leads the lineage, for <see cref="Publication.NotYours"/>. Named when known, because
        /// "somebody else" leaves a reader with nowhere to look; the sentence still works without
        /// it, since an account can be gone or a server too old to say.
        /// </param>
        public static string Effect(Publication publication, string? owner = null)
        {
            switch (publication)
            {
                case Publication.Published:
                    return "This translation is on the site. Publishing again updates it.";

                case Publication.NotYours:
                    string who = string.IsNullOrWhiteSpace(owner)
                        ? "Somebody else leads this translation."
                        : People.Mention(owner) + " leads this translation.";

                    // ⚠ Says the ACT, not the ownership. Somebody reading this is about to press a
                    // button, and what they need is that it will not create a translation of their
                    // own — it will offer their work to whoever leads this one.
                    return who + " Nothing of yours is on the site. Sending your version makes it "
                         + "a contribution for them to review.";

                case Publication.NotDownloaded:
                    return "Published by somebody, and nothing for this game is on this machine yet.";

                default:
                    return "This exists on this machine only. Publishing it would put it on the "
                         + "site under your name for the first time.";
            }
        }
    }
}
