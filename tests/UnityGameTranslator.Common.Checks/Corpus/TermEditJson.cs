using System;
using System.Collections.Generic;
using System.Text.Json;
using static UnityGameTranslator.Common.Checks.Corpus.JsonValues;

namespace UnityGameTranslator.Common.Checks.Corpus
{
    /// <summary>
    /// A term window's draft in the corpus's form —
    /// <c>{key, word, word_touched, forms:[{id, text, added}], selected, is_new, becomes, original:{v, forms:[{id, text}]}}</c> —
    /// and a line with only the fields it holds (<c>{v, t, term?, forms?, use?, class?, oracle?, links?, unlinked?}</c>).
    /// </summary>
    internal static class TermEditJson
    {
        public static TermDraft DraftOf(JsonElement e, string key)
        {
            if (!e.TryGetProperty(key, out var s) || s.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"'{key}' must be a term draft");

            var original = s.GetProperty("original");
            return new TermDraft
            {
                Key = Str(s, "key") ?? "",
                Word = Str(s, "word") ?? "",
                WordTouched = Bool(s, "word_touched"),
                Forms = FormsOf(s, "forms"),
                Selected = Str(s, "selected"),
                IsNew = Bool(s, "is_new"),
                Becomes = Bool(s, "becomes"),
                OriginalWord = Str(original, "v") ?? "",
                OriginalForms = FormsOf(original, "forms"),
            };
        }

        private static List<DraftForm> FormsOf(JsonElement e, string key)
        {
            var forms = new List<DraftForm>();
            if (e.TryGetProperty(key, out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var item in list.EnumerateArray())
                    forms.Add(new DraftForm { Id = Str(item, "id") ?? "", Text = Str(item, "text") ?? "", Added = Bool(item, "added") });
            return forms;
        }

        public static Dictionary<string, object?> Json(TermDraft draft)
        {
            var forms = new List<object?>();
            foreach (var f in draft.Forms) forms.Add(new Dictionary<string, object?> { ["id"] = f.Id, ["text"] = f.Text, ["added"] = f.Added });
            var original = new List<object?>();
            foreach (var f in draft.OriginalForms) original.Add(new Dictionary<string, object?> { ["id"] = f.Id, ["text"] = f.Text });
            return new Dictionary<string, object?>
            {
                ["key"] = draft.Key,
                ["word"] = draft.Word,
                ["word_touched"] = draft.WordTouched,
                ["forms"] = forms,
                ["selected"] = draft.Selected,
                ["is_new"] = draft.IsNew,
                ["becomes"] = draft.Becomes,
                ["original"] = new Dictionary<string, object?> { ["v"] = draft.OriginalWord, ["forms"] = original },
            };
        }

        /// <summary>A line with only the fields it holds.</summary>
        public static Dictionary<string, object?> Present(TranslationLine line, bool withValueAndTag = true)
        {
            var map = new Dictionary<string, object?>();
            if (withValueAndTag)
            {
                map["v"] = line.Value;
                map["t"] = line.Tag;
            }
            if (line.Term) map["term"] = true;
            if (line.Forms != null && line.Forms.Count > 0) map["forms"] = Pairs(line.Forms);
            if (line.Use != null && line.Use.Count > 0)
            {
                var use = new Dictionary<string, object?>();
                foreach (var u in line.Use)
                    use[u.Key] = u.Value.ByClass != null && u.Value.ByClass.Count > 0 ? Pairs(u.Value.ByClass) : (object?)u.Value.Form;
                map["use"] = use;
            }
            if (line.Class != null && line.Class.Count > 0) map["class"] = Pairs(line.Class);
            if (!string.IsNullOrEmpty(line.Oracle)) map["oracle"] = line.Oracle;
            if (line.Links != null && line.Links.Count > 0)
            {
                var links = new List<object?>();
                foreach (var l in line.Links)
                {
                    var link = new Dictionary<string, object?> { ["to"] = l.To };
                    if (l.Form != null) link["form"] = l.Form;
                    if (l.Case != null) link["case"] = l.Case;
                    if (l.By != null) link["by"] = l.By;
                    if (l.Was != null) link["was"] = l.Was;
                    if (l.Own != null) link["own"] = l.Own;
                    links.Add(link);
                }
                map["links"] = links;
            }
            if (line.Unlinked != null && line.Unlinked.Count > 0)
            {
                var unlinked = new List<object?>();
                foreach (var to in line.Unlinked) unlinked.Add(new Dictionary<string, object?> { ["to"] = to });
                map["unlinked"] = unlinked;
            }
            return map;
        }

        private static Dictionary<string, object?> Pairs(IReadOnlyList<KeyValuePair<string, string>> pairs)
        {
            var map = new Dictionary<string, object?>();
            foreach (var p in pairs) map[p.Key] = p.Value;
            return map;
        }

        public static Dictionary<string, object?> Json(TermWrite write) => new Dictionary<string, object?>
        {
            ["key"] = write.Key,
            ["value"] = write.Value,
            ["value_changed"] = write.ValueChanged,
            ["tag"] = write.Tag,
            ["original_value"] = write.OriginalValue,
            ["original_tag"] = write.OriginalTag,
            ["is_new"] = write.IsNew,
            ["fields"] = Present(write.Line, withValueAndTag: false),
        };

        public static List<object?> Json(IReadOnlyList<TermChange> changes)
        {
            var list = new List<object?>();
            foreach (var c in changes)
            {
                var one = new Dictionary<string, object?> { ["kind"] = c.Kind };
                if (c.Id != null) one["id"] = c.Id;
                list.Add(one);
            }
            return list;
        }

        public static List<KeyValuePair<string, IReadOnlyList<string>>> Lists(JsonElement e)
        {
            var lists = new List<KeyValuePair<string, IReadOnlyList<string>>>();
            if (e.TryGetProperty("lists", out var l) && l.ValueKind == JsonValueKind.Object)
                foreach (var p in l.EnumerateObject()) lists.Add(new KeyValuePair<string, IReadOnlyList<string>>(p.Name, Strings(l, p.Name)));
            return lists;
        }
    }
}
