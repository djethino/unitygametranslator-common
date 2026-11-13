using System;
using System.Collections.Generic;
using System.Text;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What the corpus cannot say about <see cref="LinkEditing"/>:
    ///
    ///  · a format 1 text, and a format 2 text with no link, go through <see cref="LinkEditing.Show"/>,
    ///    <see cref="LinkEditing.Step"/> and <see cref="LinkEditing.Resume"/> as the SAME string and with NOT ONE
    ///    byte allocated (a format 1 file must pay nothing for a format it does not use);
    ///  · a step on a text of 50 000 code points with 20 passages allocates an amount LINEAR in the text: doubling
    ///    the text at most doubles the bytes (a table of n × m would quadruple them);
    ///  · whatever a person types, one change at a time, the rules never throw and never leave a state that cannot
    ///    be written: passages inside the text, in order, not overlapping, one link each, and a <c>v</c> holding exactly
    ///    one marker per passage that reads back to the text she left (a seeded pseudo-random walk — the same on
    ///    every machine).
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
            Pass(line, 1, edited);

            check(ReferenceEquals(LineEndings.ToLf(plain), plain) && ReferenceEquals(LineEndings.ToLf(null), null),
                "a text with no CR comes back as the same string", "the common case costs nothing");

            long before = GC.GetAllocatedBytesForCurrentThread();
            var shown = LinkEditing.Show(line, 1, Lookup, null, null);
            var same = LinkEditing.Step(shown, plain);
            var changed = LinkEditing.Step(shown, edited);
            var unchanged = LinkEditing.Resume(shown, same, Lookup, null, null);
            var saved = LinkEditing.Resume(shown, changed, Lookup, null, null);
            long after = GC.GetAllocatedBytesForCurrentThread();

            check(ReferenceEquals(shown.Text, plain), "a format 1 text is shown as the same string", "no copy of the game's text");
            check(!unchanged.Changed, "saving it unchanged writes nothing", "a save with nothing different is no act");
            check(saved.Changed && ReferenceEquals(saved.V, edited), "an edited format 1 text comes back as the very string typed",
                "no marker, no link, no decision on the way");
            check(after - before == 0, "and none of it allocates (" + (after - before) + " bytes)", "a format 1 file pays nothing for a format it does not use");

            var plain2 = new TranslationLine("Bonjour tout le monde", "A");
            Pass(plain2, 2, plain2.Value + "!");
            before = GC.GetAllocatedBytesForCurrentThread();
            var shown2 = LinkEditing.Show(plain2, 2, Lookup, null, null);
            var r2 = LinkEditing.Step(shown2, "Bonjour tout le monde!");
            var o2 = LinkEditing.Resume(shown2, r2, Lookup, null, null);
            after = GC.GetAllocatedBytesForCurrentThread();
            check(ReferenceEquals(shown2.Text, plain2.Value) && o2.Changed && o2.V == "Bonjour tout le monde!" && after - before == 0,
                "a format 2 sentence with no link allocates nothing either (" + (after - before) + " bytes)", "the same path");

            // ── a step is linear ─────────────────────────────────────────────────
            long small = StepCost(25000), large = StepCost(50000);
            check(large <= small * 5 / 2 && large > 0,
                "a step on 50 000 code points with 20 passages allocates " + (large / 1024) + " KB (" + (small / 1024) + " KB for 25 000)",
                "doubling the text must at most double the bytes: a table of n × m would multiply them by four");

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
                var original = LinkEditing.Show(linked, 2, Lookup, null, null);
                var state = original;
                string text = state.Text;
                var trail = new StringBuilder();

                try
                {
                    int edits = 1 + random.Next(8);
                    for (int e = 0; e < edits && failures == 0; e++)
                    {
                        int kind = random.Next(10);
                        int at = random.Next(text.Length + 1);
                        int cut = random.Next(Math.Min(6, text.Length - at) + 1);
                        var add = new StringBuilder();
                        int typed = random.Next(4);
                        for (int k = 0; k < typed; k++) add.Append(alphabet[random.Next(alphabet.Length)]);
                        if (kind == 0) add.Append("Touche");                  // a paste of a passage's words
                        if (kind == 1) { at = 0; cut = text.Length; }          // replace everything
                        if (kind == 2 && state.Lost != null && state.Lost.Count > 0) add.Append(state.Lost[0].Words); // an undo-like paste

                        text = text.Substring(0, at) + add + text.Substring(at + cut);
                        trail.Append('[').Append(text).Append(']');
                        state = LinkEditing.Step(state, text);

                        string bad = Invalid(state, text);
                        if (bad != null) throw new InvalidOperationException(bad);
                    }

                    walks++;
                    var s = LinkEditing.Settle(state, Lookup, null, null);
                    var q = LinkEditing.NextQuestion(s, Lookup, null, null);
                    int n = 0;
                    while (q.HasValue && n++ < 10)
                    {
                        s = LinkEditing.Answer(s, q.Value, (round + n) % 2 == 0 ? EditAnswer.AllTexts : EditAnswer.OnlyThisText, Lookup, null, null);
                        q = LinkEditing.NextQuestion(s, Lookup, null, null);
                    }

                    string invalid = Invalid(s, text);
                    if (invalid != null) throw new InvalidOperationException(invalid);

                    var o = LinkEditing.Resume(original, s, Lookup, null, null);
                    if (o.Changed && o.Problem == null && !o.Question.HasValue)
                    {
                        int markers = LineMarkers.LinkNumbers(o.V).Count;
                        if (markers != s.Passages!.Count || o.Links!.Count != s.Passages.Count) throw new InvalidOperationException("markers/links do not match the passages");

                        var back = LinkEditing.Show(new TranslationLine(o.V!, "H") { Links = new List<LineLink>(o.Links) }, 2, EntriesWith(o.NewForms), null, null);
                        if (back.Text != text) throw new InvalidOperationException("reading the saved line gives another text: [" + back.Text + "] for [" + text + "]");
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    if (firstFailure.Length == 0) firstFailure = ex.GetType().Name + ": " + ex.Message + " — steps " + trail;
                }
            }

            check(failures == 0, $"{walks} random sequences of changes leave a state that can be written, and read back",
                failures + " failed, the first: " + firstFailure);
        }

        /// <summary>Bytes allocated by ONE step that replaces a stretch in the middle of a long text holding 20 passages.</summary>
        private static long StepCost(int length)
        {
            var text = new StringBuilder(length);
            var passages = new List<Passage>();
            var links = new List<LineLink>();
            var random = new Random(7);
            for (int i = 0; i < length; i++) text.Append((char)('a' + random.Next(26)));
            for (int i = 0; i < 20; i++)
            {
                int start = i * (length / 20) + 10;
                passages.Add(new Passage { Start = start, Length = 15, Origin = text.ToString(start, 15), Order = i });
                links.Add(new LineLink { To = "Key", By = "A", Was = "x" });
            }

            string before = text.ToString();
            string after = before.Substring(0, length / 2 + 100) + "typed" + before.Substring(length / 2 + 130);
            var state = new EditState
            {
                Text = before, Format2 = true, Passages = passages, Links = links,
                Lost = new LostPassage[0], Unlinked = new string[0], NewForms = new NewForm[0],
            };

            LinkEditing.Step(state, after);   // warm up
            long start0 = GC.GetAllocatedBytesForCurrentThread();
            LinkEditing.Step(state, after);
            return GC.GetAllocatedBytesForCurrentThread() - start0;
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

        private static void Pass(TranslationLine line, int format, string edited)
        {
            var shown = LinkEditing.Show(line, format, Lookup, null, null);
            var changed = LinkEditing.Step(shown, edited);
            LinkEditing.Resume(shown, changed, Lookup, null, null);
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
