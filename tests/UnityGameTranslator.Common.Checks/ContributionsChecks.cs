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

            // ── What there is to look at, and what it is made of ───────────────
            //
            // 🔴 Measured on a real lineage the day this was written: 56 rows to decide and 38
            // worth taking — 21 lines nobody had, all written by hand, and 35 both sides hold
            // differently, of which 17 are validations and 18 two machine translations that
            // disagree. Neither figure follows from the other, and "38 lines" says none of it.
            var added = new TagTally { Human = 21 };
            var differing = new TagTally { Validated = 17, Machine = 18 };

            check(Contributions.WhatKindOfWork(56, added, differing)
                  == "56 to review: 21 new (H 21) · 35 differing (V 17, A 18)",
                "both axes, each broken down by tag",
                "how long a review takes and whether anything comes of it are two questions");

            check(Contributions.WhatKindOfWork(21, added, new TagTally()) == "21 to review: 21 new (H 21)",
                "a side with nothing in it is left out entirely",
                "\"0 differing\" is a word to read for nothing, in the reader's fourth language");

            check(Contributions.WhatKindOfWork(0, new TagTally(), new TagTally()) == ""
                  && Contributions.WhatKindOfWork(null, added, differing) == "",
                "nothing waiting and nothing known both say nothing",
                "a caller then shows the total alone, as it did before this existed");

            // ── The letters ───────────────────────────────────────────────────
            check(new TagTally { Human = 2, Validated = 3, Machine = 4, Skipped = 1 }.Letters()
                  == "H 2, V 3, A 4, S 1",
                "best quality first, always in that order",
                "a letter is recognised by its place as much as by itself");

            check(new TagTally { Machine = 4 }.Letters() == "A 4",
                "and the empty ones are not printed",
                "a zero beside three others is noise on a card measured in pixels");

            // 🔴 H and S sit level on the merge ladder and are NOT merged here. One says somebody
            // wrote the line, the other that somebody ruled it must not be written — reporting
            // them together would count as translated what nobody translated.
            check(new TagTally { Human = 3, Skipped = 2 }.Letters() == "H 3, S 2",
                "a refusal is never folded in with a hand-written line",
                "they rank the same and they do not mean the same");

            check(new TagTally { Human = 3, Skipped = 2 }.Total == 5,
                "the total is the four added up",
                "it is what the two halves are checked against");

            // ── The drawn form and the printed one are one answer ──────────────────────────────
            //
            // 🔴 The mod and the Manager draw chips where the sentence prints letters. The two
            // must not be able to disagree about what is shown or in what order, so the property
            // is checked rather than each side's output being described twice.
            var mixed = new TagTally { Human = 9, Validated = 0, Machine = 3, Skipped = 1 };
            var counted = mixed.Counted();

            check(counted.Length == 3,
                "a zero is left out of the pieces too",
                "the sentence hides it; a row of chips that did not would show one nobody counted");

            string rebuilt = "";
            foreach (var piece in counted)
            {
                rebuilt = rebuilt.Length == 0
                    ? piece.Letter + " " + piece.Count
                    : rebuilt + ", " + piece.Letter + " " + piece.Count;
            }

            check(rebuilt == mixed.Letters(),
                "the pieces say exactly what the sentence says",
                "same letters, same counts, same order — one of them drifting is a silent lie");

            // And the grouping above them: same order, same exclusions.
            var kinds = Contributions.KindsOfWork(
                new TagTally { Human = 2 },
                new TagTally { Machine = 4 });

            check(kinds.Length == 2 && kinds[0].Label == "new" && kinds[1].Label == "differing",
                "what is new comes before what differs",
                "a line the Main does not hold is a different proposition from one it holds otherwise");

            check(Contributions.KindsOfWork(default(TagTally), new TagTally { Machine = 4 }).Length == 1,
                "a kind holding nothing is not a piece",
                "the sentence skips it, so the drawn form skips it");

            check(Contributions.WhatKindOfWork(6, new TagTally { Human = 2 }, new TagTally { Machine = 4 })
                    == "6 to review: 2 new (H 2) · 4 differing (A 4)",
                "and the sentence is still the sentence",
                "the pieces were extracted from it, not written beside it");
        }
    }
}
