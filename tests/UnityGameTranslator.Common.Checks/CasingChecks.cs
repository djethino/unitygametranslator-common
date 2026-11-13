using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The two forms of the first-letter case tables, held equal: the C# tables (<see cref="Casing"/>) and the file a
    /// port in another language reads (corpus/data/first-letter.json) — entry by entry, with the same Unicode
    /// version. A table in one and not in the other would give two answers to one text.
    ///
    /// ⚠ It does not compare either with the machine's own casing: that is the Unicode of whatever runs this, the
    /// very thing the tables exist to avoid. The corpus holds the answers.
    /// </summary>
    internal static class CasingChecks
    {
        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the data is read from it");
            if (commonRoot == null) return;

            string path = Path.Combine(commonRoot, "corpus", "data", "first-letter.json");
            check(File.Exists(path), "corpus/data/first-letter.json is there", "the site's editor reads it");
            if (!File.Exists(path)) return;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            check(root.GetProperty("unicode_version").GetString() == Casing.UnicodeVersion,
                "the data and the tables carry the same Unicode version (" + Casing.UnicodeVersion + ")", "two versions would give two answers");

            foreach (bool capital in new[] { true, false })
            {
                string name = capital ? "capital" : "small";
                var fromJson = root.GetProperty(name).EnumerateObject()
                    .Select(p => new KeyValuePair<int, string>(int.Parse(p.Name, NumberStyles.HexNumber, CultureInfo.InvariantCulture), p.Value.GetString()!))
                    .OrderBy(p => p.Key).ToList();
                var fromCode = Casing.Entries(capital).OrderBy(p => p.Key).ToList();

                bool same = fromJson.Count == fromCode.Count
                            && fromJson.Zip(fromCode, (a, b) => a.Key == b.Key && string.Equals(a.Value, b.Value, StringComparison.Ordinal)).All(x => x);
                check(same, $"the {name} entries are the same in the file and in the tables ({fromCode.Count})",
                    "the JSON has " + fromJson.Count + ", the tables " + fromCode.Count + " — regenerate both with tools/generate-casing.py");

                // A key is in a table only when it changes something, and is never listed twice.
                check(fromCode.All(p => char.ConvertFromUtf32(p.Key) != p.Value) && fromCode.Select(p => p.Key).Distinct().Count() == fromCode.Count,
                    $"no {name} entry is the identity, none is listed twice", "a table of what changes");
            }

            check(root.GetProperty("sources_sha256").EnumerateObject().Count() == 2,
                "the JSON names the two source files it was made from", "a table made from other bytes must be visible");
        }
    }
}
