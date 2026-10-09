using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Which keys of a translations.json are metadata, which are lines of the game's text, and
    /// which are neither.
    ///
    /// 🔴 **Not « starts with an underscore »** (2026-10-09). A game's text can begin with one — a
    /// signature line drawn with underscores, a label « _One-quarter Brick Triangular Wall » found
    /// in a published translation — and the prefix rule made every reader skip it: the mod wrote
    /// the line, dropped it on the next load and sent it to the model again at every launch, and
    /// the site never counted, showed or published it.
    ///
    /// The three answers (decided by the user, 2026-10-09):
    ///
    /// | key | is |
    /// |---|---|
    /// | one of <see cref="MetadataKeys"/> — the <c>properties</c> of spec/translation-file/schema.json | metadata |
    /// | any other name not starting with « _ » | a line, whatever its value (a reader still takes the old bare string) |
    /// | any other name starting with « _ », its value shaped like a line (an object with a <c>v</c>) | a line |
    /// | any other name starting with « _ », any other value | neither — left alone |
    ///
    /// ⚠ The last row is the contract's tolerance for a NEWER writer: a metadata key added later is
    /// an object, a list, a number — never a <c>{v, …}</c> — so an older reader leaves it alone
    /// rather than reading an empty line out of it (spec case
    /// translation-file/metadata/unknown-underscore-key). A new metadata key goes into this list
    /// and the schema together; the checks hold the two equal.
    ///
    /// Ported to the site (app/Support/TranslationFileKeys.php) and the editor
    /// (resources/js/rules/translation-file.js), held to corpus/rules/sync.json.
    /// </summary>
    public static class TranslationFileKeys
    {
        private static readonly HashSet<string> Metadata = new HashSet<string>(StringComparer.Ordinal)
        {
            "_engine_version", "_uuid", "_source_language", "_target_language", "_local_changes",
            "_metadata_dirty", "_game", "_source", "_forked_from", "_fonts", "_font_overrides",
            "_image_replacements", "_exclusions", "_variables", "_settings",
        };

        /// <summary>The names the tools write ABOUT a file.</summary>
        public static IReadOnlyCollection<string> MetadataKeys => Metadata;

        /// <summary>Is this key one of the names the tools write about a file?</summary>
        public static bool IsMetadataKey(string key) => key != null && Metadata.Contains(key);

        /// <summary>
        /// Is this entry a line of the game's text?
        /// </summary>
        /// <param name="key">The key as the file holds it.</param>
        /// <param name="lineShaped">Its value is a JSON object with a <c>v</c> member — asked only of a
        /// name that starts with « _ » and is not metadata. This library reads no JSON: the caller does.</param>
        public static bool IsLine(string key, bool lineShaped)
        {
            if (key == null || IsMetadataKey(key)) return false;
            // An empty key stays what it always was here: a line (nothing writes one).
            return key.Length == 0 || key[0] != '_' || lineShaped;
        }
    }
}
