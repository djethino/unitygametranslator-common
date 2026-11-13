using System;
using System.Collections.Generic;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>One linked stretch of the text a person edits. UTF-16 units of <see cref="EditState.Text"/>.</summary>
    public struct Passage
    {
        public int Start;
        public int Length;

        /// <summary>
        /// The words it had when its link was last settled (opening, an answer, an act). Null reads as its current
        /// words. Words that differ from it, and are no known writing, wait for the question.
        /// </summary>
        public string? Origin;

        /// <summary>Its end was just replaced or erased: the next characters typed there are its own (<see cref="PassageBorder"/>).</summary>
        public bool Open;

        /// <summary>Its place at opening, never reused: what orders the lost ones when a paste restores one.</summary>
        public int Order;
    }

    /// <summary>A passage a step removed, kept so a later step can bring it back.</summary>
    public struct LostPassage
    {
        public string Words;
        public string? Origin;
        public LineLink Link;
        public PassageReason Reason;
        public int Order;
    }

    /// <summary>A written form to add to a term when the editing is saved.</summary>
    public struct NewForm
    {
        public string Entry;
        public string Id;
        public string Text;
    }

    public enum PassageReason
    {
        None,

        /// <summary>All its words were erased.</summary>
        Deleted,

        /// <summary>A change covered the passage AND words around it: nothing is guessed.</summary>
        Straddled,

        /// <summary>The term no longer exists: its words stay, as ordinary text.</summary>
        EntryGone,
    }

    /// <summary>
    /// Everything an editor holds while a person edits a sentence, and all of it. The editor keeps this value and
    /// replaces it with what the rules return; it computes nothing (spec/translation-file/editing.md §3).
    /// </summary>
    public struct EditState
    {
        /// <summary>The text in clear: every link replaced by its words.</summary>
        public string Text;

        /// <summary>The file is of the format 2. In a format 1 file nothing below exists.</summary>
        public bool Format2;

        /// <summary>The linked stretches, in reading order.</summary>
        public IReadOnlyList<Passage> Passages;

        /// <summary>The link behind each passage (passage i, link i): one passage, one link.</summary>
        public IReadOnlyList<LineLink> Links;

        /// <summary>The passages removed by a step, remembered (spec §5).</summary>
        public IReadOnlyList<LostPassage> Lost;

        public IReadOnlyList<string> Unlinked;
        public IReadOnlyList<NewForm> NewForms;

        /// <summary>The line held a link marker with no link: the text shown lacks it, and saving removes it.</summary>
        public bool Damaged;
    }

    /// <summary>The ONE question asked about changed words: another writing of the term for everybody, or for this text only?</summary>
    public struct EditQuestion
    {
        public string Entry;
        public string Text;

        /// <summary>The passages (indices in the SETTLED state) it settles: several when they are the same term with the same new words.</summary>
        public IReadOnlyList<int> Passages;
    }

    public enum EditAnswer
    {
        /// <summary>« All texts »: the words become a written form of the term.</summary>
        AllTexts,

        /// <summary>« Only this text »: the link keeps its term and gets a writing of its own.</summary>
        OnlyThisText,
    }

    /// <summary>What an act of the person gave: the new state, or why it cannot be done (a code, null when done).</summary>
    public struct EditAct
    {
        public EditState State;
        public string? Problem;
    }

    /// <summary>What the editor writes back.</summary>
    public struct EditOutcome
    {
        /// <summary>Nothing differs from what was shown: nothing is written.</summary>
        public bool Changed;

        /// <summary>A code, when the text cannot be saved as it stands.</summary>
        public string? Problem;

        /// <summary>A question to answer before anything is written.</summary>
        public EditQuestion? Question;

        public string? V;
        public IReadOnlyList<LineLink>? Links;
        public IReadOnlyList<NewForm>? NewForms;
        public IReadOnlyList<string>? Unlinked;
    }

    /// <summary>How a passage reads and reads again, for a colour and a hint.</summary>
    public struct PassageView
    {
        public int Start;
        public int Length;
        public LinkSource Source;
        public bool NeedsReview;
        public bool CaseNotApplied;

        /// <summary>Changed words nobody has said what they are yet.</summary>
        public bool Pending;
        public bool Open;
    }

    /// <summary>
    /// The text a person edits, with its links (spec/translation-file/editing.md): the same pure rules for the four
    /// editors, which only draw.
    ///
    /// 🔴 **The person never sees a marker or a number.** She edits the sentence in clear; the colour of the linked
    /// words is the editor's. The editor calls <see cref="Step"/> at every change of the text it sees — one
    /// keystroke, paste, undo or composition is one change, one hunk, EXACT — and keeps the state it gets back.
    /// That is not following the keyboard: no cursor, no key, no clipboard, only two successive texts.
    ///
    /// ⚠ **A link is known, never guessed** (R4): words that were never a passage stay ordinary, even when they
    /// spell a term exactly. A change that cannot be read as the same link (<see cref="PassageReason.Straddled"/>)
    /// drops the link and says why; the preview loses the colour and the person links again in one gesture.
    ///
    /// ⚠ **A format 1 text goes through untouched**: no link, no decision, the same string (checked, including
    /// that it allocates nothing).
    /// </summary>
    public static class LinkEditing
    {
        private static readonly Passage[] NoPassages = new Passage[0];
        private static readonly LineLink[] NoLinks = new LineLink[0];
        private static readonly string[] NoStrings = new string[0];
        private static readonly NewForm[] NoForms = new NewForm[0];
        private static readonly LostPassage[] NoLost = new LostPassage[0];

        // ── Show ────────────────────────────────────────────────────────────

        /// <summary>The text in clear and its passages, from a line of the file.</summary>
        public static EditState Show(TranslationLine line, int fileFormat, Func<string, TranslationLine?> entryOf,
                                     Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            string v = line.Value ?? string.Empty;
            bool format2 = fileFormat >= 2;
            var unlinked = line.Unlinked ?? (IReadOnlyList<string>)NoStrings;

            // A format 1 text, or a format 2 text with no link and no marker: the string as it is.
            if (!format2 || (CountOf(line.Links) == 0 && v.IndexOf("[!g*", StringComparison.Ordinal) < 0))
                return Plain(v, format2, unlinked);

            var spans = LineMarkers.LinkSpans(v);
            var links = line.Links ?? (IReadOnlyList<LineLink>)NoLinks;
            var text = new StringBuilder(v.Length);
            var passages = new List<Passage>();
            var kept = new List<LineLink>();
            bool damaged = false;
            int at = 0;

            foreach (var span in spans)
            {
                text.Append(v, at, span.Start - at);
                at = span.Start + span.Length;

                if (span.Number >= links.Count) { damaged = true; continue; }

                LineLink link = links[span.Number];
                string shown = LinkReading.Read(link, entryOf, upperFirst, lowerFirst).Text;
                passages.Add(new Passage { Start = text.Length, Length = shown.Length, Origin = shown, Order = passages.Count });
                kept.Add(link);
                text.Append(shown);
            }
            text.Append(v, at, v.Length - at);

            return new EditState
            {
                Text = text.ToString(),
                Format2 = true,
                Passages = passages,
                Links = kept,
                Lost = NoLost,
                Unlinked = unlinked,
                NewForms = NoForms,
                Damaged = damaged,
            };
        }

        /// <summary>How each passage reads now: its source, whether it is « to read again », whether it waits for its question.</summary>
        public static List<PassageView> View(EditState state, Func<string, TranslationLine?> entryOf,
                                             Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var settled = Settle(state, entryOf, upperFirst, lowerFirst);
            var lookup = WithNewForms(entryOf, settled.NewForms);
            var views = new List<PassageView>();
            for (int i = 0; i < CountOf(settled.Passages); i++)
            {
                Passage p = settled.Passages![i];
                LineLink link = settled.Links![i];
                var read = LinkReading.Read(link, lookup, upperFirst, lowerFirst);
                views.Add(new PassageView
                {
                    Start = p.Start,
                    Length = p.Length,
                    Source = read.Source,
                    NeedsReview = LinkReading.NeedsReview(link, read),
                    CaseNotApplied = read.CaseNotApplied,
                    Pending = IsPending(settled, i),
                    Open = p.Open,
                });
            }
            return views;
        }

        // ── Step ────────────────────────────────────────────────────────────

        /// <summary>
        /// What one change of the text does to the passages (spec §4). Call it at every change of the text; the state
        /// it returns is the base of the next call. The links are not touched: whether changed words are a known
        /// writing is read by <see cref="Settle"/>, when somebody needs to know.
        /// </summary>
        public static EditState Step(EditState state, string newText)
        {
            if (newText == null) throw new ArgumentNullException(nameof(newText));
            if (string.Equals(state.Text, newText, StringComparison.Ordinal)) return state;

            // No passage, nothing remembered: the text is only a text.
            if (CountOf(state.Passages) == 0 && CountOf(state.Lost) == 0)
            {
                state.Text = newText;
                return state;
            }

            return StepCore(state, newText);
        }

        // Apart from Step on purpose: the plain paths above must allocate nothing.
        private static EditState StepCore(EditState state, string newText)
        {
            TextHunk hunk = Slide(state.Text, newText, TextDiff.Hunk(state.Text, newText)!.Value, state.Passages);
            int shift = (hunk.NewEnd - hunk.NewStart) - (hunk.OldEnd - hunk.OldStart);

            var passages = new List<Passage>(CountOf(state.Passages) + 1);
            var links = new List<LineLink>(CountOf(state.Passages) + 1);
            var lost = new List<LostPassage>(state.Lost ?? NoLost);
            var before = new List<LostPassage>(lost);   // what a LATER step may bring back: not what this one removes

            for (int i = 0; i < CountOf(state.Passages); i++)
            {
                Passage p = state.Passages![i];
                int a = p.Start, b = p.Start + p.Length;
                LineLink link = state.Links![i];

                if (PassageBorder.Straddles(hunk.OldStart, hunk.OldEnd, a, b, p.Open))
                {
                    lost.Add(new LostPassage { Words = state.Text.Substring(a, p.Length), Origin = p.Origin, Link = link, Reason = PassageReason.Straddled, Order = p.Order });
                    continue;
                }

                if (PassageBorder.Touches(hunk.OldStart, hunk.OldEnd, a, b, p.Open))
                {
                    int end = b + shift;
                    if (end <= a)
                    {
                        lost.Add(new LostPassage { Words = state.Text.Substring(a, p.Length), Origin = p.Origin, Link = link, Reason = PassageReason.Deleted, Order = p.Order });
                        continue;
                    }
                    p.Length = end - a;
                    p.Open = PassageBorder.StaysOpen(hunk.OldStart, hunk.OldEnd, a, b, p.Open);
                }
                else
                {
                    if (hunk.OldEnd <= a) p.Start = a + shift;
                    p.Open = false;
                }

                passages.Add(p);
                links.Add(link);
            }

            // A passage lost EARLIER comes back when the words inserted now are exactly its words, once.
            int inserted = hunk.NewEnd - hunk.NewStart;
            if (inserted > 0 && before.Count > 0)
            {
                string words = newText.Substring(hunk.NewStart, inserted);
                int pick = -1;
                for (int i = 0; i < before.Count; i++)
                    if (before[i].Reason != PassageReason.EntryGone && string.Equals(before[i].Words, words, StringComparison.Ordinal)
                        && (pick < 0 || before[i].Order < before[pick].Order)) pick = i;

                if (pick >= 0 && !OverlapsAny(passages, hunk.NewStart, hunk.NewEnd))
                {
                    LostPassage back = before[pick];
                    int at = 0;
                    while (at < passages.Count && passages[at].Start < hunk.NewStart) at++;
                    passages.Insert(at, new Passage { Start = hunk.NewStart, Length = inserted, Origin = back.Origin, Order = back.Order });
                    links.Insert(at, back.Link);
                    lost.Remove(back);
                }
            }

            state.Text = newText;
            state.Passages = passages;
            state.Links = links;
            state.Lost = lost;
            return state;
        }

        /// <summary>
        /// A hunk can often be placed more than one way with no change in what the texts say (erasing the last word of
        /// a passage with the space after it, or the space before it). The three lines of the diff place it at the
        /// right-most; it slides left or right while the text it passes over is the text it leaves behind, to the
        /// placement that straddles the fewest passages — the unslid one first, then one step left, one step right,
        /// two steps left… (spec §4).
        /// </summary>
        private static TextHunk Slide(string o, string f, TextHunk cur, IReadOnlyList<Passage>? passages)
        {
            int count = CountOf(passages);
            if (count == 0) return cur;

            int maxLeft = 0;
            while (cur.OldStart - maxLeft - 1 >= 0 && cur.NewStart - maxLeft - 1 >= 0
                   && o[cur.OldStart - maxLeft - 1] == o[cur.OldEnd - maxLeft - 1]
                   && f[cur.NewStart - maxLeft - 1] == f[cur.NewEnd - maxLeft - 1]
                   && !char.IsSurrogate(o[cur.OldStart - maxLeft - 1]) && !char.IsSurrogate(f[cur.NewStart - maxLeft - 1]))
                maxLeft++;

            int maxRight = 0;
            while (cur.OldEnd + maxRight < o.Length && cur.NewEnd + maxRight < f.Length
                   && o[cur.OldStart + maxRight] == o[cur.OldEnd + maxRight]
                   && f[cur.NewStart + maxRight] == f[cur.NewEnd + maxRight]
                   && !char.IsSurrogate(o[cur.OldEnd + maxRight]) && !char.IsSurrogate(f[cur.NewEnd + maxRight]))
                maxRight++;

            TextHunk best = cur;
            int bestCost = Straddled(cur, passages!);
            for (int step = 1; bestCost > 0 && (step <= maxLeft || step <= maxRight); step++)
            {
                for (int side = 0; side < 2; side++)
                {
                    int shift = side == 0 ? -step : step;
                    if (shift < 0 ? -shift > maxLeft : shift > maxRight) continue;
                    var moved = new TextHunk
                    {
                        OldStart = cur.OldStart + shift, OldEnd = cur.OldEnd + shift,
                        NewStart = cur.NewStart + shift, NewEnd = cur.NewEnd + shift,
                    };
                    int cost = Straddled(moved, passages!);
                    if (cost < bestCost) { best = moved; bestCost = cost; }
                }
            }
            return best;
        }

        private static int Straddled(TextHunk h, IReadOnlyList<Passage> passages)
        {
            int n = 0;
            foreach (var p in passages)
                if (PassageBorder.Straddles(h.OldStart, h.OldEnd, p.Start, p.Start + p.Length, p.Open)) n++;
            return n;
        }

        private static bool OverlapsAny(List<Passage> passages, int from, int to)
        {
            foreach (var p in passages)
                if (from < p.Start + p.Length && to > p.Start) return true;
            return false;
        }

        // ── Settle ──────────────────────────────────────────────────────────

        /// <summary>
        /// Reads what the changed passages are: words equal to their <c>Origin</c> are as they were; words that are a
        /// writing the term already has (exact, or with the first letter's case) point the link at it; words that
        /// complete such a writing found in the text are widened to it (absorption); a term that no longer exists
        /// drops the passage (its words stay, as ordinary text). What remains changed is waiting for its question
        /// (<see cref="NextQuestion"/>). Idempotent; the acts and <see cref="Resume"/> call it first.
        /// </summary>
        public static EditState Settle(EditState state, Func<string, TranslationLine?> entryOf,
                                       Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            int n = CountOf(state.Passages);
            if (n == 0) return state;

            bool any = false;
            for (int i = 0; i < n; i++) if (IsPending(state, i)) { any = true; break; }
            if (!any) return state;

            var lookup = WithNewForms(entryOf, state.NewForms);
            var passages = new List<Passage>(state.Passages!);
            var links = new List<LineLink>(state.Links!);
            var lost = new List<LostPassage>(state.Lost ?? NoLost);
            var dropped = new List<int>();

            for (int i = 0; i < n; i++)
            {
                if (!IsPending(state, i)) continue;

                Passage p = passages[i];
                LineLink link = links[i];
                string words = state.Text.Substring(p.Start, p.Length);
                TranslationLine? entry = string.IsNullOrEmpty(link.To) ? (TranslationLine?)null : lookup(link.To!);

                if (!entry.HasValue)
                {
                    lost.Add(new LostPassage { Words = words, Origin = p.Origin, Link = link, Reason = PassageReason.EntryGone, Order = p.Order });
                    dropped.Add(i);
                    continue;
                }

                string? form, casing;
                if (!Known(entry.Value, words, upperFirst, lowerFirst, out form, out casing))
                {
                    if (!Absorb(state, passages, i, entry.Value, upperFirst, lowerFirst, out int start, out string? absorbed)) continue;   // still pending
                    p.Start = start; p.Length = absorbed!.Length; words = absorbed;
                    Known(entry.Value, words, upperFirst, lowerFirst, out form, out casing);
                }

                p.Origin = words;
                passages[i] = p;
                links[i] = new LineLink { To = link.To, Form = form, Case = casing, By = LinkReading.ByPerson, Was = words };
            }

            for (int k = dropped.Count - 1; k >= 0; k--) { passages.RemoveAt(dropped[k]); links.RemoveAt(dropped[k]); }

            state.Passages = passages;
            state.Links = links;
            state.Lost = lost;
            return state;
        }

        /// <summary>A writing of the term that contains the changed words, found in the text and overlapping no other passage.</summary>
        private static bool Absorb(EditState state, List<Passage> passages, int index, TranslationLine entry,
                                   Func<string, string>? upperFirst, Func<string, string>? lowerFirst, out int start, out string? words)
        {
            Passage p = passages[index];
            foreach (string writing in Variants(entry, upperFirst, lowerFirst))
            {
                int at = PassageBorder.AbsorbedAt(state.Text, p.Start, p.Start + p.Length, writing);
                if (at < 0) continue;

                bool clash = false;
                for (int j = 0; j < passages.Count && !clash; j++)
                    clash = j != index && at < passages[j].Start + passages[j].Length && at + writing.Length > passages[j].Start;
                if (clash) continue;

                start = at; words = writing;
                return true;
            }
            start = 0; words = null;
            return false;
        }

        // ── The question ────────────────────────────────────────────────────

        /// <summary>
        /// The one question to ask next, or null. Passages that are the same term with the same new words share it
        /// (one answer for both: it is the same writing of the same term); others are asked one at a time.
        /// The indices are those of <see cref="Settle"/>'s state.
        /// </summary>
        public static EditQuestion? NextQuestion(EditState state, Func<string, TranslationLine?> entryOf,
                                                 Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            state = Settle(state, entryOf, upperFirst, lowerFirst);
            for (int i = 0; i < CountOf(state.Passages); i++)
            {
                if (!IsPending(state, i)) continue;

                string words = state.Text.Substring(state.Passages![i].Start, state.Passages[i].Length);
                string entry = state.Links![i].To ?? string.Empty;
                var same = new List<int> { i };
                for (int j = i + 1; j < state.Passages.Count; j++)
                    if (IsPending(state, j) && (state.Links[j].To ?? string.Empty) == entry
                        && string.Equals(state.Text.Substring(state.Passages[j].Start, state.Passages[j].Length), words, StringComparison.Ordinal))
                        same.Add(j);
                return new EditQuestion { Entry = entry, Text = words, Passages = same };
            }
            return null;
        }

        /// <summary>The answer to <see cref="NextQuestion"/>.</summary>
        public static EditState Answer(EditState state, EditQuestion question, EditAnswer answer, Func<string, TranslationLine?> entryOf,
                                       Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            state = Settle(state, entryOf, upperFirst, lowerFirst);
            var passages = new List<Passage>(state.Passages!);
            var links = new List<LineLink>(state.Links!);
            var forms = new List<NewForm>(state.NewForms ?? NoForms);

            string? id = null;
            if (answer == EditAnswer.AllTexts)
            {
                foreach (var f in forms)
                    if (f.Entry == question.Entry && f.Text == question.Text) id = f.Id;

                if (id == null)
                {
                    int max = 0;
                    TranslationLine? entry = entryOf(question.Entry);
                    if (entry.HasValue && entry.Value.Forms != null)
                        foreach (var form in entry.Value.Forms) max = Math.Max(max, FormNumber(form.Key));
                    foreach (var f in forms) if (f.Entry == question.Entry) max = Math.Max(max, FormNumber(f.Id));
                    id = "f" + (max + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    forms.Add(new NewForm { Entry = question.Entry, Id = id, Text = question.Text });
                }
            }

            foreach (int i in question.Passages)
            {
                LineLink link = links[i];
                links[i] = answer == EditAnswer.AllTexts
                    ? new LineLink { To = link.To, Form = id, By = LinkReading.ByPerson, Was = question.Text }
                    : new LineLink { To = link.To, Form = link.Form, By = LinkReading.ByPerson, Was = question.Text, Own = question.Text };
                Passage p = passages[i];
                p.Origin = question.Text;
                passages[i] = p;
            }

            state.Passages = passages;
            state.Links = links;
            state.NewForms = forms;
            return state;
        }

        // ── The acts of the person ─────────────────────────────────────────

        /// <summary><c>Unlink</c>: the words stay, the link goes, and the term is retained as « not here » for the machine.</summary>
        public static EditAct Unlink(EditState state, int passage, Func<string, TranslationLine?> entryOf,
                                     Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            state = Settle(state, entryOf, upperFirst, lowerFirst);
            if (HasPending(state)) return Problem(state, "answer-first");
            if (passage < 0 || passage >= CountOf(state.Passages)) return Problem(state, "no-such-passage");

            var passages = new List<Passage>(state.Passages!);
            var links = new List<LineLink>(state.Links!);
            string term = links[passage].To ?? string.Empty;
            passages.RemoveAt(passage);
            links.RemoveAt(passage);

            var unlinked = new List<string>(state.Unlinked ?? NoStrings);
            // A term that no longer exists has nothing to be kept away from.
            if (entryOf(term).HasValue && !unlinked.Contains(term)) unlinked.Add(term);

            // The machine would take the other passages of that term away on its next pass: a person's word keeps them.
            for (int i = 0; i < links.Count; i++)
                if (links[i].To == term && links[i].By != LinkReading.ByPerson)
                {
                    LineLink l = links[i];
                    l.By = LinkReading.ByPerson;
                    links[i] = l;
                }

            state.Passages = passages;
            state.Links = links;
            state.Unlinked = unlinked;
            return new EditAct { State = state };
        }

        /// <summary>
        /// <c>Link to term</c>: the selected words become a passage of that term. With a form given, the selection is
        /// replaced by that form's words. A selection that is not a known writing becomes a changed passage — the same
        /// question as changed words.
        /// </summary>
        public static EditAct LinkTo(EditState state, int start, int length, string entryKey, string? formId,
                                     Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            state = Settle(state, entryOf, upperFirst, lowerFirst);
            if (HasPending(state)) return Problem(state, "answer-first");
            if (start < 0 || length <= 0 || start + length > state.Text.Length) return Problem(state, "bad-selection");

            var passages = new List<Passage>(state.Passages ?? NoPassages);
            var links = new List<LineLink>(state.Links ?? NoLinks);
            foreach (var p in passages)
                if (start < p.Start + p.Length && start + length > p.Start) return Problem(state, "already-a-term");

            TranslationLine? entry = WithNewForms(entryOf, state.NewForms)(entryKey);
            if (!entry.HasValue) return Problem(state, "term-gone");

            string text = state.Text;
            string words = text.Substring(start, length);
            LineLink link = new LineLink { To = entryKey, By = LinkReading.ByPerson, Was = words };
            string? origin = words;

            if (formId != null)
            {
                string? writing = formId == LinkReading.OwnForm ? entry.Value.Value : FormOf(entry.Value, formId);
                if (string.IsNullOrEmpty(writing)) return Problem(state, "no-such-form");
                text = text.Substring(0, start) + writing + text.Substring(start + length);
                int shift = writing!.Length - length;
                for (int i = 0; i < passages.Count; i++)
                    if (passages[i].Start >= start + length) { Passage q = passages[i]; q.Start += shift; passages[i] = q; }
                link.Form = formId == LinkReading.OwnForm ? null : formId;
                link.Was = writing;
                length = writing.Length;
                origin = writing;
            }
            else if (Known(entry.Value, words, upperFirst, lowerFirst, out string? form, out string? casing))
            {
                link.Form = form;
                link.Case = casing;
            }
            else origin = string.Empty;   // not a writing of the term: it waits for its question like changed words

            int order = 0;
            foreach (var p in passages) order = Math.Max(order, p.Order + 1);
            foreach (var l in state.Lost ?? NoLost) order = Math.Max(order, l.Order + 1);

            int at = 0;
            while (at < passages.Count && passages[at].Start < start) at++;
            passages.Insert(at, new Passage { Start = start, Length = length, Origin = origin, Order = order });
            links.Insert(at, link);

            var unlinked = new List<string>(state.Unlinked ?? NoStrings);
            unlinked.Remove(entryKey);

            state.Text = text;
            state.Passages = passages;
            state.Links = links;
            state.Unlinked = unlinked;
            return new EditAct { State = state };
        }

        /// <summary><c>Mark as read</c>: every link takes the words it reads today as the ones a person read. Nothing else moves.</summary>
        public static EditAct MarkAsRead(EditState state, Func<string, TranslationLine?> entryOf,
                                         Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            state = Settle(state, entryOf, upperFirst, lowerFirst);
            if (HasPending(state)) return Problem(state, "answer-first");

            var lookup = WithNewForms(entryOf, state.NewForms);
            var links = new List<LineLink>(state.Links ?? NoLinks);
            bool any = false;
            for (int i = 0; i < links.Count; i++)
            {
                var read = LinkReading.Read(links[i], lookup, upperFirst, lowerFirst);
                if (read.Source == LinkSource.Was || read.Source == LinkSource.Key) continue;
                LineLink l = links[i];
                l.Was = read.Text;
                links[i] = l;
                any = true;
            }
            // The term is gone: there is nothing to read again, and the person is told what to do instead.
            if (!any) return Problem(state, "nothing-to-read");

            state.Links = links;
            return new EditAct { State = state };
        }

        // ── Resume ──────────────────────────────────────────────────────────

        /// <summary>
        /// What to write: <c>v</c> with its markers, the links, the forms to add, the refused entries — or the question
        /// still to ask, or why the text cannot be saved. <paramref name="original"/> is the state <see cref="Show"/> gave.
        ///
        /// A real modification confirms the links of the line (they were under the person's eyes): each takes the
        /// words it reads now as <c>was</c> and becomes <c>by: H</c>. Saving with nothing different writes nothing.
        /// <c>Mark as read</c> alone moves only <c>was</c>.
        /// </summary>
        public static EditOutcome Resume(EditState original, EditState current, Func<string, TranslationLine?> entryOf,
                                         Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            current = Settle(current, entryOf, upperFirst, lowerFirst);
            var question = NextQuestion(current, entryOf, upperFirst, lowerFirst);
            if (question.HasValue) return new EditOutcome { Changed = true, Question = question };

            if (current.Format2 && current.Text.IndexOf("[!g*", StringComparison.Ordinal) >= 0
                && LineMarkers.LinkNumbers(current.Text).Count > 0)
                return new EditOutcome { Changed = true, Problem = "text-has-link-marker" };

            bool sameText = string.Equals(original.Text, current.Text, StringComparison.Ordinal);
            int passageCount = CountOf(current.Passages);

            // The plain case: no link anywhere. The string goes through as it is.
            if (passageCount == 0 && CountOf(original.Passages) == 0 && !current.Damaged && !original.Damaged
                && CountOf(current.NewForms) == 0 && SameStrings(original.Unlinked, current.Unlinked))
                return sameText
                    ? new EditOutcome { Changed = false }
                    : new EditOutcome { Changed = true, V = current.Text, Links = NoLinks, NewForms = NoForms, Unlinked = current.Unlinked ?? NoStrings };

            bool onlyWas = sameText && SamePassages(original, current) && SameStrings(original.Unlinked, current.Unlinked)
                           && CountOf(current.NewForms) == CountOf(original.NewForms) && SameLinks(original.Links, current.Links, false);
            bool identical = onlyWas && SameLinks(original.Links, current.Links, true);
            if (identical) return new EditOutcome { Changed = false };

            var links = new List<LineLink>(current.Links ?? NoLinks);
            if (!onlyWas)
            {
                var lookup = WithNewForms(entryOf, current.NewForms);
                for (int i = 0; i < links.Count; i++)
                {
                    LineLink l = links[i];
                    var read = LinkReading.Read(l, lookup, upperFirst, lowerFirst);
                    l.By = LinkReading.ByPerson;
                    l.Was = read.Text;
                    links[i] = l;
                }
            }

            var v = new StringBuilder(current.Text.Length + 8 * passageCount);
            int at = 0;
            for (int i = 0; i < passageCount; i++)
            {
                Passage p = current.Passages![i];
                v.Append(current.Text, at, p.Start - at).Append("[!g*").Append(i.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(']');
                at = p.Start + p.Length;
            }
            v.Append(current.Text, at, current.Text.Length - at);

            return new EditOutcome
            {
                Changed = true,
                V = v.ToString(),
                Links = links,
                NewForms = current.NewForms ?? NoForms,
                Unlinked = current.Unlinked ?? NoStrings,
            };
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static EditState Plain(string text, bool format2, IReadOnlyList<string> unlinked) => new EditState
        {
            Text = text,
            Format2 = format2,
            Passages = NoPassages,
            Links = NoLinks,
            Lost = NoLost,
            Unlinked = unlinked,
            NewForms = NoForms,
        };

        private static EditAct Problem(EditState state, string code) => new EditAct { State = state, Problem = code };

        private static int CountOf<T>(IReadOnlyList<T>? list) => list == null ? 0 : list.Count;

        /// <summary>Changed words: not what the passage had when its link was last settled.</summary>
        private static bool IsPending(EditState state, int index)
        {
            Passage p = state.Passages![index];
            if (p.Origin == null) return false;
            return p.Origin.Length != p.Length
                || string.CompareOrdinal(state.Text, p.Start, p.Origin, 0, p.Length) != 0;
        }

        private static bool HasPending(EditState state)
        {
            for (int i = 0; i < CountOf(state.Passages); i++) if (IsPending(state, i)) return true;
            return false;
        }

        private static int FormNumber(string id)
        {
            if (id.Length > 1 && id[0] == 'f' && int.TryParse(id.Substring(1), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out int n)) return n;
            return 0;
        }

        private static string? FormOf(TranslationLine entry, string formId)
        {
            if (entry.Forms != null)
                foreach (var f in entry.Forms)
                    if (f.Key == formId) return f.Value;
            return null;
        }

        /// <summary>The writings of a term, each with the first-letter case variants the caller's functions give — in order.</summary>
        private static IEnumerable<string> Variants(TranslationLine entry, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var writings = new List<string> { entry.Value ?? string.Empty };
            if (entry.Forms != null) foreach (var f in entry.Forms) writings.Add(f.Value);

            var seen = new List<string>();
            foreach (string w in writings)
            {
                if (w.Length == 0) continue;
                var variants = new List<string> { w };
                if (upperFirst != null) variants.Add(upperFirst(w));
                if (lowerFirst != null) variants.Add(lowerFirst(w));
                foreach (string variant in variants)
                    if (!string.IsNullOrEmpty(variant) && !seen.Contains(variant)) { seen.Add(variant); yield return variant; }
            }
        }

        /// <summary>
        /// Are these words a writing the term already has? Exactly, or with the first letter's case as <see cref="Casing"/>
        /// gives it (the functions the caller hands in). A known writing needs no question.
        /// </summary>
        private static bool Known(TranslationLine entry, string words, Func<string, string>? upperFirst, Func<string, string>? lowerFirst,
                                  out string? form, out string? casing)
        {
            var writings = new List<KeyValuePair<string?, string>>();
            writings.Add(new KeyValuePair<string?, string>(null, entry.Value ?? string.Empty));
            if (entry.Forms != null)
                foreach (var f in entry.Forms) writings.Add(new KeyValuePair<string?, string>(f.Key, f.Value));

            foreach (var w in writings)
                if (w.Value.Length > 0 && string.Equals(w.Value, words, StringComparison.Ordinal)) { form = w.Key; casing = null; return true; }

            foreach (var w in writings)
            {
                if (w.Value.Length == 0) continue;
                if (upperFirst != null && string.Equals(upperFirst(w.Value), words, StringComparison.Ordinal))
                { form = w.Key; casing = LinkReading.UpperFirst; return true; }
                if (lowerFirst != null && string.Equals(lowerFirst(w.Value), words, StringComparison.Ordinal))
                { form = w.Key; casing = LinkReading.LowerFirst; return true; }
            }

            form = null; casing = null;
            return false;
        }

        /// <summary>The file's lines, with the forms this editing adds already on their term.</summary>
        private static Func<string, TranslationLine?> WithNewForms(Func<string, TranslationLine?> entryOf, IReadOnlyList<NewForm>? added)
        {
            if (CountOf(added) == 0) return entryOf;
            return key =>
            {
                TranslationLine? line = entryOf(key);
                if (!line.HasValue) return line;
                TranslationLine entry = line.Value;
                var forms = new List<KeyValuePair<string, string>>(entry.Forms ?? new KeyValuePair<string, string>[0]);
                foreach (var f in added!)
                    if (f.Entry == key) forms.Add(new KeyValuePair<string, string>(f.Id, f.Text));
                entry.Forms = forms;
                return entry;
            };
        }

        private static bool SamePassages(EditState a, EditState b)
        {
            if (CountOf(a.Passages) != CountOf(b.Passages)) return false;
            for (int i = 0; i < CountOf(a.Passages); i++)
                if (a.Passages![i].Start != b.Passages![i].Start || a.Passages[i].Length != b.Passages[i].Length) return false;
            return true;
        }

        private static bool SameLinks(IReadOnlyList<LineLink>? a, IReadOnlyList<LineLink>? b, bool withWas)
        {
            if (CountOf(a) != CountOf(b)) return false;
            for (int i = 0; i < CountOf(a); i++)
            {
                LineLink x = a![i], y = b![i];
                if (x.To != y.To || x.Form != y.Form || x.Case != y.Case || x.By != y.By || x.Own != y.Own) return false;
                if (withWas && x.Was != y.Was) return false;
            }
            return true;
        }

        private static bool SameStrings(IReadOnlyList<string>? a, IReadOnlyList<string>? b)
        {
            if (CountOf(a) != CountOf(b)) return false;
            for (int i = 0; i < CountOf(a); i++)
            {
                bool found = false;
                for (int j = 0; j < CountOf(b); j++) if (a![i] == b![j]) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }
    }
}
