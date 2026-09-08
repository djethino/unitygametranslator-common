using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace UnityGameTranslator.Common.Checks.Corpus
{
    /// <summary>
    /// The C# executor of corpus/ — the first of N. It loads every rule the manifest names, plays
    /// each case through <see cref="Operations"/>, compares the answer with what the case expects,
    /// and then asks <see cref="Coverage"/> whether any public method of a covered rule has no
    /// case at all.
    ///
    /// ⚠ The corpus is what the specification demands, not what this code answers: a case is
    /// written from the rule as stated (the `why` is the rule in one sentence), never read back
    /// from the implementation. This executor only says whether the C# agrees.
    /// </summary>
    internal static class CorpusRunner
    {
        private sealed class Case
        {
            public string Id = "";
            public string Op = "";
            public JsonElement In;
            public JsonElement Out;
            public bool HasOut;
            public bool Symmetric;
            public string Why = "";
        }

        /// <summary>What was loaded, for the coverage check.</summary>
        public sealed class LoadedRule
        {
            public string Name = "";
            public string[] Classes = new string[0];
            public Dictionary<string, int> CasesPerOp = new Dictionary<string, int>(StringComparer.Ordinal);
        }

        public static void Run(Action<bool, string, string> check)
        {
            string? root = FindCorpus();
            check(root != null, "corpus/ is found", "the executor reads files; without them it proves nothing");
            if (root == null) return;

            using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "manifest.json")));
            var loaded = new List<LoadedRule>();

            foreach (var ruleName in manifest.RootElement.GetProperty("rules").EnumerateArray())
            {
                string name = ruleName.GetString() ?? "";
                string path = Path.Combine(root, "rules", name + ".json");

                Console.WriteLine();
                Console.WriteLine("Corpus: " + name);
                Console.WriteLine(new string('-', 8 + name.Length));

                check(File.Exists(path), $"rules/{name}.json exists", "the manifest names it");
                if (!File.Exists(path)) continue;

                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var rule = document.RootElement;
                var record = new LoadedRule { Name = name, Classes = Strings(rule, "classes") };
                loaded.Add(record);

                var cases = ReadCases(rule);
                var byId = new Dictionary<string, Case>(StringComparer.Ordinal);
                foreach (var c in cases) byId[c.Id] = c;

                check(cases.Count > 0, $"{name} holds cases", "an empty rule file guards nothing");

                foreach (var c in cases)
                {
                    var op = Operations.Find(name, c.Op);
                    if (op == null)
                    {
                        check(false, c.Id, $"no operation '{name}/{c.Op}' in the executor's table");
                        continue;
                    }

                    record.CasesPerOp.TryGetValue(c.Op, out int seen);
                    record.CasesPerOp[c.Op] = seen + 1;

                    bool passed = Play(op, c, byId, out string detail);
                    check(passed, c.Id, passed ? c.Why : c.Why + " — " + detail);
                }
            }

            Coverage.Run(check, loaded);
        }

        /// <summary>
        /// One case: call, compare, and — when asked — the mirror call. The expectation can be a
        /// value, or a relation to another answer: <c>{"same_as": "case-id"}</c>,
        /// <c>{"above": in}</c>, <c>{"below": in}</c>, <c>{"level": in}</c>.
        /// </summary>
        private static bool Play(Operation op, Case c, Dictionary<string, Case> byId, out string detail)
        {
            object? actual;
            try
            {
                actual = JsonValues.Canon(op.Call(c.In));
            }
            catch (Exception e)
            {
                detail = "threw " + e.GetType().Name + ": " + e.Message;
                return false;
            }

            if (!c.HasOut)
            {
                detail = "the case has no 'out'";
                return false;
            }

            if (c.Out.ValueKind == JsonValueKind.Object)
            {
                if (c.Out.TryGetProperty("same_as", out var sameAs))
                {
                    string otherId = sameAs.GetString() ?? "";
                    if (!byId.TryGetValue(otherId, out var other))
                    {
                        detail = $"same_as names an unknown case '{otherId}'";
                        return false;
                    }
                    var otherOp = Operations.Find(op.Rule, other.Op);
                    if (otherOp == null)
                    {
                        detail = $"same_as case '{otherId}' has no operation";
                        return false;
                    }
                    object? expected = JsonValues.Canon(otherOp.Call(other.In));
                    if (JsonValues.Equal(actual, expected)) return SymmetricHolds(op, c, out detail);
                    detail = $"expected the answer of {otherId}, {JsonValues.Show(expected)}, got {JsonValues.Show(actual)}";
                    return false;
                }

                foreach (var relation in new[] { "above", "below", "level" })
                {
                    if (!c.Out.TryGetProperty(relation, out var otherIn)) continue;
                    object? rhs = JsonValues.Canon(op.Call(otherIn));
                    if (!(actual is long left) || !(rhs is long right))
                    {
                        detail = $"'{relation}' compares numbers; got {JsonValues.Show(actual)} and {JsonValues.Show(rhs)}";
                        return false;
                    }
                    bool holds = relation == "above" ? left > right
                               : relation == "below" ? left < right
                               : left == right;
                    if (holds) return true;
                    detail = $"expected {left} {relation} {right}";
                    return false;
                }
            }

            if (!JsonValues.Matches(c.Out, actual, out detail)) return false;
            return SymmetricHolds(op, c, out detail);
        }

        /// <summary>
        /// For an ordering: the first two inputs swapped must give the negated sign. What
        /// <c>Older</c> and <c>Same</c> used to check by hand, now a flag on the case.
        /// </summary>
        private static bool SymmetricHolds(Operation op, Case c, out string detail)
        {
            detail = "";
            if (!c.Symmetric) return true;

            var keys = new List<string>();
            foreach (var property in c.In.EnumerateObject()) keys.Add(property.Name);
            if (keys.Count < 2)
            {
                detail = "symmetric needs two inputs to swap";
                return false;
            }

            using var swapped = JsonDocument.Parse(Swap(c.In, keys[0], keys[1]));
            object? mirrored = JsonValues.Canon(op.Call(swapped.RootElement));
            if (!(mirrored is long sign) || c.Out.ValueKind != JsonValueKind.Number)
            {
                detail = "symmetric applies to a signed comparison";
                return false;
            }
            long expected = -c.Out.GetInt64();
            if (sign == expected) return true;
            detail = $"and the reverse does not hold: expected {expected}, got {sign}";
            return false;
        }

        private static string Swap(JsonElement input, string first, string second)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var property in input.EnumerateObject())
                {
                    string name = property.Name == first ? second : property.Name == second ? first : property.Name;
                    writer.WritePropertyName(name);
                    property.Value.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }

        private static List<Case> ReadCases(JsonElement rule)
        {
            var cases = new List<Case>();
            if (!rule.TryGetProperty("cases", out var array)) return cases;
            foreach (var element in array.EnumerateArray())
            {
                var c = new Case
                {
                    Id = JsonValues.Str(element, "id") ?? "(no id)",
                    Op = JsonValues.Str(element, "op") ?? "",
                    Why = JsonValues.Str(element, "why") ?? "",
                    Symmetric = JsonValues.Bool(element, "symmetric"),
                };
                if (element.TryGetProperty("in", out var input)) c.In = input.Clone();
                if (element.TryGetProperty("out", out var output))
                {
                    c.Out = output.Clone();
                    c.HasOut = true;
                }
                cases.Add(c);
            }
            return cases;
        }

        private static string[] Strings(JsonElement e, string key)
        {
            var list = new List<string>();
            if (e.TryGetProperty(key, out var array) && array.ValueKind == JsonValueKind.Array)
                foreach (var item in array.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String) list.Add(item.GetString()!);
            return list.ToArray();
        }

        /// <summary>Up from the binary until the repository's corpus/ folder is found.</summary>
        private static string? FindCorpus()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "corpus");
                if (File.Exists(Path.Combine(candidate, "manifest.json"))) return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
