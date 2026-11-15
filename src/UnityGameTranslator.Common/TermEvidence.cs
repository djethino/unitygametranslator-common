using System;
using System.Collections.Generic;
using System.Linq;
using static UnityGameTranslator.Common.LinkFinding;

namespace UnityGameTranslator.Common
{
    public enum TermEvidenceVerdict
    {
        /// <summary>No validated line names the term: nothing to say, the question stays.</summary>
        None,

        /// <summary>Validated lines name the term, and no writing is held by two of them: too thin to decide.</summary>
        Thin,

        /// <summary>One writing is held by the most lines: the term's word (<see cref="TermEvidenceResult.Rivals"/> says the contradiction, if any).</summary>
        Word,

        /// <summary>Two writings are held by as many lines, and as many of them by hand (H): nothing decides. The pass's word is written and marked to read again.</summary>
        Tied,

        /// <summary>Another term receives the very same writing: a block of a sentence shared by several terms, not a word. The question stays, for every one of them.</summary>
        Ambiguous,
    }

    /// <summary>A writing of the target language, and the keys of the validated lines that carry it.</summary>
    public sealed class TermWriting
    {
        public string Words = "";
        public List<string> Keys = new List<string>();
    }

    /// <summary>What a person's own lines say about how a term is written.</summary>
    public sealed class TermEvidenceResult
    {
        /// <summary>The source form of the term.</summary>
        public string Term = "";

        public TermEvidenceVerdict Verdict;

        /// <summary>The writing, for <see cref="TermEvidenceVerdict.Word"/> and <see cref="TermEvidenceVerdict.Ambiguous"/>; null otherwise.</summary>
        public string? Word;

        /// <summary>The keys of the lines that carry <see cref="Word"/>, in the file's order.</summary>
        public List<string> Keys = new List<string>();

        /// <summary>
        /// The contradiction: the other writings the lines of the term carry. For <see cref="TermEvidenceVerdict.Word"/>, the best of the lines
        /// that do not carry <see cref="Word"/> (at most one); for <see cref="TermEvidenceVerdict.Tied"/>, each of the writings that tie.
        /// </summary>
        public List<TermWriting> Rivals = new List<TermWriting>();

        public bool Contradicted => Rivals.Count > 0;
    }

    /// <summary>
    /// How a person's own lines write a term, found with no cutting of a text into words — so the same for a language that spaces its words and one
    /// that does not (decided with UGT Manager, 2026-11-15; measured on French and on Japanese).
    ///
    /// 🔴 **A person's lines are never rewritten to agree** (user): this only reads them, to decide what the pass writes for the term and to tell a
    /// contradiction. The rule:
    ///
    ///  · the evidence E is the validated lines (H or V, with words) whose KEY holds the term as a word (<see cref="LinkFinding.WrittenIn"/>: word
    ///    edges, first letter in either case);
    ///  · a candidate is a stretch of a line of E that begins where a word may begin and ends where a word may end (<see cref="WordGlue"/>; no
    ///    white space at its ends; no length written in a constant — a script with no spaces has an edge at every place) that at least 2 lines of E
    ///    hold (the same way), and that is CONFINED: more of the validated lines of the whole file that hold it are lines of E than are not
    ///    (« de » is held everywhere, a term's word is not);
    ///  · the candidates that the very same lines hold are one writing: the longest, spelled the way most of its lines spell it;
    ///  · the writing held by the most lines wins; at the same count the one more lines of which are by hand (H); at the same again,
    ///    <see cref="TermEvidenceVerdict.Tied"/>;
    ///  · the rival is the same search among the lines of E that do not carry the winner.
    ///
    /// ⚠ **Not measured**: the agreement of a word (« disponibles » is not « disponible »: the plural is a line that does not hold the singular,
    /// and a residue of such lines can make a false rival of at least 2), and a candidate that CONTAINS another term's word (« réservoir de
    /// carburant » for one term, « carburant » for another) is not told apart from an ordinary writing.
    /// </summary>
    public static class TermEvidence
    {
        /// <param name="term">The term as the game writes it (its source form).</param>
        /// <param name="validated">The lines of the WHOLE file, key and line. Only those a person validated and wrote words in (H or V, a value) are read.</param>
        public static TermEvidenceResult Of(string term, IReadOnlyList<KeyValuePair<string, TranslationLine>> validated)
        {
            var result = new TermEvidenceResult { Term = term ?? "" };
            var lines = ValidatedLines(validated);

            var evidence = new List<int>();
            for (int i = 0; i < lines.Count; i++)
                if (!string.IsNullOrEmpty(term) && Holds(lines[i].Key, term!)) evidence.Add(i);
            if (evidence.Count == 0)
            {
                result.Verdict = TermEvidenceVerdict.None;
                return result;
            }

            var candidates = Candidates(lines, evidence);
            if (candidates.Count == 0)
            {
                result.Verdict = TermEvidenceVerdict.Thin;
                return result;
            }

            var groups = Groups(candidates, lines, inside: null);
            int most = groups.Max(g => g.Lines.Count);
            var top = groups.Where(g => g.Lines.Count == most).ToList();
            int mostByHand = top.Max(g => g.ByHand);
            top = top.Where(g => g.ByHand == mostByHand).ToList();

            if (top.Count > 1)
            {
                result.Verdict = TermEvidenceVerdict.Tied;
                foreach (var tied in top.OrderBy(g => g.Lines.Min())) result.Rivals.Add(Writing(tied, lines));
                return result;
            }

            var winner = top[0];
            result.Verdict = TermEvidenceVerdict.Word;
            var word = Writing(winner, lines);
            result.Word = word.Words;
            result.Keys = word.Keys;

            // The best writing among the lines of the term that do not carry the winner
            var rest = new HashSet<int>(evidence.Where(i => !winner.Lines.Contains(i)));
            if (rest.Count >= MinCount)
            {
                var rivals = Groups(candidates, lines, rest);
                if (rivals.Count > 0)
                {
                    int rivalMost = rivals.Max(g => g.Lines.Count);
                    var best = rivals.Where(g => g.Lines.Count == rivalMost).OrderByDescending(g => g.ByHand)
                                     .ThenByDescending(g => g.Words.Length).ThenBy(g => g.Words, StringComparer.Ordinal).First();
                    result.Rivals.Add(Writing(best, lines));
                }
            }

            return result;
        }

