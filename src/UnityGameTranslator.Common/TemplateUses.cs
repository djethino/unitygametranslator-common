using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What a template takes from an entry, per SLOT's list: the keys of a line's <c>use</c> (spec/translation-file/lists.md, « One entry, two lists »).
    ///
    /// A key of <c>use</c> is a template's id (<c>tab12cd34ef56</c>) — what the template takes from the entry in EVERY slot — or the
    /// id and a list, <c>tab12cd34ef56:familyName</c> — what it takes in the slots that read THAT list. The same character is a family
    /// name in one slot and a given name in the next (« Bo » and « Bai »): the one key per template could not say it.
    ///
    /// <c>:</c> cannot be in an id (<c>^t[0-9a-f]{12}$</c>) nor in a list's name (a slot's grammar,
    /// <c>[^\]\[:=*]+</c>): the first one splits.
    /// </summary>
    public static class TemplateUses
    {
        public const char ListSeparator = ':';

        /// <summary>The key of what a template takes from an entry in the slots of one list.</summary>
        public static string Key(string templateId, string list)
        {
            if (templateId == null) throw new ArgumentNullException(nameof(templateId));
            if (string.IsNullOrEmpty(list)) throw new ArgumentException("a list has a name", nameof(list));
            return templateId + ListSeparator + list;
        }

        /// <summary>The template and the list of a key of <c>use</c>: the list is null for the key of the whole template.</summary>
        public static string TemplateOf(string key, out string? list)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            int at = key.IndexOf(ListSeparator);
            if (at < 0) { list = null; return key; }
            list = key.Substring(at + 1);
            return key.Substring(0, at);
        }

        /// <summary>
        /// What the template takes from the entry in a slot of this list: the key of that list, else the key of the whole template,
        /// else nothing (the entry's own words). A file that holds no key with a list answers as it always did.
        /// </summary>
        public static LineUse? For(IReadOnlyList<KeyValuePair<string, LineUse>>? use, string templateId, string list)
        {
            if (use == null) return null;
            LineUse? whole = null;
            foreach (var u in use)
            {
                string id = TemplateOf(u.Key, out string? of);
                if (!string.Equals(id, templateId, StringComparison.Ordinal)) continue;
                if (of == null) whole = u.Value;
                else if (string.Equals(of, list, StringComparison.Ordinal)) return u.Value;
            }
            return whole;
        }
    }
}
