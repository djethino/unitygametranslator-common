using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

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

            Cost(check);
            Templates(check);
        }

        /// <summary>
        /// What the window costs at the size of a big game: a file of 20 000 lines, 7 000 of them citing the term, asked for the impact of a
        /// new word at each keystroke (<see cref="TermEdit.Effect"/> reads the whole file once). Printed, and held to a generous ceiling:
        /// the site measures about 150 ms a keystroke at this size in JavaScript; the socle must not do worse.
        /// </summary>
        private static void Cost(Action<bool, string, string> check)
        {
            var lines = new List<KeyValuePair<string, TranslationLine>>();
            var term = new TranslationLine { Value = "pièce", Tag = "V", Term = true, Forms = new[] { new KeyValuePair<string, string>("f1", "pièces") } };
            lines.Add(new KeyValuePair<string, TranslationLine>("Coin", term));
            for (int i = 0; i < 20000; i++)
            {
                bool cites = i % 3 == 0;
                lines.Add(new KeyValuePair<string, TranslationLine>("S" + i, cites
                    ? new TranslationLine { Value = "Voici [!g*0] " + i, Tag = "A", Links = new[] { new LineLink { To = "Coin", Form = i % 2 == 0 ? "f1" : null, By = "A", Case = i % 5 == 0 ? LinkReading.UpperFirst : null, Was = i % 2 == 0 ? "pièces" : "pièce" } } }
                    : new TranslationLine { Value = "Texte " + i, Tag = "A" }));
            }
            var lists = new List<KeyValuePair<string, IReadOnlyList<string>>>();
            var draft = TermEdit.Open("Coin", term, 2)!.Value;
            draft = TermEdit.SetWord(draft, "gemme");
            TermEdit.Effect(draft, term, lines, lists);   // warm up

            var clock = System.Diagnostics.Stopwatch.StartNew();
            const int times = 20;
            int texts = 0;
            for (int i = 0; i < times; i++) texts = TermEdit.Effect(TermEdit.SetWord(draft, "gemme" + i), term, lines, lists).Texts;
            double ms = clock.Elapsed.TotalMilliseconds / times;
            Console.WriteLine($"    cost: Effect over {lines.Count} lines, {lines.Count / 3} citing the term: {ms:F1} ms a keystroke ({texts} texts change)");
            check(texts > 1000 && ms < 150, $"the impact of a keystroke over 20 000 lines costs {ms:F1} ms, less than the site's 150 ms", "the socle must not do worse than the page it replaces");
        }

        /// <summary>
        /// What a template takes from the term, per slot's list (<see cref="TemplateUses"/>): held against a reading written here from the
        /// rule, key by key, with no helper of the library. In files with NO key that has a list, the answers are also those of the rule as
        /// it was before `T:LIST` existed (one key per template, read by every slot) — the promise that a file written before reads as before.
        /// </summary>
        private static void Templates(Action<bool, string, string> check)
        {
            var random = new Random(20261115);
            int compared = 0, byListSeen = 0, oldDisagree = 0, newDisagree = 0;
            string first = "";
            string[] listNames = { "a", "b" };
            string[] templateKeys = { "[!L*a][!L*b]", "[!L*a][!L*a*]", "[!L*b] de [!L*a]", "Only [!L*a]" };

            for (int n = 0; n < 600 && newDisagree + oldDisagree < 5; n++)
            {
                bool withLists = n % 2 == 1;
                var lines = new List<KeyValuePair<string, TranslationLine>>();
                string term = "term";
                var forms = new List<KeyValuePair<string, string>>();
                for (int f = 1; f <= 3; f++) forms.Add(new KeyValuePair<string, string>("f" + f, Word(random) + f));

                var use = new List<KeyValuePair<string, LineUse>>();
                var used = new List<string>();
                foreach (string templateKey in templateKeys)
                {
                    string id = TemplateIds.Of(templateKey);
                    if (random.Next(3) != 0) use.Add(new KeyValuePair<string, LineUse>(id, new LineUse { Form = "f" + (1 + random.Next(3)) }));
                    if (withLists)
                        foreach (string list in listNames)
                            if (random.Next(2) == 0) use.Add(new KeyValuePair<string, LineUse>(id + ":" + list, new LineUse { Form = random.Next(4) == 0 ? "0" : "f" + (1 + random.Next(3)) }));
                }
                var before = new TranslationLine { Value = Word(random), Tag = "V", Forms = forms, Use = use.Count == 0 ? null : use };
                lines.Add(new KeyValuePair<string, TranslationLine>(term, before));
                foreach (string templateKey in templateKeys)
                    lines.Add(new KeyValuePair<string, TranslationLine>(templateKey, new TranslationLine { Value = templateKey, Tag = random.Next(2) == 0 ? "A" : "H", Oracle = "accepted" }));

                var lists = new List<KeyValuePair<string, IReadOnlyList<string>>>();
                foreach (string list in listNames)
                    lists.Add(new KeyValuePair<string, IReadOnlyList<string>>(list, random.Next(3) == 0 ? new[] { "other" } : new[] { term, "other" }));
                var holding = lists.Where(l => l.Value.Contains(term)).Select(l => l.Key).ToList();

                TranslationLine? after = Change(random, before);
                var impact = TermChanges.Impact(term, lines, lists, after);

                foreach (string templateKey in templateKeys)
                {
                    var slotLists = Regex.Matches(templateKey, @"\[!L\*([^\]\[:=*]+)").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().Where(holding.Contains).ToList();
                    var announced = impact.Uses.Where(u => u.Key == templateKey).ToList();
                    if (slotLists.Count == 0) { if (announced.Count != 0) newDisagree++; continue; }

                    string id = TemplateIds.Of(templateKey);
                    // the reading, from the rule: each slot of a list that holds the term takes `ID:LIST`, else `ID`, else its own words
                    bool expected = false, old = false;
                    if (!after.HasValue) expected = old = true;
                    else
                    {
                        foreach (string list in slotLists)
                        {
                            string? form = FormOf(use, id + ":" + list) ?? FormOf(use, id);
                            if (form != null && use.Any(u => u.Key == id + ":" + list)) byListSeen++;
                            if (FormText(before, form ?? "0") != FormText(after.Value, form ?? "0")) expected = true;
                        }
                        // what the rule said before: one key per template, whatever the slot
                        string? whole = FormOf(use, id);
                        if (FormText(before, whole ?? "0") != FormText(after.Value, whole ?? "0")) old = true;
                    }
                    compared++;
                    if (announced.Count != 1 || announced[0].TextChanges != expected)
                    {
                        newDisagree++;
                        if (first.Length == 0) first = "file " + n + ", « " + templateKey + " » announced " + (announced.Count == 0 ? "nothing" : announced[0].TextChanges.ToString()) + ", the reading says " + expected;
                    }
                    if (!withLists && announced.Count == 1 && announced[0].TextChanges != old) oldDisagree++;
                }
            }

            check(compared > 800 && byListSeen > 200, "the random templates exercise it (" + compared + " templates read, " + byListSeen + " slots by a key of their list)", "a property over nothing proves nothing");
            check(newDisagree == 0, "what a template takes from the term is what each slot of each list that holds it reads", newDisagree + " disagreement(s); " + first);
            check(oldDisagree == 0, "a file with no key that has a list gives the answers it gave before", oldDisagree + " disagreement(s)");
        }

        private static string? FormOf(List<KeyValuePair<string, LineUse>> use, string key)
        {
            foreach (var u in use) if (u.Key == key) return u.Value.Form;
            return null;
        }

        private static string? FormText(TranslationLine line, string form)
        {
            if (form == "0") return line.Value;
            if (line.Forms != null) foreach (var f in line.Forms) if (f.Key == form) return f.Value;
            return line.Value;   // a form that is gone reads as the entry's own words, as a template does
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
