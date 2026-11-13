using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;

namespace UnityGameTranslator.Common.Checks.Corpus
{
    /// <summary>
    /// The corpus's JSON forms, read into the library's types — and the library's answers, put
    /// into a shape a case can be compared against.
    ///
    /// ⚠ The forms are those of corpus/README.md, and they are the forms of the REAL files where
    /// one exists: a translation line is <c>{"v": "…", "t": "H"}</c> because that is what
    /// translations.json holds, so a hash case is an excerpt of a file rather than a description
    /// of one. A port reads the same JSON with its own JSON library and needs nothing else.
    /// </summary>
    internal static class JsonValues
    {
        // ── Reading a case's `in` ─────────────────────────────────────────────

        /// <summary>A string, or null when the key is absent or JSON null.</summary>
        public static string? Str(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        /// <summary>A boolean; absent or null reads as <paramref name="fallback"/>.</summary>
        public static bool Bool(JsonElement e, string key, bool fallback = false) =>
            e.TryGetProperty(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                ? v.GetBoolean() : fallback;

        /// <summary>A tri-state: absent or JSON null is "not asked", which is a real answer here.</summary>
        public static bool? NullableBool(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                ? v.GetBoolean() : (bool?)null;

        public static int? NullableInt(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : (int?)null;

        public static int Int(JsonElement e, string key, int fallback = 0) => NullableInt(e, key) ?? fallback;

        /// <summary>A list of strings; absent or not a list reads as empty.</summary>
        public static List<string> Strings(JsonElement e, string key)
        {
            var result = new List<string>();
            if (!e.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Array) return result;
            foreach (var item in v.EnumerateArray())
                if (item.ValueKind == JsonValueKind.String) result.Add(item.GetString()!);
            return result;
        }

        public static double? NullableDouble(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null;

        /// <summary>An enum by its C# name; absent or null is "not asked".</summary>
        public static T? EnumOf<T>(JsonElement e, string key) where T : struct
        {
            string? name = Str(e, key);
            if (name == null) return null;
            if (!Enum.TryParse<T>(name, out var value))
                throw new InvalidOperationException($"'{name}' is not a {typeof(T).Name}");
            return value;
        }

        /// <summary>
        /// The same, for an argument the rule has no default for: a case that forgets it must fail
        /// loudly rather than quietly exercise whatever the first enum member happens to be.
        /// </summary>
        public static T EnumRequired<T>(JsonElement e, string key) where T : struct
        {
            var value = EnumOf<T>(e, key);
            if (value == null) throw new InvalidOperationException($"'{key}' is required");
            return value.Value;
        }

        /// <summary>Where a fork came from: <c>{"author": "alice", "lines": 3120}</c>, or null.</summary>
        public static Origin? OriginOf(JsonElement e, string key)
        {
            if (!e.TryGetProperty(key, out var v) || v.ValueKind != JsonValueKind.Object) return null;
            return new Origin(Str(v, "author"), NullableInt(v, "lines"));
        }

        /// <summary>
        /// One translation line, in the file's own form: an object <c>{"v", "t"}</c> (no "t" =
        /// no tag; <c>"v": null</c> = a null value), a bare JSON string = the pre-tag form, and
        /// JSON null or an absent key = no such line on this side.
        /// </summary>
        public static TranslationLine? Line(JsonElement e, string key)
        {
            if (!e.TryGetProperty(key, out var v)) return null;
            return LineOf(v);
        }

        public static TranslationLine LineRequired(JsonElement e, string key)
        {
            var line = Line(e, key);
            if (line == null) throw new InvalidOperationException($"'{key}' must be a line, not null");
            return line.Value;
        }

        public static TranslationLine? LineOf(JsonElement v)
        {
            switch (v.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.String:
                    return TranslationLine.Bare(v.GetString()!);
                case JsonValueKind.Object:
                    string? value = v.TryGetProperty("v", out var val) && val.ValueKind == JsonValueKind.String ? val.GetString() : null;
                    string? tag = v.TryGetProperty("t", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
                    var line = new TranslationLine(value!, tag!);
                    ReadFormat2(v, ref line);
                    return line;
                default:
                    throw new InvalidOperationException($"a line is an object, a string or null, not {v.ValueKind}");
            }
        }

        /// <summary>
        /// The fields of the format 2 a line holds, read the way a port reads them: an absent key is
        /// an absent field, and a field of the wrong shape is a case written wrong — it throws.
        /// </summary>
        private static void ReadFormat2(JsonElement v, ref TranslationLine line)
        {
            if (v.TryGetProperty("term", out var term)) line.Term = term.ValueKind == JsonValueKind.True;
            line.Forms = StringMap(v, "forms");
            line.Class = StringMap(v, "class");
            line.Oracle = Str(v, "oracle");

            if (v.TryGetProperty("use", out var use) && use.ValueKind == JsonValueKind.Object)
            {
                var list = new List<KeyValuePair<string, LineUse>>();
                foreach (var p in use.EnumerateObject())
                {
                    var entry = new LineUse();
                    if (p.Value.ValueKind == JsonValueKind.String) entry.Form = p.Value.GetString();
                    else if (p.Value.ValueKind == JsonValueKind.Object) entry.ByClass = StringPairs(p.Value);
                    list.Add(new KeyValuePair<string, LineUse>(p.Name, entry));
                }
                line.Use = list;
            }

            if (v.TryGetProperty("links", out var links) && links.ValueKind == JsonValueKind.Array)
            {
                var list = new List<LineLink>();
                foreach (var l in links.EnumerateArray()) list.Add(LinkOf(l));
                line.Links = list;
            }

            if (v.TryGetProperty("unlinked", out var unlinked) && unlinked.ValueKind == JsonValueKind.Array)
            {
                var list = new List<string>();
                foreach (var u in unlinked.EnumerateArray()) list.Add(Str(u, "to") ?? "");
                line.Unlinked = list;
            }
        }

        /// <summary>A link in the file's form: <c>{to, form?, case?, by, was?}</c>; an absent key is an absent field.</summary>
        public static LineLink LinkOf(JsonElement l) =>
            new LineLink { To = Str(l, "to"), Form = Str(l, "form"), Case = Str(l, "case"), By = Str(l, "by"), Was = Str(l, "was"), Own = Str(l, "own") };

        public static LineLink LinkRequired(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Object
                ? LinkOf(v) : throw new InvalidOperationException($"'{key}' must be a link");

        /// <summary>The lines of an object keyed by the game's text, as a lookup (absent or null = no such line).</summary>
        public static Func<string, TranslationLine?> LineLookup(JsonElement e, string key)
        {
            var map = new Dictionary<string, TranslationLine>(StringComparer.Ordinal);
            if (e.TryGetProperty(key, out var obj) && obj.ValueKind == JsonValueKind.Object)
                foreach (var p in obj.EnumerateObject())
                {
                    var line = LineOf(p.Value);
                    if (line != null) map[p.Name] = line.Value;
                }
            return k => map.TryGetValue(k, out var found) ? found : (TranslationLine?)null;
        }

        /// <summary>A line with every field, in the shape of a case's expectation (a map is an object, unlinked a list of keys).</summary>
        public static Dictionary<string, object?> FullLine(TranslationLine line)
        {
            var forms = new Dictionary<string, object?>();
            if (line.Forms != null) foreach (var f in line.Forms) forms[f.Key] = f.Value;
            return new Dictionary<string, object?>
            {
                ["v"] = line.Value,
                ["t"] = line.Tag,
                ["forms"] = forms,
                ["links"] = Canon(line.Links ?? (IReadOnlyList<LineLink>)new LineLink[0]),
                ["unlinked"] = Canon(line.Unlinked ?? (IReadOnlyList<string>)new string[0]),
            };
        }

        /// <summary>An edit state in the corpus's form: <c>{text, passages:[{start,length,pending?}], links, unlinked, new_forms, format2?, damaged?}</c>.</summary>
        public static EditState StateOf(JsonElement e, string key)
        {
            if (!e.TryGetProperty(key, out var s) || s.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"'{key}' must be an edit state");

            string text0 = Str(s, "text") ?? "";
            var passages = new List<Passage>();
            if (s.TryGetProperty("passages", out var ps) && ps.ValueKind == JsonValueKind.Array)
                foreach (var p in ps.EnumerateArray())
                {
                    int start = Int(p, "start"), length = Int(p, "length");
                    // No origin written: the passage is as it was shown (its origin is its words).
                    string? origin = p.TryGetProperty("origin", out var o) ? o.GetString() : text0.Substring(start, length);
                    passages.Add(new Passage { Start = start, Length = length, Origin = origin, Open = Bool(p, "open"), Order = Int(p, "order", passages.Count) });
                }

            var lost = new List<LostPassage>();
            if (s.TryGetProperty("lost", out var lj) && lj.ValueKind == JsonValueKind.Array)
                foreach (var l in lj.EnumerateArray())
                    lost.Add(new LostPassage
                    {
                        Words = Str(l, "words") ?? "",
                        Origin = Str(l, "origin") ?? Str(l, "words"),
                        Link = l.TryGetProperty("link", out var lk) ? LinkOf(lk) : new LineLink(),
                        Reason = EnumOf<PassageReason>(l, "reason") ?? PassageReason.Deleted,
                        Order = Int(l, "order", lost.Count),
                    });

            var links = new List<LineLink>();
            if (s.TryGetProperty("links", out var ls) && ls.ValueKind == JsonValueKind.Array)
                foreach (var l in ls.EnumerateArray()) links.Add(LinkOf(l));

            var forms = new List<NewForm>();
            if (s.TryGetProperty("new_forms", out var fs) && fs.ValueKind == JsonValueKind.Array)
                foreach (var f in fs.EnumerateArray())
                    forms.Add(new NewForm { Entry = Str(f, "entry")!, Id = Str(f, "id")!, Text = Str(f, "text")! });

            return new EditState
            {
                Text = text0,
                Format2 = Bool(s, "format2", true),
                Passages = passages,
                Links = links,
                Lost = lost,
                Unlinked = Strings(s, "unlinked"),
                NewForms = forms,
                Damaged = Bool(s, "damaged"),
            };
        }

        public static EditQuestion QuestionOf(JsonElement e, string key)
        {
            var q = e.GetProperty(key);
            var indices = new List<int>();
            foreach (var i in q.GetProperty("passages").EnumerateArray()) indices.Add(i.GetInt32());
            return new EditQuestion { Entry = Str(q, "entry")!, Text = Str(q, "text")!, Passages = indices };
        }

        private static List<KeyValuePair<string, string>>? StringMap(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Object ? StringPairs(v) : null;

        private static List<KeyValuePair<string, string>> StringPairs(JsonElement obj)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var p in obj.EnumerateObject())
                list.Add(new KeyValuePair<string, string>(p.Name, p.Value.GetString() ?? ""));
            return list;
        }

        /// <summary>The lines of a file: an object keyed by the game's text, in file order.</summary>
        public static List<KeyValuePair<string, TranslationLine>> Lines(JsonElement e, string key)
        {
            var list = new List<KeyValuePair<string, TranslationLine>>();
            if (!e.TryGetProperty(key, out var obj) || obj.ValueKind != JsonValueKind.Object) return list;
            foreach (var property in obj.EnumerateObject())
            {
                var line = LineOf(property.Value);
                if (line == null) throw new InvalidOperationException($"line '{property.Name}' cannot be null inside a file");
                list.Add(new KeyValuePair<string, TranslationLine>(property.Name, line.Value));
            }
            return list;
        }

        // ── Putting an answer into a comparable shape ─────────────────────────

        /// <summary>
        /// The canonical shape of an answer: null, bool, long, double, string, a list, or a
        /// dictionary. Enums become their C# name; the library's structs become the dictionaries
        /// corpus/README.md describes.
        /// </summary>
        public static object? Canon(object? value)
        {
            switch (value)
            {
                case null: return null;
                case bool b: return b;
                case int i: return (long)i;
                case long l: return l;
                case float f: return (double)f;
                case double d: return d;
                case string s: return s;
                case Enum en: return en.ToString();
                case MergeDecision decision:
                    return new Dictionary<string, object?>
                    {
                        ["verdict"] = decision.Verdict.ToString(),
                        ["conflict"] = decision.IsConflict,
                        ["kind"] = decision.IsConflict ? decision.Conflict.ToString() : null,
                        ["reason"] = decision.Reason.ToString(),
                        ["fields_from"] = decision.FieldsFrom.ToString(),
                        ["links_dropped"] = decision.LinksDropped,
                        ["merged_unlinked"] = Canon(decision.MergedUnlinked),
                        ["merged_links"] = decision.MergedLinks == null ? null : Canon(decision.MergedLinks),
                    };
                case Badge badge:
                    return new Dictionary<string, object?>
                    {
                        ["kind"] = badge.Kind.ToString(),
                        ["text"] = badge.Text,
                        ["tone"] = badge.Tone.ToString(),
                        ["tip"] = badge.Tip,
                    };
                case CardLine line:
                    return new Dictionary<string, object?>
                    {
                        ["text"] = line.Text,
                        ["mention"] = line.Mention,
                        ["needs_attention"] = line.NeedsAttention,
                    };
                case CardNotice cardNotice:
                    return new Dictionary<string, object?>
                    {
                        ["text"] = cardNotice.Text,
                        ["tone"] = cardNotice.Tone.ToString(),
                        ["dismissable"] = cardNotice.Dismissable,
                        ["verb"] = cardNotice.Verb,
                    };
                case SyncNotice notice:
                    return new Dictionary<string, object?>
                    {
                        ["show"] = notice.Show,
                        ["message"] = notice.Message,
                        ["mention"] = notice.Mention,
                        ["action"] = notice.Action.ToString(),
                        ["verb"] = notice.Verb,
                        ["offers_branch"] = notice.OffersBranch,
                        ["branch_closed"] = notice.BranchClosed,
                        ["fork_closed"] = notice.ForkClosed,
                        ["wall"] = notice.Wall,
                    };
                case UploadButton button:
                    return new Dictionary<string, object?>
                    {
                        ["act"] = button.Act?.ToString(),
                        ["verb"] = button.Verb,
                        ["hint"] = button.Hint,
                        ["hint_is_translatable"] = button.HintIsTranslatable,
                        ["mention"] = button.Mention,
                        ["closed"] = button.Closed,
                        ["wall"] = button.Wall,
                        ["enabled"] = button.Enabled,
                    };
                case WallText wall:
                    return new Dictionary<string, object?>
                    {
                        ["fact"] = wall.Fact,
                        ["way_out"] = wall.WayOut,
                        ["whole"] = wall.Whole,
                    };
                case Standing standing:
                    return new Dictionary<string, object?>
                    {
                        ["publication"] = standing.Publication.ToString(),
                        ["sync"] = standing.Sync?.ToString(),
                        ["account"] = standing.Account.ToString(),
                        ["role"] = standing.Role.ToString(),
                        ["branches_waiting"] = standing.BranchesWaiting is int waiting ? (long?)waiting : null,
                        ["lines_available"] = standing.LinesAvailable is int lines ? (long?)lines : null,
                        ["main_missing"] = standing.MainMissing,
                        ["main_abandoned"] = standing.MainAbandoned,
                        ["branch_frozen"] = standing.BranchFrozen,
                        ["finished"] = standing.Finished,
                        ["main_owner"] = standing.MainOwner,
                    };
                case TermImpact impact:
                    return new Dictionary<string, object?> { ["uses"] = Canon(impact.Uses), ["forms_to_redo"] = Canon(impact.FormsToRedo) };
                case TermUse use:
                    return new Dictionary<string, object?>
                    {
                        ["key"] = use.Key,
                        ["kind"] = use.Kind.ToString(),
                        ["text_changes"] = use.TextChanges,
                        ["needs_review_after"] = use.NeedsReviewAfter,
                        ["source_after"] = use.SourceAfter.ToString(),
                    };
                case LineLink link:
                    return new Dictionary<string, object?>
                    {
                        ["to"] = link.To, ["form"] = link.Form, ["case"] = link.Case, ["by"] = link.By, ["was"] = link.Was, ["own"] = link.Own,
                    };
                case TextHunk hunk:
                    return new Dictionary<string, object?>
                    {
                        ["old_start"] = hunk.OldStart, ["old_end"] = hunk.OldEnd, ["new_start"] = hunk.NewStart, ["new_end"] = hunk.NewEnd,
                    };
                case Passage passage:
                    return new Dictionary<string, object?>
                    {
                        ["start"] = passage.Start, ["length"] = passage.Length, ["origin"] = passage.Origin, ["open"] = passage.Open, ["order"] = passage.Order,
                    };
                case LostPassage lostPassage:
                    return new Dictionary<string, object?>
                    {
                        ["words"] = lostPassage.Words, ["origin"] = lostPassage.Origin, ["reason"] = lostPassage.Reason.ToString(),
                        ["order"] = lostPassage.Order, ["link"] = Canon(lostPassage.Link),
                    };
                case NewForm form:
                    return new Dictionary<string, object?> { ["entry"] = form.Entry, ["id"] = form.Id, ["text"] = form.Text };
                case EditState state:
                    return new Dictionary<string, object?>
                    {
                        ["text"] = state.Text,
                        ["format2"] = state.Format2,
                        ["passages"] = Canon(state.Passages),
                        ["links"] = Canon(state.Links),
                        ["unlinked"] = Canon(state.Unlinked),
                        ["lost"] = Canon(state.Lost ?? new LostPassage[0]),
                        ["new_forms"] = Canon(state.NewForms),
                        ["damaged"] = state.Damaged,
                    };
                case EditQuestion q:
                    return new Dictionary<string, object?> { ["entry"] = q.Entry, ["text"] = q.Text, ["passages"] = Canon(q.Passages) };
                case EditAct act:
                    return new Dictionary<string, object?> { ["state"] = Canon(act.State), ["problem"] = act.Problem };
                case PassageView view:
                    return new Dictionary<string, object?>
                    {
                        ["start"] = view.Start, ["length"] = view.Length, ["source"] = view.Source.ToString(),
                        ["needs_review"] = view.NeedsReview, ["case_not_applied"] = view.CaseNotApplied, ["pending"] = view.Pending, ["open"] = view.Open,
                    };
                case EditOutcome outcome:
                    return new Dictionary<string, object?>
                    {
                        ["changed"] = outcome.Changed,
                        ["problem"] = outcome.Problem,
                        ["question"] = outcome.Question.HasValue ? Canon(outcome.Question.Value) : null,
                        ["v"] = outcome.V,
                        ["links"] = outcome.Links == null ? null : Canon(outcome.Links),
                        ["new_forms"] = outcome.NewForms == null ? null : Canon(outcome.NewForms),
                        ["unlinked"] = outcome.Unlinked == null ? null : Canon(outcome.Unlinked),
                    };
                case ClosedSlot slot:
                    return new Dictionary<string, object?> { ["list"] = slot.List, ["repeated"] = slot.Repeated, ["agrees"] = slot.Agrees };
                case LinkRead read:
                    return new Dictionary<string, object?>
                    {
                        ["text"] = read.Text,
                        ["source"] = read.Source.ToString(),
                        ["case_not_applied"] = read.CaseNotApplied,
                    };
                case TranslationLine line:
                    return line.BareString
                        ? (object?)line.Value
                        : new Dictionary<string, object?> { ["v"] = line.Value, ["t"] = line.Tag };
                case IDictionary dict:
                {
                    var result = new Dictionary<string, object?>();
                    foreach (DictionaryEntry entry in dict) result[entry.Key.ToString()!] = Canon(entry.Value);
                    return result;
                }
                case IEnumerable list:
                {
                    var result = new List<object?>();
                    foreach (var item in list) result.Add(Canon(item));
                    return result;
                }
                default:
                    throw new InvalidOperationException($"no canonical form for {value.GetType().Name}; add one in JsonValues.Canon");
            }
        }

        /// <summary>
        /// Whether an answer, in canonical shape, is what a case expects.
        ///
        /// Objects are PARTIAL: every key the case names must be there and match, keys the case
        /// does not name are not judged — a case pins what the specification pins. Lists are exact,
        /// in length and in order. <c>{"approx": x, "tol": t}</c> compares a number within t
        /// (1e-9 by default).
        /// </summary>
        public static bool Matches(JsonElement expected, object? actual, out string detail)
        {
            detail = "";
            switch (expected.ValueKind)
            {
                case JsonValueKind.Null:
                    if (actual == null) return true;
                    detail = $"expected null, got {Show(actual)}";
                    return false;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (actual is bool b && b == expected.GetBoolean()) return true;
                    detail = $"expected {expected.GetBoolean()}, got {Show(actual)}";
                    return false;

                case JsonValueKind.Number:
                    if (actual is int whole) actual = (long)whole;
                    if (actual is long l && expected.TryGetInt64(out long el) && l == el) return true;
                    if (actual is double d && Math.Abs(d - expected.GetDouble()) < 1e-9) return true;
                    if (actual is long l2 && Math.Abs(l2 - expected.GetDouble()) < 1e-9) return true;
                    detail = $"expected {expected.GetRawText()}, got {Show(actual)}";
                    return false;

                case JsonValueKind.String:
                    if (actual is string s && s == expected.GetString()) return true;
                    detail = $"expected {Show(expected.GetString())}, got {Show(actual)}";
                    return false;

                case JsonValueKind.Array:
                {
                    if (!(actual is List<object?> list))
                    {
                        detail = $"expected a list, got {Show(actual)}";
                        return false;
                    }
                    int n = expected.GetArrayLength();
                    if (list.Count != n)
                    {
                        detail = $"expected {n} item(s), got {list.Count}: {Show(actual)}";
                        return false;
                    }
                    int i = 0;
                    foreach (var item in expected.EnumerateArray())
                    {
                        if (!Matches(item, list[i], out var inner))
                        {
                            detail = $"item {i}: {inner}";
                            return false;
                        }
                        i++;
                    }
                    return true;
                }

                case JsonValueKind.Object:
                {
                    if (expected.TryGetProperty("approx", out var approx))
                    {
                        double tol = expected.TryGetProperty("tol", out var t) ? t.GetDouble() : 1e-9;
                        double got = actual is double dd ? dd : actual is long ll ? ll : double.NaN;
                        if (Math.Abs(got - approx.GetDouble()) <= tol) return true;
                        detail = $"expected ≈{approx.GetRawText()} (±{tol}), got {Show(actual)}";
                        return false;
                    }

                    // 🔴 **A bound, where a value cannot be written down.** Some rules are stated as
                    // a limit rather than an answer — "the edge must not saw up and down by more
                    // than a fifth of a notch" — and a rule of that shape has no exact result to
                    // freeze: it is physics in floating point, so a port in another language is
                    // right and writes different decimals. `approx` above cannot express it either,
                    // since what the specification demands is not a particular number.
                    //
                    // ⚠ Only ever a bound the SPECIFICATION states. A bound invented to make a
                    // measurement pass is a case that tests nothing, and `why` has to say which
                    // rule the limit comes from.
                    if (expected.TryGetProperty("at_most", out var most)
                        || expected.TryGetProperty("at_least", out var least))
                    {
                        double got = actual is double gd ? gd : actual is long gl ? gl : double.NaN;

                        if (expected.TryGetProperty("at_most", out most) && !(got <= most.GetDouble()))
                        {
                            detail = $"expected at most {most.GetRawText()}, got {Show(actual)}";
                            return false;
                        }

                        if (expected.TryGetProperty("at_least", out least) && !(got >= least.GetDouble()))
                        {
                            detail = $"expected at least {least.GetRawText()}, got {Show(actual)}";
                            return false;
                        }

                        return true;
                    }
                    if (!(actual is Dictionary<string, object?> dict))
                    {
                        detail = $"expected an object, got {Show(actual)}";
                        return false;
                    }
                    foreach (var property in expected.EnumerateObject())
                    {
                        if (!dict.TryGetValue(property.Name, out var field))
                        {
                            detail = $"no '{property.Name}' in {Show(actual)}";
                            return false;
                        }
                        if (!Matches(property.Value, field, out var inner))
                        {
                            detail = $"'{property.Name}': {inner}";
                            return false;
                        }
                    }
                    return true;
                }

                default:
                    detail = $"unsupported expectation {expected.ValueKind}";
                    return false;
            }
        }

        /// <summary>Two canonical answers, equal — for a case that expects "the same as that case".</summary>
        public static bool Equal(object? a, object? b)
        {
            if (a == null || b == null) return a == null && b == null;
            if (a is List<object?> la && b is List<object?> lb)
            {
                if (la.Count != lb.Count) return false;
                for (int i = 0; i < la.Count; i++) if (!Equal(la[i], lb[i])) return false;
                return true;
            }
            if (a is Dictionary<string, object?> da && b is Dictionary<string, object?> db)
            {
                if (da.Count != db.Count) return false;
                foreach (var pair in da)
                    if (!db.TryGetValue(pair.Key, out var other) || !Equal(pair.Value, other)) return false;
                return true;
            }
            if (a is double x && b is double y) return Math.Abs(x - y) < 1e-9;
            return a.Equals(b);
        }

        public static string Show(object? value)
        {
            switch (value)
            {
                case null: return "null";
                case string s: return "\"" + s + "\"";
                case List<object?> list:
                {
                    var parts = new List<string>();
                    foreach (var item in list) parts.Add(Show(item));
                    return "[" + string.Join(", ", parts) + "]";
                }
                case Dictionary<string, object?> dict:
                {
                    var parts = new List<string>();
                    foreach (var pair in dict) parts.Add(pair.Key + ": " + Show(pair.Value));
                    return "{" + string.Join(", ", parts) + "}";
                }
                default: return value.ToString() ?? "?";
            }
        }
    }
}
