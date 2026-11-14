using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// <see cref="LinkFinding.WrittenIn"/> — where ONE term is written in a text, every place kept — held against the two things it
    /// must agree with, on random texts and a fixed seed:
    ///
    ///  1. <see cref="LinkFinding.Find"/>: where WrittenIn answers one place, Find links that very place (same form, same case,
    ///     same words); where it answers none or several, Find links nothing. Two rules about the same thing, one answer.
    ///  2. <see cref="LinkEditing.LinkTo"/>: linking each place WrittenIn answered, one call each, on the text the editor shows
    ///     (a line that already holds a link, whose passage is the span claimed), is never refused, and the link it writes has the
    ///     form and the case WrittenIn announced. No new case in the editing: it is how a found place becomes a link.
    ///
    /// The cost of asking it of a few hundred lines is printed.
    /// </summary>
    internal static class WrittenInChecks
    {
        private static readonly string[] Words =
        {
            "pièce", "gemme", "étoile", "jeton", "maître", "ça", "straße", "σοφία", "այբ", "монета", "ǆar", "coin", "Coin", "star coin", "Star Coin",
        };

        private static readonly string[] Fillers = { "une", "le", "et", "de la", "voici", "ici", "—", "2", "(x)" };

        public static void Run(Action<bool, string, string> check)
        {
            var random = new Random(20261114);
            int cases = 0, single = 0, several = 0, none = 0, findDiffers = 0, linkToProblems = 0, linkToDiffers = 0;
            string firstFind = "", firstLink = "";

            for (int n = 0; n < 1500; n++)
            {
                // one term: its word and up to three forms, all different words
                var chosen = new List<string>();
                int count = 1 + random.Next(4);
                while (chosen.Count < count)
                {
                    string w = Words[random.Next(Words.Length)] + (random.Next(3) == 0 ? " " + Words[random.Next(Words.Length)] : "");
                    if (!chosen.Contains(w)) chosen.Add(w);
                }
                var forms = new List<KeyValuePair<string, string>>();
                for (int f = 1; f < chosen.Count; f++) forms.Add(new KeyValuePair<string, string>("f" + f, chosen[f]));
                var term = new TranslationLine { Value = chosen[0], Tag = "V", Term = true, Forms = forms.Count == 0 ? null : forms };
                var writings = new List<LinkFinding.LinkWriting> { new LinkFinding.LinkWriting { Words = chosen[0], Form = null } };
                foreach (var f in forms) writings.Add(new LinkFinding.LinkWriting { Words = f.Value, Form = f.Key });

                // a text made of the term's writings in their cases, stuck-on endings, and filler
                var text = new System.Text.StringBuilder();
                int tokens = 1 + random.Next(7);
                for (int t = 0; t < tokens; t++)
                {
                    if (t > 0) text.Append(random.Next(5) == 0 ? ", " : " ");
                    int roll = random.Next(10);
                    if (roll < 4)
                    {
                        string writing = writings[random.Next(writings.Count)].Words;
                        int variant = random.Next(4);
                        string shown = variant == 1 ? Casing.UpperFirst(writing)! : variant == 2 ? Casing.LowerFirst(writing)! : writing;
                        int ending = random.Next(8);
                        text.Append(shown).Append(ending == 0 ? "s" : ending == 1 ? "2" : ending == 2 ? "-op" : "");
                    }
                    else text.Append(Fillers[random.Next(Fillers.Length)]);
                }
                string rendered = text.ToString();

                var places = LinkFinding.WrittenIn(rendered, writings, null);
                cases++;
                if (places.Count == 1) single++; else if (places.Count > 1) several++; else none++;

                // 1. against Find, on an index of this one term
                var lines = new List<KeyValuePair<string, TranslationLine>> { new KeyValuePair<string, TranslationLine>("Star Treasure", term) };
                var found = LinkFinding.Find(TermIndex.Build(lines), rendered, "the Star Treasure here", null);
                bool agrees;
                if (places.Count == 1)
                {
                    var p = places[0];
                    agrees = found.Links.Count == 1 && found.Links[0].Form == writings[p.Writing].Form && found.Links[0].Case == p.Case
                             && found.Links[0].Was == rendered.Substring(p.Start, p.End - p.Start);
                }
                else agrees = found.Links.Count == 0;
                if (!agrees)
                {
                    findDiffers++;
                    if (firstFind.Length == 0) firstFind = "« " + rendered + " » with " + string.Join(" | ", writings.Select(w => w.Words)) + ": " + places.Count + " place(s), Find " + found.Links.Count + " link(s)";
                }

                // 2. against LinkTo, on the text the editor shows (with a passage already linked, sometimes)
                bool withOther = random.Next(2) == 0;
                var all = new List<KeyValuePair<string, TranslationLine>>
                {
                    new KeyValuePair<string, TranslationLine>("Star Treasure", term),
                    new KeyValuePair<string, TranslationLine>("Other", new TranslationLine { Value = "Autre", Tag = "V", Term = true }),
                };
                Func<string, TranslationLine?> entryOf = k => { foreach (var l in all) if (l.Key == k) return l.Value; return null; };
                var line = withOther
                    ? new TranslationLine { Value = "[!g*0] " + rendered, Tag = "H", Links = new[] { new LineLink { To = "Other", By = "H", Was = "Autre" } } }
                    : new TranslationLine { Value = rendered, Tag = "H" };
                var state = LinkEditing.Show(line, 2, entryOf, Casing.UpperFirst!, Casing.LowerFirst!);
                var claimed = (state.Passages ?? new List<Passage>()).Select(p => new KeyValuePair<int, int>(p.Start, p.Start + p.Length)).ToList();
                var shownPlaces = LinkFinding.WrittenIn(state.Text, writings, claimed);

                bool problem = false, differs = false;
                foreach (var place in shownPlaces)
                {
                    var act = LinkEditing.LinkTo(state, place.Start, place.End - place.Start, "Star Treasure", null, entryOf, Casing.UpperFirst!, Casing.LowerFirst!);
                    if (act.Problem != null) { problem = true; if (firstLink.Length == 0) firstLink = "« " + state.Text + " » " + place.Start + "-" + place.End + ": " + act.Problem; break; }
                    state = act.State;
                }
                if (!problem)
                {
                    var made = (state.Links ?? new List<LineLink>()).Where(l => l.To == "Star Treasure").ToList();
                    differs = made.Count != shownPlaces.Count;
                    for (int i = 0; i < made.Count && !differs; i++)
                        differs = made[i].Form != writings[shownPlaces[i].Writing].Form || made[i].Case != shownPlaces[i].Case;
                    if (differs && firstLink.Length == 0)
                        firstLink = "« " + state.Text + " » writings [" + string.Join(" | ", writings.Select(w => (w.Form ?? "word") + "=" + w.Words)) + "] announced "
                                    + string.Join(", ", shownPlaces.Select(p => p.Start + "-" + p.End + " w" + p.Writing + " " + (p.Case ?? "-")))
                                    + "; written " + string.Join(", ", made.Select(l => (l.Form ?? "word") + " " + (l.Case ?? "-") + " " + l.Was));
                }
                if (problem) linkToProblems++;
                if (differs) linkToDiffers++;
            }

            check(single > 150 && several > 100 && none > 100, "the random texts exercise it (" + cases + " texts: " + single + " with one place, " + several + " with several, " + none + " with none)", "a property over one answer proves nothing");
            check(findDiffers == 0, "where one place is found Find links it, where none or several Find links nothing", findDiffers + " disagreement(s); " + firstFind);
            check(linkToProblems == 0, "linking each place found, one LinkTo each, is never refused (a line that already holds a link included)", linkToProblems + " refused; " + firstLink);
            check(linkToDiffers == 0, "and the links LinkTo writes have the form and the case the places announced", linkToDiffers + " differ; " + firstLink);

            // cost: a few hundred lines, ten writings, asked again at each change of the translation
            var many = new List<LinkFinding.LinkWriting>();
            for (int i = 0; i < 10; i++) many.Add(new LinkFinding.LinkWriting { Words = Words[i] + " " + Words[(i * 3 + 1) % Words.Length], Form = i == 0 ? null : "f" + i });
            var sentences = new List<string>();
            for (int i = 0; i < 1000; i++)
                sentences.Add("une phrase de soixante lettres environ avec " + Words[i % Words.Length] + " et " + many[i % 10].Words + " voici le reste, de la, ici.");
            var clock = Stopwatch.StartNew();
            int total = 0;
            foreach (string s in sentences) total += LinkFinding.WrittenIn(s, many, null).Count;
            long micros = clock.ElapsedTicks * 1000000 / Stopwatch.Frequency;
            Console.WriteLine($"    cost: {sentences.Count} lines, {many.Count} writings: {micros / 1000.0:F1} ms in all, {micros / (double)sentences.Count:F1} µs a line ({total} places)");
            check(total > 500, "the cost run finds places to measure", "a measure over nothing measures nothing");
        }
    }
}
