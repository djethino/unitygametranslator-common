using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The chips that say what a translation IS.
    ///
    /// ⚠ Written when the "finished" chip was added, and the gap is worth naming: this file did not
    /// exist, so the rule that decides what every card in three products shows had no check at all.
    /// </summary>
    internal static class BadgesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            FrozenBranch(check);

            // ── The author's own word ─────────────────────────────────────────
            //
            // 🔴 A DECLARATION, not a measurement. The review stage and the completeness are read
            // from the file; this one somebody decided, and only they can change it — so a card
            // showing the measurements and hiding the declaration leaves its author unable to tell
            // whether they still have to go and say it.
            var finished = Badges.For(Publication.Published, true, null, false, null, null,
                                      null, 0, 0, finished: true);
            var writing = Badges.For(Publication.Published, true, null, false, null, null,
                                     null, 0, 0, finished: false);

            check(Has(finished, BadgeKind.Finished) && Has(writing, BadgeKind.Finished),
                "a published translation says whether its author calls it finished",
                "a declaration nobody can see can be neither checked nor corrected");

            check(Text(finished, BadgeKind.Finished) != Text(writing, BadgeKind.Finished),
                "and the two answers do not read the same",
                "one chip for both states would say nothing at all");

            // ⚠ Before publishing there is nobody to declare it to.
            var never = Badges.For(Publication.NeverPublished, null, null, false, null, null,
                                   null, 0, 0, finished: false);

            check(!Has(never, BadgeKind.Finished),
                "a translation that never left this machine declares nothing",
                "reporting a state that does not exist yet is not reporting a choice");

            check(!Has(Badges.For(Publication.Published, true, null, false, null, null, null, 0, 0),
                       BadgeKind.Finished),
                "and an unknown declaration shows nothing rather than a guess",
                "an older server saying nothing is not the same as saying 'still writing'");

            // ── Every chip carries its sentence ───────────────────────────────
            //
            // ⚠ A chip is two or three words; the sentence behind it is where the meaning is. One
            // without it is a label somebody has to guess at.
            foreach (var badge in Badges.For(Publication.Published, true, 2, false,
                                             SyncDirection.Upload, ReviewStage.Reviewed,
                                             0.9, 12, 300, finished: true))
            {
                check(!string.IsNullOrEmpty(badge.Text),
                    badge.Kind + " has something written on it", "an empty chip is a rendering fault");

                check(!string.IsNullOrEmpty(badge.Tip),
                    badge.Kind + " explains itself", "a chip nobody can expand is a chip nobody trusts");
            }
        }

        /// <summary>
        /// A branch whose Main closed its contributions says so, once.
        ///
        /// 🔴 The state was reachable nowhere on a branch's card: "Accepts contributions" is the
        /// Main's own declaration and is shown on the Main only, so from the branch side the door
        /// closing was invisible. It mattered the day notifications became deletable — a fact that
        /// lives only in a message somebody can delete does not live anywhere.
        /// </summary>
        private static void FrozenBranch(Action<bool, string, string> check)
        {
            var frozen = Badges.For(Publication.Published, false, null, false,
                                    null, null, null, 0, 0, branchFrozen: true);

            check(Has(frozen, BadgeKind.BranchFrozen),
                "a frozen branch says its Main closed",
                "the only other place saying so is a notification, which can now be deleted");

            // ⚠ Never both. They answer the same question — can this still be merged — and two
            // chips would read as two problems.
            var gone = Badges.For(Publication.Published, false, null, true,
                                  null, null, null, 0, 0, branchFrozen: true);

            check(Has(gone, BadgeKind.MainMissing) && !Has(gone, BadgeKind.BranchFrozen),
                "a Main that is gone outranks a Main that closed",
                "nobody refused anything when the translation itself is no longer there");

            // ⚠ Attention, not Wrong: nothing is broken. The translation is still published and the
            // work is still its author's; what changed is the road it was on.
            foreach (var badge in frozen)
            {
                if (badge.Kind != BadgeKind.BranchFrozen) continue;

                check(badge.Tone == BadgeTone.Attention,
                    "a closed Main is worth noticing, not an error",
                    "colouring it as a fault would read as a reproach to whoever closed it");
            }
        }

        private static bool Has(List<Badge> badges, BadgeKind kind)
        {
            foreach (var badge in badges)
                if (badge.Kind == kind) return true;

            return false;
        }

        private static string Text(List<Badge> badges, BadgeKind kind)
        {
            foreach (var badge in badges)
                if (badge.Kind == kind) return badge.Text;

            return null;
        }
    }
}
