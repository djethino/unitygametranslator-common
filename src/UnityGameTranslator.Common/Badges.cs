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

    /// <summary>
    /// What a chip is ABOUT, so a screen can drop the ones it already says another way.
    ///
    /// ⚠ **Selection is a rendering decision; the words, order and tones are not.** The manager's
    /// game page has nothing but this strip, so it shows everything. The mod's card already draws a
    /// quality bar and a vote row, and repeating those as chips would spend attention on something
    /// the reader can already see — the same reasoning the website's own badge component gives for
    /// carrying only two. What must never differ is what a shown chip SAYS.
    /// </summary>
    public enum BadgeKind
    {
        Publication,
        Role,
        BranchesWaiting,
        MainMissing,

        /// <summary>
        /// Which translation this one was forked from.
        ///
        /// 🔴 In the lineage group and not among the measurements, because it answers "whose is
        /// this" and not "what is it worth". A fork IS a Main — <c>lineageRole()</c> says so —
        /// so the role chip alone leaves out the one thing that distinguishes it from a Main
        /// somebody started from nothing.
        /// </summary>
        Origin,

        Sync,

        /// <summary>
        /// The author's own "this is finished".
        ///
        /// 🔴 Distinct from <see cref="ReviewStage"/> and from <see cref="Completeness"/>, which
        /// are MEASURED from the file. This one is a declaration: somebody decided it, and only
        /// they can change it. A card that showed the measurements and hid the declaration left an
        /// author unable to tell whether they still had to go and say it.
        /// </summary>
        Finished,

        /// <summary>
        /// Whether this lineage takes contributions — the Main's own decision.
        ///
        /// 🔴 Same nature as <see cref="Finished"/> and for the same reason: a DECLARATION, not
        /// something measured. Only the Main can change it, and a card that hid it left a reader
        /// discovering the answer at the moment they tried to contribute.
        ///
        /// ⚠ Both states are shown, and neither is a reproach. Keeping a translation open is work
        /// nobody agreed to by publishing; "Solo work" says how somebody works, not that they
        /// refused anybody.
        /// </summary>
        Contributions,

        ReviewStage,
        Completeness,
        Votes,
        Downloads,
    }

    /// <summary>One chip: what it says, how loudly, and what it means in full.</summary>
    public struct Badge
    {
        public string Text;
        public BadgeTone Tone;
        public BadgeKind Kind;

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
    ///
    /// 🔴 **Plain, international English, because there is no other.** The mod and the manager ship
    /// no translations: whatever is written here is what a Polish, Brazilian or Korean player
    /// reads, forever. So the words are the ones every program has used for thirty years — "Up to
    /// date", "Update available", "Conflict" — and never the precise-but-bookish ones a native
    /// reaches for first. This strip read "In step", "Behind", "Ahead" and "Diverged" until
    /// somebody pointed out that nobody outside a version-control habit says any of them.
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
        /// <param name="mainAbandoned">
        /// A branch whose Main is still on the site and whose owner erased their account. Ignored
        /// when <paramref name="mainMissing"/> is set: a Main that is gone is the whole story.
        /// </param>
        /// <param name="sync">Where this file stands against the published version, if known.</param>
        /// <param name="stage">How far the review got, from <see cref="Quality.Stage"/>.</param>
        /// <param name="completeness">Share of what the file met in game that is translated.</param>
        /// <param name="votes">What the site records. Zero means "none", which is not shown.</param>
        /// <param name="downloads">Same.</param>
        /// <param name="finished">
        /// The author's own declaration, or null when it is not known — an older server, or a
        /// translation that is not ours to speak for. Null shows nothing rather than guessing.
        /// </param>
        /// <param name="acceptsContributions">
        /// Whether the Main of this lineage takes branches. Null when unknown — an older server —
        /// and unknown shows nothing: announcing "Solo work" on a server that never said so would
        /// turn a missing field into somebody's decision.
        ///
        /// ⚠ Dropped outright when <paramref name="isMain"/> is false, so a caller holding a
        /// branch may pass the lineage's answer without having to remember the rule.
        /// </param>
        /// <param name="linesAvailable">
        /// How many lines those contributions hold, counted once each. Null when unknown — an older
        /// server — and the chip then says how many contributions without saying what they carry.
        /// </param>
        /// <param name="origin">
        /// Which translation this one was forked from, when it was forked from one at all. Null on
        /// anything that started from nothing, and on a server too old to have said.
        ///
        /// ⚠ Not derivable from the other arguments. A fork reads as a Main in every other respect
        /// — that is deliberate, a fork owner leads their own lineage — so nothing else here can
        /// tell one apart from a translation somebody wrote from scratch.
        /// </param>
        /// <param name="mainOwner">
        /// Who leads this lineage, used only to name them in the tip of
        /// <see cref="Publication.NotYours"/>. Null is fine — an account can be gone, or a server
        /// too old to say — and the sentence then says "Somebody else" rather than nothing.
        /// </param>
        public static List<Badge> For(Publication publication, bool? isMain, int? branchesWaiting,
                                      bool mainMissing, SyncDirection? sync, ReviewStage? stage,
                                      double? completeness, int votes, int downloads,
                                      bool? finished = null,
                                      bool? acceptsContributions = null,
                                      int? linesAvailable = null,
                                      Origin? origin = null,
                                      string? mainOwner = null,
                                      bool mainAbandoned = false)
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
            // ⚠ "Not yours" is a NOTICE, never a warning: holding somebody else's translation is
            // the ordinary way a player starts, and nothing is wrong. What earns the colour is that
            // the buttons below mean something else there — publishing contributes rather than
            // creates.
            BadgeTone publicationTone;
            if (publication == Publication.NeverPublished) publicationTone = BadgeTone.Attention;
            else if (publication == Publication.NotYours) publicationTone = BadgeTone.Notice;
            else publicationTone = BadgeTone.Plain;

            badges.Add(new Badge
            {
                Text = Publications.Name(publication),
                Kind = BadgeKind.Publication,
                Tone = publicationTone,
                Tip = Publications.Effect(publication, mainOwner),
            });

            // ── 2. Who you are in this lineage ────────────────────────────────
            if (isMain == true)
            {
                badges.Add(new Badge
                {
                    Text = "Main",
                    Kind = BadgeKind.Role,
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
                    Kind = BadgeKind.Role,
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
                    Kind = BadgeKind.BranchesWaiting,
                    Tone = BadgeTone.Good,
                    // ⚠ The chip stays two words; what it is worth goes in the tip, which is where
                    // somebody looks before deciding whether to open the merge screen at all.
                    Tip = Contributions.WhatIsWaiting(branchesWaiting.Value, linesAvailable),
                });
            }

            if (mainMissing)
            {
                badges.Add(new Badge
                {
                    Text = "Main is gone",
                    Kind = BadgeKind.MainMissing,
                    Tone = BadgeTone.Wrong,
                    Tip = "The translation yours contributes to is no longer on the site.",
                });
            }

            // ⚠ **Else, not a second chip.** Both say the same thing about what can be done — this
            // work will never be merged — and wearing two would suggest two problems. When the Main
            // is gone, that is the whole story and the account behind it no longer matters.
            //
            // 🔴 Its own wording rather than reusing the one above, because the difference is the
            // one thing a reader actually needs: the translation is still on the site, still
            // downloadable, and still good to play with. "Main is gone" would say the opposite.
            else if (mainAbandoned)
            {
                badges.Add(new Badge
                {
                    Text = "No owner",
                    Kind = BadgeKind.MainMissing,
                    Tone = BadgeTone.Wrong,
                    Tip = "The account behind the translation yours contributes to has been "
                        + "deleted. The translation itself is still there.",
                });
            }

            // ⚠ Last of the lineage group, and quiet. It is a credit, not a warning: a fork is an
            // ordinary and legitimate way for a translation to exist, and colouring its provenance
            // would read as a reproach to whoever made it.
            if (origin.HasValue)
            {
                badges.Add(new Badge
                {
                    Text = Origins.Name(origin.Value),
                    Kind = BadgeKind.Origin,
                    Tone = BadgeTone.Quiet,
                    Tip = Origins.Effect(origin.Value),
                });
            }

            // ── 3. Up to date? ────────────────────────────────────────────────
            if (sync.HasValue)
            {
                switch (sync.Value)
                {
                    // ⚠ **The words a program uses, not the words a novel uses.** These read
                    // "In step", "Behind", "Ahead" and "Diverged" — precise, and nobody outside a
                    // version-control habit says any of them. Somebody looking at a game wants to
                    // know whether there is an update, in the four words every piece of software
                    // has used for thirty years.
                    case SyncDirection.InSync:
                        badges.Add(new Badge
                        {
                            Text = "Up to date",
                    Kind = BadgeKind.Sync,
                            Tone = BadgeTone.Good,
                            Tip = "This file and the published version hold the same content.",
                        });
                        break;

                    case SyncDirection.Download:
                        badges.Add(new Badge
                        {
                            Text = "Update available",
                    Kind = BadgeKind.Sync,
                            Tone = BadgeTone.Notice,
                            Tip = "The published version has moved on. Nothing of yours is at risk "
                                + "— you have no unpublished changes here.",
                        });
                        break;

                    case SyncDirection.Upload:
                        badges.Add(new Badge
                        {
                            Text = "Unpublished changes",
                    Kind = BadgeKind.Sync,
                            Tone = BadgeTone.Attention,
                            Tip = "You have changes here that the published version does not have.",
                        });
                        break;

                    default:
                        badges.Add(new Badge
                        {
                            Text = "Conflict",
                    Kind = BadgeKind.Sync,
                            Tone = BadgeTone.Attention,
                            Tip = "Both this file and the published one have moved. Settling that "
                                + "is done line by line.",
                        });
                        break;
                }
            }

            // ── 3b. What its author says about it ─────────────────────────────
            //
            // ⚠ Only when it has been published. Before that there is nobody to declare it to, and
            // a chip saying "in progress" on a private file would report a state that does not
            // exist yet rather than one somebody chose.
            if (finished.HasValue && publication == Publication.Published)
            {
                badges.Add(new Badge
                {
                    Text = finished.Value ? "Finished" : "Still writing",
                    Kind = BadgeKind.Finished,

                    // Quiet either way: this is a statement of intent, not a verdict on quality.
                    // Colouring "finished" as good would rank two legitimate answers.
                    Tone = finished.Value ? BadgeTone.Good : BadgeTone.Quiet,
                    Tip = finished.Value
                        ? "Its author says this translation is finished."
                        : "Its author is still working on this one.",
                });
            }

            // ⚠ Beside the author's other declaration, and published only: an unpublished file
            // has no lineage for anybody to contribute to, so the question does not arise.
            // 🔴 **Never on a branch, whatever the caller passes.** The decision belongs to the
            // lineage's Main; rendered beside "Branch" it reads as a claim about THIS row, which
            // its author cannot change and did not make. The website already hid it there, and a
            // fact that reads one way on one product and another way on the next is a defect even
            // when each is defensible alone.
            //
            // ⚠ `isMain == false` and not `!= true`: null means "the question has no answer here"
            // — the community list passes null on purpose, because "Main" would be read as a claim
            // about the reader — and every row in that list IS a Main whose decision is exactly
            // what helps somebody choose.
            // 🔴 **No publication guard.** It carried one — `publication == Published` — and that
            // was importing a question about YOUR standing into a fact about the LINEAGE. Somebody
            // holding a translation they downloaded is "Never published" here, and is exactly the
            // person who needs to know whether their corrections can be sent back. They saw
            // nothing; the chip appeared only on one's own work, which reads as nonsense.
            //
            // Nothing has to replace it: the value is null whenever no Main has decided, and null
            // already shows nothing.
            if (acceptsContributions.HasValue && isMain != false)
            {
                badges.Add(new Badge
                {
                    Text = acceptsContributions.Value ? "Accepts contributions" : "Solo work",
                    Kind = BadgeKind.Contributions,

                    // Quiet either way, exactly like Finished. Two legitimate ways of working;
                    // colouring one as good would make the other read as a refusal.
                    Tone = BadgeTone.Quiet,
                    Tip = acceptsContributions.Value
                        ? "Its author takes contributions: your work can be sent to them for review."
                        : "Its author works alone on this one. You can still publish your own "
                          + "version of it.",
                });
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
                    Kind = BadgeKind.ReviewStage,
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
                    Kind = BadgeKind.Completeness,
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
                    Kind = BadgeKind.Votes,
                    Tone = BadgeTone.Plain,
                    Tip = "What players thought of it on the site.",
                });
            }

            if (downloads > 0)
            {
                badges.Add(new Badge
                {
                    Text = downloads + (downloads == 1 ? " download" : " downloads"),
                    Kind = BadgeKind.Downloads,
                    Tone = BadgeTone.Quiet,
                    Tip = "How many times it has been taken from the site.",
                });
            }

            return badges;
        }
    }
}
