using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The mod's interface file, checked against what depends on its name.
    ///
    /// ⚠ The stake is not cosmetic. The mod writes this file; the manager decides from its NAME
    /// whether to list it as recognised data or as a stray file somebody should judge for
    /// themselves. Two spellings and one of them offers a translated interface for deletion under
    /// a warning written for clutter.
    ///
    /// ⚠ And the set-aside name is derived, never remembered: coming back to a language must find
    /// the file that was put away for it. A name that does not reproduce is a pass of the
    /// translator thrown away silently.
    /// </summary>
    internal static class ModUiChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── The file itself ───────────────────────────────────────────
            check(ModUi.FileName == "modui-translate.json",
                "the file is modui-translate.json",
                "the manager groups the mod's data by name; a rename orphans it into 'other files'");

            check(ModUi.Tag == "M" && !Merge.IsGameLine(ModUi.Tag),
                "the tag is M, and M is not a line of the game",
                "one spelling of the letter, and the rule that keeps it out of every count reads it");

            check(ModUi.IsOurs(ModUi.FileName),
                "the file recognises itself",
                "the reader and the writer must agree without a second list of names");

            // ── Set aside by language ─────────────────────────────────────
            check(ModUi.SetAsideFileName("French") == "modui-translate.fr.json",
                "a name becomes its code",
                "the config holds names; the file holds something a filesystem accepts everywhere");

            check(ModUi.SetAsideFileName("fr") == ModUi.SetAsideFileName("French"),
                "a code and its name set aside the same file",
                "the config may hold either, and both must find the work put away for that language");

            check(ModUi.SetAsideFileName("French") != ModUi.SetAsideFileName("German"),
                "two languages never share one file",
                "an interface translated into one language is meaningless in another");

            check(ModUi.IsOurs(ModUi.SetAsideFileName("French")),
                "a set-aside file is still ours",
                "filed as unrecognised it sits under 'judge them yourself' — the exact trap this closes");

            check(ModUi.SetAsideFileName("French") != ModUi.FileName,
                "setting aside never overwrites the file in use",
                "that is the whole difference between putting work away and destroying it");

            // ── Names a filesystem would refuse ───────────────────────────
            // The catalogue holds a handful of translatable languages with no ISO code at all, so
            // the name itself is what gets folded. It must still produce ONE segment, and never an
            // empty one: `modui-translate..json` names nothing.
            check(ModUi.SetAsideFileName("Simplified Chinese").IndexOf(' ') < 0
                  && ModUi.SetAsideFileName("Simplified Chinese").EndsWith(".json", StringComparison.Ordinal),
                "a multi-word name folds to one segment",
                "a space in a path is where quoting bugs live, on every platform");

            check(ModUi.SetAsideFileName("中文") == "modui-translate.zh.json"
                  || ModUi.SetAsideFileName("中文").Length > "modui-translate..json".Length,
                "a name in another script still yields a segment",
                "folding it away entirely would leave modui-translate..json, which names nothing");

            check(ModUi.SetAsideFileName(null) == ModUi.SetAsideFileName(""),
                "no language and an empty language are the same file",
                "both mean 'we do not know', and two files for one unknown is one of them lost");

            check(ModUi.SetAsideFileName(null).Length > "modui-translate..json".Length,
                "an unknown language still names something",
                "the file is set aside before anything can ask what it holds");

            // ── What is NOT ours ──────────────────────────────────────────
            check(!ModUi.IsOurs("translations.json")
                  && !ModUi.IsOurs("translations.json.ancestor")
                  && !ModUi.IsOurs("config.json"),
                "the game's files are not ours",
                "the two must never be grouped together: one is the mod, the other is the work");

            check(!ModUi.IsOurs(null) && !ModUi.IsOurs("") && !ModUi.IsOurs("modui-translate..json"),
                "nothing, and a nameless language, are refused",
                "a scan of somebody's folder must not claim a file on a prefix alone");
        }
    }
}
