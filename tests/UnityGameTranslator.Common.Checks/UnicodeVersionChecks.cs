using System;
using System.IO;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// One Unicode for everything: <c>common/UNICODE_VERSION</c> is the project's version, written once; every table of
    /// <c>common</c> generated from the Unicode Character Database carries it (and its generator refuses a table made at
    /// another: `--check`), and the mod's own generators are to read the same file. This holds the generated code and the
    /// data file the site copies to it, so that a version moved in the file and not in a table — or the other way round —
    /// is red.
    /// </summary>
    internal static class UnicodeVersionChecks
    {
        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the version file is read from it");
            if (commonRoot == null) return;

            string path = Path.Combine(commonRoot, "UNICODE_VERSION");
            check(File.Exists(path), "UNICODE_VERSION is there", "the one place the project's Unicode version is written");
            if (!File.Exists(path)) return;

            string version = File.ReadAllText(path).Trim();
            check(version.Length > 0 && version != "latest", "it names a version (" + version + "), never « latest »", "a moving version is no pin");
            check(Casing.UnicodeVersion == version, "the case tables carry it", "regenerate: python tools/generate-casing.py");
            check(WordGlue.UnicodeVersion == version, "the word table carries it", "regenerate: python tools/generate-word-glue.py");
            check(KeyChars.UnicodeVersion == version, "the key's character tables carry it", "regenerate: python tools/generate-key-chars.py");
            check(Names.UnicodeVersion == version, "the case-mapping table carries it", "regenerate: python tools/generate-case-mapping.py");
            check(UnicodeCategories.UnicodeVersion == version, "the general-category table carries it", "regenerate: python tools/generate-general-category.py");
        }
    }
}
