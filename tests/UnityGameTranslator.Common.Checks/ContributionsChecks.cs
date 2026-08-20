using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What contributions are holding, and the words both products use to say it.
    ///
    /// ⚠ Written from the rule as stated, not read back from the code. The stake is small in
    /// appearance and real in effect: a counter that announces work which is not there is a counter
    /// people stop reading, and it then hides the times there IS something to do.
    /// </summary>
    internal static class ContributionsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── Nothing waiting says nothing ──────────────────────────────────
            check(Contributions.WhatIsWaiting(0, 0) == "",
                "no contribution waiting produces no sentence",
                "a screen with nothing to say must not announce a zero");

            check(Contributions.WhatIsWaiting(0, 12) == "",
                "and lines without a contribution to carry them say nothing either",
                "the two numbers describe one set: no set, no sentence");

            // ── One is one ────────────────────────────────────────────────────
            check(Contributions.WhatIsWaiting(1, 1).Contains("1 contribution")
                  && !Contributions.WhatIsWaiting(1, 1).Contains("contributions"),
                "one contribution is not pluralised",
                "the mod is read in a fourth language: a stray s is a word to decode");

            check(Contributions.WhatIsWaiting(1, 1).Contains("1 line ")
                  && !Contributions.WhatIsWaiting(1, 1).Contains("lines"),
                "nor is one line", "same reason");

            check(Contributions.WhatIsWaiting(3, 12).Contains("3 contributions")
                  && Contributions.WhatIsWaiting(3, 12).Contains("12 lines"),
                "several carry both counts",
                "how many people and how much work are two different decisions to make");

            // ── An older server counted nothing, which is not "nothing to take" ─
            check(Contributions.WhatIsWaiting(2, null).Contains("2 contributions")
                  && !Contributions.WhatIsWaiting(2, null).Contains("line"),
                "an unknown amount of work is not claimed",
                "a server too old to count them has not said there is nothing");

            // ── The contributor's own side ────────────────────────────────────
            //
            // ⚠ Zero speaks here, where it stays silent on the Main's side: it means the work
            // arrived. Saying nothing would read as a screen that failed to load.
            check(Contributions.WhatYouAreOffering(0).Length > 0,
                "a contributor with nothing outstanding is told so",
                "silence would read as a screen that did not load, not as work delivered");

            check(Contributions.WhatYouAreOffering(0).Contains("in the Main"),
                "and told where it went", "the answer to \"was I read\" is the point of the line");

            check(Contributions.WhatYouAreOffering(1).Contains("1 line ")
                  && Contributions.WhatYouAreOffering(4).Contains("4 lines"),
                "what is still outstanding is counted plainly",
                "it is the one measure of whether their work has arrived");
        }
    }
}
