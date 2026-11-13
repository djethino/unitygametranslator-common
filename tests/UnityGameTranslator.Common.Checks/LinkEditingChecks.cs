using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What the corpus cannot say about <see cref="LinkEditing"/>:
    ///
    ///  · a format 1 text, and a format 2 text with no link, go through <see cref="LinkEditing.Show"/>,
    ///    <see cref="LinkEditing.Reconcile"/> and <see cref="LinkEditing.Resume"/> as the SAME string
    ///    and with NOT ONE byte allocated (a format 1 file must pay nothing for a format it does not use);
    ///  · whatever a person types, the rules never throw and never leave a state that cannot be written:
    ///    passages inside the text, in order, not overlapping, one link each, and a <c>v</c> holding
    ///    exactly one marker per passage (a seeded pseudo-random walk — the same on every machine).
    /// </summary>
    internal static class LinkEditingChecks
    {
        private static readonly Dictionary<string, TranslationLine> Entries = new Dictionary<string, TranslationLine>
        {
            ["Star Coin"] = new TranslationLine("Pièce d'étoile", "V")
            {
                Forms = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("f1", "pièces d'étoile"),
                    new KeyValuePair<string, string>("f2", "de la pièce d'étoile"),
                },
            },
            ["Key"] = new TranslationLine("Touche", "V"),
        };

        private static TranslationLine? Lookup(string key) => Entries.TryGetValue(key, out var line) ? line : (TranslationLine?)null;

        public static void Run(Action<bool, string, string> check)
        {
            // ── a text with no link costs nothing ───────────────────────────────
            string plain = new string('a', 40) + " [!g*0] [!v*1]";
            var line = new TranslationLine(plain, "A");
            string edited = plain + " more";

            // Warm up (JIT, static lists) so only the steady state is measured.
            Pass(line, plain, edited, 1);

            check(ReferenceEquals(LineEndings.ToLf(plain), plain) && ReferenceEquals(LineEndings.ToLf(null), null),
                "a text with no CR comes back as the same string", "the common case costs nothing");

            long before = GC.GetAllocatedBytesForCurrentThread();
            var shown = LinkEditing.Show(line, 1, Lookup, null, null);
            var same = LinkEditing.Reconcile(shown, plain, Lookup, null, null);
            var changed = LinkEditing.Reconcile(shown, edited, Lookup, null, null);
            var unchanged = LinkEditing.Resume(shown, same.State, Lookup, null, null);
            var saved = LinkEditing.Resume(shown, changed.State, Lookup, null, null);
            long after = GC.GetAllocatedBytesForCurrentThread();

            check(ReferenceEquals(shown.Text, plain), "a format 1 text is shown as the same string", "no copy of the game's text");
            check(!unchanged.Changed, "saving it unchanged writes nothing", "a save with nothing different is no act");
            check(saved.Changed && ReferenceEquals(saved.V, edited), "an edited format 1 text comes back as the very string typed",
                "no marker, no link, no decision on the way");
            check(after - before == 0, "and none of it allocates (" + (after - before) + " bytes)", "a format 1 file pays nothing for a format it does not use");

            var plain2 = new TranslationLine("Bonjour tout le monde", "A");
            Pass(plain2, plain2.Value, plain2.Value + "!", 2);
            before = GC.GetAllocatedBytesForCurrentThread();
            var shown2 = LinkEditing.Show(plain2, 2, Lookup, null, null);
            var r2 = LinkEditing.Reconcile(shown2, "Bonjour tout le monde!", Lookup, null, null);
            var o2 = LinkEditing.Resume(shown2, r2.State, Lookup, null, null);
            after = GC.GetAllocatedBytesForCurrentThread();
            check(ReferenceEquals(shown2.Text, plain2.Value) && o2.Changed && o2.V == "Bonjour tout le monde!" && after - before == 0,
                "a format 2 sentence with no link allocates nothing either (" + (after - before) + " bytes)", "the same path");

            // ── the walk ────────────────────────────────────────────────────────
            var linked = new TranslationLine("Pas assez de [!g*0] et [!g*1], [!g*0] !", "A")
            {
                Links = new List<LineLink>
                {
                    new LineLink { To = "Star Coin", Form = "f1", By = "A", Was = "pièces d'étoile" },
                    new LineLink { To = "Key", By = "A", Was = "Touche" },
                },
            };
            var random = new Random(20261113);
            string alphabet = "abc d'é";
            int walks = 0, failures = 0;
            string firstFailure = "";

            for (int round = 0; round < 3000; round++)
            {
                var state = LinkEditing.Show(linked, 2, Lookup, null, null);
                string text = state.Text;
                int edits = 1 + random.Next(3);
                for (int e = 0; e < edits; e++)
                {
                    int at = random.Next(text.Length + 1);
                    int cut = random.Next(Math.Min(6, text.Length - at) + 1);
                    int type = random.Next(5);
                    var add = new System.Text.StringBuilder();
                    for (int k = 0; k < type; k++) add.Append(alphabet[random.Next(alphabet.Length)]);
                    // now and then paste a whole passage's words back, the way a cut and paste does
                    if (random.Next(8) == 0) add.Append("Touche");
                    text = text.Substring(0, at) + add + text.Substring(at + cut);
                }

                walks++;
                try
                {
                    var rec = LinkEditing.Reconcile(state, text, Lookup, null, null);
                    var s = rec.State;
                    string bad = Invalid(s, text);
                    if (bad == null)
                    {
                        // answer whatever is asked, alternately, then write
                        var q = LinkEditing.NextQuestion(s);
                        int n = 0;
                        while (q.HasValue && n++ < 10)
                        {
                            s = LinkEditing.Answer(s, q.Value, (round + n) % 2 == 0 ? EditAnswer.AllTexts : EditAnswer.OnlyThisText, Lookup);
                            q = LinkEditing.NextQuestion(s);
                        }
                        bad = Invalid(s, text);
                        if (bad == null)
                        {
                            var o = LinkEditing.Resume(state, s, Lookup, null, null);
                            if (o.Changed && o.Problem == null && !o.Question.HasValue)
                            {
                                int markers = LineMarkers.LinkNumbers(o.V).Count;
                                if (markers != s.Passages!.Count || o.Links!.Count != s.Passages.Count) bad = "markers/links do not match the passages";
                                else
                                {
                                    // and the file's line reads back to the text the person left
                                    var back = LinkEditing.Show(new TranslationLine(o.V!, "H") { Links = new List<LineLink>(o.Links) }, 2, EntriesWith(o.NewForms), null, null);
                                    if (back.Text != text) bad = "reading the saved line gives another text: [" + back.Text + "] for [" + text + "]";
                                }
                            }
                        }
                    }
                    if (bad != null) { failures++; if (firstFailure.Length == 0) firstFailure = bad + " — text [" + text + "]"; }
                }
                catch (Exception ex)
                {
                    failures++;
                    if (firstFailure.Length == 0) firstFailure = ex.GetType().Name + ": " + ex.Message + " — text [" + text + "]";
                }
            }

            check(failures == 0, $"{walks} random edits leave a state that can be written, and read back",
                failures + " failed, the first: " + firstFailure);
        }

        private static Func<string, TranslationLine?> EntriesWith(IReadOnlyList<NewForm>? added)
        {
            if (added == null || added.Count == 0) return Lookup;
            return key =>
            {
                var line = Lookup(key);
                if (!line.HasValue) return line;
                var entry = line.Value;
                var forms = new List<KeyValuePair<string, string>>(entry.Forms ?? new KeyValuePair<string, string>[0]);
                foreach (var f in added) if (f.Entry == key) forms.Add(new KeyValuePair<string, string>(f.Id, f.Text));
                entry.Forms = forms;
                return entry;
            };
        }

        private static void Pass(TranslationLine line, string text, string edited, int format)
        {
            var shown = LinkEditing.Show(line, format, Lookup, null, null);
            var changed = LinkEditing.Reconcile(shown, edited, Lookup, null, null);
            LinkEditing.Resume(shown, changed.State, Lookup, null, null);
        }

        /// <summary>Null when the state is one that can be written.</summary>
        private static string? Invalid(EditState s, string text)
        {
            if (s.Text != text) return "the state's text is not the text left";
            int n = s.Passages == null ? 0 : s.Passages.Count;
            if ((s.Links == null ? 0 : s.Links.Count) != n) return "passages and links differ in number";
            int last = 0;
            for (int i = 0; i < n; i++)
            {
                var p = s.Passages![i];
                if (p.Start < last || p.Length <= 0 || p.Start + p.Length > text.Length) return "a passage is out of the text or overlaps";
                last = p.Start + p.Length;
            }
            return null;
        }
    }
}
