using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>What became of a term between two versions of a file (<see cref="Merge.TermsThatDiffer"/>).</summary>
    public enum TermDifference
    {
        /// <summary>Held by both, and not the same.</summary>
        Changed,

        /// <summary>Held by the second only.</summary>
        Added,

        /// <summary>Held by the first only.</summary>
        Removed,
    }

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

        /// <summary>
        /// The machine rewrote the words of a sentence on which a person had set a link: the
        /// person's line is shown and somebody decides. (The machine never replaces a person's decision.)
        /// </summary>
        PersonDecisionLost,
    }

    /// <summary>
    /// Whose fields of the format 2 the merged line carries, when they are not those of the side
    /// whose text was taken. <see cref="None"/> is the whole line of the side the verdict names.
    /// </summary>
    public enum MergeFieldsFrom
    {
        /// <summary>The line is the chosen side's, as it stands.</summary>
        None,

        /// <summary>The text (v, t) is the chosen side's, the fields are the LOCAL line's.</summary>
        Local,

        /// <summary>The text (v, t) is the chosen side's, the fields are the REMOTE line's.</summary>
        Remote,
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

        /// <summary>
        /// Set only when a side of the format 1 is the one taken while the other holds fields of the
        /// format 2: the fields stay, they are not « removed by a side that cannot hold them ».
        /// </summary>
        public MergeFieldsFrom FieldsFrom;

        /// <summary>
        /// The fields kept (<see cref="FieldsFrom"/>) held links, and the taken text is not the text
        /// they were written for: the links cannot follow (no marker of theirs is in it) and the
        /// merged line holds none. Said, so a caller can show it.
        /// </summary>
        public bool LinksDropped;

        /// <summary>
        /// The refused entries (<c>unlinked</c>) the merged line must carry when they are not the
        /// chosen line's own — null when the chosen line's already are the merge. Merged ELEMENT by
        /// element, three ways: a refusal one side added is kept, one a side took back stays taken
        /// back, and a side's refusal is never lost because the other side won the line.
        /// </summary>
        public IReadOnlyList<string>? MergedUnlinked;

        /// <summary>
        /// The links the merged line must carry when they are not the chosen line's own — only
        /// when both lines hold the same words (a link means something for its words alone), null
        /// otherwise. Merged link by link: one side moved it and the other did not, that move
        /// stands; both moved it, the one a person set stands over a machine's.
        /// </summary>
        public IReadOnlyList<LineLink>? MergedLinks;
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
        /// 🔴 **M is the mod's own interface, and it is not one.** It has no author in the sense H
        /// does, it is not what a player came to read, and counting it would inflate every measure
        /// of what a translation covers.
        ///
        /// ⚠ It now has a file of its own (<see cref="ModUi.FileName"/>), so a current mod writes
        /// none of these into a translation at all. This stays, and stays load-bearing, for every
        /// file written before that — and because a rule that only holds while nobody makes a
        /// mistake is not a rule.
        /// </summary>
        public static bool IsGameLine(string tag)
        {
            return tag != ModUi.Tag;
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
        /// <summary>
        /// Whether a person wrote this line, reviewed it, or ruled that it must stay as it is.
        ///
        /// 🔴 **The value matters, and leaving it out is what made this wrong.** A counter asked
        /// the tag alone — H, V or S — and so counted every CAPTURED line as somebody's work: a
        /// capture is tagged H with nothing in it, which is the file's way of saying "met in game,
        /// nobody has written it yet". A game played once in capture-only mode then reported a
        /// hundred and twenty-eight lines "by hand" that nobody had touched.
        ///
        /// ⚠ Read off <see cref="PriorityOf"/> rather than listing tags again: that ladder already
        /// knows an empty H is the bottom of it, and a second list would be a second chance to
        /// forget.
        /// </summary>
        public static bool IsByHand(string tag, string value) => PriorityOf(tag, value) >= 2;

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
                                           TranslationLine? ancestor) =>
            Decide(local, remote, ancestor, 1, 1, 1);

        /// <summary>
        /// The same verdict when the three sides may not be of the same format (draft §5).
        ///
        /// 🔴 **A line « moved » when any field the hash writes moved**, and choosing a side takes
        /// its WHOLE line — v and links never part, because a link means something only for the
        /// words it was written for. Two lines that differ only by a link are therefore two
        /// different lines, settled like two different words.
        ///
        /// 🔴 **A side of the format 1 holds no field, and says nothing about them**: a branch
        /// written before the Main grew its marks must not erase one by not having it. So:
        ///
        ///  · between two LIVE sides, fields are compared only when both are of the format 2;
        ///  · against the ANCESTOR, a format 1 ancestor held NO field (so the fields a side holds
        ///    now are what it added), while a format 1 side is never said to have removed one;
        ///  · when the side taken is of the format 1 and the other holds fields, the merged line
        ///    is the taken side's text WITH the other's fields (<see cref="MergeDecision.FieldsFrom"/>):
        ///    taking a validation or a rewritten word must not cost the term mark and the written
        ///    forms of the line. Only the LINKS cannot follow words they were not written for
        ///    (<see cref="MergeDecision.LinksDropped"/>).
        ///
        /// ⚠ Nothing here says how the merged file is written: the caller still builds it.
        /// </summary>
        /// <param name="localFormat">The format of the local FILE (<see cref="TranslationFormat.OfFile"/>).</param>
        /// <param name="remoteFormat">The format of the remote file.</param>
        /// <param name="ancestorFormat">The format of the ancestor file.</param>
        public static MergeDecision Decide(TranslationLine? local, TranslationLine? remote,
                                           TranslationLine? ancestor,
                                           int localFormat, int remoteFormat, int ancestorFormat)
        {
            var decision = Settle(local, remote, ancestor, localFormat, remoteFormat, ancestorFormat);

            if (decision.Verdict == MergeVerdict.Drop || !local.HasValue || !remote.HasValue) return decision;

            if (localFormat >= 2 && remoteFormat >= 2)
                KeepPeopleDecisions(ref decision, local.GetValueOrDefault(), remote.GetValueOrDefault(), ancestor, ancestorFormat);

            bool takesLocal = decision.Verdict == MergeVerdict.TakeLocal;
            TranslationLine taken = takesLocal ? local.GetValueOrDefault() : remote.GetValueOrDefault();
            TranslationLine other = takesLocal ? remote.GetValueOrDefault() : local.GetValueOrDefault();
            int takenFormat = takesLocal ? localFormat : remoteFormat;
            int otherFormat = takesLocal ? remoteFormat : localFormat;

            if (takenFormat < 2 && otherFormat >= 2 && other.HasFormat2Fields)
            {
                decision.FieldsFrom = takesLocal ? MergeFieldsFrom.Remote : MergeFieldsFrom.Local;
                decision.LinksDropped = other.Links != null && other.Links.Count > 0
                    && !string.Equals(taken.Value, other.Value, StringComparison.Ordinal);
            }

            return decision;
        }

        /// <summary>
        /// 🔴 **The machine never replaces a decision of a person**, whatever won the line: what a
        /// person decided on a line that is NOT in the text — the entries they refused, the links
        /// they set — is merged element by element, not carried away with the line the verdict
        /// names. Only lines of the format 2 on both sides: a format 1 side holds none.
        /// </summary>
        private static void KeepPeopleDecisions(ref MergeDecision decision, TranslationLine local, TranslationLine remote,
                                                TranslationLine? ancestor, int ancestorFormat)
        {
            bool takesLocal = decision.Verdict == MergeVerdict.TakeLocal;
            TranslationLine chosen = takesLocal ? local : remote;
            TranslationLine other = takesLocal ? remote : local;
            bool sameWords = string.Equals(chosen.Value, other.Value, StringComparison.Ordinal);

            // The machine rewrote the words of a sentence a person had linked: not its call.
            if (!decision.IsConflict && !sameWords && Tag(chosen) == "A" && HasPersonLink(other))
            {
                decision.Verdict = takesLocal ? MergeVerdict.TakeRemote : MergeVerdict.TakeLocal;
                decision.IsConflict = true;
                decision.Conflict = ConflictKind.PersonDecisionLost;
                decision.Reason = MergeReason.Conflict;
                takesLocal = !takesLocal;
                TranslationLine swap = chosen; chosen = other; other = swap;
            }

            TranslationLine? was = ancestor.HasValue && ancestorFormat >= 2 ? ancestor : null;

            // Refused entries: a set, merged three ways.
            var merged = new List<string>();
            AddMerged(merged, chosen.Unlinked, other.Unlinked, was.HasValue ? was.Value.Unlinked : null);
            AddMerged(merged, other.Unlinked, chosen.Unlinked, was.HasValue ? was.Value.Unlinked : null);
            merged.Sort(StringComparer.Ordinal);
            if (!SameSet(merged, chosen.Unlinked)) decision.MergedUnlinked = merged;

            // Links: only for the same words, and the same number of them.
            if (sameWords && CountOf(chosen.Links) == CountOf(other.Links) && CountOf(chosen.Links) > 0)
            {
                bool ancestorKnows = was.HasValue && string.Equals(was.Value.Value, chosen.Value, StringComparison.Ordinal)
                                     && CountOf(was.Value.Links) == CountOf(chosen.Links);
                var links = new List<LineLink>();
                bool differs = false;
                for (int i = 0; i < chosen.Links!.Count; i++)
                {
                    LineLink c = chosen.Links[i], o = other.Links![i];
                    LineLink pick = c;
                    if (!SameLink(c, o))
                    {
                        bool ancestorLink = ancestorKnows;
                        LineLink a = ancestorKnows ? was!.Value.Links![i] : default;
                        if (ancestorLink && SameLink(c, a)) pick = o;
                        else if (ancestorLink && SameLink(o, a)) pick = c;
                        else if (o.By == LinkReading.ByPerson && c.By != LinkReading.ByPerson) pick = o;
                    }
                    if (!SameLink(pick, c)) differs = true;
                    links.Add(pick);
                }
                if (differs) decision.MergedLinks = links;
            }
        }

        private static void AddMerged(List<string> into, IReadOnlyList<string>? side, IReadOnlyList<string>? otherSide, IReadOnlyList<string>? was)
        {
            if (side == null) return;
            foreach (string x in side)
            {
                if (into.Contains(x)) continue;
                bool inOther = Has(otherSide, x), inWas = Has(was, x);
                // Kept when both hold it, or when it is new (not in the ancestor); dropped when the
                // ancestor held it and the other side took it back.
                if (inOther || !inWas) into.Add(x);
            }
        }

        private static bool Has(IReadOnlyList<string>? list, string x)
        {
            if (list == null) return false;
            foreach (string item in list) if (string.Equals(item, x, StringComparison.Ordinal)) return true;
            return false;
        }

        private static int CountOf<T>(IReadOnlyList<T>? list) => list == null ? 0 : list.Count;

        private static bool HasPersonLink(TranslationLine line)
        {
            if (line.Links != null)
                foreach (var link in line.Links) if (link.By == LinkReading.ByPerson) return true;
            return false;
        }

        private static bool SameLink(LineLink a, LineLink b) =>
            a.To == b.To && a.Form == b.Form && a.Case == b.Case && a.By == b.By && a.Was == b.Was && a.Own == b.Own;

        private static bool SameSet(List<string> merged, IReadOnlyList<string>? held)
        {
            if (merged.Count != CountOf(held)) return false;
            foreach (string x in merged) if (!Has(held, x)) return false;
            return true;
        }

        private static MergeDecision Settle(TranslationLine? local, TranslationLine? remote,
                                            TranslationLine? ancestor,
                                            int localFormat, int remoteFormat, int ancestorFormat)
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

                if (Same(here, localFormat, there, remoteFormat))
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

                    if (SameAsAncestor(here, localFormat, was, ancestorFormat))
                        return Decision(MergeVerdict.TakeRemote, MergeReason.RemoteUpdated);

                    if (SameAsAncestor(there, remoteFormat, was, ancestorFormat))
                        return Decision(MergeVerdict.TakeLocal, MergeReason.LocalModified);

                    return Conflict(ShownWhenBothMoved(localFormat, remoteFormat), ConflictKind.BothModified);
                }

                // ⚠ Without an ancestor this is NOT "take the newer one": there is no way to tell
                // which side moved, and picking silently would overwrite work at random.
                return Conflict(ShownWhenBothMoved(localFormat, remoteFormat), ConflictKind.NoAncestor);
            }

            // Here and in the ancestor, gone from there.
            if (inLocal && inAncestor)
            {
                return SameAsAncestor(local.GetValueOrDefault(), localFormat, ancestor.GetValueOrDefault(), ancestorFormat)
                    ? Decision(MergeVerdict.Drop, MergeReason.Deleted)
                    : Conflict(MergeVerdict.TakeLocal, ConflictKind.LocalModifiedRemoteDeleted);
            }

            // There and in the ancestor, gone from here.
            if (inRemote && inAncestor)
            {
                return SameAsAncestor(remote.GetValueOrDefault(), remoteFormat, ancestor.GetValueOrDefault(), ancestorFormat)
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
            SameText(a, b) && TranslationLine.SameFields(a, b);

        /// <summary>
        /// Two lines of two live files saying the same thing, each file of its own format: the text
        /// and the tag always, the fields only when BOTH files hold fields (a format 1 file has no
        /// opinion about them; see the six-argument <see cref="Decide(TranslationLine?, TranslationLine?, TranslationLine?, int, int, int)"/>).
        /// </summary>
        public static bool Same(TranslationLine a, int aFormat, TranslationLine b, int bFormat) =>
            SameText(a, b) && (aFormat < 2 || bFormat < 2 || TranslationLine.SameFields(a, b));

        /// <summary>
        /// A line against the ancestor of its own lineage: the text and the tag, and the fields,
        /// except that a format 1 LINE cannot be said to have removed any, while a format 1
        /// ANCESTOR held none, so whatever a format 2 line holds against it was added since.
        /// </summary>
        public static bool SameAsAncestor(TranslationLine line, int lineFormat, TranslationLine ancestor, int ancestorFormat)
        {
            if (!SameText(line, ancestor)) return false;
            if (lineFormat < 2) return true;
            return ancestorFormat >= 2 ? TranslationLine.SameFields(line, ancestor) : !line.HasFormat2Fields;
        }

        private static bool SameText(TranslationLine a, TranslationLine b) =>
            string.Equals(a.Value, b.Value, StringComparison.Ordinal)
            && string.Equals(Tag(a), Tag(b), StringComparison.Ordinal);

        private static string Tag(TranslationLine line) =>
            string.IsNullOrEmpty(line.Tag) ? "A" : line.Tag;

        /// <summary>
        /// The terms that differ between two versions of a file, one entry per key and what became of it — what a screen that says
        /// « N terms » over two files counts, with the rule the merge grid already reads (<see cref="Same(TranslationLine, int, TranslationLine, int)"/>).
        ///
        /// A key is a TERM when either side holds it with the mark (<see cref="TranslationLine.Term"/>). Held by both and not the same
        /// (the words, the tag, and the fields when both files are of the format 2): <see cref="TermDifference.Changed"/>. Held by
        /// <paramref name="theirs"/> only: <see cref="TermDifference.Added"/>. Held by <paramref name="mine"/> only:
        /// <see cref="TermDifference.Removed"/>. A sentence (its links, its refused entries) is never listed, nor a line that is a term
        /// on neither side.
        ///
        /// ⚠ It does not say which kinds a notification adds up: that is the caller's, and it is the answer to « how does that
        /// notification count its lines » (N terms are the terms among THOSE lines, never another count). Order: the keys of
        /// <paramref name="theirs"/> in their order, then the removed ones in the order of <paramref name="mine"/>.
        /// </summary>
        public static List<KeyValuePair<string, TermDifference>> TermsThatDiffer(
            IReadOnlyList<KeyValuePair<string, TranslationLine>> mine, int mineFormat,
            IReadOnlyList<KeyValuePair<string, TranslationLine>> theirs, int theirsFormat)
        {
            var result = new List<KeyValuePair<string, TermDifference>>();
            var mineByKey = new Dictionary<string, TranslationLine>(StringComparer.Ordinal);
            foreach (var line in mine) mineByKey[line.Key] = line.Value;
            var theirKeys = new HashSet<string>(StringComparer.Ordinal);

            foreach (var line in theirs)
            {
                theirKeys.Add(line.Key);
                if (!mineByKey.TryGetValue(line.Key, out TranslationLine mineLine))
                {
                    if (line.Value.Term) result.Add(new KeyValuePair<string, TermDifference>(line.Key, TermDifference.Added));
                    continue;
                }
                if ((line.Value.Term || mineLine.Term) && !Same(mineLine, mineFormat, line.Value, theirsFormat))
                    result.Add(new KeyValuePair<string, TermDifference>(line.Key, TermDifference.Changed));
            }

            foreach (var line in mine)
                if (line.Value.Term && !theirKeys.Contains(line.Key))
                    result.Add(new KeyValuePair<string, TermDifference>(line.Key, TermDifference.Removed));

            return result;
        }

        private static MergeDecision Decision(MergeVerdict verdict, MergeReason reason) =>
            new MergeDecision { Verdict = verdict, Reason = reason };

        /// <summary>
        /// The side a conflict between two changed lines OFFERS first (the person still decides). The other side's, as always —
        /// except a file of the format 2 held here against a format 1 on the other side: the format 2 replaces the format 1, so
        /// its line is offered first (decided by the user, 2026-11-22: a format 1 line must not quietly win over the work of a
        /// format 2 file).
        /// </summary>
        private static MergeVerdict ShownWhenBothMoved(int localFormat, int remoteFormat) =>
            localFormat >= 2 && remoteFormat < 2 ? MergeVerdict.TakeLocal : MergeVerdict.TakeRemote;

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
