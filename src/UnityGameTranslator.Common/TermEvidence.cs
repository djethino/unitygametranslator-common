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

        /// <summary>
        /// The word was read in the MACHINE's lines (tag A), no person's line deciding (<see cref="TermEvidence.OfAll(IReadOnlyList{string}, IReadOnlyList{KeyValuePair{string, TranslationLine}}, bool)"/>):
        /// never said to be « written by a person ».
        /// </summary>
        public bool FromMachine;
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
            var lines = ValidatedLines(validated);
            return OfLines(term, lines, new Grams(lines));
        }

        private static TermEvidenceResult OfLines(string term, List<Line> lines, Grams grams)
        {
            var result = new TermEvidenceResult { Term = term ?? "" };

            var evidence = new List<int>();
            if (!string.IsNullOrEmpty(term))
                foreach (int i in grams.Holding(term!, keys: true))
                    if (Holds(lines[i].Key, term!)) evidence.Add(i);
            if (evidence.Count == 0)
            {
                result.Verdict = TermEvidenceVerdict.None;
                return result;
            }

            var candidates = Candidates(lines, evidence, grams);
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
            var lines = ValidatedLines(validated);
            var grams = new Grams(lines);
            return MarkShared(terms.Select(term => OfLines(term, lines, grams)).ToList());
        }

        /// <summary>
        /// <see cref="OfAll(IReadOnlyList{string}, IReadOnlyList{KeyValuePair{string, TranslationLine}})"/>, then, with <paramref name="machineLines"/>, the
        /// MACHINE's lines (tag A, or none) for a term no person's line decides (<see cref="TermEvidenceVerdict.None"/> or <see cref="TermEvidenceVerdict.Thin"/>)
        /// — a file translated while playing, optimised later (user, 2026-11-24: « dans les A seulement si elles sont nettement d'accord entre elles,
        /// sinon l'IA, une fois par terme »). Clearly agreeing: the same search finds ONE writing held by at least two of those lines, with NO rival and
        /// no other term receiving it, and every line of a person that names the term (too few to decide alone) holding it; anything less leaves the term
        /// as it was (the question stays). A person's verdict is never changed by the machine's lines, and such a word is marked
        /// <see cref="TermEvidenceResult.FromMachine"/>.
        /// </summary>
        public static List<TermEvidenceResult> OfAll(IReadOnlyList<string> terms, IReadOnlyList<KeyValuePair<string, TranslationLine>> lines, bool machineLines)
        {
            var results = OfAll(terms, lines);
            if (!machineLines) return results;
            var machine = MachineLines(lines);
            if (machine.Count == 0) return results;
            var person = ValidatedLines(lines);
            var machineGrams = new Grams(machine);
            var machineText = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var m in machine) machineText[m.Key] = m.Text;

            var taken = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in results)
                if (r.Word != null) taken.Add(Casing.LowerFirst(r.Word) ?? "");
            var read = new List<TermEvidenceResult>();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].Verdict != TermEvidenceVerdict.None && results[i].Verdict != TermEvidenceVerdict.Thin) continue;
                var fromMachine = OfLines(results[i].Term, machine, machineGrams);
                if (fromMachine.Verdict != TermEvidenceVerdict.Word || fromMachine.Contradicted || taken.Contains(Casing.LowerFirst(fromMachine.Word) ?? "")) continue;
                // too few of a person's lines to decide is still what a person wrote: a machine's word that one of them does not hold is no word
                string term = results[i].Term;
                if (person.Any(p => !string.IsNullOrEmpty(term) && Holds(p.Key, term) && !Holds(p.Text, fromMachine.Word!))) continue;
                // CLEARLY agreeing, measured on two files (2026-11-24, 2 to 3 wrong words of 38 with « the most lines » alone: a phrase two lines
                // share, a compound of a language that glues its words): more than half of the machine's lines that name the term hold it…
                int naming = machineGrams.Holding(term, keys: true).Count(m => Holds(machine[m].Key, term));
                if (fromMachine.Keys.Count * 2 <= naming) continue;
                // …and the lines that hold it do not all hold the term beside the same other word (first real optimisation, 2026-11-24: `FPS Counter` and `Advanced
                // FPS Counter` gave « FPS-räknare » to `Counter`, `Additional Localization Support` the whole « Ytterligare lokaliseringsstöd » to `Support`, `Error loading
                // PlayerPrefs` « av PlayerPrefs »): what such lines share is the translation of the phrase, and nothing tells which part is the term's
                if (SharedNeighbour(term, fromMachine.Keys.ToList())) continue;
                // …and the machine's own line of the term alone, when the file has one, writes it (first letter in either case)
                if (machineText.TryGetValue(term, out var own) && !string.Equals(Casing.LowerFirst(own), Casing.LowerFirst(fromMachine.Word), StringComparison.Ordinal)) continue;
                fromMachine.FromMachine = true;
                results[i] = fromMachine;
                read.Add(fromMachine);
            }
            // two terms the machine's lines give the very same writing: a block they share, the word of neither — both stay questions
            foreach (var r in read)
                if (read.Count(o => string.Equals(Casing.LowerFirst(o.Word), Casing.LowerFirst(r.Word), StringComparison.Ordinal)) > 1)
                    results[results.IndexOf(r)] = new TermEvidenceResult { Term = r.Term, Verdict = TermEvidenceVerdict.Thin };
            return results;
        }

        /// <summary>How near a word must stand to the term to be its companion: within two words of it, on either side (« Additional Localization Support »).</summary>
        private const int NeighbourReach = 2;

        /// <summary>
        /// Whether every key holds the term with the same other word within <see cref="NeighbourReach"/> words of it. A word is a run of letters, digits and marks (Unicode's
        /// categories), compared in small letters (<see cref="Casing.Lower"/>); a key whose words do not hold the term's own words in a row (a script written without spaces)
        /// gives no neighbour, so it never shares one.
        /// </summary>
        private static bool SharedNeighbour(string term, List<string> keys)
        {
            var termWords = WordsOf(term);
            if (termWords.Count == 0 || keys.Count == 0) return false;
            HashSet<string>? shared = null;
            foreach (string key in keys)
            {
                var words = WordsOf(key);
                var near = new HashSet<string>(StringComparer.Ordinal);
                for (int at = 0; at + termWords.Count <= words.Count; at++)
                {
                    bool here = true;
                    for (int k = 0; k < termWords.Count && here; k++) here = words[at + k] == termWords[k];
                    if (!here) continue;
                    for (int k = Math.Max(0, at - NeighbourReach); k < Math.Min(words.Count, at + termWords.Count + NeighbourReach); k++)
                        if (k < at || k >= at + termWords.Count) near.Add(words[k]);
                }
                near.ExceptWith(termWords);
                if (shared == null) shared = near; else shared.IntersectWith(near);
                if (shared.Count == 0) return false;
            }
            return shared!.Count > 0;
        }

        /// <summary>The words of a text in small letters: each run of letters, digits and combining marks.</summary>
        private static List<string> WordsOf(string text)
        {
            var words = new List<string>();
            int start = -1;
            for (int i = 0; i <= text.Length; i = i < text.Length ? Next(text, i) : i + 1)
            {
                bool inWord = false;
                if (i < text.Length)
                {
                    string category = UnicodeCategories.Of(text, i);
                    inWord = category[0] == 'L' || category[0] == 'N' || category[0] == 'M';
                }
                if (inWord && start < 0) start = i;
                else if (!inWord && start >= 0) { words.Add(Casing.Lower(text.Substring(start, i - start))!); start = -1; }
            }
            return words;
        }

        private static List<TermEvidenceResult> MarkShared(List<TermEvidenceResult> results)
        {
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

        /// <summary>The machine's lines with words (tag A, or no tag), each key once; read like a person's, none by hand.</summary>
        private static List<Line> MachineLines(IReadOnlyList<KeyValuePair<string, TranslationLine>> all)
        {
            var lines = new List<Line>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in all)
            {
                var line = pair.Value;
                if ((line.Tag != null && line.Tag != "A") || string.IsNullOrEmpty(line.Value) || !seen.Add(pair.Key)) continue;
                lines.Add(new Line { Key = pair.Key, Text = line.Value, ByHand = false });
            }
            return lines;
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
        private static List<Candidate> Candidates(List<Line> lines, List<int> evidence, Grams grams)
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

                // confined: a MAJORITY of the lines that hold it are lines of the term's evidence (a word the term's lines share with the rest
                // of the file is not the term's; the measure is flat from a third to two thirds). Counted on the lines that CAN hold it
                // (Grams), and no further than the answer: once as many lines outside the evidence hold it, it is not confined.
                var inEvidence = new HashSet<int>(holding);
                int outside = 0;
                foreach (int i in grams.Holding(pair.Key, keys: false))
                    if (!inEvidence.Contains(i) && Holds(lines[i].Text, pair.Key) && ++outside >= holding.Count) break;
                if (outside >= holding.Count) continue;

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

        /// <summary>
        /// Which lines CAN hold a stretch, so that the exact test (<see cref="Holds"/>, word edges, first letter in either case) is asked of them only:
        /// a stretch written in a text holds every pair of UTF-16 units of the stretch after its first character, so the lines that hold the rarest such
        /// pair are a superset of the answer (a stretch of one or two units: the lines holding its last unit, or its first in either case). Built once
        /// per set of lines; a necessary condition only — it never decides.
        /// </summary>
        private sealed class Grams
        {
            private readonly Dictionary<long, List<int>> _keyPairs = new Dictionary<long, List<int>>();
            private readonly Dictionary<long, List<int>> _textPairs = new Dictionary<long, List<int>>();
            private readonly Dictionary<char, List<int>> _keyUnits = new Dictionary<char, List<int>>();
            private readonly Dictionary<char, List<int>> _textUnits = new Dictionary<char, List<int>>();
            private readonly int _count;

            public Grams(List<Line> lines)
            {
                _count = lines.Count;
                for (int i = 0; i < lines.Count; i++)
                {
                    Index(lines[i].Key, i, _keyPairs, _keyUnits);
                    Index(lines[i].Text, i, _textPairs, _textUnits);
                }
            }

            private static void Index(string text, int line, Dictionary<long, List<int>> pairs, Dictionary<char, List<int>> units)
            {
                for (int k = 0; k < text.Length; k++)
                {
                    Add(units, text[k], line);
                    if (k + 1 < text.Length) Add(pairs, ((long)text[k] << 16) | text[k + 1], line);
                }
            }

            private static void Add<T>(Dictionary<T, List<int>> map, T at, int line) where T : notnull
            {
                if (!map.TryGetValue(at, out var list)) map[at] = list = new List<int>();
                if (list.Count == 0 || list[list.Count - 1] != line) list.Add(line);
            }

            private static readonly List<int> NoLine = new List<int>();

            /// <summary>The lines, in order, that can hold <paramref name="stretch"/> in their key or their text.</summary>
            public IEnumerable<int> Holding(string stretch, bool keys)
            {
                var pairs = keys ? _keyPairs : _textPairs;
                var units = keys ? _keyUnits : _textUnits;
                if (stretch.Length >= 3)
                {
                    List<int>? rarest = null;
                    for (int k = 1; k + 1 < stretch.Length; k++)
                    {
                        if (!pairs.TryGetValue(((long)stretch[k] << 16) | stretch[k + 1], out var list)) return NoLine;
                        if (rarest == null || list.Count < rarest.Count) rarest = list;
                    }
                    return rarest!;
                }
                if (stretch.Length == 2) return units.TryGetValue(stretch[1], out var last) ? last : NoLine;
                // one unit: the lines holding it in either case of its first letter
                string other = Casing.UpperFirst(stretch) == stretch ? Casing.LowerFirst(stretch) ?? stretch : Casing.UpperFirst(stretch) ?? stretch;
                var a = units.TryGetValue(stretch[0], out var one) ? one : NoLine;
                var b = other.Length == 1 && other[0] != stretch[0] && units.TryGetValue(other[0], out var two) ? two : NoLine;
                return a.Concat(b).Distinct().OrderBy(i => i);
            }
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
