using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>How loudly a badge should read. Each product maps these to its own palette.</summary>
    public enum BadgeTone
    {
        /// <summary>The ordinary case. Grey, quiet, present.</summary>
        Plain,

        /// <summary>A detail worth having, not worth looking at. Dimmer than Plain.</summary>
        Quiet,

        /// <summary>Finished, settled, nothing to do.</summary>
        Good,

        /// <summary>Neither good nor bad — something happened elsewhere.</summary>
        Notice,

        /// <summary>Worth catching an eye. Not a fault.</summary>
        Attention,

        /// <summary>Something is actually wrong.</summary>
        Wrong,
    }

    /// <summary>One chip: what it says, how loudly, and what it means in full.</summary>
    public struct Badge
    {
        public string Text;
        public BadgeTone Tone;

        /// <summary>The sentence behind it, for a tooltip or a long-press. Never empty.</summary>
        public string Tip;
    }

    /// <summary>
    /// What a translation is, in chips somebody reads without reading.
    ///
    /// 🔴 **Here, and not in either product, because it is the same question in both.** The website
    /// already answers it on its cards — role in the lineage, how far the review got, how much is
    /// translated, how many votes — and neither the mod nor the manager did. Writing that strip
    /// twice would give one file two descriptions depending on which window you opened it in, and
    /// this project has paid for that already with the review stage, whose wording existed in two
    /// identical copies free to drift.
    ///
    /// ⚠ **The ORDER is part of the rule.** A strip read left to right must not be reshuffled
    /// between screens, or the eye has to start reading again each time. Publication first because
    /// it decides what every button means; then who you are in the lineage; then whether you are up
    /// to date; then what the file is made of; then what the world made of it.
    ///
    /// ⚠ **Silence is a decision, not an omission.** A count of zero votes reads as a verdict; a
    /// completeness of 100% is the ordinary state. Both stay out. Anything shown here earned its
    /// place by saying something.
    /// </summary>
    public static class Badges
    {
        /// <param name="publication">Whether this has ever been published. Always shown.</param>
        /// <param name="isMain">
        /// True when this account leads the lineage, false when it holds a branch of somebody
        /// else's. Null when nothing is published, where the question has no answer yet.
        /// </param>
        /// <param name="branchesWaiting">Contributions sent to your Main and not settled.</param>
        /// <param name="mainMissing">A branch whose Main is no longer on the site.</param>
        /// <param name="sync">Where this file stands against the published version, if known.</param>
        /// <param name="stage">How far the review got, from <see cref="Quality.Stage"/>.</param>
        /// <param name="completeness">Share of what the file met in game that is translated.</param>
        /// <param name="votes">What the site records. Zero means "none", which is not shown.</param>
        /// <param name="downloads">Same.</param>
        public static List<Badge> For(Publication publication, bool? isMain, int? branchesWaiting,
                                      bool mainMissing, SyncDirection? sync, ReviewStage? stage,
                                      double? completeness, int votes, int downloads)
        {
            var badges = new List<Badge>();

            // ── 1. Has it ever left this machine ──────────────────────────────
            //
            // ⚠ First and always. It decides what the buttons MEAN: publishing something never
            // published creates a lineage under your name, publishing something already published
            // updates it. Two acts, one button, and nothing else on a card says which.
            //
            // "Never published" carries the warm tone — not because it is wrong, plenty of
            // translations are private on purpose, but because it is the one state where the work
            // exists in exactly one place.
            badges.Add(new Badge
            {
                Text = Publications.Name(publication),
                Tone = publication == Publication.NeverPublished ? BadgeTone.Attention : BadgeTone.Plain,
                Tip = Publications.Effect(publication),
            });

            // ── 2. Who you are in this lineage ────────────────────────────────
            if (isMain == true)
            {
                badges.Add(new Badge
                {
                    Text = "Main",
                    Tone = BadgeTone.Plain,
                    Tip = "You lead this lineage. Contributions arrive as branches for you to take "
                        + "or leave.",
                });
            }
            else if (isMain == false)
            {
                // ⚠ A branch is the one worth spotting, and the website colours it for the same
                // reason: it is not public, only its Main can read it, and what you do with it is
                // not the act you would perform on something everyone can see.
                badges.Add(new Badge
                {
                    Text = "Branch",
                    Tone = BadgeTone.Attention,
                    Tip = "Yours is a contribution to somebody else's translation. They decide "
                        + "what they keep.",
                });
            }

            if (branchesWaiting.HasValue && branchesWaiting.Value > 0)
            {
                badges.Add(new Badge
                {
                    Text = branchesWaiting.Value + " waiting",
                    Tone = BadgeTone.Good,
                    Tip = "Contributions sent to your Main that you have not settled yet.",
                });
            }

            if (mainMissing)
            {
                badges.Add(new Badge
                {
                    Text = "Main is gone",
                    Tone = BadgeTone.Wrong,
                    Tip = "The translation yours contributes to is no longer on the site.",
                });
            }

            // ── 3. Up to date? ────────────────────────────────────────────────
            if (sync.HasValue)
            {
                switch (sync.Value)
                {
                    case SyncDirection.InSync:
                        badges.Add(new Badge
                        {
                            Text = "In step",
                            Tone = BadgeTone.Good,
                            Tip = "This file and the published version hold the same content.",
                        });
                        break;

                    case SyncDirection.Download:
                        badges.Add(new Badge
                        {
                            Text = "Behind",
                            Tone = BadgeTone.Notice,
                            Tip = "The published version has moved on. Nothing of yours is at risk "
                                + "— you have no unpublished changes here.",
                        });
                        break;

                    case SyncDirection.Upload:
                        badges.Add(new Badge
                        {
                            Text = "Ahead",
                            Tone = BadgeTone.Attention,
                            Tip = "You have changes here that the published version does not have.",
                        });
                        break;

                    default:
                        badges.Add(new Badge
                        {
                            Text = "Diverged",
                            Tone = BadgeTone.Attention,
                            Tip = "Both this file and the published one have moved. Settling that "
                                + "is done line by line.",
                        });
                        break;
                }
            }

            // ── 4. What the file is made of ───────────────────────────────────
            if (stage.HasValue)
            {
                BadgeTone tone;
                if (stage.Value == ReviewStage.Reviewed) tone = BadgeTone.Good;
                else if (stage.Value == ReviewStage.Advanced) tone = BadgeTone.Notice;
                else tone = BadgeTone.Quiet;

                badges.Add(new Badge
                {
                    Text = Quality.StageName(stage.Value),
                    Tone = tone,
                    Tip = "How much of this file a human has settled.",
                });
            }

            // ⚠ Silent at 100%, like the website: a number is worth showing when it says
            // something, and "everything it met is translated" is the ordinary state.
            if (completeness.HasValue && completeness.Value < 1.0)
            {
                badges.Add(new Badge
                {
                    Text = (int)(completeness.Value * 100 + 0.5) + "% translated",
                    Tone = BadgeTone.Attention,
                    Tip = "Share of the lines this file has met in game that are actually "
                        + "translated.",
                });
            }

            // ── 5. What the world made of it ──────────────────────────────────
            //
            // ⚠ Zero is not shown. "0 votes" reads as a verdict on the translation; the absence of
            // the chip reads as nobody having voted, which is what is true.
            if (votes > 0)
            {
                badges.Add(new Badge
                {
                    Text = votes + (votes == 1 ? " vote" : " votes"),
                    Tone = BadgeTone.Plain,
                    Tip = "What players thought of it on the site.",
                });
            }

            if (downloads > 0)
            {
                badges.Add(new Badge
                {
                    Text = downloads + (downloads == 1 ? " download" : " downloads"),
                    Tone = BadgeTone.Quiet,
                    Tip = "How many times it has been taken from the site.",
                });
            }

            return badges;
        }
    }
}
