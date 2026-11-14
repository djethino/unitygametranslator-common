using System;
using System.Collections.Generic;
using System.Text.Json;
using static UnityGameTranslator.Common.Checks.Corpus.JsonValues;

namespace UnityGameTranslator.Common.Checks.Corpus
{
    /// <summary>
    /// A sentence's edit replayed as a script (<c>line_edit/run</c>): the line, the file it sits in, then the gestures one after
    /// the other — <c>type</c>, <c>unlink</c>, <c>link</c>, <c>mark_read</c>, <c>answer</c>, <c>waiting</c>, <c>preview</c>,
    /// <c>finish</c>. What comes out is what each gesture that SAYS something said, in order: the state itself is never compared, so a
    /// port keeps it however it likes — but an editor that rebuilt it from the text between two gestures would answer differently.
    /// </summary>
    internal static class LineEditJson
    {
        public static readonly string[] Methods =
        {
            nameof(LineEditing.Open), nameof(LineEditing.Type), nameof(LineEditing.Preview), nameof(LineEditing.Unlink),
            nameof(LineEditing.LinkSelection), nameof(LineEditing.MarkRead), nameof(LineEditing.Waiting),
            nameof(LineEditing.Answer), nameof(LineEditing.Finish),
        };

        public static object? Run(JsonElement e)
        {
            var entryOf = LineLookup(e, "entries");
            var up = Str(e, "casing") == "first-letter" ? Casing.UpperFirst! : (Func<string, string>?)null;
            var low = Str(e, "casing") == "first-letter" ? Casing.LowerFirst! : (Func<string, string>?)null;
            var lists = new HashSet<string>(Strings(e, "lists"), StringComparer.Ordinal);
            int format = Int(e, "file_format", 2);
            var file = new LineFile { Format = format, HasList = lists.Contains };

            var edit = LineEditing.Open(Str(e, "key") ?? "", Line(e, "line"), Line(e, "current"), format, entryOf, up, low);
            var results = new List<object?>();
            foreach (var step in e.GetProperty("steps").EnumerateArray())
            {
                string act = Str(step, "do") ?? throw new InvalidOperationException("a step needs `do`");
                switch (act)
                {
                    case "type":
                        edit = LineEditing.Type(edit, Str(step, "text") ?? "");
                        break;
                    case "answer":
                        edit = LineEditing.Answer(edit, EnumRequired<EditAnswer>(step, "choice"), entryOf, up, low);
                        break;
                    case "unlink":
                        {
                            var r = LineEditing.Unlink(edit, Int(step, "passage"), entryOf, up, low);
                            edit = r.Edit;
                            results.Add(Said(act, r.Problem));
                        }
                        break;
                    case "link":
                        {
                            var r = LineEditing.LinkSelection(edit, Int(step, "start"), Int(step, "length"), Str(step, "term")!, Str(step, "form"), entryOf, up, low);
                            edit = r.Edit;
                            results.Add(Said(act, r.Problem));
                        }
                        break;
                    case "mark_read":
                        {
                            var r = LineEditing.MarkRead(edit, entryOf, up, low);
                            edit = r.Edit;
                            results.Add(Said(act, r.Problem));
                        }
                        break;
                    case "waiting":
                        results.Add(new Dictionary<string, object?> { ["do"] = act, ["question"] = Json(LineEditing.Waiting(edit, entryOf, up, low)) });
                        break;
                    case "preview":
                        results.Add(Json(LineEditing.Preview(edit, entryOf, up, low)));
                        break;
                    case "finish":
                        {
                            var done = LineEditing.Finish(edit, file, entryOf, up, low);
                            if (done.Kind == LineFinishKind.Question) edit = done.Edit;
                            results.Add(Json(done));
                        }
                        break;
                    default:
                        throw new InvalidOperationException("unknown step: " + act);
                }
            }
            return results;
        }

        public static object? Choices(JsonElement e)
        {
            int limit = Int(e, "limit", 50);
            var list = LineEditing.TermChoices(Lines(e, "file"), Str(e, "query"), limit);
            var items = new List<object?>();
            foreach (var t in list.Items)
            {
                var forms = new List<object?>();
                foreach (var f in t.Forms) forms.Add(new Dictionary<string, object?> { ["id"] = f.Key, ["text"] = f.Value });
                items.Add(new Dictionary<string, object?> { ["key"] = t.Key, ["text"] = t.Text, ["forms"] = forms });
            }
            return new Dictionary<string, object?> { ["items"] = items, ["total"] = list.Total };
        }

        private static Dictionary<string, object?> Said(string act, string? problem) =>
            new Dictionary<string, object?> { ["do"] = act, ["problem"] = problem };

        private static Dictionary<string, object?>? Json(EditQuestion? q) =>
            q.HasValue ? new Dictionary<string, object?> { ["entry"] = q.Value.Entry, ["text"] = q.Value.Text, ["passages"] = new List<int>(q.Value.Passages) } : null;

        private static Dictionary<string, object?> Json(LinePreview p)
        {
            var passages = new List<object?>();
            foreach (var x in p.Passages)
                passages.Add(new Dictionary<string, object?>
                {
                    ["index"] = x.Index, ["start"] = x.View.Start, ["length"] = x.View.Length, ["source"] = x.View.Source.ToString(),
                    ["needs_review"] = x.View.NeedsReview, ["case_not_applied"] = x.View.CaseNotApplied, ["pending"] = x.View.Pending,
                    ["open"] = x.View.Open, ["term"] = x.Term, ["by"] = x.By, ["own"] = x.Own,
                });
            var lost = new List<object?>();
            foreach (var l in p.Lost) lost.Add(new Dictionary<string, object?> { ["words"] = l.Words, ["reason"] = l.Reason.ToString() });
            return new Dictionary<string, object?>
            {
                ["do"] = "preview", ["text"] = p.Text, ["passages"] = passages, ["lost"] = lost, ["damaged"] = p.Damaged, ["pending"] = p.Pending,
            };
        }

        private static Dictionary<string, object?> Json(LineFinish f)
        {
            var map = new Dictionary<string, object?> { ["do"] = "finish", ["kind"] = f.Kind.ToString().ToLowerInvariant() };
            if (f.Code != null) map["code"] = f.Code;
            if (f.Kind == LineFinishKind.Question) map["question"] = Json(f.Question);
            if (f.Kind == LineFinishKind.Write)
            {
                var lines = new List<object?>();
                foreach (var l in f.Lines)
                {
                    var line = TermEditJson.Present(l.Line);
                    line["key"] = l.Key;
                    lines.Add(line);
                }
                map["lines"] = lines;
            }
            return map;
        }
    }
}
