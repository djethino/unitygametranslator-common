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

        public static double? NullableDouble(JsonElement e, string key) =>
            e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : (double?)null;

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
                    return new TranslationLine(value!, tag!);
                default:
                    throw new InvalidOperationException($"a line is an object, a string or null, not {v.ValueKind}");
            }
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
                    };
                case Badge badge:
                    return new Dictionary<string, object?>
                    {
                        ["kind"] = badge.Kind.ToString(),
                        ["text"] = badge.Text,
                        ["tone"] = badge.Tone.ToString(),
                        ["tip"] = badge.Tip,
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
