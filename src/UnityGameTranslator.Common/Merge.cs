using System;

namespace UnityGameTranslator.Common
{
    /// <summary>What to put in the merged set for one key.</summary>
    public enum MergeVerdict
    {
        /// <summary>Keep what is here.</summary>
        TakeLocal,

        /// <summary>Take what the other side has.</summary>
        TakeRemote,

        /// <summary>The key goes: both sides agree it is gone.</summary>
        Drop,
    }

    /// <summary>Why a key could not be settled without asking somebody.</summary>
    public enum ConflictKind
    {
        /// <summary>Both sides changed it, differently, with equal standing.</summary>
        BothModified,

        /// <summary>No ancestor, so there is no way to tell who changed what.</summary>
        NoAncestor,

        /// <summary>Changed here, deleted there.</summary>
        LocalModifiedRemoteDeleted,

        /// <summary>Changed there, deleted here.</summary>
        RemoteModifiedLocalDeleted,
    }

    /// <summary>Which tally a decision belongs to. Counting is the caller's; naming is not.</summary>
    public enum MergeReason
    {
        Unchanged,
        LocalOnly,
        LocalModified,
        RemoteAdded,
        RemoteUpdated,
        Deleted,
        Conflict,
    }

    /// <summary>
    /// What to do with one key, and why.
    ///
    /// ⚠ <see cref="Verdict"/> is meaningful even on a conflict: it says which side to SHOW while
    /// somebody decides. A conflict with nothing in the merged set would leave the file short of a
    /// line until the question is answered, and the question may never be answered.
    /// </summary>
    public struct MergeDecision
    {
        public MergeVerdict Verdict;
        public bool IsConflict;
        public ConflictKind Conflict;
        public MergeReason Reason;
    }

    /// <summary>
    /// Deciding, key by key, what a three-way merge of a translation should do.
    ///
    /// ⚠ **The rule, and only the rule.** No dictionaries, no storage, no renumbering: each program
    /// keeps its own bookkeeping — the mod mutates the very entry objects it holds in memory and
    /// renumbers capture indices by reference identity, which is meaningless anywhere else. What
    /// must never differ between them is the ANSWER, and that is what lives here.
    ///
    /// ⚠ **It was the mod's alone, and that was a priority call rather than a principle** (the
    /// manager's file writer says so in as many words: "a second implementation here would be a
    /// second truth about the same file, and the one that ran last would win"). The way out of that
    /// was never to write a second one: it is this — one implementation, read by both.
    ///
    /// ⚠ **Computing is not applying.** Reaching a verdict is read-only and costs nothing; acting
    /// on it writes the file AND moves the ancestor, which is bookkeeping with exactly one owner at
    /// a time. Anything may call this; not everything may write.
    /// </summary>
    public static class Merge
    {
        /// <summary>
        /// Is this a line OF THE GAME — the only kind a merge arbitrates and a count counts?
        ///
        /// 🔴 **M is the mod's own interface, and it is not one.** It travels in the same file today
        /// and will get a file of its own; until then it is carried, never weighed. It has no author
        /// in the sense H does, it is not what a player came to read, and counting it would inflate
        /// every measure of what a translation covers.
        /// </summary>
        public static bool IsGameLine(string tag)
        {
            return tag != "M";
        }

