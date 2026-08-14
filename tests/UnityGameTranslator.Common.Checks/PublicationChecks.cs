using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Has this translation ever been published?
    ///
    /// ⚠ The stake is that one button means two acts: publishing something never published CREATES
    /// a lineage under your name, publishing something already published UPDATES it. Getting the
    /// tag wrong tells somebody they are about to do the other one.
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