        /// <summary>
        /// <see cref="Of"/> for every term of a file, and the guard that needs them all: two terms that receive the very same writing are
        /// both <see cref="TermEvidenceVerdict.Ambiguous"/> — a block of a sentence they share, which is not the word of either.
        /// </summary>
        public static List<TermEvidenceResult> OfAll(IReadOnlyList<string> terms, IReadOnlyList<KeyValuePair<string, TranslationLine>> validated)
        {
            var results = terms.Select(term => Of(term, validated)).ToList();
            var received = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var r in results)
                if (r.Verdict == TermEvidenceVerdict.Word)
                {
                    string key = Casing.LowerFirst(r.Word) ?? "";
                    received[key] = received.TryGetValue(key, out int n) ? n + 1 : 1;
                }
            foreach (var r in results)
                if (r.Verdict == TermEvidenceVerdict.Word && received[Casing.LowerFirst(r.Word) ?? ""] > 1)
                    r.Verdict = TermEvidenceVerdict.Ambiguous;
            return results;
        }

        private const int MinCount = 2;

        private struct Line
        {
            public string Key;
            public string Text;
            public bool ByHand;
        }

        private sealed class Candidate
        {
            public string Words = "";
            public List<int> Lines = new List<int>();
        }

        private sealed class Group
        {
            public HashSet<int> Lines = new HashSet<int>();
            public string Words = "";
            public int ByHand;
        }

