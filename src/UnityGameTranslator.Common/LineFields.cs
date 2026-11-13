using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// One link from a sentence to a term: « here is written such an entry, in such a form ». It is
    /// what a <c>[!g*N]</c> marker of the line's <c>v</c> points at — <c>links[N]</c>, N being the
    /// position in the line's list (spec/translation-file/schema.json, <c>$defs/link</c>).
    ///
    /// Every field is the file's own text, <c>null</c> meaning « the file holds no such field ».
    /// The hash writes them in this order and no other: to, form, case, by, was
    /// (<see cref="ContentHash"/>).
    /// </summary>
    public struct LineLink
    {
        /// <summary>The KEY of the entry's line — a line's key is unique, so no list needs naming.</summary>
        public string? To;

        /// <summary>The id of the written form of the entry, in the entry's <c>forms</c>. Absent = the entry's own <c>v</c>.</summary>
        public string? Form;

        /// <summary>How the sentence writes the case of that text (<see cref="LinkReading.UpperFirst"/>, <see cref="LinkReading.LowerFirst"/>). Absent = as the form is written.</summary>
        public string? Case;

        /// <summary>Who set the link: <see cref="LinkReading.ByMachine"/> or <see cref="LinkReading.ByPerson"/>.</summary>
        public string? By;

        /// <summary>The text that was read when the link was set or confirmed — the repère « à relire » is judged against.</summary>
        public string? Was;
    }

    /// <summary>
    /// What a term entry says a template takes from it: ONE written form (<see cref="Form"/>), or one
    /// per agreement class (<see cref="ByClass"/>). Exactly one of the two is held; a value holding
    /// neither is hashed as JSON null — a file nobody has fixed keeps a hash, like a null <c>v</c>.
    /// </summary>
    public struct LineUse
    {
        /// <summary>The form id this entry takes in the template, <c>"0"</c> being the entry's own <c>v</c>.</summary>
        public string? Form;

        /// <summary>Class name → form id, for an entry the template agrees with a governing entry.</summary>
        public IReadOnlyList<KeyValuePair<string, string>>? ByClass;
    }
}
