using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The mod's own interface, as a thing that lives beside a translation without being part of it.
    ///
    /// 🔴 **It has its own file now, and that is the whole point.** Interface lines used to travel
    /// inside `translations.json` under the tag <see cref="Tag"/>, carried by every count, hash,
    /// merge and upload that file goes through. Three consequences, all of them real:
    ///
    ///  · a translation published with the option on shipped the mod's menus to everyone who
    ///    downloaded it, and the identity of the file (`file_hash`) counted them;
    ///  · a downloaded translation written by a stranger both TURNED ON the interface translation
    ///    and supplied its words — someone else choosing what "Apply" and "Keep mine" say, on a mod
    ///    that handles tokens, uploads and file replacement. Not code execution: deception;
    ///  · every "unpublished changes" verdict counted menu labels as work waiting to be shared.
    ///
    /// The separate file cuts all three at once, and it only stays cut as long as **nothing ever
    /// imports an interface line from the network**.
    ///
    /// ⚠ **In the socle because two programs must spell it the same way.** The mod writes the file;
    /// the manager lists it when somebody removes their data, and a file it does not recognise is
    /// offered under "judge them yourself" beside a consequence written for stray files.
    ///
    /// ⚠ **No sharing, deliberately, for now.** This file is never uploaded and never downloaded.
    /// Copying it from one game to another by hand is the intended way to reuse the work, which is
    /// why it carries the language it was written in and the font it needs.
    /// </summary>
    public static class ModUi
    {
        /// <summary>
        /// The tag an interface line carries. Written once here so the letter has one spelling —
        /// see <see cref="Merge.IsGameLine"/>, which is the rule that keeps it out of everything.
        /// </summary>
        public const string Tag = "M";

        /// <summary>
        /// The file, beside `translations.json` in the mod's data folder for one game.
        ///
        /// ⚠ Its name is a contract with the manager's inventory, not a local choice.
        /// </summary>
        public const string FileName = "modui-translate.json";

        private const string Prefix = "modui-translate.";
        private const string Suffix = ".json";

        /// <summary>
        /// Where the file goes when the game's target language changes under it.
        ///
        /// 🔴 **Set aside rather than overwritten.** An interface translated into one language is
        /// meaningless in another, and deleting it would throw away a pass of the translator for
        /// somebody who is only trying French for an evening. Coming back to that language finds
        /// the file again, because the name is derived and not remembered.
        ///
        /// ⚠ The name is a HINT and never the authority: the file carries its own
        /// `_target_language`, which is what a reader trusts. That matters for the handful of
        /// languages the catalogue holds with no code at all — their name is sanitised into
        /// something a filesystem accepts, which no longer round-trips.
        /// </summary>
        /// <param name="language">A language name or an ISO code, as the config holds it.</param>
        public static string SetAsideFileName(string? language) =>
            Prefix + Slug(language) + Suffix;

        /// <summary>
        /// Whether a file name in the mod's data folder is one of ours — the file itself or any
        /// language set aside beside it.
        ///
        /// ⚠ Asked rather than re-derived: the manager groups what it finds on disk by name, and a
        /// second spelling of "is this the interface file" would eventually disagree with this one.
        /// </summary>
        public static bool IsOurs(string? fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return false;

            // Ordinal, not culture-aware: a file name is bytes, and the Turkish "I" has already
            // cost this project a comparison elsewhere.
            if (string.Equals(fileName, FileName, System.StringComparison.OrdinalIgnoreCase))
                return true;

            return fileName!.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase)
                   && fileName.EndsWith(Suffix, System.StringComparison.OrdinalIgnoreCase)
                   && fileName.Length > Prefix.Length + Suffix.Length;
        }

        /// <summary>
        /// A language reduced to something a filesystem accepts: its ISO code when the catalogue
        /// knows one, otherwise its name with everything but letters and digits folded to a dash.
        /// </summary>
        private static string Slug(string? language)
        {
            string? code = Languages.CodeOf(language);
            string source = !string.IsNullOrEmpty(code) ? code! : (language ?? string.Empty);

            var slug = new StringBuilder(source.Length);
            foreach (char c in source)
            {
                if (c >= 'a' && c <= 'z') slug.Append(c);
                else if (c >= 'A' && c <= 'Z') slug.Append((char)(c + 32));
                else if (c >= '0' && c <= '9') slug.Append(c);
                else if (slug.Length > 0 && slug[slug.Length - 1] != '-') slug.Append('-');
            }

            while (slug.Length > 0 && slug[slug.Length - 1] == '-')
                slug.Length--;

            // "unknown" rather than an empty segment: `modui-translate..json` names nothing, and a
            // language written entirely in a script this folds away is exactly when one is needed.
            return slug.Length > 0 ? slug.ToString() : "unknown";
        }
    }
}
