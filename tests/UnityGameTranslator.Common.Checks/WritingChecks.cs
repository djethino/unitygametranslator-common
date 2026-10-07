using System;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Strict source, decided by script before any backend is asked (Writing.OutsideSourceScript,
    /// analyse/strict-source-ecriture-unicode.md).
    ///
    /// 🔴 The one property every case defends: it may only say "not the source language" when that
    /// is CERTAIN — the line has letters and none is of the source's script. Everything else goes to
    /// the model as before. A wrong "declined" is a line of the game left untranslated for good.
    /// </summary>
    internal static class WritingChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── What a letter is written in ───────────────────────────────
            check(Writing.ScriptOfLetter('a') == "Latn" && Writing.ScriptOfLetter('语') == "Hani"
                  && Writing.ScriptOfLetter('ж') == "Cyrl" && Writing.ScriptOfLetter('か') == "Hira",
                "a letter's script is read from Unicode", "ISO 15924 codes, the catalogue's own vocabulary");
            check(Writing.ScriptOfLetter('7') == null && Writing.ScriptOfLetter('!') == null
                  && Writing.ScriptOfLetter(' ') == null,
                "digits, punctuation and spaces are no letter", "they say nothing about a language");
            check(Writing.ScriptOfLetter(0x20000) == "Hani",
                "a letter beyond the first plane is read whole", "a surrogate pair is one character");

            // ── What a catalogue code stands for ──────────────────────────
            check(Writing.ScriptsOf("Latn").SequenceEqual(new[] { "Latn" }),
                "a plain code stands for itself", "Latin is Latin");
            check(Writing.ScriptsOf("Jpan").OrderBy(s => s).SequenceEqual(new[] { "Hani", "Hira", "Kana" })
                  && Writing.ScriptsOf("Kore").OrderBy(s => s).SequenceEqual(new[] { "Hang", "Hani" })
                  && Writing.ScriptsOf("Hans").SequenceEqual(new[] { "Hani" })
                  && Writing.ScriptsOf("Hant").SequenceEqual(new[] { "Hani" }),
                "a combined code stands for the scripts ISO 15924 defines it as",
                "user's decision, 2026-10-08: Japanese is Han, Hiragana and Katakana");
            check(Writing.ScriptsOf("Qaaa").Count == 0 && Writing.ScriptsOf(null).Count == 0,
                "a code Unicode does not know stands for nothing", "then nothing is decided by script");

            // Every language of the catalogue can be asked about — none falls out of the filter by a
            // code the tables do not carry.
            var unknown = Languages.All()
                .Select(l => Languages.ScriptOf(l.Name))
                .Where(s => s != null && Writing.ScriptsOf(s).Count == 0)
                .Distinct().ToList();
            check(unknown.Count == 0,
                "every script of the catalogue is known to the tables",
                unknown.Count == 0 ? "a language whose script is unknown is never filtered" : "unknown: " + string.Join(", ", unknown));

            // ── Strict source: certain, or the model's ────────────────────
            check(Writing.OutsideSourceScript("获得金币", "English"),
                "a Chinese line in an English game is declined without asking",
                "the case the model got wrong 302 times in one game");
            check(Writing.OutsideSourceScript("获得金币", "en"),
                "the source language may be stored as a code", "both forms are stored across this project");
            check(Writing.OutsideSourceScript("<color=#FF0000>获得</color>[!v*0]金币", "English"),
                "markup and placeholders are not letters of the line",
                "<color> and [!v*0] would otherwise hide every coloured Chinese line");
            check(!Writing.OutsideSourceScript("获得 HP 10", "English"),
                "one letter of the source's script sends the line to the model",
                "user's rule, 2026-10-08: no Latin letter at all, never \"mostly not Latin\"");
            check(!Writing.OutsideSourceScript("Defeat 深渊之王·暗影吞噬者", "English"),
                "an English sentence holding a long Chinese name is not declined",
                "the case a majority rule would get wrong");
            check(!Writing.OutsideSourceScript("Bonjour", "English"),
                "two languages of one script are the model's to tell apart", "Unicode cannot");
            check(!Writing.OutsideSourceScript("100 / 250", "English") && !Writing.OutsideSourceScript("", "English"),
                "a line with no letter goes to the model", "user's decision, 2026-10-08");
            check(!Writing.OutsideSourceScript("获得金币", null) && !Writing.OutsideSourceScript("获得金币", "Klingon"),
                "no source language, or one without a known script, decides nothing", "the model, as before");

            // Combined scripts, both ways.
            check(Writing.OutsideSourceScript("Level up", "Japanese") && Writing.OutsideSourceScript("레벨 업", "Japanese"),
                "a Latin or Hangul line in a Japanese game is declined", "neither is Han, Hiragana or Katakana");
            check(!Writing.OutsideSourceScript("レベルアップ", "Japanese") && !Writing.OutsideSourceScript("経験値", "Japanese"),
                "Katakana or kanji alone is Japanese enough to be asked", "Jpan includes both");
            check(!Writing.OutsideSourceScript("经验值", "Traditional Chinese"),
                "simplified Han in a traditional Chinese game is the model's", "one Unicode script for both");
            check(Writing.OutsideSourceScript("Привет", "Korean") && !Writing.OutsideSourceScript("레벨", "Korean"),
                "Korean is Hangul and Han", "Cyrillic is neither");
        }
    }
}
