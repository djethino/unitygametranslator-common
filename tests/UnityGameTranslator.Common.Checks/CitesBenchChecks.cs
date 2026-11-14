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
    }
}
