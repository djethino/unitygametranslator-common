using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Which script a text's letters are written in, against the script a language is written in.
    ///
    /// 🔴 **A shortcut, never a condition** (user, 2026-10-07): two languages that share a script —
    /// French and English, both Latin — cannot be told apart here, and nothing may depend on this
    /// answering. What it CAN say, it says with certainty: a line whose letters are all of other
    /// scripts than a language's is not in that language.
    ///
    /// 🔴 Every fact comes from Unicode's data, generated into <c>Writing.Tables.g.cs</c> by the mod's
    /// shaping-table generator in the same run, from the same UCD (one Unicode for both products);
    /// a language's script from the catalogue (<see cref="Languages.ScriptOf"/>). No range and no
    /// script is named in this code — the rule the mod's CharacterRangeChecks holds it to.
    /// </summary>
    public static partial class Writing
    {
        /// <summary>
        /// The ISO 15924 code of the script this code point is a letter of ("Latn", "Hani", "Arab"),
        /// or null when it is not a letter of a real script (digits, punctuation, symbols, marks,
        /// and letters Unicode files under Common or Inherited).
        /// </summary>
        public static string? ScriptOfLetter(int codePoint)
        {
            int lo = 0, hi = LetterScripts.Length / 3 - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                int first = LetterScripts[mid * 3], last = LetterScripts[mid * 3 + 1];
                if (codePoint < first) hi = mid - 1;
                else if (codePoint > last) lo = mid + 1;
                else return ScriptCodes[LetterScripts[mid * 3 + 2]];
            }
            return null;
        }

        /// <summary>
        /// The Unicode scripts a catalogue script code stands for: the code itself, or the scripts
        /// ISO 15924 defines a combined code as ("Jpan" → Han, Hiragana, Katakana). Empty for a code
        /// Unicode does not know — then nothing can be said by script.
        /// </summary>
        public static IReadOnlyCollection<string> ScriptsOf(string? isoCode)
        {
            if (string.IsNullOrEmpty(isoCode)) return Array.Empty<string>();

            foreach (string[] row in CombinedScripts)
            {
                if (string.Equals(row[0], isoCode, StringComparison.OrdinalIgnoreCase))
                {
                    var parts = new string[row.Length - 1];
                    Array.Copy(row, 1, parts, 0, parts.Length);
                    return parts;
                }
            }

            foreach (string code in ScriptCodes)
            {
                if (string.Equals(code, isoCode, StringComparison.OrdinalIgnoreCase))
                    return new[] { code };
            }
            return Array.Empty<string>();
        }

        /// <summary>
        /// True only when the text HAS letters and NONE of them is written in one of
        /// <paramref name="scripts"/> — the one case where a text is certainly not in a language
        /// written in those scripts. A text without letters, or with a single letter of those
        /// scripts among others ("Talk to 李逍遥" for Latin), answers false: that is not knowable here.
        ///
        /// ⚠ Markup and the mod's placeholders are read past (<see cref="Markup.Strip"/>,
        /// <see cref="Placeholders.Tokens"/>): "&lt;color=red&gt;" and "[!STR*0]" are Latin letters
        /// that belong to no language, and would hide every Chinese line wrapped in a colour.
        /// </summary>
        public static bool NoLetterIn(string? text, IReadOnlyCollection<string> scripts)
        {
            if (string.IsNullOrEmpty(text) || scripts.Count == 0) return false;

            string bare = Markup.Strip(text!);
            foreach (string token in Placeholders.Tokens(bare))
                bare = bare.Replace(token, " ");

            bool anyLetter = false;
            for (int i = 0; i < bare.Length; i++)
            {
                int cp = bare[i];
                if (char.IsHighSurrogate(bare[i]) && i + 1 < bare.Length && char.IsLowSurrogate(bare[i + 1]))
                {
                    cp = char.ConvertToUtf32(bare[i], bare[i + 1]);
                    i++;
                }

                string? script = ScriptOfLetter(cp);
                if (script == null) continue;
                anyLetter = true;

                foreach (string wanted in scripts)
                {
                    if (string.Equals(wanted, script, StringComparison.Ordinal)) return false;
                }
            }
            return anyLetter;
        }

        /// <summary>
        /// Strict source, before any backend is asked: true when this line is certainly not in
        /// <paramref name="sourceLanguage"/> (a catalogue name or code, "English" or "en") because none of its letters
        /// is in that language's script — the line is then declined without asking anyone, as the
        /// model would be told to (analyse/strict-source-ecriture-unicode.md).
        ///
        /// False whenever it cannot be known: no source language, a language the catalogue gives no
        /// script Unicode knows, a line with no letter, a line with one letter of the source's script.
        /// The model decides those, as before.
        ///
        /// ⚠ Called by the game before asking, NEVER inside <see cref="LineTranslation.AskModel"/>:
        /// the Manager's bench shares that path and must still put every such line to the model — it
        /// measures what the model does when nothing could be pruned before (user, 2026-10-08).
        /// </summary>
        public static bool OutsideSourceScript(string? text, string? sourceLanguage)
        {
            if (string.IsNullOrEmpty(sourceLanguage)) return false;
            // A name or a code: both are stored across this project, and NameOf hands a name back.
            return NoLetterIn(text, ScriptsOf(Languages.ScriptOf(Languages.NameOf(sourceLanguage))));
        }
    }
}
