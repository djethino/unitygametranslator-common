using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>One written form of a term, as the window holds it.</summary>
    public struct DraftForm
    {
        public string Id;
        public string Text;

        /// <summary>Made in this window: nobody needs it yet, and nothing of the file names it.</summary>
        public bool Added;
    }

    /// <summary>
    /// The window of a term, as a person has left it: the word, the written forms, and the line as it stood when the window
    /// opened. IMMUTABLE — every act of <see cref="TermEdit"/> answers a new draft, as <see cref="LinkEditing.Step"/> answers a new
    /// <see cref="EditState"/>.
    /// </summary>
    public struct TermDraft
    {
        /// <summary>The key of the term's line (typed, for a term being made: not trimmed until it is written).</summary>
        public string Key;

        public string Word;

        /// <summary>The person wrote the word herself: a key typed again does not replace it.</summary>
        public bool WordTouched;

        public IReadOnlyList<DraftForm> Forms;

        /// <summary>For a term being made: the source words selected, trimmed.</summary>
        public string? Selected;

        /// <summary>A term being made (a line created, or a line of the file marked).</summary>
        public bool IsNew;

        /// <summary>The key is a line the file already holds: it BECOMES a term, its translation proposed as the word.</summary>
        public bool Becomes;

        /// <summary>The word the line had when the window opened (empty for a line that does not exist).</summary>
        public string OriginalWord;

        /// <summary>The forms the line had when the window opened.</summary>
        public IReadOnlyList<DraftForm> OriginalForms;
    }

    /// <summary>One thing that changed in the window.</summary>
    public struct TermChange
    {
        /// <summary><c>term</c> (a term made), <c>word</c>, <c>form-added</c>, <c>form-text</c>, <c>form-removed</c>.</summary>
        public string Kind;

        public string? Id;
    }

    /// <summary>Who needs a written form of a term.</summary>
    public struct FormBlockers
    {
        /// <summary>The keys of the sentences whose link names the form.</summary>
        public IReadOnlyList<string> Sentences;

        /// <summary>The term's own <c>use</c> of a template names it.</summary>
        public bool Template;
    }

    /// <summary>What the change does, said before it is applied.</summary>
    public struct TermEffect
    {
        /// <summary>How many texts change.</summary>
        public int Texts;

        /// <summary>How many sentences are to be read again once it is done.</summary>
        public int Review;

        public IReadOnlyList<TermUse> Uses;
        public IReadOnlyList<string> FormsToRedo;
    }

    /// <summary>What <c>Apply</c> writes for the term.</summary>
    public struct TermWrite
    {
        public string Key;
        public string Value;
        public bool ValueChanged;

        /// <summary>A changed word is a person's writing (<c>H</c>); a change of forms alone keeps the tag the line has.</summary>
        public string Tag;

        public string OriginalValue;
        public string OriginalTag;
        public bool IsNew;

        /// <summary>The line as it will stand: its other fields untouched, its word and forms as the window holds them.</summary>
        public TranslationLine Line;
    }

    /// <summary>
    /// Editing a term from its window: its word and its written forms (analyse/edition-glossaire-conception.md §4.2, R2, R3, R7).
    /// Pure: nothing is written — the screen asks what the window says and what <c>Apply</c> writes.
    ///
    /// 🔴 <b>ONE act writes</b>: <see cref="Write"/>, <c>Apply (N)</c>, N being what changed (<see cref="Count"/>); nothing is written
    /// until then and Cancel leaves everything as it was. The impact is said BEFORE (<see cref="Effect"/>), from the socle's own
    /// <see cref="TermChanges.Impact"/>. A form a sentence cites cannot be removed, and the window says which
    /// (<see cref="Blockers"/>). No sentence is written: they read the term through their link, so the only line written is the
    /// term's own; nothing here touches a <c>was</c>, an <c>own</c> or a text.
    ///
    /// A term is only a window of a file of the format 2.
    /// </summary>
    public static class TermEdit
    {
        /// <summary>The window of an existing term line, or null when it does not exist (a file of the format 1).</summary>
        public static TermDraft? Open(string key, TranslationLine? line, int fileFormat)
        {
            if (fileFormat < 2 || !line.HasValue) return null;
            var forms = FormsOf(line.Value);
            return new TermDraft
            {
                Key = key, Word = line.Value.Value ?? string.Empty, WordTouched = false, Forms = forms,
                OriginalWord = line.Value.Value ?? string.Empty, OriginalForms = Copy(forms),
            };
        }

        /// <summary>The same draft, counted against another line: what Apply stages is the difference from the STORED line.</summary>
        public static TermDraft Rebase(TermDraft draft, TranslationLine line)
        {
            draft.OriginalWord = line.Value ?? string.Empty;
            draft.OriginalForms = FormsOf(line);
            return draft;
        }

        /// <summary>
        /// A term made from words a person selected: the source's (the key of the line) and the translation's (its word).
        /// 🔴 The key is very often a line the file ALREADY holds, and then it is not made twice: that line BECOMES a term, its
        /// translation proposed as the word. <paramref name="existing"/> is that line, or null when the key is new.
        /// </summary>
        public static TermDraft OpenNew(string? key, string? selected, TranslationLine? existing)
        {
            var forms = existing.HasValue ? FormsOf(existing.Value) : new List<DraftForm>();
            string picked = (selected ?? string.Empty).TrimSpaces();
            return new TermDraft
            {
                Key = (key ?? string.Empty).TrimSpaces(),
                Selected = picked,
                Word = existing.HasValue ? existing.Value.Value ?? string.Empty : picked,
                WordTouched = false,
                Forms = forms,
                IsNew = true,
                Becomes = existing.HasValue,
                OriginalWord = existing.HasValue ? existing.Value.Value ?? string.Empty : string.Empty,
                OriginalForms = Copy(forms),
            };
        }

        /// <summary>The key was typed or corrected: the line it names is looked at again; a word the person wrote stays.</summary>
        public static TermDraft SetKey(TermDraft draft, string? text, TranslationLine? existing)
        {
            TermDraft next = OpenNew(text, draft.Selected, existing);
            bool keepsForms = draft.WordTouched || AnyAdded(draft.Forms);
            next.Key = text ?? string.Empty;
            next.Word = draft.WordTouched ? draft.Word : next.Word;
            next.WordTouched = draft.WordTouched;
            next.Forms = keepsForms ? draft.Forms : next.Forms;
            return next;
        }

        public static TermDraft SetWord(TermDraft draft, string? text)
        {
            draft.Word = text ?? string.Empty;
            draft.WordTouched = true;
            return draft;
        }

        public static TermDraft SetForm(TermDraft draft, string id, string? text)
        {
            var forms = new List<DraftForm>(draft.Forms);
            for (int i = 0; i < forms.Count; i++)
                if (forms[i].Id == id) { var form = forms[i]; form.Text = text ?? string.Empty; forms[i] = form; }
            draft.Forms = forms;
            return draft;
        }

        /// <summary>A new form, empty, under the next free number of this term's forms — above the forms removed too: a number is never reused.</summary>
        public static TermDraft AddForm(TermDraft draft)
        {
            int max = 0;
            foreach (var form in draft.Forms) max = Math.Max(max, FormNumber(form.Id));
            foreach (var form in draft.OriginalForms) max = Math.Max(max, FormNumber(form.Id));
            var forms = new List<DraftForm>(draft.Forms) { new DraftForm { Id = "f" + (max + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Text = string.Empty, Added = true } };
            draft.Forms = forms;
            return draft;
        }

        public static TermDraft RemoveForm(TermDraft draft, string id)
        {
            var forms = new List<DraftForm>();
            foreach (var form in draft.Forms) if (form.Id != id) forms.Add(form);
            draft.Forms = forms;
            return draft;
        }

        /// <summary>
        /// Who needs a form: the sentences whose link names it, and the term's own <c>use</c> of a template. A form added in this
        /// window is needed by nobody.
        /// </summary>
        /// <param name="file">The lines of the file, in the file's order.</param>
        public static FormBlockers Blockers(TermDraft draft, string id, IReadOnlyList<KeyValuePair<string, TranslationLine>> file)
        {
            var sentences = new List<string>();
            bool found = false, added = false;
            foreach (var form in draft.Forms) if (form.Id == id) { found = true; added = form.Added; break; }
            if (!found || added) return new FormBlockers { Sentences = sentences, Template = false };

            TranslationLine? termLine = null;
            foreach (var line in file)
            {
                if (line.Key == draft.Key) { termLine = line.Value; continue; }
                if (line.Value.Links == null) continue;
                foreach (var link in line.Value.Links)
                    if (link.To == draft.Key && link.Form == id) { sentences.Add(line.Key); break; }
            }

            bool template = false;
            if (termLine.HasValue && termLine.Value.Use != null)
                foreach (var use in termLine.Value.Use)
                {
                    if (use.Value.Form == id) template = true;
                    if (use.Value.ByClass != null)
                        foreach (var byClass in use.Value.ByClass) if (byClass.Value == id) template = true;
                }

            return new FormBlockers { Sentences = sentences, Template = template };
        }

        /// <summary>What changed in the window, one entry per word or form.</summary>
        public static IReadOnlyList<TermChange> Changes(TermDraft draft)
        {
            var changes = new List<TermChange>();
            // A new term is ONE act (a line created, or a line marked): its word is part of it unless the line was there
            if (draft.IsNew) changes.Add(new TermChange { Kind = "term" });
            if (draft.Word != draft.OriginalWord && !(draft.IsNew && draft.OriginalWord == string.Empty)) changes.Add(new TermChange { Kind = "word" });

            foreach (var form in draft.Forms)
            {
                DraftForm? was = null;
                foreach (var f in draft.OriginalForms) if (f.Id == form.Id) { was = f; break; }
                if (!was.HasValue) changes.Add(new TermChange { Kind = "form-added", Id = form.Id });
                else if (was.Value.Text != form.Text) changes.Add(new TermChange { Kind = "form-text", Id = form.Id });
            }
            foreach (var was in draft.OriginalForms)
            {
                bool still = false;
                foreach (var f in draft.Forms) if (f.Id == was.Id) { still = true; break; }
                if (!still) changes.Add(new TermChange { Kind = "form-removed", Id = was.Id });
            }
            return changes;
        }

        /// <summary>The N of <c>Apply (N)</c>.</summary>
        public static int Count(TermDraft draft) => Changes(draft).Count;

        /// <summary>Why Apply cannot write what the window holds, or null: <c>empty-key</c>, <c>bad-key</c>, <c>key-is-term</c>, <c>cites</c>, <c>empty-word</c>, <c>empty-form</c>, <c>word-has-marker</c>, <c>form-has-marker</c>, <c>word-placeholders</c>, <c>form-placeholders</c>.</summary>
        /// <param name="lookup">The line of the file by its key; null when there is none.</param>
        public static string? Problem(TermDraft draft, Func<string, TranslationLine?> lookup)
        {
            if (lookup == null) throw new ArgumentNullException(nameof(lookup));
            if (draft.IsNew)
            {
                string key = draft.Key.TrimSpaces();
                if (key.Length == 0) return "empty-key";
                // Not a text of the game: a name of the file, or a template, or one that already holds a marker
                if (TranslationFileKeys.IsMetadataKey(key) || LineMarkers.ClosedSlots(key).Count > 0 || key.IndexOf("[!", StringComparison.Ordinal) >= 0) return "bad-key";
                TranslationLine? line = lookup(key);
                // Already a term: that window is opened, not made again
                if (line.HasValue && line.Value.Term) return "key-is-term";
                // A term does not cite another term (LineWrites): a text that holds links cannot become one
                if (line.HasValue && ((line.Value.Links != null && line.Value.Links.Count > 0)
                                      || (line.Value.Value ?? string.Empty).IndexOf("[!g*", StringComparison.Ordinal) >= 0)) return "cites";
            }
            if (draft.Word.TrimSpaces().Length == 0) return "empty-word";
            foreach (var form in draft.Forms) if (form.Text.TrimSpaces().Length == 0) return "empty-form";

            // A term is a word of the game, not a sentence nor a template: no link marker (a term cites no term), no slot (a template's
            // key holds them, a term's never does). The file refuses all of these when written (LineWrites), after the click.
            if (HoldsMarker(draft.Word)) return "word-has-marker";
            foreach (var form in draft.Forms) if (HoldsMarker(form.Text)) return "form-has-marker";

            // The game's own placeholders (`[!v*0]`, `[!nl]`…) are the key's: the word keeps them as the save asks of any edit
            // (Placeholders.AcceptsEdit), and a form — set inside sentences, whose own placeholders are counted — adds none the key lacks.
            string source = draft.IsNew ? draft.Key.TrimSpaces() : draft.Key;
            if (!Placeholders.AcceptsEdit(source, draft.Word, Placeholders.FrozenSequences(source), out _)) return "word-placeholders";
            foreach (var form in draft.Forms) if (Placeholders.Invented(source, form.Text).Count > 0) return "form-placeholders";
            return null;
        }

        private static bool HoldsMarker(string text)
            => LineMarkers.LinkNumbers(text).Count > 0 || LineMarkers.ClosedSlots(text).Count > 0 || LineMarkers.OpenSlots(text).Count > 0;

        /// <summary>The term's line as it would be: its other fields untouched, its word and forms as the window holds them.</summary>
        public static TranslationLine After(TermDraft draft, TranslationLine original)
        {
            TranslationLine line = original;
            line.Value = draft.Word;
            if (draft.IsNew) line.Term = true;
            line.Forms = null;
            if (draft.Forms.Count > 0)
            {
                var forms = new List<KeyValuePair<string, string>>();
                foreach (var form in draft.Forms) forms.Add(new KeyValuePair<string, string>(form.Id, form.Text));
                line.Forms = forms;
            }
            return line;
        }

        /// <summary>
        /// What the change does, said before it is applied: <see cref="TermChanges.Impact"/> on the file as it stands, with the term's line
        /// laid over as the window would leave it.
        /// </summary>
        /// <param name="file">The lines of the file being written, in the file's order.</param>
        /// <param name="lists">The game's lists: name and values.</param>
        public static TermEffect Effect(TermDraft draft, TranslationLine original,
                                        IReadOnlyList<KeyValuePair<string, TranslationLine>> file,
                                        IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> lists)
        {
            var lines = new List<KeyValuePair<string, TranslationLine>>(file.Count + 1);
            bool placed = false;
            foreach (var line in file)
            {
                if (line.Key == draft.Key) { lines.Add(new KeyValuePair<string, TranslationLine>(line.Key, original)); placed = true; }
                else lines.Add(line);
            }
            if (!placed) lines.Add(new KeyValuePair<string, TranslationLine>(draft.Key, original));

            TermImpact impact = TermChanges.Impact(draft.Key, lines, lists, After(draft, original));
            int texts = 0, review = 0;
            foreach (var use in impact.Uses)
            {
                if (use.TextChanges) texts++;
                if (use.NeedsReviewAfter) review++;
            }
            return new TermEffect { Texts = texts, Review = review, Uses = impact.Uses, FormsToRedo = impact.FormsToRedo };
        }

        /// <summary>What Apply writes: the term's own line and nothing else — or null when nothing changed.</summary>
        public static TermWrite? Write(TermDraft draft, TranslationLine original)
        {
            if (Count(draft) == 0) return null;

            string originalValue = original.Value ?? string.Empty;
            bool valueChanged = draft.Word != originalValue;
            string originalTag = original.Tag ?? "H";
            return new TermWrite
            {
                Key = draft.IsNew ? draft.Key.TrimSpaces() : draft.Key,
                Value = draft.Word,
                ValueChanged = valueChanged,
                Tag = valueChanged ? LineEditing.TagAfterPerson(original.Tag) : originalTag,
                OriginalValue = originalValue,
                OriginalTag = originalTag,
                IsNew = draft.IsNew,
                Line = After(draft, original),
            };
        }

        private static List<DraftForm> FormsOf(TranslationLine line)
        {
            var forms = new List<DraftForm>();
            if (line.Forms != null)
                foreach (var form in line.Forms) forms.Add(new DraftForm { Id = form.Key, Text = form.Value ?? string.Empty, Added = false });
            return forms;
        }

        private static List<DraftForm> Copy(IReadOnlyList<DraftForm> forms) => new List<DraftForm>(forms);

        private static bool AnyAdded(IReadOnlyList<DraftForm> forms)
        {
            foreach (var form in forms) if (form.Added) return true;
            return false;
        }

        /// <summary>The number in an id like <c>f12</c>; 0 for anything else.</summary>
        private static int FormNumber(string id)
        {
            if (id.Length < 2 || id[0] != 'f') return 0;
            for (int i = 1; i < id.Length; i++) if (id[i] < '0' || id[i] > '9') return 0;
            return int.TryParse(id.Substring(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n) ? n : 0;
        }
    }
}
