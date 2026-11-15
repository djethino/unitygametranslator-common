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
        /// <summary>
        /// The highest format this socle reads and writes — the mod's files, the Manager's reading, what the site keeps.
        ///
        /// 🔴 **2 since 2026-11-15** (user: « le mod reste compatible v1 mais passe à la v2 »). What it governs: what a client ANNOUNCES
        /// (<see cref="Header"/>) and what it refuses to load beyond it; never the format of a file — that is what the file CONTAINS
        /// (<see cref="OfContent"/>), so a file of the format 1 stays one until a field of the format 2 is written in it. Nowhere is it
        /// the format of a dictionary that declares none, nor what a game with no mod yet will read (<see cref="Unannounced"/>): the mod
        /// it will have is the one already published.
        /// </summary>
        public const int Current = 2;

        /// <summary>What a client that says nothing reads: every mod and Manager released before this rule.</summary>
        public const int Unannounced = 1;

        /// <summary>The request header a client states its format in.</summary>
        public const string Header = "X-UGT-Format";

        /// <summary>The same, as a query parameter — for a stream whose relay asks the site on its behalf.</summary>
        public const string Query = "format";

        /// <summary>
        /// The format a client announced: the header's (or the parameter's) value, or
        /// <see cref="Unannounced"/> when it is absent or not a whole number of at least 1.
        ///
        /// 🔴 **A protocol header is ASCII.** Only the space and the tab around the value are dropped (the optional white
        /// space of HTTP, RFC 9110), on every side — here, on the site (PHP <c>trim($value, " 	")</c>), anywhere a port
        /// reads it. Any other character, a no-break space or an ideographic one included, makes the value unreadable, and
        /// an unreadable value is silence. Never Unicode's White_Space: a « 2 » between no-break spaces was read 2 here
        /// and 1 by the site's <c>trim()</c>, two readings of one header.
        /// </summary>
        public static int Announced(string? value)
        {
            if (value == null) return Unannounced;
            int from = 0, to = value.Length;
            while (from < to && (value[from] == ' ' || value[from] == '	')) from++;
            while (to > from && (value[to - 1] == ' ' || value[to - 1] == '	')) to--;
            if (from == to) return Unannounced;
            return int.TryParse(value.Substring(from, to - from), System.Globalization.NumberStyles.None,
                                System.Globalization.CultureInfo.InvariantCulture, out int n) && n >= 1
                ? n
                : Unannounced;
        }

        /// <summary>A file's format from its <c>_engine_version</c>: absent, 0 or less read as 1.</summary>
        public static int OfFile(int? engineVersion) => engineVersion.HasValue && engineVersion.Value > 1 ? engineVersion.Value : 1;

        /// <summary>
        /// The format a file must be STAMPED with (<c>_engine_version</c>): that of what it CONTAINS, never
        /// the format the writer happens to speak. 2 only when the file holds something of the format 2 —
        /// a game's list section (<c>_lists</c>) or a line with a field of it (term, forms, use, class,
        /// oracle, links, unlinked) — else 1.
        ///
        /// 🔴 **Why not <see cref="Current"/>.** A writer that stamps its own format on every save would
        /// mark every format 1 file as 2 the day <see cref="Current"/> moves, and close it to every older
        /// mod for nothing. The marker alone is no evidence: <c>[!g*0]</c> in a file with no link is the
        /// game's own text. A file that loses its last field of the format 2 is a format 1 file again.
        /// </summary>
        public static int OfContent(bool holdsGameLists, System.Collections.Generic.IEnumerable<TranslationLine> lines)
        {
            if (holdsGameLists) return 2;
            if (lines != null)
                foreach (var line in lines)
                    if (line.HasFormat2Fields) return 2;
            return 1;
        }

        /// <summary>Whether a client reading up to <paramref name="readsUpTo"/> can use a file of <paramref name="fileFormat"/>.</summary>
        public static bool Reads(int readsUpTo, int fileFormat) => fileFormat <= readsUpTo;

        /// <summary>
        /// The highest format a SERVER keeps — the other direction of <see cref="Announced"/>: what it said (the field <c>format</c> of the
        /// answer that precedes every upload), or the format 1 when it said nothing or something unreadable.
        ///
        /// 🔴 **A silent server keeps the format 1**, as a silent client reads it: it is the answer of every site released before the field,
        /// and a file of the format 2 sent to one of those would be stored by code that does not know its fields. Never a guess from the
        /// address the mod talks to.
        /// </summary>
        public static int ServerKeeps(int? said) => said.HasValue && said.Value >= 1 ? said.Value : 1;

        /// <summary>
        /// Whether a file of <paramref name="fileFormat"/> may be SENT to a server that said <paramref name="serverKeeps"/> (null: nothing).
        /// A file of the format 1 always may; one of the format 2 only to a server that says it keeps it. Asked BEFORE the click, so that
        /// no send is attempted that the server could only get wrong.
        /// </summary>
        public static bool CanSend(int fileFormat, int? serverKeeps) => Reads(ServerKeeps(serverKeeps), fileFormat);

        /// <summary>
        /// Said where a translation cannot be used by the client looking at it — the chip's tip, the
        /// greyed Download, a local file refused. No version number: the one thing to do is update.
        /// </summary>
        public const string UpdateTheMod = "Made for a newer UGT Mod. Update UGT Mod to use it.";

        /// <summary>
        /// The other direction of <see cref="UpdateTheMod"/>: said to the person who takes a file of a format beyond the format 1 (a download,
        /// a merge, a first term in a file of the format 1), for whoever else will use it. The fact and its consequence, and no number of the
        /// mod: a mod has its own version, and the format is the file's. See <c>spec/wording.json</c>, <c>format.older_mods</c>.
        /// </summary>
        public static string OlderModsCannotRead(int fileFormat) =>
            "This translation uses format " + fileFormat.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + ". An older UGT Mod cannot read it.";
    }
}
