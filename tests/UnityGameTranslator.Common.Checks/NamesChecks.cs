using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Names are compared by their simple capitals (<see cref="Names"/>). THE PROOF that this changes no answer where it does
    /// not have to: against the runtime's own OrdinalIgnoreCase, over EVERY code point and in both directions, then on the
    /// language catalogue, on synthetic names of every cased script, and on the ORDER of names (which is what a pack writes).
    ///
    /// The measured result, on .NET 8.0.34 against Unicode 18.0.0 (2026-11-14):
    ///  · the runtime NEVER folds two code points that Names keeps apart — Names knows everything the runtime knows;
    ///  · Names folds 75 pairs more: the capitals added to Unicode after the runtime's own table (Latin Extended-D and -G,
    ///    Cyrillic, Garay, Sorang Sompeng and others of Unicode 16 to 18). A game's older Mono knows fewer pairs still, so
    ///    the list there is longer: this is the part the table is FOR.
    ///  · everything else — ASCII, Latin, Greek, Cyrillic, Armenian, Georgian, Cherokee, Deseret, Glagolitic, Adlam… — is
    ///    identical, code point by code point.
    /// The list is frozen below as « small letter &gt; capital »; a newer runtime shrinks it, which is accepted (a subset),
    /// while a pair the runtime folds and Names does not, or a new one Names folds beyond the list, is a failure.
    /// </summary>
    internal static class NamesChecks
    {
        // Small letter > capital, as hexadecimal code points: the pairs Names folds and .NET 8.0.34 does not.
        private const string KnownBeyondTheRuntime =
            "019B>A7DC 0264>A7CB 0277>A7DD 027C>A7E2 10D70>10D50 10D71>10D51 10D72>10D52 10D73>10D53 10D74>10D54 " +
            "10D75>10D55 10D76>10D56 10D77>10D57 10D78>10D58 10D79>10D59 10D7A>10D5A 10D7B>10D5B 10D7C>10D5C 10D7D>10D5D " +
            "10D7E>10D5E 10D7F>10D5F 10D80>10D60 10D81>10D61 10D82>10D62 10D83>10D63 10D84>10D64 10D85>10D65 16EBB>16EA0 " +
            "16EBC>16EA1 16EBD>16EA2 16EBE>16EA3 16EBF>16EA4 16EC0>16EA5 16EC1>16EA6 16EC2>16EA7 16EC3>16EA8 16EC4>16EA9 " +
            "16EC5>16EAA 16EC6>16EAB 16EC7>16EAC 16EC8>16EAD 16EC9>16EAE 16ECA>16EAF 16ECB>16EB0 16ECC>16EB1 16ECD>16EB2 " +
            "16ECE>16EB3 16ECF>16EB4 16ED0>16EB5 16ED1>16EB6 16ED2>16EB7 16ED3>16EB8 1C8A>1C89 1DF41>1DF40 1DF49>1DF48 " +
            "1DF4B>1DF4A 1DF4E>1DF4D 1DF52>1DF51 1DF69>1DF68 1DF6B>1DF6A 1DF6D>1DF6C 1DF6F>1DF6E 1DF73>1DF72 1DF75>1DF74 " +
            "1DF77>1DF76 1DF79>1DF78 1DF7B>1DF7A 1DF7D>1DF7C 1DF7F>1DF7E A7CD>A7CC A7CF>A7CE A7D3>A7D2 A7D5>A7D4 A7DB>A7DA " +
            "AB4B>AB6C AB4C>AB6D";

        public static void Run(Action<bool, string, string> check)
        {
            var known = new HashSet<string>(KnownBeyondTheRuntime.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
            var namesFoldsMore = new List<string>();
            var runtimeFoldsMore = new List<string>();
            var byHash = new Dictionary<int, List<int>>();

            for (int cp = 0; cp <= 0x10FFFF; cp++)
            {
                if (cp >= 0xD800 && cp <= 0xDFFF) continue;
                string s = char.ConvertFromUtf32(cp);
                int folded = Names.Fold(cp);
                if (folded != cp && !string.Equals(s, char.ConvertFromUtf32(folded), StringComparison.OrdinalIgnoreCase))
                    namesFoldsMore.Add(cp.ToString("X4") + ">" + folded.ToString("X4"));

                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(s);
                if (!byHash.TryGetValue(hash, out var bucket)) byHash[hash] = bucket = new List<int>();
                bucket.Add(cp);
            }
            foreach (var bucket in byHash.Values)
            {
                if (bucket.Count < 2) continue;
                for (int i = 0; i < bucket.Count; i++)
                    for (int j = i + 1; j < bucket.Count; j++)
                    {
                        string a = char.ConvertFromUtf32(bucket[i]), b = char.ConvertFromUtf32(bucket[j]);
                        if (string.Equals(a, b, StringComparison.OrdinalIgnoreCase) && Names.Fold(bucket[i]) != Names.Fold(bucket[j]))
                            runtimeFoldsMore.Add(bucket[i].ToString("X4") + "=" + bucket[j].ToString("X4"));
                    }
            }
            var unexpected = namesFoldsMore.Where(p => !known.Contains(p)).ToList();
            check(runtimeFoldsMore.Count == 0, "the runtime never folds two code points that Names keeps apart (all of Unicode)",
                runtimeFoldsMore.Count + " pair(s): " + string.Join(" ", runtimeFoldsMore.Take(20)));
            check(unexpected.Count == 0, "Names folds only the pairs the runtime does not know yet (" + namesFoldsMore.Count + " of " + known.Count + " frozen)",
                unexpected.Count == 0 ? "the rest is identical, code point by code point" : "not in the frozen list: " + string.Join(" ", unexpected.Take(20)));

            // No bridge between ASCII and the rest, as the runtime has none.
            check(!Names.Equals("\u0131", "I") && !Names.Equals("\u017F", "s") && !Names.Equals("\u212A", "k") && !Names.Equals("\u0130", "i") && !Names.Equals("i", "\u0130"),
                "no code point beyond ASCII is the same as an ASCII letter", "dotless i, dotted I, long s, Kelvin");
            check(Names.Equals("I", "i") && Names.Equals("\u0131", "\u0131") && Names.Equals("\u0130", "\u0130"),
                "I and i are one name, the dotless i and the dotted I keep to themselves", "the Turkish pairs stay apart");

            // A lone surrogate is no code point of a text and cannot be written in the corpus's JSON: said here.
            string lone = "a\uD801b\uDC28";
            check(Names.Fold(lone) == "A\uD801B\uDC28" && Names.Equals(lone, "A\uD801B\uDC28"), "a lone surrogate stays as it is", "neither half is read as a code point");

            Catalogue(check);
            Synthetic(check);
            Order(check);
        }

        /// <summary>
        /// The two forms of the table, held equal: the C# one (<see cref="Names.Fold(int)"/>) and the file a port in another language reads
        /// (corpus/data/simple-capital.json) — entry by entry, from the same Unicode version and the same source bytes. A port never asks its
        /// engine for a capital: <c>toUpperCase</c> gives the FULL upper case (two code points for « ᾳ ») and the Unicode of the browser.
        /// </summary>
        public static void Data(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the data is read from it");
            if (commonRoot == null) return;

            string path = System.IO.Path.Combine(commonRoot, "corpus", "data", "simple-capital.json");
            check(System.IO.File.Exists(path), "corpus/data/simple-capital.json is there", "the site's editor reads it");
            if (!System.IO.File.Exists(path)) return;

            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(path));
            var root = doc.RootElement;
            check(root.GetProperty("unicode_version").GetString() == Names.UnicodeVersion,
                "the data and the table carry the same Unicode version (" + Names.UnicodeVersion + ")", "two versions would give two answers");

            var fromJson = root.GetProperty("capital").EnumerateObject()
                .Select(p => new KeyValuePair<int, int>(int.Parse(p.Name, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
                                                          char.ConvertToUtf32(p.Value.GetString()!, 0)))
                .OrderBy(p => p.Key).ToList();
            var fromCode = Names.Entries().OrderBy(p => p.Key).ToList();
            bool same = fromJson.Count == fromCode.Count && fromJson.Zip(fromCode, (a, b) => a.Key == b.Key && a.Value == b.Value).All(x => x);
            check(same, "the entries are the same in the file and in the table (" + fromCode.Count + ")",
                "the JSON has " + fromJson.Count + ", the table " + fromCode.Count + " — regenerate both with tools/generate-case-mapping.py");
            check(root.GetProperty("capital").EnumerateObject().All(p => char.ConvertToUtf32(p.Value.GetString()!, 0) != int.Parse(p.Name, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)
                                                                         && p.Value.GetString()!.Length == (char.ConvertToUtf32(p.Value.GetString()!, 0) > 0xFFFF ? 2 : 1)),
                "no entry is the identity, and each capital is ONE code point", "a table of what changes, one code point to one");

            // The source's fingerprint is the one the generated table's header carries: the JSON and the C# come from the same bytes.
            string header = System.IO.File.ReadAllText(System.IO.Path.Combine(commonRoot, "src", "UnityGameTranslator.Common", "CaseMapping.Tables.g.cs"));
            string sha = root.GetProperty("sources_sha256").GetProperty("UnicodeData").GetString()!;
            check(sha.Length == 64 && header.Contains("UnicodeData-sha256: " + sha, StringComparison.Ordinal),
                "the file and the table were made from the same UnicodeData bytes", "a table made from other bytes must be visible");

            // The one the issue showed: the Greek letters with a subscript iota give ONE code point, never the engine's two.
            check(Names.Fold(0x1FB3) == 0x1FBC && Names.Fold(0x1FC3) == 0x1FCC && Names.Fold(0x1FF3) == 0x1FFC && Names.Fold(0x1F80) == 0x1F88 && Names.Fold(0x1FA0) == 0x1FA8 && Names.Fold(0x1FA8) == 0x1FA8,
                "the letters with a subscript iota fold to their one-letter capital", "an engine's toUpperCase would say two letters");
        }

        /// <summary>Every name and code the language catalogue holds: the same equalities, the same hash contract.</summary>
        private static void Catalogue(Action<bool, string, string> check)
        {
            var all = new List<string>();
            foreach (var (code, name) in Languages.All()) { all.Add(code); all.Add(name); }
            all.AddRange(Languages.Names());
            var texts = all.Distinct(StringComparer.Ordinal).ToList();
            var variants = new List<string>(texts);
            foreach (string t in texts)
            {
                variants.Add(t.ToUpperInvariant());
                variants.Add(t.ToLowerInvariant());
            }
            var set = variants.Distinct(StringComparer.Ordinal).ToList();

            int differ = 0, hashBroken = 0;
            string first = "";
            for (int i = 0; i < set.Count; i++)
                for (int j = i; j < set.Count; j++)
                {
                    bool names = Names.Equals(set[i], set[j]);
                    bool runtime = string.Equals(set[i], set[j], StringComparison.OrdinalIgnoreCase);
                    if (names != runtime) { differ++; if (first.Length == 0) first = set[i] + " / " + set[j]; }
                    if (names && Names.Comparer.GetHashCode(set[i]) != Names.Comparer.GetHashCode(set[j])) hashBroken++;
                }
            check(set.Count > 200, "the catalogue is read (" + set.Count + " names and codes, with their cases)", "a check on an empty list proves nothing");
            check(differ == 0, "on every pair of the language catalogue, Names equals what OrdinalIgnoreCase equals", differ + " pair(s) differ, first: " + first);
            check(hashBroken == 0, "equal names have equal hashes", hashBroken + " do not");
        }

        /// <summary>One cased word per script, in several forms: Names and the runtime say the same (outside the frozen pairs).</summary>
        private static void Synthetic(Action<bool, string, string> check)
        {
            var words = new[]
            {
                "Café Été", "Łódź Gdańsk", "Ǆ ǅ ǆ Ǉ ǈ ǉ", "ßtraße", "İstanbul Istanbul",
                "ΣΠΑΡΤΑΣ σπάρτας", "Άλφα Ωμέγα",
                "Дружба Яблоко", "Այբուբեն", "აბგ ᲐᲑᲒ",
                "ᎠᎡᎢ ꭰꭱ", "ⰀⰁ ⰰⰱ", "Ꙁꙁ Ꟃꟃ", "ẞ ß", "ⅠⅡ ⅰⅱ", "Ⓐ ⓐ",
                "ＡＢ ａｂ",
                "\U00010400\U00010401 \U00010428\U00010429", "\U000104B0 \U000104D8", "\U00010C80 \U00010CC0", "\U000118A0 \U000118C0", "\U0001E900 \U0001E922",
                "\U00016E40 \U00016E60", "\U00010570 \U00010597"
            };
            int differ = 0;
            string first = "";
            foreach (string w in words)
            {
                var forms = new[] { w, w.ToUpperInvariant(), w.ToLowerInvariant(), w + "_x", "[" + w };
                foreach (string a in forms)
                    foreach (string b in forms)
                        if (Names.Equals(a, b) != string.Equals(a, b, StringComparison.OrdinalIgnoreCase))
                        { differ++; if (first.Length == 0) first = a + " / " + b; }
            }
            check(differ == 0, "synthetic names of every cased script (" + words.Length + " words, five forms each): the same equalities", differ + " differ, first: " + first);
        }

        /// <summary>The ORDER: a list sorts as OrdinalIgnoreCase sorted it — what keeps a written pack where it was.</summary>
        private static void Order(Action<bool, string, string> check)
        {
            var names = new List<string>
            {
                "a", "A", "b", "B", "_", "_a", "a_", "A_b", "[x", "x]", "x^", "x`", "x\\", "Z", "z", "Zebra", "zebra", "font", "Font", "FONT_2", "font_1", "font-1", "Font 1",
                "0", "00", "1", "10", "2", "9", "Été", "été", "è", "È", "e", "f", "ß", "SS", "ss", "ı", "I", "i", "İ",
                "Σ", "σ", "ς", "Ж", "ж", "中", "中文", "ßß", "Ａ", "ａ", "\U00010400", "\U00010428", "\U0001F600", "", "�",
                "\uE000", "\uFFFD", "Language", "language", "LANGUAGE", "Languages", "Lang_", "lang[", "Langé",
            };
            var byRuntime = names.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
            var byNames = names.OrderBy(n => n, Names.Comparer).ToList();
            check(byRuntime.SequenceEqual(byNames, StringComparer.Ordinal), "a list of names sorts as OrdinalIgnoreCase sorted it (" + names.Count + " names, « _ » « [ » « ` » among them)",
                FirstDifference(byRuntime, byNames));

            // « _ » is after « Z » and before « a » in capitals as in the runtime's order: that is why it is the CAPITAL that folds.
            check(Names.Compare("z", "_") < 0 && Names.Compare("_", "a") > 0,
                "« _ » sorts after « z », not before the letters as a lower-case fold would put it", "the order a pack was written in");
            check(Names.Compare("a", "A") == 0 && Names.Compare("a", "b") < 0 && Names.Compare(null, "a") < 0 && Names.Compare(null, null) == 0,
                "equal but for case is 0; null comes first", "the comparer's contract");
        }

        private static string FirstDifference(List<string> runtime, List<string> names)
        {
            for (int i = 0; i < Math.Min(runtime.Count, names.Count); i++)
                if (!string.Equals(runtime[i], names[i], StringComparison.Ordinal))
                    return "from place " + i + ": runtime " + string.Join(" | ", runtime.Skip(i).Take(5).Select(Show)) + " / names " + string.Join(" | ", names.Skip(i).Take(5).Select(Show));
            return "the same order";
        }

        private static string Show(string text) => string.Concat(text.Select(c => c < 0x80 ? c.ToString() : "\\u" + ((int)c).ToString("X4")));
    }
}
