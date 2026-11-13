using System;

namespace UnityGameTranslator.Common
{
    /// <summary>Which of the four answers a link gave — what the text shown comes from.</summary>
    public enum LinkSource
    {
        /// <summary>The written form the link names, on the entry's line (<c>"0"</c> is the entry's own translation).</summary>
        Form,

        /// <summary>The entry's translation: the link names no form, or the form is gone from the entry.</summary>
        Entry,

        /// <summary>What the link read when it was set or confirmed: the entry (or its translation) is gone.</summary>
        Was,

        /// <summary>The sentence's own writing of the term (<see cref="LineLink.Own"/>): it does not follow the term.</summary>
        Own,

        /// <summary>Nothing else was held: the entry's key, the game's own word. Unreachable in a file a writer kept to <see cref="LineWrites"/>.</summary>
        Key,
    }

    /// <summary>What a link reads today.</summary>
    public struct LinkRead
    {
        /// <summary>The text to show in place of the marker. Never null, never empty, never a marker.</summary>
        public string Text;

        /// <summary>Where it came from.</summary>
        public LinkSource Source;

        /// <summary>
        /// The link asks for a case change the caller could not apply (no casing was given, or the
        /// link names a case this reader does not know). The text is shown as the form is written:
        /// said, not hidden.
        /// </summary>
        public bool CaseNotApplied;
    }

    /// <summary>
    /// Reading a link: what a <c>[!g*N]</c> shows, and whether the sentence must be read again.
    ///
    /// 🔴 **Never a hole, never the marker** (draft §3). The marker is replaced by, in this order:
    ///
    ///  1. the form the link names, on the entry's line (<c>forms[form]</c>; form <c>"0"</c> is the
    ///     entry's own translation);
    ///  2. the entry's translation, when the link names no form or the form no longer exists;
    ///  3. <c>was</c>, when the entry no longer exists — or has no translation to give;
    ///  4. the entry's key, when even <c>was</c> is missing (a file the writer rules refuse).
    ///
    /// The case (<c>case</c>) is applied to 1 and 2 only: <c>was</c> is already the text as the
    /// sentence wrote it.
    ///
    /// ⚠ **The case of a letter is Unicode's, never the machine's.** The caller hands in the two functions —
    /// and they are <see cref="Casing.UpperFirst"/> and <see cref="Casing.LowerFirst"/>, the same in every product
    /// (spec/translation-file/casing.md: the first code point only, the language-independent mapping, a titlecase
    /// digraph, a script with no case left alone). They stay parameters so a caller with no case (a screen that
    /// only measures) can pass none: the text is then shown as the form is written, and the read says so.
    ///
    /// « À relire » is computed, never stored (<see cref="NeedsReview"/>).
    /// </summary>
    public static class LinkReading
    {
        /// <summary><c>case</c>: the first letter of the text is written as a capital.</summary>
        public const string UpperFirst = "upper-first";

        /// <summary><c>case</c>: the first letter of the text is written as a small letter.</summary>
        public const string LowerFirst = "lower-first";

        /// <summary><c>by</c>: the machine set the link.</summary>
        public const string ByMachine = "A";

        /// <summary><c>by</c>: a person set or confirmed the link.</summary>
        public const string ByPerson = "H";

        /// <summary>The form id that stands for the entry's own translation.</summary>
        public const string OwnForm = "0";

        /// <summary>
        /// What a link reads now.
        /// </summary>
        /// <param name="link">The link of the sentence.</param>
        /// <param name="entryOf">The line of an entry, by its key; null when there is none.</param>
        /// <param name="upperFirst">Capitalises the first letter of a text; null when the caller has none.</param>
        /// <param name="lowerFirst">Lowers the first letter of a text; null when the caller has none.</param>
        public static LinkRead Read(LineLink link, Func<string, TranslationLine?> entryOf,
                                    Func<string, string>? upperFirst, Func<string, string>? lowerFirst)
        {
            if (entryOf == null) throw new ArgumentNullException(nameof(entryOf));

            // « Only this text »: a person wrote the term their own way here, and that passes before
            // every form. The case is not applied to it: it is the text as they wrote it.
            if (!string.IsNullOrEmpty(link.Own)) return new LinkRead { Text = link.Own!, Source = LinkSource.Own };

            TranslationLine? entry = string.IsNullOrEmpty(link.To) ? (TranslationLine?)null : entryOf(link.To!);

            string? text = null;
            var source = LinkSource.Form;

            if (entry.HasValue)
            {
                TranslationLine held = entry.GetValueOrDefault();

                if (link.Form != null && link.Form != OwnForm)
                {
                    text = FormOf(held, link.Form);
                    source = LinkSource.Form;
                }
                else if (link.Form == OwnForm && !string.IsNullOrEmpty(held.Value))
                {
                    text = held.Value;
                    source = LinkSource.Form;
                }

                if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(held.Value))
                {
                    text = held.Value;
                    source = LinkSource.Entry;
                }
            }

            if (string.IsNullOrEmpty(text))
            {
                if (!string.IsNullOrEmpty(link.Was)) return new LinkRead { Text = link.Was!, Source = LinkSource.Was };
                return new LinkRead { Text = link.To ?? string.Empty, Source = LinkSource.Key };
            }

            var read = new LinkRead { Text = text!, Source = source };

            if (link.Case != null)
            {
                Func<string, string>? change =
                    link.Case == UpperFirst ? upperFirst :
                    link.Case == LowerFirst ? lowerFirst : null;

                if (change == null) read.CaseNotApplied = true;
                else
                {
                    string changed = change(read.Text);
                    // A function that answers nothing has not answered: the text stays as written.
                    if (string.IsNullOrEmpty(changed)) read.CaseNotApplied = true;
                    else read.Text = changed;
                }
            }

            return read;
        }

        /// <summary>
        /// Must the person read this sentence again? Yes when the text the link reads today is no
        /// longer the text it read when it was set or confirmed (<c>was</c>), OR when the entry is
        /// gone (or has nothing to give) and the sentence only shows <c>was</c> — the person must
        /// know the term no longer follows. <see cref="LinkRead.Source"/> says which of the two.
        ///
        /// Saving or validating the sentence puts <c>was</c> back to the text read at that moment,
        /// which is what makes this false again for the first reason — nothing else stores the state.
        /// (For the second, the sentence stays « to read » until its link is replaced or removed.)
        ///
        /// ⚠ A link with no <c>was</c> was never confirmed: the answer is yes.
        /// </summary>
        /// <param name="link">The link.</param>
        /// <param name="read">What <see cref="Read"/> answered for it.</param>
        public static bool NeedsReview(LineLink link, LinkRead read)
        {
            // The sentence's own writing does not follow the term: nothing moved under it.
            if (read.Source == LinkSource.Own) return false;

            return link.Was == null
                || read.Source == LinkSource.Was || read.Source == LinkSource.Key
                || !string.Equals(link.Was, read.Text, StringComparison.Ordinal);
        }

        private static string? FormOf(TranslationLine entry, string formId)
        {
            if (entry.Forms == null) return null;
            foreach (var form in entry.Forms)
                if (string.Equals(form.Key, formId, StringComparison.Ordinal))
                    return form.Value;
            return null;
        }
    }
}
