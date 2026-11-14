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
        /// <summary>
        /// <see cref="Casing.Lower"/> over every code point of Unicode: one code point to one (the length in UTF-16 units is kept, a position
        /// in the text is a position in its small form), never across ASCII, idempotent. And where the runtime differs, said — the Kelvin
        /// sign (the engine writes « k »), which is by rule; the count of letters it does not know yet is printed, never asserted (a newer
        /// .NET will know more, and that is not a failure of ours).
        /// </summary>
        private static void Whole(Action<bool, string, string> check)
        {
            int changed = 0, lengthMoved = 0, crossed = 0, notIdempotent = 0, engineDiffers = 0;
            for (int cp = 0; cp <= 0x10FFFF; cp++)
            {
                if (cp >= 0xD800 && cp <= 0xDFFF) continue;
                string text = char.ConvertFromUtf32(cp);
                string small = Casing.Lower(text)!;
                if (small == text) { if (!string.Equals(text.ToLowerInvariant(), text, StringComparison.Ordinal)) engineDiffers++; continue; }
                changed++;
                if (small.Length != text.Length) lengthMoved++;
                if ((cp < 0x80) != (char.ConvertToUtf32(small, 0) < 0x80)) crossed++;
                if (!string.Equals(Casing.Lower(small), small, StringComparison.Ordinal)) notIdempotent++;
                if (!string.Equals(text.ToLowerInvariant(), small, StringComparison.Ordinal)) engineDiffers++;
            }
            Console.WriteLine($"    Casing.Lower: {changed} code points change; the runtime ({System.Environment.Version}) answers otherwise for {engineDiffers}");
            check(changed > 1400, $"Lower changes {changed} code points", "a table of what changes");
            check(lengthMoved == 0, "one code point to one: no length moves", lengthMoved + " moved");
            check(crossed == 0, "no letter crosses ASCII, in either direction", crossed + " crossed");
            check(notIdempotent == 0, "a text already in small letters is left as it is", notIdempotent + " changed twice");
            check(string.Equals("K".ToLowerInvariant(), "k", StringComparison.Ordinal) && Casing.Lower("K") == "K",
                "the Kelvin sign stays itself where the runtime writes « k »", "by rule: Names keeps it apart from « k » too");
        }

        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            Whole(check);

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
