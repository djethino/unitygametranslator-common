using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The promise of the term window: what it announces BEFORE the click is exactly what the person finds AFTER.
    ///
    /// For random files and random changes of a term (its word, one of its forms, a form removed, the term gone, nothing at
    /// all), <see cref="TermChanges.Impact"/> is held against the plain reading of every sentence before and after — the one
    /// the screens do, <see cref="LinkReading.Read"/> with the socle's <see cref="Casing"/> — sentence by sentence:
    /// the text it shows changed or not, the sentence to read again or not. Fixed seed: a failure is the same failure
    /// tomorrow. (The oracle is the reader, not a second reading of the links: one rule, and this is its witness.)
    ///
    /// Why it exists: the first impact read the links WITHOUT the case, so a sentence that writes the term with a capital
    /// looked changed and to read again as soon as the window opened — 31 to read where the screen saw one.
    /// </summary>
    internal static class TermChangesChecks
    {
        private const int Files = 400;

        public static void Run(Action<bool, string, string> check)
        {
            var random = new Random(20261114);
            int disagreements = 0, sentencesCompared = 0, changedSeen = 0, reviewSeen = 0;
            string first = "";

            for (int n = 0; n < Files && disagreements < 5; n++)
            {
                var lines = Build(random, out string term);
                TranslationLine? after = Change(random, lines.First(l => l.Key == term).Value);

                var impact = TermChanges.Impact(term, lines, new List<KeyValuePair<string, IReadOnlyList<string>>>(), after);

                foreach (var line in lines)
                {
                    if (line.Value.Links == null || line.Key == term) continue;
                    var mine = line.Value.Links.Where(l => l.To == term).ToList();
                    if (mine.Count == 0) continue;

                    bool changes = false, review = false;
                    foreach (var link in mine)
                    {
                        LinkRead was = LinkReading.Read(link, k => Lookup(lines, k, term, null, false), Casing.UpperFirst!, Casing.LowerFirst!);
                        LinkRead now = LinkReading.Read(link, k => Lookup(lines, k, term, after, true), Casing.UpperFirst!, Casing.LowerFirst!);
                        if (!string.Equals(was.Text, now.Text, StringComparison.Ordinal)) changes = true;
                        if (LinkReading.NeedsReview(link, now)) review = true;
                    }

                    sentencesCompared++;
                    if (changes) changedSeen++;
                    if (review) reviewSeen++;

                    var announced = impact.Uses.Where(u => u.Key == line.Key).ToList();
                    bool ok = announced.Count == 1 && announced[0].TextChanges == changes && announced[0].NeedsReviewAfter == review;
                    if (!ok)
                    {
                        disagreements++;
                        if (first.Length == 0)
                            first = "file " + n + ", « " + line.Key + " » announced "
                                    + (announced.Count == 0 ? "nothing" : announced.Count + " use(s), changes=" + announced[0].TextChanges + " review=" + announced[0].NeedsReviewAfter)
                                    + ", the reading says changes=" + changes + " review=" + review;
                    }
                }
            }

            check(sentencesCompared > 1000 && changedSeen > 100 && reviewSeen > 100,
                "the random files exercise it (" + sentencesCompared + " sentences, " + changedSeen + " changed, " + reviewSeen + " to read)", "a property over nothing proves nothing");
            check(disagreements == 0, "what the term window announces is what reading every sentence before and after finds", disagreements + " disagreement(s); " + first);
        }

        /// <summary>The line of an entry: the term as it is or as it would be, any other entry as it stands.</summary>
        private static TranslationLine? Lookup(List<KeyValuePair<string, TranslationLine>> lines, string key, string term, TranslationLine? after, bool use)
        {
            if (key == term && use) return after;
            foreach (var line in lines) if (line.Key == key) return line.Value;
            return null;
        }

        private static readonly string[] Words = { "pièce", "gemme", "étoile", "ǆar", "jeton", "maître", "ça", "straße", "σοφία", "այբ" };

        private static List<KeyValuePair<string, TranslationLine>> Build(Random random, out string term)
        {
            var lines = new List<KeyValuePair<string, TranslationLine>>();
            int entries = 3 + random.Next(3);
            var entryKeys = new List<string>();
            for (int e = 0; e < entries; e++)
            {
                string key = "term" + e;
                entryKeys.Add(key);
                var forms = new List<KeyValuePair<string, string>>();
                for (int f = 1; f <= 3; f++)
                    if (random.Next(4) != 0) forms.Add(new KeyValuePair<string, string>("f" + f, Word(random) + (f == 1 ? "s" : " " + Word(random))));
                lines.Add(new KeyValuePair<string, TranslationLine>(key, new TranslationLine { Value = Word(random), Tag = "V", Term = true, Forms = forms.Count == 0 ? null : forms }));
            }
            term = entryKeys[random.Next(entries)];

            int sentences = 12 + random.Next(20);
            for (int s = 0; s < sentences; s++)
            {
                var links = new List<LineLink>();
                int count = 1 + random.Next(3);
                for (int i = 0; i < count; i++)
                {
                    string to = random.Next(6) == 0 ? "gone" + random.Next(2) : (random.Next(2) == 0 ? term : entryKeys[random.Next(entries)]);
                    var link = new LineLink { To = to, By = random.Next(2) == 0 ? "A" : "H" };
                    int shape = random.Next(5);
                    if (shape == 1) link.Form = "0";
                    else if (shape == 2) link.Form = "f1";
                    else if (shape == 3) link.Form = "f2";
                    int casing = random.Next(3);
                    if (casing == 1) link.Case = LinkReading.UpperFirst;
                    else if (casing == 2) link.Case = LinkReading.LowerFirst;
                    if (random.Next(10) == 0) link.Own = Word(random);

                    // What was read when it was confirmed — mostly still true, sometimes stale, sometimes missing.
                    LinkRead read = LinkReading.Read(link, k => { foreach (var l in lines) if (l.Key == k) return l.Value; return null; }, Casing.UpperFirst!, Casing.LowerFirst!);
                    int staleness = random.Next(8);
                    link.Was = staleness == 0 ? null : staleness == 1 ? Word(random) : read.Text;
                    links.Add(link);
                }
                lines.Add(new KeyValuePair<string, TranslationLine>("S" + s, new TranslationLine { Value = string.Concat(links.Select((_, i) => "[!g*" + i + "] ")).Trim(), Tag = "A", Links = links }));
            }
            return lines;
        }

        private static TranslationLine? Change(Random random, TranslationLine before)
        {
            switch (random.Next(7))
            {
                case 0: return null;   // the term goes
                case 1: return before; // nothing changes
                case 2:
                    {
                        var changed = before; changed.Value = Word(random) + "x"; return changed;
                    }
                case 3:
                    {
                        var changed = before;
                        changed.Forms = (before.Forms ?? new List<KeyValuePair<string, string>>())
                            .Select(f => f.Key == "f1" ? new KeyValuePair<string, string>("f1", Word(random) + "z") : f).ToList();
                        return changed;
                    }
                case 4:
                    {
                        var changed = before;
                        changed.Forms = (before.Forms ?? new List<KeyValuePair<string, string>>()).Where(f => f.Key != "f2").ToList();
                        return changed;
                    }
                case 5:
                    {
                        var changed = before; changed.Value = before.Value.ToUpperInvariant(); return changed;   // only its case
                    }
                default:
                    {
                        var changed = before;
                        changed.Forms = (before.Forms ?? new List<KeyValuePair<string, string>>()).Concat(new[] { new KeyValuePair<string, string>("f3", Word(random)) }).ToList();
                        return changed;
                    }
            }
        }

        private static string Word(Random random) => Words[random.Next(Words.Length)];
    }
}
