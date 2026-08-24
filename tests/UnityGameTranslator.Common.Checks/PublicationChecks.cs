using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Has this translation ever been published?
    ///
    /// ⚠ The stake is that one button means THREE acts: publishing something never published
    /// CREATES a lineage under your name, publishing something already yours UPDATES it, and
    /// publishing into somebody else's lineage makes a BRANCH for its Main to review. Getting the
    /// tag wrong tells somebody they are about to do one of the other two.
    /// </summary>
    internal static class PublicationChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(Publications.Of(hereOnDisk: true, onTheSite: false) == Publication.NeverPublished,
                "a file here and nothing on the site has never been published",
                "publishing it would be a first, under your name");

            check(Publications.Of(hereOnDisk: true, onTheSite: true) == Publication.Published,
                "a file here and something on the site is published",
                "publishing it again updates what is already there");

            check(Publications.Of(hereOnDisk: false, onTheSite: true) == Publication.NotDownloaded,
                "something on the site and nothing here is not downloaded",
                "the figures beside it describe somebody else's file");

            // ── Holding somebody else's lineage ───────────────────────────────
            //
            // 🔴 The ordinary way a player starts: download a community translation and go on
            // adding lines. All three products got it wrong, in two different directions — the
            // Manager announced "Never published" over work that IS published, the mod announced
            // "Published" over work that is not the reader's to update.
            check(Publications.Of(hereOnDisk: true, onTheSite: true, yours: false) == Publication.NotYours,
                "a published lineage somebody else leads is not yours",
                "publishing there contributes; it neither creates nor updates");

            check(Publications.Of(hereOnDisk: true, onTheSite: true, yours: true) == Publication.Published,
                "and the same lineage led by this account is published",
                "one axis, two readings, decided by who holds the Main");

            check(Publications.Of(hereOnDisk: true, onTheSite: false, yours: false) == Publication.NeverPublished,
                "nothing published stays never published whoever asks",
                "there is no Main to belong to yet, so the question does not arise");

            // ⚠ Unknown keeps the older answer rather than guessing at a fourth state. Every caller
            // that CAN tell now does; one that cannot must not invent an owner.
            check(Publications.Of(hereOnDisk: true, onTheSite: true) == Publication.Published,
                "a caller that does not ask still gets the old answer",
                "a new state must not appear behind callers that were never updated");

            check(Publications.Name(Publication.NotYours) == "Not yours",
                "not yours is the word the mod already chose for this state",
                "picked on 2026-08-14 precisely because it is NOT a branch: nothing has been sent");

            // The tip is where the act lives, and naming the Main is what gives somebody a way out.
            check(Publications.Effect(Publication.NotYours, "djeitinho").Contains("@djeitinho"),
                "and the tip names who leads it when we know",
                "'somebody else' leaves a reader with nowhere to look");

            check(Publications.Effect(Publication.NotYours).Length > 0
                  && !Publications.Effect(Publication.NotYours).Contains("@"),
                "and still says something when we do not",
                "an account can be gone, or a server too old to answer");

            check(Publications.Effect(Publication.NotYours, "djeitinho").Contains("contribution"),
                "the tip says the ACT, not only the ownership",
                "somebody is about to press a button that will not create what they expect");

            // ⚠ Pinned as literals. These three words appear on every card in three products, and
            // a rename here would silently retitle all of them.
            check(Publications.Name(Publication.NeverPublished) == "Never published",
                "never published says exactly that", "not 'local', which describes a place rather than a history");
            check(Publications.Name(Publication.Published) == "Published",
                "published borrows the switch's word ON PURPOSE",
                "one act, one term — the two are told apart by form, not by inventing a synonym");
            check(Publications.Name(Publication.NotDownloaded) == "Not downloaded",
                "and not downloaded is about this machine", "the only case with nothing here to describe");

            foreach (Publication publication in Enum.GetValues(typeof(Publication)))
            {
                check(Publications.Effect(publication).Length > 0,
                    $"{publication} says what follows from it",
                    "a tag that states a fact without its consequence is trivia");
            }
        }
    }
}
