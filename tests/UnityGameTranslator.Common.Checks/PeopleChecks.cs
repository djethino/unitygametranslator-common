using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How a person is named, checked against the rule rather than against the code.
    ///
    /// ⚠ The stake is not typography. A reader looking at a list of translations has one question
    /// — "which of these is mine?" — and before this rule existed only the mod ever answered it.
    /// A name that fails to be marked tells somebody their own work belongs to a stranger, and a
    /// name marked wrongly tells them they may write where they may not.
    /// </summary>
    internal static class PeopleChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(People.Mention("seniorito") == "@seniorito",
                "somebody else is written @name",
                "one form everywhere: the at-sign says 'a person' without translating a word");

            check(People.Mention("djeitinho", isYou: true) == "@djeitinho (you)",
                "your own name carries (you)",
                "a word, never a colour — it has to survive a screenshot and a colour-blind reader");

            check(People.Mention("djeitinho", isYou: true).StartsWith("@"),
                "the at-sign is on your own name too",
                "two forms would mean learning two, and the exception would fall where it matters most");

            check(People.Mention(null) == People.Unknown && People.Mention("  ") == People.Unknown,
                "a missing name says so",
                "an empty space where a name belongs reads as a bug, not as an absence");

            check(People.Unknown != "you" && People.Unknown.Length > 0,
                "the stand-in is never 'you' and never blank",
                "guessing the reader owns an unattributed file is the one wrong answer");

            // ── Who is "you" ──────────────────────────────────────────────
            check(People.IsYou("Djeitinho", "djeitinho"),
                "the match ignores case",
                "the site echoes a name in the case its owner typed; a strict match would disown them");

            check(!People.IsYou("seniorito", "djeitinho"),
                "another account is not you",
                "the whole point of the mark");

            check(!People.IsYou("seniorito", null) && !People.IsYou(null, "djeitinho"),
                "nobody signed in means nothing is marked",
                "an anonymous reader is not 'not you', they are somebody with no name here");

            check(People.MentionOf("seniorito", "djeitinho") == "@seniorito"
                  && People.MentionOf("djeitinho", "djeitinho") == "@djeitinho (you)",
                "the one-call form agrees with the two-call form",
                "two ways to ask must not give two answers");
        }
    }
}
