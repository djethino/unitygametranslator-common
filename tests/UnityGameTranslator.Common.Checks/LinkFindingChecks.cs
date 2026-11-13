using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What the corpus cannot say about <see cref="LinkFinding"/>:
    ///
    ///  · the cost of a sentence does not depend on how many terms the file has — a file holds thousands of terms and
    ///    tens of thousands of sentences, and a search of every term in every sentence would be quadratic. The cost is
    ///    measured on a synthetic file (seeded, the same on every machine; pseudo-words, no game's text): per sentence,
    ///    time and bytes at 50 terms and at 5 000 must stay within a small factor of each other, the figures are printed;
    ///  · a file with no term costs nothing: the same string back and not one byte allocated;
    ///  · the word table and the case tables carry the same Unicode version.
    /// </summary>
    internal static class LinkFindingChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(WordGlue.UnicodeVersion == Casing.UnicodeVersion,
                "the word table and the case tables carry the same Unicode version (" + WordGlue.UnicodeVersion + ")", "one Unicode for everything");

            var empty = TermIndex.Build(new List<KeyValuePair<string, TranslationLine>>());
            string text = "une phrase sans aucun terme dans le fichier";
            LinkFinding.Find(empty, text, "a sentence", null);   // warm up
            long before = GC.GetAllocatedBytesForCurrentThread();
            var plain = LinkFinding.Find(empty, text, "a sentence", null);
            long after = GC.GetAllocatedBytesForCurrentThread();
            check(ReferenceEquals(plain.V, text) && after - before == 0,
                "a file with no term gives the text back as the same string, allocating nothing (" + (after - before) + " bytes)", "a file with no term pays nothing");

            // ── cost against the number of terms ────────────────────────────────
            const int sentences = 100000;
            var small = Measure(50, sentences / 4);
            var large = Measure(5000, sentences);

            Console.WriteLine($"    cost: 50 terms {small.MicrosPerSentence:F1} µs and {small.BytesPerSentence:F0} bytes per sentence ({small.Linked} links in {sentences / 4} sentences); "
                              + $"5 000 terms {large.MicrosPerSentence:F1} µs and {large.BytesPerSentence:F0} bytes per sentence ({large.Linked} links in {sentences}); "
                              + $"index built in {large.BuildMillis} ms, {large.IndexKilobytes} KB; {sentences} sentences in {large.TotalMillis / 1000.0:F1} s");

            check(large.MicrosPerSentence <= small.MicrosPerSentence * 3 + 20,
                $"a sentence costs the same at 5 000 terms as at 50 ({large.MicrosPerSentence:F1} µs against {small.MicrosPerSentence:F1})",
                "the cost depends on the length of the sentence, not on the number of terms");
            check(large.BytesPerSentence <= small.BytesPerSentence * 3 + 2048,
                $"and what it allocates follows what it FINDS, not the terms ({large.BytesPerSentence:F0} bytes against {small.BytesPerSentence:F0}, with {large.Linked * 100 / sentences} links per 100 sentences against {small.Linked * 100 / (sentences / 4)})", "same");
            check(large.Linked > 0, "the synthetic file does find links", "a measure that finds nothing measures nothing");
        }

        private struct Cost
        {
            public double MicrosPerSentence, BytesPerSentence;
            public long BuildMillis, TotalMillis, IndexKilobytes, Linked;
        }

        private static Cost Measure(int termCount, int sentenceCount)
        {
            var random = new Random(20261114);
            string Word(int minLength, int maxLength)
            {
                int n = minLength + random.Next(maxLength - minLength + 1);
                var sb = new StringBuilder(n);
                for (int i = 0; i < n; i++) sb.Append((char)('a' + random.Next(26)));
                return sb.ToString();
            }

            // A vocabulary of source words and of target words; a term is one or two of each.
            var sourceWords = new List<string>();
            var targetWords = new List<string>();
            for (int i = 0; i < 4000; i++) { sourceWords.Add(Word(3, 9)); targetWords.Add(Word(3, 10)); }

            var lines = new List<KeyValuePair<string, TranslationLine>>();
            var seen = new HashSet<string>();
            while (lines.Count < termCount)
            {
                int w = random.Next(sourceWords.Count);
                string key = random.Next(2) == 0 ? sourceWords[w] : sourceWords[w] + " " + sourceWords[random.Next(sourceWords.Count)];
                if (!seen.Add(key)) continue;
                var line = new TranslationLine(targetWords[w], "V")
                {
                    Term = true,
                    Forms = new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("f1", targetWords[w] + "s") },
                };
                lines.Add(new KeyValuePair<string, TranslationLine>(key, line));
            }

            long memory0 = GC.GetTotalMemory(true);
            var clock = Stopwatch.StartNew();
            var index = TermIndex.Build(lines);
            long buildMillis = clock.ElapsedMilliseconds;
            long indexKb = (GC.GetTotalMemory(true) - memory0) / 1024;

            // Sentences: a source of 8 to 14 words, a translation of the same length, some words from the terms.
            var sources = new string[sentenceCount];
            var texts = new string[sentenceCount];
            for (int i = 0; i < sentenceCount; i++)
            {
                int n = 8 + random.Next(7);
                var s = new StringBuilder(); var t = new StringBuilder();
                for (int k = 0; k < n; k++)
                {
                    int w = random.Next(sourceWords.Count);
                    s.Append(k == 0 ? "" : " ").Append(sourceWords[w]);
                    t.Append(k == 0 ? "" : " ").Append(random.Next(3) == 0 ? targetWords[w] : targetWords[random.Next(targetWords.Count)]);
                }
                sources[i] = s.ToString();
                texts[i] = t.ToString();
            }

            for (int i = 0; i < 200 && i < sentenceCount; i++) LinkFinding.Find(index, texts[i], sources[i], null);   // warm up

            long bytes0 = GC.GetAllocatedBytesForCurrentThread();
            clock.Restart();
            long linked = 0;
            for (int i = 0; i < sentenceCount; i++) linked += LinkFinding.Find(index, texts[i], sources[i], null).Links.Count;
            long total = clock.ElapsedMilliseconds;
            long bytes = GC.GetAllocatedBytesForCurrentThread() - bytes0;

            return new Cost
            {
                MicrosPerSentence = total * 1000.0 / sentenceCount,
                BytesPerSentence = bytes / (double)sentenceCount,
                BuildMillis = buildMillis,
                TotalMillis = total,
                IndexKilobytes = indexKb,
                Linked = linked,
            };
        }
    }
}
