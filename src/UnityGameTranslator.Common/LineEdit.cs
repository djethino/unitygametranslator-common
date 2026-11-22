using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>What a sentence's edit keeps between two gestures: the line it was opened from, the text she sees, the question asked.</summary>
    public struct LineEdit
    {
        public string Key;

        /// <summary>The line as the file holds it; null when the key is not a line yet.</summary>
        public TranslationLine? Held;

        public int FileFormat;

        /// <summary>The state at opening — « nothing changed » is judged against it, never against the file as it moves.</summary>
        public EditState Original;

        public EditState State;

        /// <summary>The question <see cref="LineEditing.Finish"/> asked and waits an answer for; null when none.</summary>
        public EditQuestion? Question;

        /// <summary>The text in clear: what the field holds.</summary>
        public string Text => State.Text;
    }

    /// <summary>What the file is, for the questions a write asks of it.</summary>
    public sealed class LineFile
    {
        /// <summary>The file's format as its CONTENT says (<see cref="TranslationFormat.OfContent"/>), the line being edited included.</summary>
        public int Format = 2;

        /// <summary>Does the file's <c>_lists</c> hold a list of that name? Null = none.</summary>
        public Func<string, bool>? HasList;
    }

    /// <summary>A passage as the preview draws it.</summary>
    public struct LinePassage
    {
        public int Index;
        public PassageView View;
        public string? Term;
        public string? By;

        /// <summary>The sentence writes this term its own way (<see cref="LineLink.Own"/>).</summary>
        public bool Own;
    }

    /// <summary>A passage the last changes LOST, with the reason (a code, never words).</summary>
    public struct LinePassageLost
    {
        public string Words;
        public PassageReason Reason;
    }

    /// <summary>What the preview draws beside the field.</summary>
    public struct LinePreview
    {
        public string Text;
        public IReadOnlyList<LinePassage> Passages;
        public IReadOnlyList<LinePassageLost> Lost;
        public bool Damaged;

        /// <summary>Some passage holds changed words nobody has said what they are.</summary>
        public bool Pending;
    }

    /// <summary>The result of an act on a passage: the edit, and why it could not be done (a code, null when done).</summary>
    public struct LineAct
    {
        public LineEdit Edit;
        public string? Problem;
    }

    public enum LineFinishKind
    {
        /// <summary>Nothing differs from what was shown: nothing is written.</summary>
        Nothing,

        /// <summary>The text cannot be saved as it stands (<see cref="LineFinish.Code"/>).</summary>
        Problem,

        /// <summary>Answer <see cref="LineFinish.Question"/> (<see cref="LineEditing.Answer"/>) and finish again.</summary>
        Question,

        /// <summary>The file would not accept the write (<see cref="LineFinish.Code"/>: a <see cref="LineWrites"/> code).</summary>
        Refused,

        /// <summary>Write <see cref="LineFinish.Lines"/>, all of them or none.</summary>
        Write,
    }

    /// <summary>A line to write: its key and the line ENTIRE (words, tag, every field).</summary>
    public struct LineOut
    {
        public string Key;
        public TranslationLine Line;
    }

    public struct LineFinish
    {
        public LineFinishKind Kind;

        /// <summary>Why: a code (<see cref="LineFinishKind.Problem"/>, <see cref="LineFinishKind.Refused"/>).</summary>
        public string? Code;

        /// <summary>The refusal's sentence, in English, when the socle has one (<see cref="LineWrites"/>'s). Not part of the contract: the code is.</summary>
        public string? Words;

        public EditQuestion? Question;

        /// <summary>With a question: the edit, settled, to keep and answer.</summary>
        public LineEdit Edit;

        /// <summary>The sentence first, then each term that gained a written form.</summary>
        public IReadOnlyList<LineOut> Lines;
    }

    /// <summary>A term a selection can be linked to.</summary>
    public struct TermChoice
    {
        public string Key;
        public string Text;
        public IReadOnlyList<KeyValuePair<string, string>> Forms;
    }

    public struct TermChoiceList
    {
        public IReadOnlyList<TermChoice> Items;

        /// <summary>How many terms match, whatever <c>limit</c> cut: the list says it is cut.</summary>
        public int Total;
    }

    /// <summary>
    /// Editing a sentence that holds links — the whole conversation, pure (spec/translation-file/line-edit.md): open it, be told
    /// every change of the text, draw the preview, take the three acts on a passage, ask the question saving needs, and hand
    /// back the LINES to write. What the mod's panel and the three web editors do with it is the drawing; the rules are
    /// <see cref="LinkEditing"/>'s, held to its corpus, and what any writer owes the file is <see cref="LineWrites"/>'s.
    ///
    /// 🔴 **The state is the edit's, kept between gestures** — never rebuilt from the text: a question answered stays answered
    /// when the next one is asked (a second question used to ask the first again, with an editor that only kept the text).
    ///
    /// 🔴 **What leaves is the line ENTIRE** — the words and every other field it held — plus, when she answered « All texts »,
    /// each term that gained a written form. All of them go in one write, or none: <see cref="Finish"/> asks the file
    /// (<see cref="LineWrites.Refusal"/>) before saying <see cref="LineFinishKind.Write"/>, so an editor never offers a save
    /// the file will refuse.
    /// </summary>
    public static class LineEditing
    {
        /// <summary>
        /// The tag a line takes when a PERSON writes it: H — except M (the mod's own words) and S (skipped), which stay what they
        /// are: every save of the site has always kept them (`EditSessionController`, « manual edit → H »). A tag she chose by hand
        /// is the caller's, and wins.
        /// </summary>
        public static string TagAfterPerson(string? stored) => stored == "M" || stored == "S" ? stored : "H";

        /// <summary>
        /// The tag of words a person SAVES: <c>V</c> when they are the AI's proposal, kept as it came — she saw the machine's words and
        /// accepted them, which is exactly what the A → V gesture records (decided by the user, 2026-11-22: <c>A</c> is the AI nobody
        /// read, <c>H</c> would claim she wrote them; the tag drives the quality score and what the community sees) — else
        /// <see cref="TagAfterPerson"/> (<c>H</c>; a line tagged <c>M</c> or <c>S</c> stays what it is). A proposal she retouched is hers: equality with
        /// the proposal, to the letter, is the whole rule, so retouching and putting it back is keeping it.
        /// </summary>
        /// <param name="stored">The tag the line has, or null.</param>
        /// <param name="written">The words being saved.</param>
        /// <param name="aiProposal">What the AI last proposed for this line, or null when it proposed nothing.</param>
        public static string TagOfWords(string? stored, string? written, string? aiProposal)
            => aiProposal != null && string.Equals(written, aiProposal, StringComparison.Ordinal) && stored != "M" && stored != "S"
                ? "V" : TagAfterPerson(stored);

        // ── Opening and typing ──────────────────────────────────────────────

        /// <summary>
        /// Open a sentence. A file of the format 1 opens too — its state holds no passage and no question, so what comes out is the
        /// same string; an editor that has nothing to say about links simply does not call.
        /// </summary>
        /// <param name="line">The line as the file holds it; null when the key is no line yet.</param>
        /// <param name="current">A line edited and waiting to be saved, or null: the conversation resumes from it while « nothing changed » is still judged against the file.</param>
        public static LineEdit Open(string key, TranslationLine? line, TranslationLine? current, int fileFormat,
                                    Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var held = line ?? new TranslationLine(string.Empty, null!);
            var original = LinkEditing.Show(held, fileFormat, entryOf, upperFirst, lowerFirst);
            var state = current.HasValue ? LinkEditing.Show(current.Value, fileFormat, entryOf, upperFirst, lowerFirst) : original;
            return new LineEdit { Key = key, Held = line, FileFormat = fileFormat, Original = original, State = state };
        }

        /// <summary>The text of the field changed (a keystroke, a paste, an undo): ONE change, exact. A question that was open is closed.</summary>
        public static LineEdit Type(LineEdit edit, string text)
        {
            if (string.Equals(text, edit.State.Text, StringComparison.Ordinal)) return edit;
            edit.State = LinkEditing.Step(edit.State, text);
            edit.Question = null;
            return edit;
        }

        // ── What the preview draws ──────────────────────────────────────────

        public static LinePreview Preview(LineEdit edit, Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var state = edit.State;
            var views = LinkEditing.View(state, entryOf, upperFirst, lowerFirst);
            var passages = new List<LinePassage>();
            bool pending = false;
            for (int i = 0; i < views.Count; i++)
            {
                var link = state.Links![i];
                passages.Add(new LinePassage { Index = i, View = views[i], Term = link.To, By = link.By, Own = link.Own != null });
                if (views[i].Pending) pending = true;
            }
            var lost = new List<LinePassageLost>();
            if (state.Lost != null)
                foreach (var l in state.Lost) lost.Add(new LinePassageLost { Words = l.Words, Reason = l.Reason });
            return new LinePreview { Text = state.Text, Passages = passages, Lost = lost, Damaged = state.Damaged, Pending = pending };
        }

        // ── The three acts on a passage ─────────────────────────────────────

        /// <summary><c>Unlink</c>: the words stay, the link goes, and the term is kept away from the machine in this sentence.</summary>
        public static LineAct Unlink(LineEdit edit, int passage, Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
            => Acted(edit, LinkEditing.Unlink(edit.State, passage, entryOf, upperFirst, lowerFirst));

        /// <summary><c>Link to term</c>: the words selected become a passage of that term; with a form given, they are replaced by that form's words.</summary>
        public static LineAct LinkSelection(LineEdit edit, int start, int length, string termKey, string? formId,
                                            Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
            => Acted(edit, LinkEditing.LinkTo(edit.State, start, length, termKey, formId, entryOf, upperFirst, lowerFirst));

        /// <summary><c>Mark as read</c>: every link takes the words it reads today as the ones a person read.</summary>
        public static LineAct MarkRead(LineEdit edit, Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
            => Acted(edit, LinkEditing.MarkAsRead(edit.State, entryOf, upperFirst, lowerFirst));

        private static LineAct Acted(LineEdit edit, EditAct act)
        {
            if (act.Problem != null) return new LineAct { Edit = edit, Problem = act.Problem };
            edit.State = act.State;
            edit.Question = null;
            return new LineAct { Edit = edit };
        }

        // ── Questions ───────────────────────────────────────────────────────

        /// <summary>What <see cref="Finish"/> would ask next, without writing anything: for an editor to know whether Save is blocked.</summary>
        public static EditQuestion? Waiting(LineEdit edit, Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
            => LinkEditing.NextQuestion(edit.State, entryOf, upperFirst, lowerFirst);

        /// <summary>The answer to the question <see cref="Finish"/> asked. Call <see cref="Finish"/> again after. With no question open it is the one <see cref="Waiting"/> names.</summary>
        public static LineEdit Answer(LineEdit edit, EditAnswer choice, Func<string, TranslationLine?> entryOf, Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var question = edit.Question ?? LinkEditing.NextQuestion(edit.State, entryOf, upperFirst, lowerFirst);
            if (!question.HasValue) return edit;
            edit.State = LinkEditing.Answer(edit.State, question.Value, choice, entryOf, upperFirst, lowerFirst);
            edit.Question = null;
            return edit;
        }

        // ── Saving ──────────────────────────────────────────────────────────

        /// <summary>Saving: nothing to write, a text that cannot be saved, a question, a refusal of the file, or the lines to write.</summary>
        /// <param name="entryOf">The line of an entry of THIS file as it stands: the terms' lines are carried over from it.</param>
        public static LineFinish Finish(LineEdit edit, LineFile file, Func<string, TranslationLine?> entryOf,
                                        Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            var outcome = LinkEditing.Resume(edit.Original, edit.State, entryOf, upperFirst, lowerFirst);

            if (outcome.Question.HasValue)
            {
                edit.State = LinkEditing.Settle(edit.State, entryOf, upperFirst, lowerFirst);
                edit.Question = outcome.Question;
                return new LineFinish { Kind = LineFinishKind.Question, Question = outcome.Question, Edit = edit };
            }
            if (outcome.Problem != null) return new LineFinish { Kind = LineFinishKind.Problem, Code = outcome.Problem };
            if (!outcome.Changed) return new LineFinish { Kind = LineFinishKind.Nothing };

            // The sentence: its words with the markers, and every field the line held — links and refusals replaced
            var sentence = edit.Held ?? default;
            sentence.Value = outcome.V ?? string.Empty;
            sentence.Tag = TagAfterPerson(edit.Held?.Tag);
            sentence.BareString = false;
            sentence.Links = outcome.Links != null && outcome.Links.Count > 0 ? outcome.Links : null;
            sentence.Unlinked = outcome.Unlinked != null && outcome.Unlinked.Count > 0 ? outcome.Unlinked : null;
            var lines = new List<LineOut> { new LineOut { Key = edit.Key, Line = sentence } };

            // The terms that gained a written form (« All texts »): one line each, however many forms it gained
            var order = new List<string>();
            var added = new Dictionary<string, List<NewForm>>(StringComparer.Ordinal);
            if (outcome.NewForms != null)
                foreach (var form in outcome.NewForms)
                {
                    if (!added.TryGetValue(form.Entry, out var forms)) { added[form.Entry] = forms = new List<NewForm>(); order.Add(form.Entry); }
                    forms.Add(form);
                }
            foreach (string termKey in order)
            {
                // Resume only adds a form to a term it has just read through this same lookup: a term that is no line now is a
                // fault of the caller (a lookup that answers differently twice), never a state to hide.
                var term = entryOf(termKey)
                    ?? throw new InvalidOperationException($"The term \"{termKey}\" was read a moment ago and is no line of the file now: the entry lookup must answer the same twice.");

                var line = term;
                var forms = new List<KeyValuePair<string, string>>();
                if (line.Forms != null) forms.AddRange(line.Forms);
                foreach (var form in added[termKey])
                {
                    int at = forms.FindIndex(f => string.Equals(f.Key, form.Id, StringComparison.Ordinal));
                    var pair = new KeyValuePair<string, string>(form.Id, form.Text);
                    if (at >= 0) forms[at] = pair; else forms.Add(pair);
                }
                line.Forms = forms;
                line.Tag ??= "A";
                lines.Add(new LineOut { Key = termKey, Line = line });
            }

            // What the file owes: the file as it will be, asked before anything is offered
            bool anyFields = false;
            foreach (var l in lines) if (l.Line.HasFormat2Fields) anyFields = true;
            int format = file.Format >= 2 || anyFields ? 2 : 1;
            var written = new Dictionary<string, TranslationLine>(StringComparer.Ordinal);
            foreach (var l in lines) written[l.Key] = l.Line;
            Func<string, TranslationLine?> after = k => written.TryGetValue(k, out var w) ? w : entryOf(k);
            var hasList = file.HasList ?? (_ => false);
            foreach (var l in lines)
            {
                TranslationLine? was = l.Key == edit.Key ? edit.Held : entryOf(l.Key);
                var refusal = LineWrites.Refusal(l.Key, l.Line, format, after, hasList, false, was);
                if (refusal.HasValue)
                    return new LineFinish { Kind = LineFinishKind.Refused, Code = refusal.Value.Code, Words = refusal.Value.Words };
            }

            return new LineFinish { Kind = LineFinishKind.Write, Lines = lines };
        }

        // ── Which term to link to ───────────────────────────────────────────

        /// <summary>
        /// The terms a person can link a selection to: the lines marked <c>term</c> whose key or words contain the query, but for the
        /// case of a letter (<see cref="Names"/>, never the runtime's). <c>Total</c> is how many there are, <c>Items</c> the first
        /// <paramref name="limit"/> in the ordinal order of their keys (UTF-16 code units, the content hash's): the list says it is cut.
        /// </summary>
        public static TermChoiceList TermChoices(IEnumerable<KeyValuePair<string, TranslationLine>> file, string? query, int limit = 50)
        {
            string needle = (query ?? string.Empty).TrimSpaces();
            var all = new List<TermChoice>();
            if (file != null)
                foreach (var entry in file)
                {
                    var line = entry.Value;
                    if (!line.Term) continue;
                    string words = line.Value ?? string.Empty;
                    if (needle.Length > 0 && Names.IndexOf(entry.Key, needle) < 0 && Names.IndexOf(words, needle) < 0) continue;
                    all.Add(new TermChoice
                    {
                        Key = entry.Key,
                        Text = words,
                        Forms = line.Forms ?? (IReadOnlyList<KeyValuePair<string, string>>)new KeyValuePair<string, string>[0],
                    });
                }
            all.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return new TermChoiceList { Items = all.Count > limit ? all.GetRange(0, limit) : all, Total = all.Count };
        }
    }
}
