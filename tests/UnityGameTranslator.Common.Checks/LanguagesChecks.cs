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
            // ── Settled, or still deferred ────────────────────────────────
            // 🔴 A translation has a source and a target; they settle at its first line and are
            // frozen once published. "auto" is what a product writes when the answer is deferred —
            // detect it, or follow the system — and reading it as a language is how a mode came to
            // outrank what a server stated.
            check(Languages.IsSettled("French") && Languages.IsSettled("fr"),
                "a name and a code are answers", "both forms are stored across this project");
            check(!Languages.IsSettled(Languages.Undecided) && !Languages.IsSettled("AUTO"),
                "'auto' is not, in any case", "it is a mode, and it must never travel as a language");
            check(!Languages.IsSettled("") && !Languages.IsSettled(null) && !Languages.IsSettled("   "),
                "nor empty, absent or blank", "absent means 'nobody has said', never 'no language'");
            check(Languages.IsSettled("Klingon"),
                "a language the catalogue never heard of still counts",
                "refusing it would reclassify somebody's translation as undecided");

            // ── Disagreement, which is what anything may act on ───────────
            check(Languages.Disagree("Thai", "French"), "two languages disagree", "the acting case");
            check(!Languages.Disagree("French", "fr") && !Languages.Disagree("fr", "French"),
                "a name and its code do not",
                "compared as text, a language disagrees with itself — and blocks its own author");
            check(!Languages.Disagree(Languages.Undecided, "French")
                  && !Languages.Disagree("French", Languages.Undecided)
                  && !Languages.Disagree(null, "French") && !Languages.Disagree("French", null),
                "unsettled on either side is never a disagreement",
                "🔴 a source left at 'auto' is the commonest state there is — repair it, never refuse it");
            check(!Languages.Disagree(null, null),
                "and two silences agree", "nothing has been claimed, so nothing can be contradicted");

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
            check(Languages.CodeOf("Cantonese") == "yue",
                "and it does have a BCP 47 tag", "checked in the IANA registry, not written from memory");
            check(Languages.Names().Length == all.Count,
                "every translatable language can be offered", "each one has a tag to store");

            check(Languages.Names().Contains("French") && Languages.IsTranslatable("fr"),
                "both inventories still answer for an ordinary language", "the split is not a divide");

            // ⚠ Ce qu'un systeme ou un navigateur nous tend, et le defaut que ca corrige.
            check(Languages.FromLocale("zh-Hant-TW") == "zh-tw",
                "a Traditional Chinese system is recognised as Traditional",
                "cutting to two letters gave zh, which is Simplified - the visible bug this fixes");
            check(Languages.FromLocale("zh-CN") == "zh", "and a Simplified one as Simplified", "zh-cn is in the table");
            check(Languages.FromLocale("fr-FR") == "fr", "a region is dropped when it means nothing to us",
                "fr-fr is not a language we carry, fr is");
            check(Languages.FromLocale("fr_FR.UTF-8") == "fr", "Linux shapes are understood",
                "LANG carries an encoding and an underscore");
            check(Languages.FromLocale("nb-NO") == "nb", "nb-NO stays Norwegian Bokmal", "not truncated into nothing");
            check(Languages.FromLocale("no") == "nb", "and the older no resolves to the same language",
                "canonical, so two systems do not disagree about one person");
            check(Languages.FromLocale("iw") == "he", "the pre-1989 Hebrew code still works", "Java emits it to this day");
            check(Languages.FromLocale("qqq-XX") == null && Languages.FromLocale("") == null
                  && Languages.FromLocale(null) == null,
                "and something we cannot place gives null", "the caller keeps its own default rather than guessing");

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
            // ⚠ What a provider accepts is READ from the catalogue, never deduced from the code.
            check(Languages.GoogleCode("Cantonese") == "yue" && Languages.DeepLCode("Cantonese", true) == "YUE",
                "both providers do Cantonese", "they added it; a rule based on ISO 639-1 could never have known");
            check(Languages.GoogleCode("Dari") == null && Languages.DeepLCode("Dari", true) == "PRS",
                "DeepL does Dari and Google does not", "no rule produces that, only their own tables do");
            check(Languages.GoogleCode("Egyptian Arabic") == null && Languages.DeepLCode("Egyptian Arabic", true) == null,
                "neither does Egyptian Arabic", "said before translating, not discovered one refused line at a time");
            check(Languages.DeepLCode("Khmer", true) == null && Languages.GoogleCode("Khmer") == "km",
                "and DeepL does not do Khmer while Google does",
                "the kind of gap that used to cost a 400 per line, in silence");
        }
    }
}
