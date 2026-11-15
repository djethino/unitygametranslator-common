using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>One line of a file with the place it holds in it: the line, and its <c>i</c> (the capture rank), 0 when it has none.</summary>
    public struct CompletionEntry
    {
        public string Key;
        public TranslationLine Line;

        /// <summary><c>i</c>: kept as it is on a line that stays or is replaced, never guessed for one that is not there.</summary>
        public int Rank;
    }

    /// <summary>What became of a key when a pass completes a translation somebody already made.</summary>
    public enum CompletionVerdict
    {
        /// <summary>Written by a person (H with words, V, S) or the mod's own interface (M): never modified. Only the mark of a term may be added.</summary>
        Kept,

        /// <summary>Nobody validated it (A, an H nothing was written in, no tag) and the pass says the same words: nothing to change but the mark of a term.</summary>
        Same,

        /// <summary>Nobody validated it and the pass says other words: the line of the pass takes its place, its rank kept.</summary>
        Replaced,

        /// <summary>Not in the file: the pass's line comes in, tagged A, at the next rank.</summary>
        New,

        /// <summary>Nobody validated it and the pass has no such key: it stays.</summary>
        KeptUnvalidated,
    }

    /// <summary>One line of the completed file, and what became of it.</summary>
    public struct CompletionLine
    {
        public string Key;
        public CompletionVerdict Verdict;

        /// <summary>The line as it will be written.</summary>
        public TranslationLine Line;

        public int Rank;

        /// <summary>The mark of a term was ADDED to a line that stays (text and tag untouched). Never set on a new or a replaced line: those are counted already.</summary>
        public bool Marked;
    }

    /// <summary>A completion: every line of the file in its order, the new ones after, and the counts the screen announces.</summary>
    public sealed class CompletionResult
    {
        public List<CompletionLine> Lines = new List<CompletionLine>();

        /// <summary>Keys the file did not have.</summary>
        public int NewLines;

        /// <summary>Lines a person validated (H with words, V, S): all of them, in the pass or not.</summary>
        public int ValidatedKept;

        /// <summary>Unvalidated lines the pass replaced.</summary>
        public int Replaced;

        /// <summary>Lines that stay and received the mark of a term.</summary>
        public int Marked;

        /// <summary>
        /// What to ADD to <c>_local_changes</c>: new + replaced + marked. Over-counting protects (the mod recounts exactly at launch),
        /// under-counting does not.
        /// </summary>
        public int LocalChanges => NewLines + Replaced + Marked;

        /// <summary>The format the completed file must be stamped with: that of what it CONTAINS (<see cref="TranslationFormat.OfContent"/>).</summary>
        public int Format;
    }

    /// <summary>
    /// What a pass of UGT Manager's Smart does to a translation somebody already made, decided once (user, 2026-11-15: « ce qui est déjà
    /// validé reste, ce qui n'a jamais été validé peut sauter »).
    ///
    /// 🔴 **Identity is the key, byte for byte, and nothing else.** « j'ai mangé des chips » and « j'ai mangé des tacos » are two lines, and so
    /// are « Left Click » and « Left click »: no notion of « close », nothing is merged, nothing is de-duplicated, and the word « duplicate »
    /// does not exist here. Two different keys are two lines of the file.
    ///
    /// 🔴 **This is not <see cref="Merge.CanReplace"/>.** That one never lets a line take another's place at equal priority, so that nobody
    /// takes the place of anybody. Here the tag A means « never validated, may go », and the pass's line replaces it — a rule of its own,
    /// said as such. What a person validated is never touched (<see cref="Merge.IsByHand"/> is the question: an H with nothing in it is a
    /// capture waiting for a translation, not somebody's work).
    ///
    /// ⚠ **M is not a line of the game** (<see cref="Merge.IsGameLine"/>): it stays and is never marked.
    ///
    /// ⚠ **What survives on a replaced line**: a person's refusals of a link (<c>unlinked</c>) — « this sentence does not name that entry » is
    /// an authority no extraction takes back — are kept beside the pass's. Everything else comes from the pass.
    /// </summary>
    public static class Completion
    {
        /// <param name="mine">The lines of the file, in file order. Keys are unique; a repeated key counts once, at its first place.</param>
        /// <param name="pass">The lines of the pass, in its order.</param>
        /// <param name="termKeys">The keys of the pass that are terms. A key of the file that is one receives the mark.</param>
        /// <param name="holdsGameLists">The completed file holds a game's lists section (<c>_lists</c>): its own, or the pass's brought in.</param>
        public static CompletionResult Complete(IReadOnlyList<CompletionEntry> mine, IReadOnlyList<CompletionEntry> pass,
                                                ISet<string> termKeys, bool holdsGameLists)
        {
            var result = new CompletionResult();
            var passByKey = new Dictionary<string, TranslationLine>(StringComparer.Ordinal);
            foreach (var entry in pass)
                if (!passByKey.ContainsKey(entry.Key)) passByKey[entry.Key] = entry.Line;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            int highest = 0;
            foreach (var entry in mine)
            {
                if (!seen.Add(entry.Key)) continue;
                if (entry.Rank > highest) highest = entry.Rank;
                result.Lines.Add(Settle(entry, passByKey, termKeys, result));
            }

            foreach (var entry in pass)
            {
                if (!seen.Add(entry.Key)) continue;
                var line = entry.Line;
                line.Tag = "A";
                line.BareString = false;
                if (termKeys.Contains(entry.Key)) line.Term = true;
                result.Lines.Add(new CompletionLine { Key = entry.Key, Verdict = CompletionVerdict.New, Line = line, Rank = ++highest });
                result.NewLines++;
            }

            var lines = new List<TranslationLine>(result.Lines.Count);
            foreach (var line in result.Lines) lines.Add(line.Line);
            result.Format = TranslationFormat.OfContent(holdsGameLists, lines);
            return result;
        }

        private static CompletionLine Settle(CompletionEntry entry, Dictionary<string, TranslationLine> passByKey,
                                             ISet<string> termKeys, CompletionResult result)
        {
            var mine = entry.Line;
            bool inPass = passByKey.TryGetValue(entry.Key, out TranslationLine fromPass);
            string tag = string.IsNullOrEmpty(mine.Tag) ? "A" : mine.Tag!;

            // The mod's own interface: not a line of the game, never touched, never marked
            if (!Merge.IsGameLine(tag))
                return new CompletionLine { Key = entry.Key, Verdict = CompletionVerdict.Kept, Line = mine, Rank = entry.Rank };

            if (Merge.IsByHand(tag, mine.Value))
            {
                result.ValidatedKept++;
                return Keep(entry, mine, CompletionVerdict.Kept, inPass && termKeys.Contains(entry.Key), result);
            }

            if (!inPass)
                return new CompletionLine { Key = entry.Key, Verdict = CompletionVerdict.KeptUnvalidated, Line = mine, Rank = entry.Rank };

            if (string.Equals(mine.Value, fromPass.Value, StringComparison.Ordinal))
                return Keep(entry, mine, CompletionVerdict.Same, termKeys.Contains(entry.Key), result);

            var replaced = fromPass;
            replaced.Tag = "A";
            replaced.BareString = false;
            if (termKeys.Contains(entry.Key)) replaced.Term = true;
            replaced.Unlinked = UnionOf(mine.Unlinked, fromPass.Unlinked);
            result.Replaced++;
            return new CompletionLine { Key = entry.Key, Verdict = CompletionVerdict.Replaced, Line = replaced, Rank = entry.Rank };
        }

        /// <summary>A line that stays: text and tag untouched, the mark of a term added when it is one and does not carry it yet.</summary>
        private static CompletionLine Keep(CompletionEntry entry, TranslationLine line, CompletionVerdict verdict, bool isTerm,
                                           CompletionResult result)
        {
            bool mark = isTerm && !line.Term;
            if (mark)
            {
                line.Term = true;
                result.Marked++;
            }
            return new CompletionLine { Key = entry.Key, Verdict = verdict, Line = line, Rank = entry.Rank, Marked = mark };
        }

        private static IReadOnlyList<string>? UnionOf(IReadOnlyList<string>? first, IReadOnlyList<string>? second)
        {
            if (first == null || first.Count == 0) return second;
            var union = new List<string>(first);
            if (second != null)
                foreach (var key in second)
                    if (!union.Contains(key)) union.Add(key);
            return union;
        }

        /// <summary>
        /// What the screen says before writing: « N new lines · M validated lines kept · K unvalidated lines replaced ». The counts are the file's and
        /// the pass's, never a model of a sentence; one is « 1 new line ».
        /// </summary>
        public static string Announcement(CompletionResult result) =>
            Count(result.NewLines, "new line", "new lines") + " · "
            + Count(result.ValidatedKept, "validated line kept", "validated lines kept") + " · "
            + Count(result.Replaced, "unvalidated line replaced", "unvalidated lines replaced");

        private static string Count(int n, string one, string many) =>
            n.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + (n == 1 ? one : many);
    }
}