        /// <summary>
        /// How much a line outranks another, from its tag.
        ///
        /// ⚠ The ladder decides who wins without anybody being asked, so it is written once here
        /// and read everywhere.
        ///
        /// 🔴 **S sits WITH H, not above it.** Both are a person's decision about the line — one
        /// wrote it, the other ruled that it must not be written — and neither outranks the other.
        /// What separates them when they meet is not the ladder but who is asking: a Main keeps its
        /// own against a contribution (see <see cref="ContributionWins"/>), and the published side
        /// is shown first when somebody meets their own earlier version.
        ///
        /// An H with nothing in it is the bottom, not the top: it is a captured line waiting for a
        /// translation, and any real translation beats it.
        ///
        /// ⚠ M is not on the ladder at all — it is not a line of the game. It is ranked with the
        /// ordinary case so a stray comparison cannot promote it, but the arbitration keeps it out.
        /// </summary>
        public static int PriorityOf(string tag, string value)
        {
            if (tag == "H" && string.IsNullOrEmpty(value)) return 0;

            switch (tag)
            {
                case "A": return 1;
                case "V": return 2;
                // A refusal is a human decision about the line, exactly as a translation written by
                // hand is. Equal, therefore, and never settled by the numbers alone.
                case "H": return 3;
                case "S": return 3;
                // Includes a line with no tag at all: the older file format wrote none, and it
                // means the same as the ordinary case.
                default: return 1;
            }
        }

        /// <summary>Whether one line may take another's place with nobody asked.</summary>
        public static bool CanReplace(TranslationLine candidate, TranslationLine? existing)
        {
            // ⚠ The mod's interface neither takes a game line's place nor gives up its own: it is
            // not the same subject. Checked before the absent case, so an M cannot install itself
            // where a game line is merely missing.
            if (!IsGameLine(candidate.Tag)) return false;

            if (!existing.HasValue) return true;

            TranslationLine other = existing.GetValueOrDefault();

            // ⚠ Immutable whatever the numbers say. Priority alone would let an H overrule an S,
            // which would translate a line somebody's tooling deliberately refused.
            if (other.Tag == "S" || !IsGameLine(other.Tag)) return false;

            return PriorityOf(candidate.Tag, candidate.Value) > PriorityOf(other.Tag, other.Value);
        }

        /// <summary>
        /// Does a contribution offer a Main something the Main does not already have?
        ///
        /// 🔴 **This is NOT <see cref="Decide"/>, and the difference is the point.** A three-way
        /// merge settles two versions of one person's file, with an ancestor to say who moved. This
        /// settles somebody else's proposal against a published translation: there is no ancestor,
        /// and the two sides are not equal — **the Main keeps its own on any tie**. Ties are common,
        /// not exceptional: H against H, H against S, S against H all land here.
        ///
        /// ⚠ **A contribution can be a TAG and not a word.** Reading the Main's machine line and
        /// marking it validated changes no text and is precisely the work this site asks for.
        /// Comparing values alone drops every one of them.
        ///
        /// ⚠ The lines the mod's own interface holds are not arbitrated at all, on either side.
        ///
        /// Null <paramref name="main"/> means the Main has no such key — the case with no question
        /// in it, and the only one where a contribution wins without outranking anything.
        /// </summary>
        public static bool ContributionWins(TranslationLine? main, TranslationLine contribution)
        {
            if (!IsGameLine(contribution.Tag)) return false;

            if (!main.HasValue) return true;

            TranslationLine held = main.GetValueOrDefault();

            if (!IsGameLine(held.Tag)) return false;

            // Nothing offered: the same words with the same tag are not a contribution.
            if (Same(held, contribution)) return false;

            return PriorityOf(contribution.Tag, contribution.Value)
                 > PriorityOf(held.Tag, held.Value);
        }

