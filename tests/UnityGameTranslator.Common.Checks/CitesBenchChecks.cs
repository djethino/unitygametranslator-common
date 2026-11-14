using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What a port needs to answer « which terms does this source cite » as the socle does, held on the files it is given:
    ///  · <c>corpus/data/word-glue.json</c> — where a word ends — equals <see cref="WordGlue"/> over every code point, at the
    ///    project's Unicode version;
    ///  · <c>corpus/data/cites-bench.json</c> — 1 620 terms and 6 000 sources — gives, through an index of the keys, the very
    ///    answer a NAIVE reference written in Python only from the rules gave (tools/generate-cites-bench.py: it reads the
    ///    two data files above and never the C#). Two implementations that share nothing agree on every one of the
    ///    places; the time is printed, as a measure and not a limit.
    /// </summary>
    internal static class CitesBenchChecks
    {
        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the data files are read from it");
            if (commonRoot == null) return;
            string data = Path.Combine(commonRoot, "corpus", "data");

            // ── the table a port reads ──────────────────────────────────────────
            string gluePath = Path.Combine(data, "word-glue.json");
            check(File.Exists(gluePath), "corpus/data/word-glue.json is there", "a port reads it, never its engine's idea of a letter");
            if (!File.Exists(gluePath)) return;

            using (var doc = JsonDocument.Parse(File.ReadAllText(gluePath)))
            {
                var root = doc.RootElement;
                check(root.GetProperty("unicode_version").GetString() == WordGlue.UnicodeVersion, "its Unicode version is the table's (" + WordGlue.UnicodeVersion + ")", "two versions would give two words");
                var classes = root.GetProperty("classes").EnumerateArray().Select(c => c.GetString()).ToArray();
                check(classes.SequenceEqual(WordGlue.Classes), "it names the same line-breaking classes", "the table is read as a yes or no on them");
                // Both source files' fingerprints are the generated table's: the JSON and the C# come from the same bytes
                string header = File.ReadAllText(Path.Combine(commonRoot, "src", "UnityGameTranslator.Common", "WordGlue.Tables.g.cs"));
                check(header.Contains("LineBreak-sha256: " + root.GetProperty("line_break_sha256").GetString(), StringComparison.Ordinal)
                      && header.Contains("WordBreakProperty-sha256: " + root.GetProperty("word_break_sha256").GetString(), StringComparison.Ordinal),
                    "the file and the table were made from the same LineBreak.txt and WordBreakProperty.txt bytes", "a table made from other bytes must be visible");
                var wordBreak = root.GetProperty("word_break").EnumerateArray().Select(c => c.GetString()).ToArray();
                check(wordBreak.SequenceEqual(WordGlue.WordBreakValues), "and the same Word_Break values (UAX #29)", "a glued character is one of both: a constituent of a word that the project does not cut");

                var flat = root.GetProperty("glued").EnumerateArray().Select(x => x.GetInt32()).ToArray();
                var fromJson = new bool[0x110000];
                for (int i = 0; i + 1 < flat.Length; i += 2)
                    for (int cp = flat[i]; cp <= flat[i + 1]; cp++) fromJson[cp] = true;
                int differ = 0;
                for (int cp = 0; cp <= 0x10FFFF; cp++)
                    if (cp < 0xD800 || cp > 0xDFFF)
                        if (fromJson[cp] != WordGlue.IsGlued(cp)) differ++;
                check(differ == 0, "it holds exactly the glued code points of WordGlue (" + flat.Length / 2 + " ranges, " + new FileInfo(gluePath).Length + " bytes)", differ + " differ");
            }

            // ── the bench ───────────────────────────────────────────────────────
            string benchPath = Path.Combine(data, "cites-bench.json");
            check(File.Exists(benchPath), "corpus/data/cites-bench.json is there", "a file big enough to be felt by a naive port");
            if (!File.Exists(benchPath)) return;

            using var bench = JsonDocument.Parse(File.ReadAllText(benchPath));
            var b = bench.RootElement;
            var terms = b.GetProperty("terms").EnumerateArray().Select(t => t.GetString()!).ToList();
            var sources = b.GetProperty("sources").EnumerateArray().Select(t => t.GetString()!).ToList();
            check(b.GetProperty("unicode_version").GetString() == WordGlue.UnicodeVersion, "the bench is of the same Unicode version", "regenerate: python tools/generate-cites-bench.py");

            var clock = Stopwatch.StartNew();
            var index = TermIndex.OfKeys(terms);
            long built = clock.ElapsedMilliseconds;
            var text = new StringBuilder();
            int places = 0;
            for (int s = 0; s < sources.Count; s++)
                foreach (var place in LinkFinding.Cites(index, sources[s], null))
                {
                    text.Append(s).Append('\t').Append(place.Key).Append('\t').Append(place.Start).Append('\t').Append(place.End).Append('\n');
                    places++;
                }
            long total = clock.ElapsedMilliseconds;

            string sha;
            using (var hash = SHA256.Create())
                sha = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(x => x.ToString("x2")));

            Console.WriteLine($"    cost: {terms.Count} terms indexed in {built} ms, {sources.Count} sources answered in {total - built} ms ({places} places)");
            check(places == b.GetProperty("places").GetInt32(), "the index finds the " + places + " places of the naive reference", "expected " + b.GetProperty("places").GetInt32());
            check(sha == b.GetProperty("result_sha256").GetString(), "and they are the same places, to the position (SHA-256 of the canonical text)",
                "the two answers differ: the C# index and the Python reference that read only the rules");
        }

        /// <summary>
        /// <c>corpus/data/decorations-bench.json</c>: invented sources of the FORM of a real game's texts (emphasis stars, hash signs, rich-text tags,
        /// symbols round a name) and the places a naive reference finds under « a word's edge is Unicode's, a tag is not text ». The C# index finds the
        /// same, to the position; no place lies inside a tag (recomputed here from the sources, with <see cref="Markup.TagEnd"/>); and the file states what
        /// the former table (UAX #14 alone) lost against this one: every such place is covered by a longer one — asserted when the file is generated,
        /// the former table being at hand only there.
        /// </summary>
        public static void Decorations(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the data files are read from it");
            if (commonRoot == null) return;
            string path = Path.Combine(commonRoot, "corpus", "data", "decorations-bench.json");
            check(File.Exists(path), "corpus/data/decorations-bench.json is there", "run tools/generate-decorations-bench.py");
            if (!File.Exists(path)) return;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var b = doc.RootElement;
            var terms = b.GetProperty("terms").EnumerateArray().Select(t => t.GetString()!).ToList();
            var sources = b.GetProperty("sources").EnumerateArray().Select(t => t.GetString()!).ToList();
            check(b.GetProperty("unicode_version").GetString() == WordGlue.UnicodeVersion, "the bench is of the same Unicode version", "regenerate: python tools/generate-decorations-bench.py");

            var index = TermIndex.OfKeys(terms);
            var clock = Stopwatch.StartNew();
            var text = new StringBuilder();
            int places = 0, insideATag = 0, found = 0;
            for (int s = 0; s < sources.Count; s++)
            {
                var spans = new List<int[]>();
                for (int i = 0; i < sources[s].Length; i++)
                {
                    if (sources[s][i] != '<') continue;
                    int end = Markup.TagEnd(sources[s], i);
                    if (end >= 0) { spans.Add(new[] { i, end + 1 }); i = end; }
                }
                foreach (var place in LinkFinding.Cites(index, sources[s], null))
                {
                    text.Append(s).Append('\t').Append(place.Key).Append('\t').Append(place.Start).Append('\t').Append(place.End).Append('\n');
                    places++;
                    if (spans.Any(sp => sp[0] < place.End && place.Start < sp[1])) insideATag++;
                }
                found++;
            }
            long ms = clock.ElapsedMilliseconds;

            string sha;
            using (var hash = SHA256.Create())
                sha = string.Concat(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())).Select(x => x.ToString("x2")));
            Console.WriteLine($"    cost: {found} decorated sources answered in {ms} ms ({places} places)");

            check(places == b.GetProperty("places").GetInt32() && sha == b.GetProperty("result_sha256").GetString(),
                "the index finds the " + places + " places of the naive reference, to the position",
                "expected " + b.GetProperty("places").GetInt32() + " places; the two answers differ");
            check(insideATag == 0, "no place lies inside a tag", insideATag + " do: a tag is not text");
            check(b.GetProperty("places_lost").GetInt32() > 0 && b.GetProperty("lost_without_a_longer_term").GetInt32() == 0,
                "every place the former table found and this one does not is covered by a longer one (" + b.GetProperty("places_lost").GetInt32() + " of them)",
                "the file says otherwise: the new rule loses a term");
            check(b.GetProperty("places").GetInt32() > b.GetProperty("places_former").GetInt32(),
                "and there are more places than before (" + b.GetProperty("places_former").GetInt32() + " -> " + places + ")", "the decorations were what was missed");
        }
    }
}
