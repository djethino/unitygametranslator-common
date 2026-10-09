using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The metadata list against the contract it comes from: the <c>properties</c> of
    /// spec/translation-file/schema.json, re-read here rather than trusted.
    ///
    /// ⚠ The corpus (rules/translation_file_keys.json) holds the ANSWERS; this holds the LIST, which a
    /// case cannot. A key added to the schema and forgotten in <see cref="TranslationFileKeys"/>
    /// would be left alone by every reader — written by the mod, never read back — and a key left in
    /// the list after leaving the schema would hide a line of somebody's game.
    /// </summary>
    internal static class TranslationFileKeysChecks
    {
        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the schema is read from it; without it this proves nothing");
            if (commonRoot == null) return;

            string path = Path.Combine(commonRoot, "spec", "translation-file", "schema.json");
            check(File.Exists(path), "spec/translation-file/schema.json is there", "the list is checked against it");
            if (!File.Exists(path)) return;

            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var inSchema = doc.RootElement.GetProperty("properties").EnumerateObject().Select(p => p.Name)
                .OrderBy(n => n, StringComparer.Ordinal).ToList();
            var inCode = TranslationFileKeys.MetadataKeys.OrderBy(n => n, StringComparer.Ordinal).ToList();

            var missing = inSchema.Except(inCode, StringComparer.Ordinal).ToList();
            var extra = inCode.Except(inSchema, StringComparer.Ordinal).ToList();

            check(inSchema.Count > 0, $"the schema names {inSchema.Count} metadata key(s)",
                "an empty read would make the comparison below pass without asking anything");
            check(missing.Count == 0,
                missing.Count == 0 ? "every metadata key of the schema is in the list"
                                   : "IN THE SCHEMA, NOT IN THE LIST: " + string.Join(", ", missing),
                "the mod would write it and every reader leave it alone");
            check(extra.Count == 0,
                extra.Count == 0 ? "and the list holds nothing the schema does not"
                                 : "IN THE LIST, NOT IN THE SCHEMA: " + string.Join(", ", extra),
                "a game's text under that name would be hidden as metadata");

            // Every name of the list is an underscore name: the tolerance for a newer writer
            // (TranslationFileKeys.IsLine) only holds if metadata never looks like a line's text.
            var bare = inCode.Where(n => n.Length == 0 || n[0] != '_').ToList();
            check(bare.Count == 0,
                bare.Count == 0 ? "and every metadata key starts with an underscore"
                                : "A METADATA KEY WITHOUT AN UNDERSCORE: " + string.Join(", ", bare),
                "a name a game could show would make that text impossible to store");
        }
    }
}