        private static List<Line> ValidatedLines(IReadOnlyList<KeyValuePair<string, TranslationLine>> validated)
        {
            var lines = new List<Line>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in validated)
            {
                var line = pair.Value;
                if ((line.Tag != "H" && line.Tag != "V") || string.IsNullOrEmpty(line.Value) || !seen.Add(pair.Key)) continue;
                lines.Add(new Line { Key = pair.Key, Text = line.Value, ByHand = line.Tag == "H" });
            }
            return lines;
        }

        /// <summary>Does the text hold the writing as a word: at word edges, the first letter in either case (the rule that finds a link).</summary>
        private static bool Holds(string text, string words) =>
            WrittenIn(text, new[] { new LinkWriting { Words = words } }, null).Count > 0;

        /// <summary>
        /// The stretches of 2 to 24 characters that at least two lines of the evidence hold and that are confined to it, each with the lines of
        /// the evidence that hold it. A stretch is looked for with its first letter lowered, so « Roue » and « roue » are one candidate.
        /// </summary>
        private static List<Candidate> Candidates(List<Line> lines, List<int> evidence)
        {
            var raw = new Dictionary<string, HashSet<int>>(StringComparer.Ordinal);
            foreach (int i in evidence)
            {
                string text = lines[i].Text;
                // From a place a word may begin to a place a word may end (WordGlue): in a script that spaces its words that is the
                // words and the phrases made of them; in one that does not, every place is an edge, and the confinement sorts it out
                for (int start = 0; start < text.Length; start = Next(text, start))
                {
                    if (KeyChars.IsSpace(text[start]) || !WordGlue.EdgeBefore(text, start)) continue;
                    for (int end = Next(text, start); end <= text.Length; end = end < text.Length ? Next(text, end) : text.Length + 1)
                    {
                        if (KeyChars.IsSpace(text[end - 1]) || !WordGlue.EdgeAfter(text, end)) continue;
                        string stretch = Casing.LowerFirst(text.Substring(start, end - start)) ?? "";
                        if (!raw.TryGetValue(stretch, out var set)) raw[stretch] = set = new HashSet<int>();
                        set.Add(i);
                    }
                }
            }

            var found = new List<Candidate>();
            foreach (var pair in raw)
            {
                if (pair.Value.Count < MinCount) continue;
                var holding = evidence.Where(i => Holds(lines[i].Text, pair.Key)).ToList();
                if (holding.Count < MinCount) continue;

                var inEvidence = new HashSet<int>(holding);
                int everywhere = 0;
                for (int i = 0; i < lines.Count; i++)
                    if (inEvidence.Contains(i) || Holds(lines[i].Text, pair.Key)) everywhere++;
                // confined: a MAJORITY of the lines that hold it are lines of the term's evidence (a word the term's lines share with the rest
                // of the file is not the term's; the measure is flat from a third to two thirds)
                if (2 * holding.Count <= everywhere) continue;

                found.Add(new Candidate { Words = pair.Key, Lines = holding });
            }
            return found;
        }

        /// <summary>The next position of a text after the character (the code point) at <paramref name="at"/>.</summary>
        private static int Next(string text, int at) =>
            at < text.Length && char.IsHighSurrogate(text[at]) && at + 1 < text.Length && char.IsLowSurrogate(text[at + 1]) ? at + 2 : at + 1;

        /// <summary>
        /// The candidates the very same lines hold, as writings: one group per set of lines — the longest candidate speaks for it. With
        /// <paramref name="inside"/>, only the lines of that set count (the search for a rival).
        /// </summary>
        private static List<Group> Groups(List<Candidate> candidates, List<Line> lines, HashSet<int>? inside)
        {
            var byLines = new Dictionary<string, Group>(StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                var held = inside == null ? candidate.Lines : candidate.Lines.Where(inside.Contains).ToList();
                if (held.Count < MinCount) continue;
                string id = string.Join(",", held.OrderBy(i => i));
                if (!byLines.TryGetValue(id, out var group))
                {
                    group = new Group { Lines = new HashSet<int>(held), ByHand = held.Count(i => lines[i].ByHand) };
                    byLines[id] = group;
                }
                if (CodePoints(candidate.Words) > CodePoints(group.Words)
                    || (CodePoints(candidate.Words) == CodePoints(group.Words) && string.CompareOrdinal(candidate.Words, group.Words) < 0))
                    group.Words = candidate.Words;
            }
            return byLines.Values.ToList();
        }

        private static int CodePoints(string text)
        {
            int n = 0;
            for (int at = 0; at < text.Length; at = Next(text, at)) n++;
            return n;
        }

        /// <summary>The group as a writing: spelled the way most of its lines spell it (first letter in the case they write), its keys in the file's order.</summary>
        private static TermWriting Writing(Group group, List<Line> lines)
        {
            string lower = group.Words, upper = Casing.UpperFirst(lower) ?? lower;
            var ordered = group.Lines.OrderBy(i => i).ToList();
            int lowerLines = ordered.Count(i => lines[i].Text.IndexOf(lower, StringComparison.Ordinal) >= 0);
            int upperLines = lower == upper ? 0 : ordered.Count(i => lines[i].Text.IndexOf(upper, StringComparison.Ordinal) >= 0);
            string spelled = upperLines > lowerLines ? upper : lower;
            if (upperLines == lowerLines && lower != upper)
            {
                // the first line of the group decides
                string first = lines[ordered[0]].Text;
                int atLower = first.IndexOf(lower, StringComparison.Ordinal), atUpper = first.IndexOf(upper, StringComparison.Ordinal);
                spelled = atUpper >= 0 && (atLower < 0 || atUpper < atLower) ? upper : lower;
            }
            var writing = new TermWriting { Words = spelled };
            foreach (int i in ordered) writing.Keys.Add(lines[i].Key);
            return writing;
        }
    }
}
