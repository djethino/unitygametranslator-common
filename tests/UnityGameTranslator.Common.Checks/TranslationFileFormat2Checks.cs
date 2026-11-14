using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using UnityGameTranslator.Common.Checks.Corpus;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The format 2 of spec/translation-file/, held against the C# that implements it — the half
    /// `check-spec.py` cannot do (it knows the schema, not the rules a writer owes).
    ///
    /// Three things, each re-read from the files rather than trusted:
    ///
    ///  · every case of cases.json is replayed through <see cref="LineWrites"/>: a case that
    ///    names a `guarantee` must be refused with exactly that code, and a case the door
    ///    ACCEPTS must be refused by nobody — which also makes every format 1 case of the file a
    ///    check that the new rules ask nothing of an old file;
    ///  · a case's `read2` (what a reader of the format 2 derives) must hash like the document:
    ///    a reader that drops a field the hash covers would differ;
    ///  · the marker grammar of the schema (`x-markers`) is the one of <see cref="LineMarkers"/>,
    ///    and the codes of `x-writer-guarantees` are the codes of <see cref="LineWrites"/> — no
    ///    more, no fewer — and each says something in words.
    /// </summary>
    internal static class TranslationFileFormat2Checks
    {
        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the spec is read from it; without it this proves nothing");
            if (commonRoot == null) return;

            string folder = Path.Combine(commonRoot, "spec", "translation-file");
            using var cases = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "cases.json")));
            using var schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "schema.json")));

            int guaranteed = 0, accepted = 0, read2 = 0;

            foreach (var c in cases.RootElement.GetProperty("cases").EnumerateArray())
            {
                string id = c.GetProperty("id").GetString()!;
                if (!c.TryGetProperty("document", out var document) || document.ValueKind != JsonValueKind.Object) continue;

                var lines = LinesOf(document);
                int format = TranslationFormat.OfFile(
                    document.TryGetProperty("_engine_version", out var ev) && ev.ValueKind == JsonValueKind.Number ? ev.GetInt32() : (int?)null);
                var lists = ListsOf(document);

                string? code = null;
                string? words = null;
                foreach (var line in lines)
                {
                    var found = LineWrites.Refusal(line.Key, line.Value, format,
                        key => lines.Where(l => l.Key == key).Select(l => (TranslationLine?)l.Value).FirstOrDefault(),
                        lists.Contains, false, null);
                    if (found.HasValue) { code = found.Value.Code; words = found.Value.Words; break; }
                }

                // the game's tokens (`_lists.tokens`): a file-level guarantee, asked once the lines have had theirs
                if (code == null && document.TryGetProperty("_lists", out var tokenSection) && tokenSection.ValueKind == JsonValueKind.Object
                    && tokenSection.TryGetProperty("tokens", out var tokenArray) && tokenArray.ValueKind == JsonValueKind.Array)
                {
                    var refusal = GameTokenList.Refusal(tokenArray.EnumerateArray().Select(t => t.GetString() ?? "").ToList());
                    if (refusal.HasValue) { code = refusal.Value.Code; words = refusal.Value.Words; }
                }

                if (c.TryGetProperty("guarantee", out var guarantee))
                {
                    guaranteed++;
                    check(code == guarantee.GetString(), id + " is refused for the reason it names",
                        $"expected {guarantee.GetString()}, got {code ?? "no refusal"}");
                    check(!string.IsNullOrWhiteSpace(words),
                        id + " says why in words", "a refusal with no sentence tells nobody what to fix");
                }
                else if (c.TryGetProperty("upload", out var upload) && upload.GetString() == "accepted")
                {
                    accepted++;
                    check(code == null, id + " is accepted by the writer rules too", $"refused for {code}: {words}");
                }

                if (c.TryGetProperty("read2", out var read))
                {
                    read2++;
                    string uuid = read.TryGetProperty("uuid", out var u) ? u.GetString()! : "";
                    var derived = JsonValues.Lines(read, "lines");
                    // The reader normalises line endings first (LineEndings.Of): the hash is taken over LF alone.
                    var normalised = new List<KeyValuePair<string, TranslationLine>>();
                    foreach (var l in lines)
                        normalised.Add(new KeyValuePair<string, TranslationLine>(LineEndings.ToLf(l.Key)!, LineEndings.Of(l.Value)));
                    check(ContentHash.Of(normalised, uuid) == ContentHash.Of(derived, uuid),
                        id + ": a reader of the format 2 keeps everything the hash covers",
                        "the lines it derives do not hash like the document's");
                }
            }

            check(guaranteed >= 7, $"{guaranteed} cases name a guarantee", "an empty replay passes everything");
            check(accepted >= 8, $"{accepted} accepted cases were asked of the writer rules", "an empty replay passes everything");
            check(read2 >= 3, $"{read2} cases say what a reader of the format 2 derives", "an empty replay passes everything");

            // ── the grammar ──────────────────────────────────────────────────────
            var markers = schema.RootElement.GetProperty("x-markers");
            check(markers.GetProperty("link").GetString() == LineMarkers.LinkPattern,
                "the schema's link marker is the library's", "two grammars for one marker");
            check(markers.GetProperty("closedSlot").GetString() == LineMarkers.ClosedSlotPattern,
                "the schema's closed slot is the library's", "two grammars for one marker");
            check(markers.GetProperty("openSlot").GetString() == LineMarkers.OpenSlotPattern,
                "the schema's open slot is the library's", "two grammars for one marker");

            // ── the codes ────────────────────────────────────────────────────────
            var inSchema = schema.RootElement.GetProperty("x-writer-guarantees").GetProperty("codes").EnumerateObject()
                .Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToList();
            var inCode = typeof(LineWrites).GetFields()
                .Where(f => f.IsLiteral && f.FieldType == typeof(string)).Select(f => (string)f.GetRawConstantValue()!)
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            check(inSchema.SequenceEqual(inCode),
                "the guarantees of the schema are the codes of LineWrites",
                "schema only: " + string.Join(", ", inSchema.Except(inCode)) + " — code only: " + string.Join(", ", inCode.Except(inSchema)));
        }

        /// <summary>The lines of a document, in file order: what <see cref="TranslationFileKeys.IsLine"/> says is one and holds an object.</summary>
        private static List<KeyValuePair<string, TranslationLine>> LinesOf(JsonElement document)
        {
            var lines = new List<KeyValuePair<string, TranslationLine>>();
            foreach (var p in document.EnumerateObject())
            {
                if (p.Value.ValueKind != JsonValueKind.Object) continue;
                if (!TranslationFileKeys.IsLine(p.Name, p.Value.TryGetProperty("v", out _))) continue;
                lines.Add(new KeyValuePair<string, TranslationLine>(p.Name, JsonValues.LineOf(p.Value)!.Value));
            }
            return lines;
        }

        private static HashSet<string> ListsOf(JsonElement document)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (document.TryGetProperty("_lists", out var lists) && lists.ValueKind == JsonValueKind.Object
                && lists.TryGetProperty("lists", out var inner) && inner.ValueKind == JsonValueKind.Object)
                foreach (var p in inner.EnumerateObject()) names.Add(p.Name);
            return names;
        }
    }
}
