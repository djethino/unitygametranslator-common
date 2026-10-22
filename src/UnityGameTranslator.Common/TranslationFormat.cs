namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Which format of translation file a client reads, and what it does with one it does not.
    ///
    /// 🔴 **No table of versions, on purpose** (user, 2026-10-22). A table « format N → first mod that
    /// reads it » would have to be kept by hand at every change, here and in the site. It is not
    /// needed, because the past has a single answer: every client released before this rule reads
    /// the format 1 and says nothing — <see cref="Unannounced"/>. From this rule on, each client
    /// SAYS what it reads (<see cref="Header"/>, or <see cref="Query"/> on a stream), and the site
    /// answers accordingly: a translation it cannot read is not listed for it, is not announced as
    /// an update, and is not downloaded.
    ///
    /// ⚠ The format is the file's <c>_engine_version</c> (spec/translation-file/schema.json): the
    /// form its placeholders are written in. 0 — the old <c>[vN]</c> — is migrated by every reader
    /// on load and counts as 1 (<see cref="OfFile"/>).
    /// </summary>
    public static class TranslationFormat
    {
        /// <summary>The format this socle reads and writes — the mod's files, the Manager's reading.</summary>
        public const int Current = 1;

        /// <summary>What a client that says nothing reads: every mod and Manager released before this rule.</summary>
        public const int Unannounced = 1;

        /// <summary>The request header a client states its format in.</summary>
        public const string Header = "X-UGT-Format";

        /// <summary>The same, as a query parameter — for a stream whose relay asks the site on its behalf.</summary>
        public const string Query = "format";

        /// <summary>
        /// The format a client announced: the header's (or the parameter's) value, or
        /// <see cref="Unannounced"/> when it is absent or not a whole number of at least 1.
        /// </summary>
        public static int Announced(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Unannounced;
            return int.TryParse(value!.Trim(), System.Globalization.NumberStyles.None,
                                System.Globalization.CultureInfo.InvariantCulture, out int n) && n >= 1
                ? n
                : Unannounced;
        }

        /// <summary>A file's format from its <c>_engine_version</c>: absent, 0 or less read as 1.</summary>
        public static int OfFile(int? engineVersion) => engineVersion.HasValue && engineVersion.Value > 1 ? engineVersion.Value : 1;

        /// <summary>Whether a client reading up to <paramref name="readsUpTo"/> can use a file of <paramref name="fileFormat"/>.</summary>
        public static bool Reads(int readsUpTo, int fileFormat) => fileFormat <= readsUpTo;

        /// <summary>
        /// Said where a translation cannot be used by the client looking at it — the chip's tip, the
        /// greyed Download, a local file refused. No version number: the one thing to do is update.
        /// </summary>
        public const string UpdateTheMod = "Made for a newer UGT Mod. Update UGT Mod to use it.";
    }
}
