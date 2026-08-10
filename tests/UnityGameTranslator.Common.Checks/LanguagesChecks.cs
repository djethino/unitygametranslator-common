using System;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The language lookup, and above all the two facts about it that are easy to break.
    ///
    /// It carries TWO inventories that are not the same size, and every temptation to "tidy" this
    /// file is a temptation to collapse them. Codes exist to talk to the outside world; names exist
    /// because a model translates into languages that ISO 639-1 never gave a code to. Merging them
    /// either drops those languages or invents codes no API accepts.
    ///
    /// And several codes point at one name, which is correct for recognising input and wrong for
    /// building a list a human reads — that mistake once showed Simplified Chinese three times in
    /// a dropdown with no way to tell which to pick.
    /// </summary>
    internal static class LanguagesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // Codes in, names out.
            check(Languages.NameOf("fr") == "French", "fr -> French", "the ordinary case");
            check(Languages.NameOf("FR") == "French", "FR -> French", "a system locale can arrive upper-cased");
            check(Languages.NameOf("qqq") == "qqq", "an unknown code comes back as it was",
                "showing what we were given beats showing nothing");
            check(Languages.NameOf(null) == null && Languages.NameOf("") == "",
                "nothing in, nothing out", "no invented default hiding in a lookup");

            // Names in, codes out.
            check(Languages.CodeOf("French") == "fr", "French -> fr", "the reverse direction");
            check(Languages.CodeOf("  french  ") == "fr", "spacing and case do not matter",
                "these values come from files and APIs, not from us");
            check(Languages.CodeOf("fr") == "fr", "a code hands itself back", "callers pass either");
            check(Languages.CodeOf("Klingon") == null, "an unknown name gives null",
                "which callers read as 'no API can address this'");

            // ⚠ Several codes, one language. Recognise them all, offer it once.
            check(Languages.NameOf("zh") == "Simplified Chinese"
                  && Languages.NameOf("zh-cn") == "Simplified Chinese"
                  && Languages.NameOf("zh-hans") == "Simplified Chinese",
                "three codes recognised as one language", "whatever a system or an API hands over");
            check(Languages.CodeOf("Simplified Chinese") == "zh",
                "and the shortest is the canonical one", "it is what gets written into a game config");

            var all = Languages.All().ToList();
            check(all.Count(entry => entry.Name == "Simplified Chinese") == 1,
                "a picker offers it exactly once", "listing the raw table showed it three times");
            check(all.Count == all.Select(entry => entry.Name).Distinct().Count(),
                "and no language appears twice at all", "the same trap, for every other language");
            check(all.SequenceEqual(all.OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)),
                "sorted by name", "a picker is read by a person");

            // ⚠ The two inventories. This is the fact worth protecting.
            check(Languages.IsTranslatable("Cantonese"),
                "a language with no ISO code is still translatable",
                "five of them exist as names only; dropping them loses real choices");
            check(Languages.CodeOf("Cantonese") == null,
                "and it honestly has no code", "inventing one would produce a request no API accepts");
            check(Languages.Names().Length > all.Count,
                "so there are more translatable languages than codes",
                "the day these two are equal, something has been collapsed");
            check(Languages.Names().Contains("French") && Languages.IsTranslatable("fr"),
                "both inventories still answer for an ordinary language", "the split is not a divide");

            // Matching an API answer against what a player asked for.
            check(Languages.Matches("French", "fr"), "French answers to fr", "the ordinary case");
            check(Languages.Matches("Simplified Chinese", "zh-hans")
                  && Languages.Matches("Simplified Chinese", "zh-cn")
                  && Languages.Matches("Simplified Chinese", "zh"),
                "and every spelling of its code still matches", "the API picks its spelling, not us");
            check(Languages.Canonical("zh-hans") == "zh" && Languages.Canonical("zh-cn") == "zh",
                "because codes are canonicalised before comparing",
                "raw comparison answered 'different language' for a language against itself");
            check(Languages.Canonical("qqq") == "qqq" && Languages.Canonical(null) == null,
                "and an unknown code is left alone", "we do not invent a canonical form we do not have");
            check(!Languages.Matches("German", "fr"), "a different language does not", "obviously");
            check(!Languages.Matches("", "fr") && !Languages.Matches(null, "fr"),
                "and nothing matches nothing", "an empty answer is not a match");

            // Provider spellings. Not policy about languages — policy about two APIs.
            check(Languages.GoogleCode("Simplified Chinese") == "zh-CN", "Google wants zh-CN", "its spelling");
            check(Languages.DeepLCode("English", isTarget: true) == "EN-US",
                "DeepL wants EN-US as a target", "it refuses plain EN there");
            check(Languages.DeepLCode("English", isTarget: false) == "EN",
                "and plain EN as a source", "the same language, two codes, depending on the side");
            check(Languages.DeepLCode("French", isTarget: true) == "FR",
                "anything without a special case is upper-cased", "no table of exceptions to maintain");
            check(Languages.GoogleCode("Cantonese") == null && Languages.DeepLCode("Cantonese", true) == null,
                "a codeless language reaches no provider", "said plainly rather than sent and rejected");
        }
    }
}
