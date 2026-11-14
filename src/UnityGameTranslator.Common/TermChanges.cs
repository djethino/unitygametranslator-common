using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>How a line uses a term.</summary>
    public enum TermUseKind
    {
        /// <summary>A sentence that shows the term through a link (<c>[!g*N]</c>).</summary>
        Link,

        /// <summary>A template whose slots read a list the term belongs to.</summary>
        Template,
    }

    /// <summary>One line that uses the term, and what the change does to it.</summary>
    public struct TermUse
    {
        /// <summary>The line's key.</summary>
        public string Key;

        public TermUseKind Kind;

        /// <summary>The text this line shows for the term is no longer the same.</summary>
        public bool TextChanges;

        /// <summary>
        /// The line is « to read again » once the change is made: a sentence whose link no longer
        /// reads its <c>was</c> (or whose term is gone); a template a person validated whose
        /// assembled text changes. A template written by the machine is simply assembled again.
        /// </summary>
        public bool NeedsReviewAfter;

        /// <summary>For a sentence: what its link reads after (<see cref="LinkSource"/> says form, entry, <c>was</c> or key).</summary>
        public LinkSource SourceAfter;
    }

    /// <summary>What changing a term touches — what the editors show BEFORE the change is validated.</summary>
    public struct TermImpact
    {
        /// <summary>Every line that uses the term, in the file's order, changed or not.</summary>
        public IReadOnlyList<TermUse> Uses;

        /// <summary>
        /// Written forms that stay as they were while the term's own text moved: they were derived
        /// from the old word and are to be redone (by a person, or by one question to the model).
        /// </summary>
        public IReadOnlyList<string> FormsToRedo;
    }

    /// <summary>
    /// Changing the word of a term, or one of its written forms (draft §7): which lines are touched
    /// and in what state they are left. Pure — it reads a file and the entry as it WOULD be, and
    /// writes nothing.
    ///
    /// 🔴 **Nothing is changed behind the sentences' back.** A sentence keeps its own <c>was</c>:
    /// that is how it learns the text it shows is no longer the one a person read
    /// (<see cref="LinkReading.NeedsReview"/>). So « a sentence is to read again » is not stored,
    /// it is what this function (and the screen) computes from the file as it will stand.
    ///
    /// ⚠ Lists are the game's (<c>_lists</c>): a term is used by a template when it is a value of a
    /// list one of the template's slots reads. What the template makes of it is not a line of the
    /// file, so there is no <c>was</c>: a template a person validated (H or V) is to read again
    /// when its assembled text changes, a machine's (A) is just assembled again. That reading of H
    /// and V on a template is a proposal, not a decision (analyse/smart-suivi-socle.md).
    /// </summary>
    public static class TermChanges
    {
        /// <param name="entryKey">The key of the term's line.</param>
        /// <param name="lines">The lines of the file, in file order.</param>
        /// <param name="lists">The game's lists: name and values.</param>
        /// <param name="after">The term's line as it would be (new text, new forms, new use/class); null when the term goes.</param>
        public static TermImpact Impact(string entryKey, IReadOnlyList<KeyValuePair<string, TranslationLine>> lines,
                                        IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> lists,
                                        TranslationLine? after)
        {
            if (entryKey == null) throw new ArgumentNullException(nameof(entryKey));
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            if (lists == null) throw new ArgumentNullException(nameof(lists));

            var uses = new List<TermUse>();
            var redo = new List<string>();

            TranslationLine? before = null;
            foreach (var line in lines)
                if (string.Equals(line.Key, entryKey, StringComparison.Ordinal)) { before = line.Value; break; }
            if (!before.HasValue) return new TermImpact { Uses = uses, FormsToRedo = redo };

            TranslationLine was = before.GetValueOrDefault();

            if (after.HasValue && !string.Equals(after.Value.Value, was.Value, StringComparison.Ordinal) && was.Forms != null)
                foreach (var form in was.Forms)
                    if (FormText(after.Value, form.Key, false) == form.Value) redo.Add(form.Key);

            foreach (var entry in lines)
            {
                if (string.Equals(entry.Key, entryKey, StringComparison.Ordinal)) continue;

                if (LineMarkers.ClosedSlots(entry.Key).Count > 0)
                {
                    if (!ReadsATermList(entry.Key, entryKey, lists)) continue;
                    bool changes = TemplateChanges(TemplateIds.Of(entry.Key), was, after);
                    bool person = entry.Value.Tag == "H" || entry.Value.Tag == "V";
                    uses.Add(new TermUse { Key = entry.Key, Kind = TermUseKind.Template, TextChanges = changes, NeedsReviewAfter = changes && person });
                    continue;
                }

                if (entry.Value.Links == null) continue;

                bool any = false, textChanges = false, review = false;
                var source = LinkSource.Form;
                foreach (var link in entry.Value.Links)
                {
                    if (!string.Equals(link.To, entryKey, StringComparison.Ordinal)) continue;

                    // The same reader the screens use, WITH the case of the socle: a link that asks for a capital reads « Pièce »
                    // where the term says « pièce », and what it confirmed (was) is the capitalised text — read without the case,
                    // every such sentence looked changed and to read again, and the announcement was no longer what the screen shows.
                    LinkRead readBefore = LinkReading.Read(link, k => k == entryKey ? was : (TranslationLine?)null, Casing.UpperFirst!, Casing.LowerFirst!);
                    LinkRead readAfter = LinkReading.Read(link, k => k == entryKey ? after : null, Casing.UpperFirst!, Casing.LowerFirst!);

                    if (!string.Equals(readBefore.Text, readAfter.Text, StringComparison.Ordinal)) textChanges = true;
                    bool needs = LinkReading.NeedsReview(link, readAfter);
                    if (!any || (needs && !review)) source = readAfter.Source;
                    review |= needs;
                    any = true;
                }

                if (any)
                    uses.Add(new TermUse { Key = entry.Key, Kind = TermUseKind.Link, TextChanges = textChanges, NeedsReviewAfter = review, SourceAfter = source });
            }

            return new TermImpact { Uses = uses, FormsToRedo = redo };
        }

        private static bool ReadsATermList(string templateKey, string entryKey, IReadOnlyList<KeyValuePair<string, IReadOnlyList<string>>> lists)
        {
            foreach (var slot in LineMarkers.ClosedSlots(templateKey))
                foreach (var list in lists)
                    if (string.Equals(list.Key, slot.List, StringComparison.Ordinal))
                        foreach (var value in list.Value)
                            if (string.Equals(value, entryKey, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Does what the template takes from the term (its forms, its class) differ?</summary>
        private static bool TemplateChanges(string templateId, TranslationLine before, TranslationLine? after)
        {
            if (!after.HasValue) return true;
            TranslationLine now = after.Value;

            if (!string.Equals(ClassOf(before, templateId), ClassOf(now, templateId), StringComparison.Ordinal)) return true;

            var taken = new List<string>();
            LineUse? use = UseOf(before, templateId);
            if (use.HasValue && use.Value.ByClass != null && use.Value.ByClass.Count > 0)
                foreach (var byClass in use.Value.ByClass) taken.Add(byClass.Value);
            else
                taken.Add(use.HasValue ? use.Value.Form ?? LinkReading.OwnForm : LinkReading.OwnForm);

            foreach (string formId in taken)
                if (!string.Equals(FormText(before, formId, true), FormText(now, formId, true), StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>The text of a form of an entry; the entry's own text for « 0 », and when the form is not there (when <paramref name="fallBack"/>).</summary>
        private static string? FormText(TranslationLine entry, string formId, bool fallBack)
        {
            if (formId != LinkReading.OwnForm && entry.Forms != null)
                foreach (var form in entry.Forms)
                    if (string.Equals(form.Key, formId, StringComparison.Ordinal) && !string.IsNullOrEmpty(form.Value)) return form.Value;
            return formId == LinkReading.OwnForm || fallBack ? entry.Value : null;
        }

        private static string? ClassOf(TranslationLine entry, string templateId)
        {
            if (entry.Class != null)
                foreach (var c in entry.Class)
                    if (string.Equals(c.Key, templateId, StringComparison.Ordinal)) return c.Value;
            return null;
        }

        private static LineUse? UseOf(TranslationLine entry, string templateId)
        {
            if (entry.Use != null)
                foreach (var u in entry.Use)
                    if (string.Equals(u.Key, templateId, StringComparison.Ordinal)) return u.Value;
            return null;
        }
    }
}
