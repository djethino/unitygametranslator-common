using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The library asks the RUNTIME nothing about a character. What a character is — a letter, a digit, a space, its case,
    /// where it breaks — is Unicode's data, generated from the project's one version (<c>UNICODE_VERSION</c>) into tables
    /// (<c>KeyChars</c>, <c>WordGlue</c>, <c>Casing</c>…). A runtime call answers with the Unicode of whatever runs the code: the
    /// Manager's .NET 8, a game's old Mono, an IL2CPP table frozen years ago — three answers to one question (measured
    /// 2026-11-14 against Unicode 18: 22 041 letters outside the plane and 27 inside it unknown to .NET 8; Mono and IL2CPP not
    /// measured, older still).
    ///
    /// 🔴 **A ratchet, counting down to zero.** The calls that remain are listed in <c>runtime-unicode.baseline</c> (one line per
    /// file and kind, with the count), each one a debt with a step of the plan that removes it (analyse/smart-suivi-socle.md,
    /// § « troisième Unicode »). The check fails when a count GOES UP (a new call), and also when it goes DOWN without the
    /// baseline following (the debt paid must be written off, or the next call hides behind the room it left). Regenerate with
    /// <c>dotnet run -- unicode-list runtime-unicode.baseline</c>, after reading what changed.
    ///
    /// ⚠ **Allowed without a count** (they carry no Unicode data): <c>char.IsHighSurrogate / IsLowSurrogate / IsSurrogate</c>,
    /// <c>char.ConvertToUtf32</c>; an <c>InvariantCulture</c> used to FORMAT or parse digits; <c>string.Equals</c> with
    /// <c>Ordinal</c>. Anything declared ASCII goes through <c>Ascii</c> (once it exists), not through the runtime.
    ///
    /// Read with comments, strings and character literals blanked (so this file's own words are not counted) — except the
    /// VERBATIM strings, which are where a regular expression's classes (<c>\d \s \w \b</c>) are written.
    /// </summary>
    internal static class NoRuntimeUnicode
    {
        public const string BaselineName = "runtime-unicode.baseline";

        private const RegexOptions Opt = RegexOptions.Compiled | RegexOptions.CultureInvariant;

        private static readonly Regex Scanner = new Regex(
            "(?<comment>//[^\\n]*|/\\*.*?\\*/)|(?<verbatim>\\$?@\"(?:[^\"]|\"\")*\")|(?<string>\\$?\"(?:[^\"\\\\\\n]|\\\\.)*\")|(?<char>'(?:[^'\\\\\\n]|\\\\.)')",
            Opt | RegexOptions.Singleline);

        private static readonly (string Kind, Regex Pattern)[] Kinds =
        {
            ("char.Is*", new Regex(@"\bchar\.Is(?!(?:High|Low)?Surrogate\b)\w+\s*\(", Opt)),
            ("char.To*", new Regex(@"\bchar\.To(?:Upper|Lower)\w*\s*\(", Opt)),
            ("CharUnicodeInfo", new Regex(@"\bCharUnicodeInfo\b|\bUnicodeCategory\b", Opt)),
            ("ToUpper/ToLower", new Regex(@"\.To(?:Upper|Lower)(?:Invariant)?\s*\(", Opt)),
            ("Normalize", new Regex(@"\.Normalize\s*\(", Opt)),
            ("Trim()", new Regex(@"\.Trim(?:Start|End)?\s*\(\s*\)", Opt)),
            ("IsNullOrWhiteSpace", new Regex(@"\bIsNullOrWhiteSpace\s*\(", Opt)),
            ("OrdinalIgnoreCase", new Regex(@"\bOrdinalIgnoreCase\b", Opt)),
            ("culture comparison", new Regex(@"\b(?:StringComparison|StringComparer)\.(?:Current|Invariant)\w*|\bCultureInfo\.CurrentCulture\b|\bCompareInfo\b|\bstring\.Compare\s*\(", Opt)),
            ("RegexOptions.IgnoreCase", new Regex(@"\bRegexOptions\.IgnoreCase\b", Opt)),
        };

        private const string RegexClassKind = "regex class (d s w b)";
        private const string ParseKind = "parse without a culture";

        // A character class of the runtime inside a regular expression: \d \D \s \S \w \W \b \B (a verbatim string).
        private static readonly Regex RuntimeClass = new Regex(@"(?<!\\)\\[dDsSwWbB]", Opt);
        private static readonly Regex Parse = new Regex(@"\b(?:int|long|double|float|decimal|short|byte|uint|ulong)\.(?:Try)?Parse\s*\(", Opt);

        /// <summary>How many calls of each kind this source holds.</summary>
        public static SortedDictionary<string, int> Count(string source)
        {
            var verbatim = new List<string>();
            var code = new StringBuilder(source.Length);
            int at = 0;
            foreach (Match m in Scanner.Matches(source))
            {
                code.Append(source, at, m.Index - at);
                if (m.Groups["verbatim"].Success) verbatim.Add(m.Value);
                // Blanked, never removed: a position stays a position. A literal keeps a filler, so that Trim('.') is
                // not read as Trim() — a comment leaves only blanks.
                code.Append(m.Groups["comment"].Success ? ' ' : '_', m.Length);
                at = m.Index + m.Length;
            }
            code.Append(source, at, source.Length - at);
            string text = code.ToString();

            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            foreach (var (kind, pattern) in Kinds)
            {
                int n = pattern.Matches(text).Count;
                if (n > 0) counts[kind] = n;
            }

            int classes = verbatim.Count(v => RuntimeClass.IsMatch(v));
            if (classes > 0) counts[RegexClassKind] = classes;

            // A parse that names no culture: the argument list is read to its closing parenthesis.
            int unspecified = 0;
            foreach (Match m in Parse.Matches(text))
            {
                int depth = 1, i = m.Index + m.Length;
                for (; i < text.Length && depth > 0; i++)
                {
                    if (text[i] == '(') depth++;
                    else if (text[i] == ')') depth--;
                }
                // A culture is named in code (CultureInfo.InvariantCulture), never in a string.
                string arguments = text.Substring(m.Index + m.Length, Math.Max(0, i - (m.Index + m.Length)));
                if (!arguments.Contains("CultureInfo")) unspecified++;
            }
            if (unspecified > 0) counts[ParseKind] = unspecified;
            return counts;
        }

        /// <summary>Every source of the library but the generated tables, with its counts, in a stable order.</summary>
        public static List<(string File, string Kind, int Count)> Inventory(string librarySourceFolder)
        {
            var list = new List<(string, string, int)>();
            string obj = Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar;
            string bin = Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar;
            foreach (string path in Directory.GetFiles(librarySourceFolder, "*.cs", SearchOption.AllDirectories)
                         .Where(p => !p.EndsWith(".g.cs", StringComparison.Ordinal)
                                     && p.IndexOf(obj, StringComparison.Ordinal) < 0 && p.IndexOf(bin, StringComparison.Ordinal) < 0)
                         .OrderBy(p => p, StringComparer.Ordinal))
            {
                string name = Path.GetRelativePath(librarySourceFolder, path).Replace('\\', '/');
                foreach (var pair in Count(File.ReadAllText(path)))
                    list.Add((name, pair.Key, pair.Value));
            }
            return list;
        }

        public static string Format(List<(string File, string Kind, int Count)> inventory)
        {
            var sb = new StringBuilder();
            sb.Append("# Runtime Unicode calls still in common/src: one line per file and kind — file TAB kind TAB count.\n");
            sb.Append("# A RATCHET (NoRuntimeUnicode.cs): the count may only go down, and this file follows it. Regenerate: dotnet run -- unicode-list runtime-unicode.baseline\n");
            foreach (var (file, kind, raw) in inventory)
            {
                // the named exceptions are no debt: counted off here exactly as Run counts them off
                int count = raw;
                foreach (var ex in Exceptions)
                    if (ex.File == file && ex.Kind == kind) count = Math.Max(0, count - ex.Count);
                if (count == 0) continue;
                sb.Append(file).Append('\t').Append(kind).Append('\t').Append(count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }

        /// <summary>
        /// What the library still asks the runtime on purpose, each with its reason. Counted exactly: a new call of the same
        /// kind in the same file is a new debt, and an exception whose call has been removed must go.
        /// </summary>
        private static readonly (string File, string Kind, int Count, string Reason)[] Exceptions =
        {
            ("UnityFiles/GameFonts.cs", "OrdinalIgnoreCase", 1,
                "a PATH (the game's data folder against a font's path): the case of a path is the file system's own table, not Unicode's — nothing of ours can say it"),
            ("UnityFiles/GameFonts.cs", "char.Is*", 1,
                "char.IsControl, the C0 and C1 controls (Cc): a category no Unicode version has ever changed, read to keep them out of a file name"),
        };

        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            SelfCheck(check);
            check(commonRoot != null, "the library's folder is found", "the sources are read from it");
            if (commonRoot == null) return;

            string sources = Path.Combine(commonRoot, "src", "UnityGameTranslator.Common");
            string baselinePath = Path.Combine(commonRoot, "tests", "UnityGameTranslator.Common.Checks", BaselineName);
            check(File.Exists(baselinePath), BaselineName + " is there", "the debts still to pay, one line each");
            if (!File.Exists(baselinePath)) return;

            var baseline = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string line in File.ReadAllLines(baselinePath))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                string[] parts = line.Split('\t');
                if (parts.Length == 3 && int.TryParse(parts[2], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int n))
                    baseline[parts[0] + "\t" + parts[1]] = n;
            }

            var now = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (file, kind, count) in Inventory(sources)) now[file + "\t" + kind] = count;

            // The named exceptions: counted off first, exactly — one that is no longer there is a failure too.
            var gone = new List<string>();
            foreach (var (file, kind, count, _) in Exceptions)
            {
                string key = file + "\t" + kind;
                now.TryGetValue(key, out int have);
                if (have < count) gone.Add(file + " " + kind + ": " + have + " of " + count);
                if (have > 0) now[key] = Math.Max(0, have - count);
            }
            check(gone.Count == 0, "every named exception is still there (" + Exceptions.Length + ")",
                gone.Count == 0 ? "each one has its reason in the list" : "remove the exception: " + string.Join(" | ", gone));

            var up = new List<string>();
            var down = new List<string>();
            foreach (string key in now.Keys.Union(baseline.Keys).OrderBy(k => k, StringComparer.Ordinal))
            {
                now.TryGetValue(key, out int have);
                baseline.TryGetValue(key, out int allowed);
                if (have > allowed) up.Add(key.Replace('\t', ' ') + ": " + allowed + " -> " + have);
                else if (have < allowed) down.Add(key.Replace('\t', ' ') + ": " + allowed + " -> " + have);
            }

            int total = now.Values.Sum();
            check(up.Count == 0, "no new call asks the runtime what a character is (" + total + " remain)",
                up.Count == 0 ? "ask a generated table, or Ascii for a text declared ASCII" : string.Join(" | ", up));
            check(down.Count == 0, "the baseline follows the calls removed",
                down.Count == 0 ? "debt paid is written off" : "run `dotnet run -- unicode-list` and commit the file: " + string.Join(" | ", down));
        }

        private static void SelfCheck(Action<bool, string, string> check)
        {
            string sample = string.Join("\n", new[]
            {
                "// char.IsLetter(c) in a comment",
                "var s = \"x.Trim()\";",
                "bool a = char.IsLetterOrDigit(c) && char.IsHighSurrogate(c) && char.IsSurrogate(c);",
                "var b = x.Trim() + y.TrimEnd() + z.Trim('.');",
                "var r = new Regex(@\"\\d+\\s\", RegexOptions.IgnoreCase);",
                "var k = new Regex(@\"[0-9]+\\\\s\");",
                "int n = int.Parse(t); int m = int.Parse(t, CultureInfo.InvariantCulture);",
                "var u = string.Equals(p, q, StringComparison.OrdinalIgnoreCase) ? w.ToLowerInvariant() : null;",
            });
            var seen = Count(sample);
            int Of(string kind) => seen.TryGetValue(kind, out int n) ? n : 0;
            check(Of("char.Is*") == 1, "the check counts a runtime letter test, not a surrogate test or a comment", "char.IsLetterOrDigit once");
            check(Of("Trim()") == 2, "it counts Trim without an argument, not Trim('.') nor a string", "x.Trim() and y.TrimEnd()");
            check(Of(RegexClassKind) == 1, "it counts a verbatim regular expression with a runtime class, once", "an escaped backslash is not a class");
            check(Of("RegexOptions.IgnoreCase") == 1 && Of("OrdinalIgnoreCase") == 1 && Of("ToUpper/ToLower") == 1,
                "it counts the case-folding calls", "IgnoreCase, OrdinalIgnoreCase, ToLowerInvariant");
            check(Of(ParseKind) == 1, "it counts a parse that names no culture, not one that does", "int.Parse(t) only");
        }
    }
}
