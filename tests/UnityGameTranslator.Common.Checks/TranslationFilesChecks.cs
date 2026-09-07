using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The files a translation keeps beside itself, checked against what depends on their names.
    ///
    /// 🔴 The stake is a defect that shipped: the ancestor spelled once as
    /// <c>translations.ancestor.json</c> among five sites, so a fork never dropped the ancestor of
    /// the lineage it left. And the images section read as <c>_images</c> while the file writes
    /// <c>_image_replacements</c>, so no "saved" backup ever carried an image. Neither threw. These
    /// cases pin the spelling the file actually uses, so a second writer cannot drift from it.
    /// </summary>
    internal static class TranslationFilesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            const string path = "C:/Games/Some Game/BepInEx/plugins/UnityGameTranslator/translations.json";

            // ── The translation and its two ancestors ─────────────────────
            check(TranslationFiles.Name == "translations.json",
                "the translation is translations.json",
                "the manager finds a game's work by this name and the site serves it under it");

            check(TranslationFiles.AncestorOf(path) == path + ".ancestor",
                "the ancestor is the translation's path plus .ancestor",
                "appended, never substituted: substituting is how translations.ancestor.json was born");

            check(TranslationFiles.MainAncestorOf(path) == path + ".mainancestor",
                "the upstream ancestor is the path plus .mainancestor",
                "a branch keeps two baselines — its own last sync and the Main it merged from — and they must stay two files");

            check(TranslationFiles.AncestorOf(path) != TranslationFiles.MainAncestorOf(path),
                "the two ancestors never share a name",
                "one fed to the other's merge reads every owned line as a remote deletion");

            check(TranslationFiles.AncestorOf(path).StartsWith(path, StringComparison.Ordinal)
                  && TranslationFiles.MainAncestorOf(path).StartsWith(path, StringComparison.Ordinal),
                "both names begin with the whole translation path",
                "the manager's uninstall sweep files everything starting with that name; a name built otherwise escapes it");

            check(!TranslationFiles.AncestorOf(path).EndsWith(".json", StringComparison.Ordinal)
                  && !TranslationFiles.MainAncestorOf(path).EndsWith(".json", StringComparison.Ordinal),
                "an ancestor does not end in .json",
                "a folder listing must never offer a baseline as if it were a translation");

            check(TranslationFiles.AncestorSuffix != EditSessions.MarkerSuffix
                  && TranslationFiles.MainAncestorSuffix != EditSessions.MarkerSuffix,
                "the ancestors and the edit-session marker are different files",
                "all three sit beside the translation; three suffixes, three meanings");

            bool refused = false;
            try { TranslationFiles.AncestorOf(null!); }
            catch (ArgumentNullException) { refused = true; }
            check(refused,
                "no path names no ancestor",
                "a null would become '.ancestor' at the root of nowhere; refusing is the only honest answer");

            // ── Where the images are named ────────────────────────────────
            check(TranslationFiles.ImagesSection == "_image_replacements",
                "images are listed under _image_replacements",
                "the backups read _images for weeks and copied no image; this is the key the file writes");

            check(TranslationFiles.ImageFileField == "file",
                "an image entry names its file under 'file'",
                "the name a reader resolves under images/; a different field copies nothing");

            check(Array.IndexOf(TranslationFiles.ImageFileLegacyFields, "replacement_file") >= 0
                  && Array.IndexOf(TranslationFiles.ImageFileLegacyFields, "original_file") >= 0,
                "the two older spellings of the file field are still read",
                "files written by earlier versions are still lying in game folders");

            check(Array.IndexOf(TranslationFiles.ImageFileLegacyFields, TranslationFiles.ImageFileField) < 0,
                "the current spelling is not listed among the legacy ones",
                "a reader tries the current field first; listing it twice would hide a typo in either list");
        }
    }
}
