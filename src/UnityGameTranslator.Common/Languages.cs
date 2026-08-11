using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What language a code means, and what code a language answers to.
    ///
    /// ⚠ A LANGUAGE IS IDENTIFIED BY ITS NAME, not by its code. The website stores a name, its
    /// upload endpoint validates against a list of names, the mod resolves to a name and the game
    /// config holds a name. Codes are a means — a system locale, a Google or DeepL request, a
    /// setting — never the identity.
    ///
    /// ⚠ TWO INVENTORIES, deliberately not the same size:
    ///  · <see cref="Table"/> maps ISO 639-1 codes to names. It is what talks to anything that
    ///    speaks in codes.
    ///  · <see cref="TranslatableNames"/> is the CONTRACT with the website — exactly the list in
    ///    its config/languages.php. It is wider because five of those names have no ISO 639-1 code
    ///    at all.
    /// Drift in that second list does not degrade a translation: it gets the upload REJECTED by a
    /// validation error the player did not cause and cannot read. It moves only together with the
    /// website. Collapsing the two would either drop five languages the site accepts, or invent
    /// codes no API accepts.
    ///
    /// ⚠ Neither list says what any model can translate. Models are chosen from a catalogue and
    /// most will attempt any pair, with varying success; naming one here would read as a limit
    /// that does not exist.
    ///
    /// ⚠ SEVERAL CODES MAP TO ONE NAME on purpose: "zh", "zh-cn" and "zh-hans" all mean Simplified
    /// Chinese, because this has to recognise whatever a system or an API hands over. That gives
    /// the table two jobs, and each has its own entry point:
    ///  · offering a choice to a human goes through <see cref="All"/>, one entry per language —
    ///    reading the table directly put Simplified Chinese in a dropdown three times;
    ///  · comparing two codes goes through <see cref="Canonical"/> — compared raw, a language
    ///    failed to match itself when the two sides spelled its code differently.
    /// The shortest code wins, and it is the one the website uses.
    ///
    /// No language is special-cased as a policy: this is a lookup. The exceptions below exist
    /// because Google and DeepL ask for particular spellings, not because any language is treated
    /// differently from another.
    /// </summary>
    public static partial class Languages
    {
        /// <summary>
        /// Code to language name. Case-insensitive: a locale can arrive as "FR".
        ///
        /// Built from the catalogue rather than written here — see Languages.Tables.g.cs and
        /// generate-common-languages.py. Every code a language answers to is in it, so the same
        /// language is reachable as "zh", "zh-cn" and "zh-hans".
        /// </summary>
        // ⚠ Built on first use, not at type initialisation. The data lives in the generated
        // partial, and C# does not promise which partial's static fields run first — building
        // eagerly threw a null reference that looked like a corrupt catalogue.
        private static Dictionary<string, string>? _table;
        private static Dictionary<string, string> Table => _table ??= BuildTable();

        private static Dictionary<string, string> BuildTable()
        {
            var table = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string[] row in CatalogueCodes)
            {
                for (int i = 1; i < row.Length; i++) table[row[i]] = row[0];
            }

            return table;
        }

        /// <summary>
        /// Every language a translation can target: the website's list, name for name.
        ///
        /// ⚠ Case-sensitive, and the spelling is not ours to choose — it is what the upload
        /// endpoint compares against.
        /// </summary>
        private static HashSet<string>? _names;
        private static HashSet<string> TranslatableNames => _names ??= BuildNames();

        private static HashSet<string> BuildNames()
        {
            var names = new HashSet<string>();
            foreach (string[] row in CatalogueCodes) names.Add(row[0]);
            return names;
        }

        /// <summary>
        /// Name to code. Built once, keeping the SHORTEST code for each name — "zh" over "zh-cn" —
        /// because that is the form written into config files on both sides.
        /// </summary>
        private static Dictionary<string, string>? _reverse;
        private static Dictionary<string, string> Reverse => _reverse ??= BuildReverse();

        private static Dictionary<string, string> BuildReverse()
        {
            var reverse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in Table)
            {
                string? existing;
                if (!reverse.TryGetValue(entry.Value, out existing) || entry.Key.Length < existing.Length)
                    reverse[entry.Value] = entry.Key;
            }

            return reverse;
        }

        /// <summary>
        /// "fr" -> "French". Anything unknown comes back untouched, never null: a code we do not
        /// carry is far more likely to be a language nobody listed than an error, and showing it
        /// beats showing nothing.
        /// </summary>
        public static string? NameOf(string? code)
        {
            // Tested field by field rather than with IsNullOrEmpty: on this framework floor that
            // helper carries no nullability annotation, so the compiler cannot see the narrowing.
            if (code == null || code.Length == 0) return code;

            string? name;
            return Table.TryGetValue(code, out name) ? name : code;
        }

        /// <summary>
        /// "French" -> "fr", and a code hands itself back in lower case. Null when we know neither,
        /// which callers read as "cannot address this language through an API".
        /// </summary>
        public static string? CodeOf(string? nameOrCode)
        {
            if (nameOrCode == null || nameOrCode.Length == 0) return null;

            string trimmed = nameOrCode.Trim();

            string? code;
            if (Reverse.TryGetValue(trimmed, out code)) return code;

            return Table.ContainsKey(trimmed) ? trimmed.ToLowerInvariant() : null;
        }

        /// <summary>
        /// The one code that stands for a language: "zh-cn" and "zh-hans" both come back "zh".
        /// A code we do not carry comes back untouched.
        ///
        /// ⚠ Anything COMPARING two codes has to go through this. The table lists several codes
        /// per language so it can recognise whatever arrives, which means two values can name the
        /// same language and differ as strings — comparing them raw answers "different language"
        /// for a language against itself.
        /// </summary>
        public static string? Canonical(string? code)
        {
            if (code == null || code.Length == 0) return code;

            return CodeOf(NameOf(code)) ?? code;
        }

        /// <summary>
        /// True when an answer such as "French" is the language someone asked for as "fr".
        ///
        /// Both sides are canonicalised first: a player whose setting reads "zh-hans" and a
        /// translation published as Simplified Chinese are the same language, and they used to
        /// miss each other — the list simply came back empty, which reads as "nobody has
        /// translated this game" rather than as a spelling mismatch.
        ///
        /// Falls back to comparing names when the value is not one we carry, so an unknown
        /// language still matches itself instead of silently matching nothing.
        /// </summary>
        public static bool Matches(string? languageName, string? isoCode)
        {
            if (languageName == null || languageName.Trim().Length == 0) return false;

            string? code = CodeOf(languageName);
            if (code != null)
                return string.Equals(Canonical(code), Canonical(isoCode), StringComparison.OrdinalIgnoreCase);

            return string.Equals(languageName.Trim(), NameOf(isoCode), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// What language an operating system or a browser is asking for.
        ///
        /// ⚠ Locales arrive longer than a language code, and in shapes that differ per system:
        /// "zh-Hant-TW" on Windows, "fr_FR.UTF-8" on Linux, "pt-BR" in a browser header. Cutting
        /// them to two letters is what everything here used to do, and it is wrong in exactly one
        /// visible way: "zh-Hant-TW" becomes "zh", which is SIMPLIFIED Chinese. Someone whose
        /// system is set to Traditional was being offered Simplified, in the mod and in the tool
        /// alike, while the table right here holds "zh-hant" and could have answered.
        ///
        /// So the tag is shortened one segment at a time and the first form we recognise wins —
        /// "zh-hant-tw", then "zh-hant", then "zh". That is the lookup rule BCP 47 itself
        /// describes, and it costs nothing over cutting blindly.
        ///
        /// Returns the canonical code, or null when nothing is recognised. Null is not a failure
        /// to report: an unknown locale simply means the caller keeps its own default.
        /// </summary>
        public static string? FromLocale(string? locale)
        {
            if (locale == null) return null;

            // Linux writes fr_FR.UTF-8 and can list several; a browser sends fr-FR;q=0.9.
            string tag = locale.Trim().ToLowerInvariant();
            foreach (char cut in new[] { '.', ':', '@', ';', ',' })
            {
                int at = tag.IndexOf(cut);
                if (at >= 0) tag = tag.Substring(0, at);
            }

            tag = tag.Replace('_', '-').Trim();
            if (tag.Length == 0) return null;

            while (true)
            {
                if (Knows(tag)) return Canonical(tag);

                int dash = tag.LastIndexOf('-');
                if (dash <= 0) return null;

                tag = tag.Substring(0, dash);
            }
        }

        /// <summary>
        /// True when this exact code is one we carry.
        ///
        /// Distinct from <see cref="NameOf"/>, which hands an unknown code back rather than
        /// failing: detecting a system locale needs to know whether the match happened, because
        /// "fr-FR" has to fall through to "fr" instead of being taken as a language of its own.
        /// </summary>
        public static bool Knows(string? code) => code != null && code.Length > 0 && Table.ContainsKey(code);

        /// <summary>True when this is a language a translation can target.</summary>
        public static bool IsTranslatable(string? language)
        {
            if (language == null || language.Length == 0) return false;

            return TranslatableNames.Contains(language) || Table.ContainsKey(language);
        }

        /// <summary>Every translatable language name, sorted. Includes the five with no code.</summary>
        public static string[] Names()
        {
            var list = new List<string>(TranslatableNames);
            list.Sort(StringComparer.Ordinal);
            return list.ToArray();
        }

        /// <summary>
        /// One entry per language for a picker: the code to write down, and the name to show.
        ///
        /// ⚠ Only languages that HAVE a code. The five without one are translatable but cannot be
        /// stored as a code, so a caller offering them has to decide what it writes — see Names().
        /// </summary>
        public static IEnumerable<(string Code, string Name)> All()
        {
            return Table
                .GroupBy(entry => entry.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => (
                    Code: group.OrderBy(entry => entry.Key.Length)
                               .ThenBy(entry => entry.Key, StringComparer.Ordinal)
                               .First().Key,
                    Name: group.Key))
                .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// What each provider accepts, from the catalogue. Null when it does not do this language.
        ///
        /// ⚠ Read, never deduced. These used to be worked out by rule — take the ISO code, upper
        /// case it for DeepL, send it and hope — so the only refusal anybody could predict was a
        /// language with no code at all. Everything else went out and came back 400, leaving a
        /// line untranslated with nothing but a log entry, once per line, forever.
        /// </summary>
        private static string? Provider(string? languageName, int column)
        {
            if (languageName == null) return null;

            foreach (string[] row in CatalogueProviders)
            {
                if (string.Equals(row[0], languageName, StringComparison.OrdinalIgnoreCase))
                    return row[column];
            }

            return null;
        }

        /// <summary>The code Google Translate expects, or null when it does not do this language.</summary>
        public static string? GoogleCode(string? languageName) => Provider(languageName, 1);

        /// <summary>
        /// The code DeepL expects, or null when it does not do this language.
        ///
        /// ⚠ Source and target differ, and sending the wrong one is refused: the same language is
        /// "EN" going in and "EN-US" coming out.
        /// </summary>
        public static string? DeepLCode(string? languageName, bool isTarget) =>
            Provider(languageName, isTarget ? 3 : 2);
    }
}
