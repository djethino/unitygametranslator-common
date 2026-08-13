using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Settling one line between what is here, what was published, and what both came from.
    ///
    /// ⚠ These answers are written out from the rule as stated, not read back from the code that
    /// implements it. That is the whole point of the exercise: a merge that agrees with itself
    /// proves nothing, and this rule now has two readers — a running game and a tool that never
    /// opens one — which must never settle the same line differently.
    ///
    /// The stakes are not academic. Every wrong answer here is somebody's translation silently
    /// replaced by somebody else's, or a review nobody performed being claimed.
    /// </summary>
    internal static class MergeChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── The ladder ────────────────────────────────────────────────────
            check(Merge.PriorityOf("H", "Bonjour") > Merge.PriorityOf("V", "Bonjour"),
                "a human outranks a validated line", "somebody wrote it, somebody else only agreed");
            check(Merge.PriorityOf("V", "x") > Merge.PriorityOf("A", "x"),
                "a validated line outranks a machine one", "a person passed over it");
            check(Merge.PriorityOf("H", "") < Merge.PriorityOf("A", "x"),
                "an empty human line is the BOTTOM, not the top",
                "it is a captured line waiting for a translation, so any translation beats it");
            check(Merge.PriorityOf("S", "x") > Merge.PriorityOf("H", "x")
                  && Merge.PriorityOf("M", "x") > Merge.PriorityOf("H", "x"),
                "a refusal and the mod's own interface sit above everything",
                "they are statements about the file, not translations of the game");
            check(Merge.PriorityOf(null, "x") == Merge.PriorityOf("A", "x"),
                "no tag reads as A", "the older file format wrote none and meant exactly that");

            // ⚠ The immutable pair cannot be replaced even by something that outranks it, which is
            // a different statement from the ladder and the one a priority comparison alone loses.
            check(!Merge.CanReplace(Line("Bonjour", "H"), Line("Bonjour", "S")),
                "nothing replaces a refusal", "a merge must not translate what tooling refused");
            check(!Merge.CanReplace(Line("Bonjour", "H"), Line("Salut", "M")),
                "nor a line of the mod's own interface", "same reason");
            check(Merge.CanReplace(Line("x", "A"), (TranslationLine?)null),
                "anything replaces nothing", "an absent line is not a line to protect");

            // ── One side only ─────────────────────────────────────────────────
            var added = Merge.Decide(Line("Salut", "H"), null, null);
            check(added.Verdict == MergeVerdict.TakeLocal && !added.IsConflict
                  && added.Reason == MergeReason.LocalOnly,
                "a line only here is kept", "nobody else has an opinion about it");

            var theirs = Merge.Decide(null, Line("Salut", "A"), null);
            check(theirs.Verdict == MergeVerdict.TakeRemote && theirs.Reason == MergeReason.RemoteAdded,
                "a line only there is taken", "same, from the other side");

            var gone = Merge.Decide(null, null, Line("Salut", "A"));
            check(gone.Verdict == MergeVerdict.Drop && gone.Reason == MergeReason.Deleted,
                "a line only the ancestor still has goes", "both sides removed it");

            // ── Both sides ────────────────────────────────────────────────────
            var same = Merge.Decide(Line("Salut", "A"), Line("Salut", "A"), null);
            check(same.Verdict == MergeVerdict.TakeLocal && !same.IsConflict
                  && same.Reason == MergeReason.Unchanged,
                "identical lines are not a conflict", "there is nothing to settle");

            // ⚠ Same words, different tag: NOT identical. The tag records that somebody read it.
            var reviewed = Merge.Decide(Line("Salut", "A"), Line("Salut", "V"), null);
            check(reviewed.Verdict == MergeVerdict.TakeRemote && !reviewed.IsConflict,
                "the same words reviewed by somebody win over the machine's",
                "treating them as identical would lose the review");

            var outranked = Merge.Decide(Line("Salut", "H"), Line("Bonjour", "A"), null);
            check(outranked.Verdict == MergeVerdict.TakeLocal && !outranked.IsConflict
                  && outranked.Reason == MergeReason.LocalModified,
                "a human line here beats a machine line there, with nobody asked",
                "the ladder settles it");

            // ── Equal standing: the ancestor is the only witness ──────────────
            var theyMoved = Merge.Decide(Line("Salut", "A"), Line("Bonjour", "A"), Line("Salut", "A"));
            check(theyMoved.Verdict == MergeVerdict.TakeRemote
                  && !theyMoved.IsConflict && theyMoved.Reason == MergeReason.RemoteUpdated,
                "unchanged here, moved there: take theirs", "nothing of ours is at stake");

            var weMoved = Merge.Decide(Line("Bonjour", "A"), Line("Salut", "A"), Line("Salut", "A"));
            check(weMoved.Verdict == MergeVerdict.TakeLocal
                  && !weMoved.IsConflict && weMoved.Reason == MergeReason.LocalModified,
                "moved here, unchanged there: keep ours", "same, mirrored");

            var both = Merge.Decide(Line("Bonjour", "A"), Line("Coucou", "A"), Line("Salut", "A"));
            check(both.IsConflict && both.Conflict == ConflictKind.BothModified
                  && both.Verdict == MergeVerdict.TakeRemote,
                "both moved: a conflict, shown as theirs while it is settled",
                "leaving the merged set short of the line would lose it until somebody answers");

            // ⚠ No ancestor is NOT "take the newer one".
            var blind = Merge.Decide(Line("Bonjour", "A"), Line("Coucou", "A"), null);
            check(blind.IsConflict && blind.Conflict == ConflictKind.NoAncestor,
                "equal standing with no ancestor is a conflict",
                "there is no way to tell who moved, and picking silently overwrites at random");

            // ── Deleted on one side ───────────────────────────────────────────
            var droppedThere = Merge.Decide(Line("Salut", "A"), null, Line("Salut", "A"));
            check(droppedThere.Verdict == MergeVerdict.Drop && !droppedThere.IsConflict,
                "untouched here and deleted there: it goes", "we had no stake in it");

            var keptHere = Merge.Decide(Line("Bonjour", "H"), null, Line("Salut", "A"));
            check(keptHere.IsConflict && keptHere.Conflict == ConflictKind.LocalModifiedRemoteDeleted
                  && keptHere.Verdict == MergeVerdict.TakeLocal,
                "changed here and deleted there is a conflict, shown as ours",
                "deleting work somebody just did is exactly what must be asked about");

            var keptThere = Merge.Decide(null, Line("Bonjour", "H"), Line("Salut", "A"));
            check(keptThere.IsConflict && keptThere.Conflict == ConflictKind.RemoteModifiedLocalDeleted
                  && keptThere.Verdict == MergeVerdict.TakeRemote,
                "deleted here and changed there is a conflict, shown as theirs", "mirrored");
        }

        private static TranslationLine Line(string value, string tag) => new TranslationLine(value, tag);
    }
}