        /// <summary>
        /// The verdict for one key, given what each of the three sides holds for it.
        ///
        /// Null means "this side has no such key" — which is a different thing from an empty value,
        /// and the difference is what separates a deletion from a captured line.
        /// </summary>
        public static MergeDecision Decide(TranslationLine? local, TranslationLine? remote,
                                           TranslationLine? ancestor)
        {
            bool inLocal = local.HasValue;
            bool inRemote = remote.HasValue;
            bool inAncestor = ancestor.HasValue;

            // Added here, and nowhere else.
            if (inLocal && !inRemote && !inAncestor)
                return Decision(MergeVerdict.TakeLocal, MergeReason.LocalOnly);

            // Added there, and nowhere else.
            if (!inLocal && inRemote && !inAncestor)
                return Decision(MergeVerdict.TakeRemote, MergeReason.RemoteAdded);

            // Only the ancestor still has it: both sides removed it.
            if (!inLocal && !inRemote && inAncestor)
                return Decision(MergeVerdict.Drop, MergeReason.Deleted);

            if (inLocal && inRemote)
            {
                TranslationLine here = local.GetValueOrDefault();
                TranslationLine there = remote.GetValueOrDefault();

                if (Same(here, there))
                    return Decision(MergeVerdict.TakeLocal, MergeReason.Unchanged);

                // One outranks the other: settled, and nobody is asked.
                if (CanReplace(there, here))
                    return Decision(MergeVerdict.TakeRemote, MergeReason.RemoteUpdated);

                if (CanReplace(here, there))
                    return Decision(MergeVerdict.TakeLocal, MergeReason.LocalModified);

                // Equal standing: the ancestor is the only thing that can say who moved.
                if (inAncestor)
                {
                    TranslationLine was = ancestor.GetValueOrDefault();

                    if (Same(here, was))
                        return Decision(MergeVerdict.TakeRemote, MergeReason.RemoteUpdated);

                    if (Same(there, was))
                        return Decision(MergeVerdict.TakeLocal, MergeReason.LocalModified);

                    return Conflict(MergeVerdict.TakeRemote, ConflictKind.BothModified);
                }

                // ⚠ Without an ancestor this is NOT "take the newer one": there is no way to tell
                // which side moved, and picking silently would overwrite work at random.
                return Conflict(MergeVerdict.TakeRemote, ConflictKind.NoAncestor);
            }

            // Here and in the ancestor, gone from there.
            if (inLocal && inAncestor)
            {
                return Same(local.GetValueOrDefault(), ancestor.GetValueOrDefault())
                    ? Decision(MergeVerdict.Drop, MergeReason.Deleted)
                    : Conflict(MergeVerdict.TakeLocal, ConflictKind.LocalModifiedRemoteDeleted);
            }

            // There and in the ancestor, gone from here.
            if (inRemote && inAncestor)
            {
                return Same(remote.GetValueOrDefault(), ancestor.GetValueOrDefault())
                    ? Decision(MergeVerdict.Drop, MergeReason.Deleted)
                    : Conflict(MergeVerdict.TakeRemote, ConflictKind.RemoteModifiedLocalDeleted);
            }

            // Nowhere at all. Unreachable for a key that came from one of the three, and answered
            // rather than thrown: a merge that stops halfway leaves a file nobody can use.
            return Decision(inRemote ? MergeVerdict.TakeRemote : MergeVerdict.TakeLocal,
                            MergeReason.Unchanged);
        }

        /// <summary>
        /// Two lines saying the same thing.
        ///
        /// ⚠ Value AND tag: the same words tagged H rather than A is a different fact about the
        /// file — somebody read it — and a merge that treated them as identical would quietly lose
        /// that somebody's review.
        ///
        /// A missing tag reads as "A", which is what the older file format meant by writing none.
        ///
        /// ⚠ Public because "has this line changed" is asked well outside a merge — counting what
        /// somebody edited since the last sync, for one — and answering it a second way is how two
        /// screens end up reporting different numbers about one file.
        /// </summary>
        public static bool Same(TranslationLine a, TranslationLine b) =>
            string.Equals(a.Value, b.Value, StringComparison.Ordinal)
            && string.Equals(Tag(a), Tag(b), StringComparison.Ordinal);

        private static string Tag(TranslationLine line) =>
            string.IsNullOrEmpty(line.Tag) ? "A" : line.Tag;

        private static MergeDecision Decision(MergeVerdict verdict, MergeReason reason) =>
            new MergeDecision { Verdict = verdict, Reason = reason };

        private static MergeDecision Conflict(MergeVerdict show, ConflictKind kind) =>
            new MergeDecision
            {
                Verdict = show,
                IsConflict = true,
                Conflict = kind,
                Reason = MergeReason.Conflict,
            };
    }
}
