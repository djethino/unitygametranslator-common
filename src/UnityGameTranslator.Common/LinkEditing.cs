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

        /// <summary>The words were changed and the person has not said what they are yet (<see cref="LinkEditing.NextQuestion"/>).</summary>
        public bool Pending;
    }

    /// <summary>A written form to add to a term when the editing is saved.</summary>
    public struct NewForm
    {
        public string Entry;
        public string Id;
        public string Text;
    }

    /// <summary>
    /// Everything an editor holds while a person edits a sentence, and all of it: the text in clear
    /// (links replaced by their words), the linked stretches, the link behind each one (passage
    /// <c>i</c> ↔ link <c>i</c>, one passage one link), the refused entries and the forms to add.
    /// The editor keeps this value and replaces it with what the rules return; it computes nothing.
    /// </summary>
    public struct EditState
    {
        public string Text;

        /// <summary>The file is of the format 2. In a format 1 file nothing below exists.</summary>
        public bool Format2;

        public IReadOnlyList<Passage> Passages;
        public IReadOnlyList<LineLink> Links;
        public IReadOnlyList<string> Unlinked;
        public IReadOnlyList<NewForm> NewForms;

        /// <summary>The line held a link marker with no link: the text shown lacks it, and saving removes it.</summary>
        public bool Damaged;
    }

    public enum PassageOutcome
    {
        /// <summary>Nothing touched it: it follows its text.</summary>
        Intact,

        /// <summary>Its words changed (or it is a known writing now).</summary>
        Modified,

        /// <summary>It was cut and pasted elsewhere; the link follows.</summary>
        Moved,

        /// <summary>The link is gone; the text typed around is ordinary.</summary>
        Gone,
    }

    public enum PassageReason
    {
        None,

        /// <summary>The words were deleted.</summary>
        Deleted,

        /// <summary>A change covers the passage AND the words around it: nothing is guessed.</summary>
        Straddled,

        /// <summary>Its words reappear in several places, or several lost passages claim one place: nothing is chosen for the person.</summary>
        Ambiguous,

        /// <summary>The term no longer exists: its words stay, as ordinary text.</summary>
        EntryGone,
    }

    /// <summary>What became of one passage of the original text.</summary>
    public struct PassageChange
    {
        public PassageOutcome Outcome;
        public PassageReason Reason;

        /// <summary>For Intact, Modified, Moved: where it is now.</summary>
        public int NewStart;
        public int NewLength;
    }

    /// <summary>The state after the comparison, and what became of each original passage.</summary>
    public struct Reconciliation
    {
        public EditState State;
        public IReadOnlyList<PassageChange> Changes;
    }

    /// <summary>The ONE question asked about changed words: is this another writing of the term for everybody, or for this text only?</summary>
    public struct EditQuestion
    {
        public string Entry;
        public string Text;

        /// <summary>The passages (indices in the state) it settles: several when they are the same term with the same new words.</summary>
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
        public bool Pending;
    }

    /// <summary>
    /// The text a person edits with its links (analyse/smart-edition-cas.md): the same pure rules for the
    /// four editors, which only draw.
    ///
    /// 🔴 **The person never sees a marker or a number.** She edits the sentence in clear; the colour of
    /// the linked words is the editor's. At saving, a <c>v</c> with markers and consistent
    /// <c>links</c> are rebuilt — from TWO texts, the one shown and the one she left
    /// (<see cref="Reconcile"/>), never from keystrokes: no editor has to follow a keyboard, and
    /// undo, composition, drag and paste are all just another text.
    ///
    /// ⚠ **A link is known, never guessed** (R4): words that were never a passage stay ordinary,
    /// even when they spell a term exactly. A change that cannot be read as the same link
    /// (<see cref="PassageReason.Straddled"/>, <see cref="PassageReason.Ambiguous"/>) drops the link
    /// and says why; the preview loses the colour and the person links again in one gesture.
    ///
    /// ⚠ **A format 1 text goes through untouched**: no link, no decision, the same string
    /// (checked, including that it allocates nothing).
    /// </summary>
    public static class LinkEditing
    {
        private static readonly Passage[] NoPassages = new Passage[0];
        private static readonly LineLink[] NoLinks = new LineLink[0];
        private static readonly string[] NoStrings = new string[0];
        private static readonly NewForm[] NoForms = new NewForm[0];
        private static readonly PassageChange[] NoChanges = new PassageChange[0];

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
                passages.Add(new Passage { Start = text.Length, Length = shown.Length });
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
                Unlinked = unlinked,
                NewForms = NoForms,
                Damaged = damaged,
            };
        }

        /// <summary>How each passage reads now: its source, whether it is « to read again », for the colour and the hint under the field.</summary>
        public static List<PassageView> View(EditState state, Func<string, TranslationLine?> entryOf,
                                             Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var views = new List<PassageView>();
            var lookup = WithNewForms(entryOf, state.NewForms);
            for (int i = 0; i < CountOf(state.Passages); i++)
            {
                Passage p = state.Passages![i];
                LineLink link = state.Links![i];
                var read = LinkReading.Read(link, lookup, upperFirst, lowerFirst);
                views.Add(new PassageView
                {
                    Start = p.Start,
                    Length = p.Length,
                    Source = read.Source,
                    NeedsReview = LinkReading.NeedsReview(link, read),
                    CaseNotApplied = read.CaseNotApplied,
                    Pending = p.Pending,
                });
            }
            return views;
        }

        // ── Reconcile ───────────────────────────────────────────────────────

        /// <summary>
        /// What became of each passage between the text shown and the one the person left.
        /// Re-run at every change of the field for the preview, and at saving.
        /// </summary>
        public static Reconciliation Reconcile(EditState before, string finalText, Func<string, TranslationLine?> entryOf,
                                               Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            if (finalText == null) throw new ArgumentNullException(nameof(finalText));

            if (string.Equals(before.Text, finalText, StringComparison.Ordinal))
                return new Reconciliation { State = before, Changes = Unchanged(before) };

            if (CountOf(before.Passages) == 0)
            {
                EditState plain = before;
                plain.Text = finalText;
                return new Reconciliation { State = plain, Changes = NoChanges };
            }

            return Compare(before, finalText, entryOf, upperFirst, lowerFirst);
        }

        // Apart from Reconcile on purpose: this method's lambdas make the compiler allocate a closure on ENTRY,
        // and the plain paths above must allocate nothing.
        private static Reconciliation Compare(EditState before, string finalText, Func<string, TranslationLine?> entryOf,
                                              Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var hunks = Slide(before.Text, finalText, TextDiff.Hunks(before.Text, finalText), before.Passages!);
            int count = before.Passages!.Count;
            var changes = new PassageChange[count];
            var texts = new string?[count];         // the new words of a surviving passage
            var spans = new int[count][];           // new [start, end) of a surviving passage

            // 1. Which passages are touched, and how.
            for (int i = 0; i < count; i++)
            {
                Passage p = before.Passages[i];
                int a = p.Start, b = p.Start + p.Length;
                int delta = 0, touchDelta = 0;
                bool touched = false, straddled = false;

                foreach (var h in hunks)
                {
                    int shift = (h.NewEnd - h.NewStart) - (h.OldEnd - h.OldStart);
                    if (PassageBorder.Touches(h.OldStart, h.OldEnd, a, b))
                    {
                        touched = true;
                        touchDelta += shift;
                        if (PassageBorder.Straddles(h.OldStart, h.OldEnd, a, b)) straddled = true;
                    }
                    else if (h.OldEnd <= a) delta += shift;
                }

                if (straddled) { changes[i] = Gone(PassageReason.Straddled); continue; }

                int newStart = a + delta;
                int newEnd = b + delta + touchDelta;

                if (!touched)
                {
                    changes[i] = new PassageChange { Outcome = PassageOutcome.Intact, NewStart = newStart, NewLength = newEnd - newStart };
                    spans[i] = new[] { newStart, newEnd };
                    continue;
                }

                string words = finalText.Substring(newStart, newEnd - newStart);
                if (words.Length == 0) { changes[i] = Gone(PassageReason.Deleted); continue; }

                texts[i] = words;
                spans[i] = new[] { newStart, newEnd };
                changes[i] = new PassageChange { Outcome = PassageOutcome.Modified, NewStart = newStart, NewLength = newEnd - newStart };
            }

            // 2. A lost passage whose exact words come back ONCE, inserted elsewhere, has moved.
            var lost = new bool[count];
            for (int i = 0; i < count; i++)
                lost[i] = changes[i].Outcome == PassageOutcome.Gone && changes[i].Reason == PassageReason.Deleted;

            for (int i = 0; i < count; i++)
            {
                if (!lost[i]) continue;

                string words = before.Text.Substring(before.Passages[i].Start, before.Passages[i].Length);
                var found = new List<int>();
                foreach (var h in hunks)
                {
                    for (int from = h.NewStart; from + words.Length <= h.NewEnd; from++)
                    {
                        int hit = finalText.IndexOf(words, from, h.NewEnd - from, StringComparison.Ordinal);
                        if (hit < 0) break;
                        if (!Overlaps(spans, hit, hit + words.Length)) found.Add(hit);
                        from = hit;
                    }
                }

                int claimants = 0;
                for (int j = 0; j < count; j++)
                    if (lost[j]
                        && string.Equals(before.Text.Substring(before.Passages[j].Start, before.Passages[j].Length), words, StringComparison.Ordinal))
                        claimants++;

                if (found.Count == 1 && claimants == 1)
                {
                    changes[i] = new PassageChange { Outcome = PassageOutcome.Moved, NewStart = found[0], NewLength = words.Length };
                    spans[i] = new[] { found[0], found[0] + words.Length };
                }
                else if (found.Count >= 1)
                    changes[i] = Gone(PassageReason.Ambiguous);
            }

            // 3. The new passages, in reading order; the words of a modified one are settled when known.
            var order = new List<int>();
            for (int i = 0; i < count; i++)
                if (changes[i].Outcome != PassageOutcome.Gone) order.Add(i);
            order.Sort((x, y) => spans[x][0].CompareTo(spans[y][0]));

            var passages = new List<Passage>();
            var links = new List<LineLink>();
            int lastEnd = -1;
            foreach (int i in order)
            {
                if (spans[i][0] < lastEnd) { changes[i] = Gone(PassageReason.Ambiguous); continue; }

                LineLink link = before.Links![i];
                bool pending = false;

                if (changes[i].Outcome == PassageOutcome.Modified)
                {
                    TranslationLine? entry = string.IsNullOrEmpty(link.To) ? (TranslationLine?)null : WithNewForms(entryOf, before.NewForms)(link.To!);
                    if (!entry.HasValue) { changes[i] = Gone(PassageReason.EntryGone); continue; }

                    // Words typed right against a changed passage are outside it (the border rule), but when
                    // they complete a writing the term already has, the person retyped that writing.
                    Passage was = before.Passages[i];
                    int lead = 0, trail = 0;
                    foreach (var h in hunks)
                    {
                        int side = PassageBorder.Adjacent(h.OldStart, h.OldEnd, was.Start, was.Start + was.Length);
                        if (side < 0 && h.NewEnd == spans[i][0]) lead = h.NewEnd - h.NewStart;
                        if (side > 0 && h.NewStart == spans[i][1]) trail = h.NewEnd - h.NewStart;
                    }

                    string? matchedForm = null, matchedCase = null;
                    int takeLead = 0, takeTrail = 0;
                    bool known = false;
                    foreach (var choice in new[] { new[] { 0, 0 }, new[] { lead, 0 }, new[] { 0, trail }, new[] { lead, trail } })
                    {
                        string candidate = finalText.Substring(spans[i][0] - choice[0], spans[i][1] - spans[i][0] + choice[0] + choice[1]);
                        if (Known(entry.GetValueOrDefault(), candidate, upperFirst, lowerFirst, out matchedForm, out matchedCase))
                        {
                            known = true; takeLead = choice[0]; takeTrail = choice[1];
                            texts[i] = candidate;
                            break;
                        }
                    }

                    if (known)
                    {
                        spans[i] = new[] { spans[i][0] - takeLead, spans[i][1] + takeTrail };
                        changes[i] = new PassageChange { Outcome = PassageOutcome.Modified, NewStart = spans[i][0], NewLength = spans[i][1] - spans[i][0] };
                        link = new LineLink { To = link.To, Form = matchedForm, Case = matchedCase, By = LinkReading.ByPerson, Was = texts[i] };
                    }
                    else
                        pending = true;
                }

                passages.Add(new Passage { Start = spans[i][0], Length = spans[i][1] - spans[i][0], Pending = pending });
                links.Add(link);
                lastEnd = spans[i][1];
            }

            return new Reconciliation
            {
                State = new EditState
                {
                    Text = finalText,
                    Format2 = before.Format2,
                    Passages = passages,
                    Links = links,
                    Unlinked = before.Unlinked ?? NoStrings,
                    NewForms = before.NewForms ?? NoForms,
                    Damaged = before.Damaged,
                },
                Changes = changes,
            };
        }

        /// <summary>
        /// A difference can often be placed in more than one way without changing what the two texts
        /// say (deleting the last word of a passage is « word + space » or « space + word »); the
        /// definition of <see cref="TextDiff"/> picks the rightmost, which sometimes covers a
        /// character past the edge of a passage that the person never touched. Here each hunk may
        /// slide while the text it moves over is the text it leaves behind, and takes the placement
        /// that straddles the fewest passages — the unslid one first, then one step to the left,
        /// one to the right, two to the left… (the first of equals wins; no passage is involved
        /// in a difference that does not need it).
        /// </summary>
        private static List<TextHunk> Slide(string o, string f, List<TextHunk> hunks, IReadOnlyList<Passage> passages)
        {
            if (hunks.Count == 0) return hunks;
            var placed = new List<TextHunk>(hunks.Count);

            for (int h = 0; h < hunks.Count; h++)
            {
                TextHunk cur = hunks[h];
                int leftOld = h == 0 ? 0 : placed[h - 1].OldEnd;
                int leftNew = h == 0 ? 0 : placed[h - 1].NewEnd;
                int rightOld = h + 1 < hunks.Count ? hunks[h + 1].OldStart : o.Length;
                int rightNew = h + 1 < hunks.Count ? hunks[h + 1].NewStart : f.Length;

                int maxLeft = 0;
                while (cur.OldStart - maxLeft - 1 >= leftOld && cur.NewStart - maxLeft - 1 >= leftNew
                       && o[cur.OldStart - maxLeft - 1] == o[cur.OldEnd - maxLeft - 1]
                       && f[cur.NewStart - maxLeft - 1] == f[cur.NewEnd - maxLeft - 1]
                       && !char.IsSurrogate(o[cur.OldStart - maxLeft - 1]) && !char.IsSurrogate(f[cur.NewStart - maxLeft - 1]))
                    maxLeft++;

                int maxRight = 0;
                while (cur.OldEnd + maxRight < rightOld && cur.NewEnd + maxRight < rightNew
                       && o[cur.OldStart + maxRight] == o[cur.OldEnd + maxRight]
                       && f[cur.NewStart + maxRight] == f[cur.NewEnd + maxRight]
                       && !char.IsSurrogate(o[cur.OldEnd + maxRight]) && !char.IsSurrogate(f[cur.NewEnd + maxRight]))
                    maxRight++;

                TextHunk best = cur;
                int bestCost = Straddled(cur, passages);
                for (int step = 1; bestCost > 0 && (step <= maxLeft || step <= maxRight); step++)
                {
                    foreach (int shift in new[] { -step, step })
                    {
                        if (shift < 0 ? -shift > maxLeft : shift > maxRight) continue;
                        var moved = new TextHunk
                        {
                            OldStart = cur.OldStart + shift, OldEnd = cur.OldEnd + shift,
                            NewStart = cur.NewStart + shift, NewEnd = cur.NewEnd + shift,
                        };
                        int cost = Straddled(moved, passages);
                        if (cost < bestCost) { best = moved; bestCost = cost; }
                    }
                }
                placed.Add(best);
            }
            return placed;
        }

        private static int Straddled(TextHunk h, IReadOnlyList<Passage> passages)
        {
            int n = 0;
            foreach (var p in passages)
                if (PassageBorder.Straddles(h.OldStart, h.OldEnd, p.Start, p.Start + p.Length)) n++;
            return n;
        }

        // ── The question ────────────────────────────────────────────────────

        /// <summary>
        /// The one question to ask next, or null. Passages that are the same term with the same new
        /// words share it (one answer for both: it is the same writing of the same term); others
        /// are asked one at a time.
        /// </summary>
        public static EditQuestion? NextQuestion(EditState state)
        {
            for (int i = 0; i < CountOf(state.Passages); i++)
            {
                if (!state.Passages![i].Pending) continue;

                string words = state.Text.Substring(state.Passages[i].Start, state.Passages[i].Length);
                string entry = state.Links![i].To ?? string.Empty;
                var same = new List<int> { i };
                for (int j = i + 1; j < state.Passages.Count; j++)
                    if (state.Passages[j].Pending && (state.Links[j].To ?? string.Empty) == entry
                        && string.Equals(state.Text.Substring(state.Passages[j].Start, state.Passages[j].Length), words, StringComparison.Ordinal))
                        same.Add(j);
                return new EditQuestion { Entry = entry, Text = words, Passages = same };
            }
            return null;
        }

        /// <summary>The answer to <see cref="NextQuestion"/>.</summary>
        public static EditState Answer(EditState state, EditQuestion question, EditAnswer answer, Func<string, TranslationLine?> entryOf)
        {
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
                p.Pending = false;
                passages[i] = p;
            }

            EditState next = state;
            next.Passages = passages;
            next.Links = links;
            next.NewForms = forms;
            return next;
        }

        // ── The acts of the person ─────────────────────────────────────────

        /// <summary><c>Unlink</c>: the words stay, the link goes, and the term is retained as « not here » for the machine.</summary>
        public static EditAct Unlink(EditState state, int passage, Func<string, TranslationLine?> entryOf)
        {
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

            EditState next = state;
            next.Passages = passages;
            next.Links = links;
            next.Unlinked = unlinked;
            return new EditAct { State = next };
        }

        /// <summary>
        /// <c>Link to term</c>: the selected words become a passage of that term. With a form given,
        /// the selection is replaced by that form's words. A selection that is not a known writing
        /// becomes a pending passage — the same question as changed words.
        /// </summary>
        public static EditAct LinkTo(EditState state, int start, int length, string entryKey, string? formId,
                                     Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
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
            bool pending = false;

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
            }
            else if (Known(entry.Value, words, upperFirst, lowerFirst, out string? form, out string? casing))
            {
                link.Form = form;
                link.Case = casing;
            }
            else pending = true;

            int at = 0;
            while (at < passages.Count && passages[at].Start < start) at++;
            passages.Insert(at, new Passage { Start = start, Length = length, Pending = pending });
            links.Insert(at, link);

            var unlinked = new List<string>(state.Unlinked ?? NoStrings);
            unlinked.Remove(entryKey);

            EditState next = state;
            next.Text = text;
            next.Passages = passages;
            next.Links = links;
            next.Unlinked = unlinked;
            return new EditAct { State = next };
        }

        /// <summary><c>Mark as read</c>: every link takes the words it reads today as the ones a person read. Nothing else moves.</summary>
        public static EditAct MarkAsRead(EditState state, Func<string, TranslationLine?> entryOf,
                                         Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
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

            EditState next = state;
            next.Links = links;
            return new EditAct { State = next };
        }

        // ── Resume ──────────────────────────────────────────────────────────

        /// <summary>
        /// What to write: <c>v</c> with its markers, the links, the forms to add, the refused
        /// entries — or the question still to ask, or why the text cannot be saved.
        ///
        /// A real modification confirms the links of the line (they were under the person's eyes):
        /// each takes the words it reads now as <c>was</c> and becomes <c>by: H</c>. Saving with
        /// nothing different writes nothing. <c>Mark as read</c> alone moves only <c>was</c>.
        /// </summary>
        public static EditOutcome Resume(EditState original, EditState current, Func<string, TranslationLine?> entryOf,
                                         Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var question = NextQuestion(current);
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
            Unlinked = unlinked,
            NewForms = NoForms,
        };

        private static PassageChange[] Unchanged(EditState state)
        {
            int n = CountOf(state.Passages);
            if (n == 0) return NoChanges;
            var all = new PassageChange[n];
            for (int i = 0; i < n; i++)
                all[i] = new PassageChange { Outcome = PassageOutcome.Intact, NewStart = state.Passages![i].Start, NewLength = state.Passages[i].Length };
            return all;
        }

        private static PassageChange Gone(PassageReason reason) => new PassageChange { Outcome = PassageOutcome.Gone, Reason = reason };

        private static EditAct Problem(EditState state, string code) => new EditAct { State = state, Problem = code };

        private static int CountOf<T>(IReadOnlyList<T>? list) => list == null ? 0 : list.Count;

        private static bool HasPending(EditState state)
        {
            for (int i = 0; i < CountOf(state.Passages); i++) if (state.Passages![i].Pending) return true;
            return false;
        }

        private static bool Overlaps(int[][] spans, int from, int to)
        {
            foreach (var s in spans)
                if (s != null && from < s[1] && to > s[0]) return true;
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

        /// <summary>
        /// Are these words a writing the term already has? Exactly, or with the first letter's case
        /// the caller's own functions give. A known writing needs no question.
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
