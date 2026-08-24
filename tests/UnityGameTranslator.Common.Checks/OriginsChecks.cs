using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Where a fork came from — the one fact in the strip that is recorded rather than measured.
    ///
    /// ⚠ The cases are written from the specification, not from the implementation: what the chip
    /// must NAME, what the tip must not let somebody believe, and where the chip sits in the strip.
    /// </summary>
    internal static class OriginsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var known = new Origin("alice", 3120);
            var noCount = new Origin("alice", null);
            var gone = new Origin(null, 3120);

            // ── The chip names somebody ───────────────────────────────────────
            check(Origins.Name(known).Contains("alice"),
                "the chip names the account it came from",
                "a credit that does not say whose is not a credit");

            check(Origins.Name(known).Contains("@alice"),
                "and names them the way every other name on screen is written",
                "two forms for one person is what People exists to prevent");

            check(Origins.Name(known).Contains("Fork"),
                "the chip uses the word the three roles already use",
                "Main, Branch and Fork are the vocabulary; a fourth word would have to be learnt");

            // ── A missing account is stated, never blank and never 'unknown' ──
            //
            // The column carries no foreign key on purpose, so the row outlives the account. What
            // must not happen is the chip falling through to People.Unknown and reading
            // "Forked from unknown", which sounds like a fault rather than a fact.
            check(!Origins.Name(gone).Contains(People.Unknown),
                "a removed account is said in words, not as 'unknown'",
                "a placeholder where a name belongs reads as a bug");

            check(Origins.Name(gone).Trim().Length > 0 && Origins.Effect(gone).Trim().Length > 0,
                "and it still says something rather than nothing",
                "the credit stands without a name rather than not at all");

            // ── The tip carries the count, and only when there is one ─────────
            check(Origins.Effect(known).Contains("3120"),
                "the tip says how many lines were handed over",
                "the snapshot is the whole point of recording it");

            check(!Origins.Effect(noCount).Contains("0 lines"),
                "and an unknown count is left out rather than shown as none",
                "an older server saying nothing is not the same as saying zero");

            // ── The tip must not let a fork read as a live link ───────────────
            //
            // 🔴 Forking SEVERS the tie — the mod has to, or it would keep offering to merge from a
            // lineage it left. Without this sentence "Forked from @alice" reads as a subscription.
            foreach (var origin in new[] { known, noCount, gone })
            {
                check(Origins.Effect(origin).Contains("separate"),
                    "the tip says the two are separate translations now",
                    "otherwise somebody expects updates from a lineage they walked away from");
            }

            // ── Where it sits in the strip ────────────────────────────────────
            //
            // ⚠ The ORDER is part of the rule. Origin answers "whose is this", so it belongs with
            // the role and before anything about being up to date.
            var strip = Badges.For(Publication.Published, true, null, false,
                                   SyncDirection.InSync, ReviewStage.Reviewed, null, 3, 40,
                                   origin: known);

            int atOrigin = IndexOf(strip, BadgeKind.Origin);
            int atRole = IndexOf(strip, BadgeKind.Role);
            int atSync = IndexOf(strip, BadgeKind.Sync);

            check(atOrigin > atRole && atOrigin < atSync,
                "the chip sits after the role and before the sync verdict",
                "a strip reshuffled between screens has to be read again each time");

            check(IndexOf(Badges.For(Publication.Published, true, null, false, null, null,
                                     null, 0, 0), BadgeKind.Origin) < 0,
                "a translation nobody forked shows no origin at all",
                "silence is the answer, not 'forked from nowhere'");

            // ⚠ A fork IS a Main, and both chips show. The pair is the point: one says what you may
            // do with it, the other says where it came from.
            check(IndexOf(strip, BadgeKind.Role) >= 0,
                "a fork still says Main beside its origin",
                "a fork leads its own lineage; demoting it would misstate who may write");
        }

        private static int IndexOf(List<Badge> badges, BadgeKind kind)
        {
            for (int i = 0; i < badges.Count; i++)
                if (badges[i].Kind == kind) return i;

            return -1;
        }
    }
}
