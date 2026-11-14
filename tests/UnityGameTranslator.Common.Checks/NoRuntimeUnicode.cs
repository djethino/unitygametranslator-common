using System;
using System.IO;
using UnityGameTranslator.Checks.Shared;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The library asks the RUNTIME nothing about a character — the rule, the scanner and the ratchet are written once in
    /// <c>tests/Shared/RuntimeUnicode.cs</c>, linked by every check project that holds the rule (this one, for the library's own sources;
    /// the Manager's, for what it asks of its own folders). What stays here is the library's own: its folder, its baseline, and what it
    /// still asks the runtime ON PURPOSE.
    /// </summary>
    internal static class NoRuntimeUnicode
    {
        public const string BaselineName = "runtime-unicode.baseline";

        /// <summary>The command that rewrites the library's baseline.</summary>
        public const string Regenerate = "dotnet run -- unicode-list runtime-unicode.baseline";

        /// <summary>
        /// What the library still asks the runtime on purpose, each with its reason. Counted exactly: a new call of the same
        /// kind in the same file is a new debt, and an exception whose call has been removed must go.
        /// </summary>
        public static readonly (string File, string Kind, int Count, string Reason)[] Exceptions =
        {
            ("UnityFiles/GameFonts.cs", "OrdinalIgnoreCase", 1,
                "a PATH (the game's data folder against a font's path): the case of a path is the file system's own table, not Unicode's — nothing of ours can say it"),
            ("UnityFiles/GameFonts.cs", "char.Is*", 1,
                "char.IsControl, the C0 and C1 controls (Cc): a category no Unicode version has ever changed, read to keep them out of a file name"),
        };

        public static void Run(Action<bool, string, string> check, string? commonRoot)
        {
            check(commonRoot != null, "the library's folder is found", "the sources are read from it");
            if (commonRoot == null) return;

            RuntimeUnicode.Verify(check, Path.Combine(commonRoot, "src", "UnityGameTranslator.Common"),
                Path.Combine(commonRoot, "tests", "UnityGameTranslator.Common.Checks", BaselineName), Exceptions, "the library", Regenerate);
        }
    }
}
