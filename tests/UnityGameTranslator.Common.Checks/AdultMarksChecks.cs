using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The "Adults only" box of a first publication, as decided with the owner on 2026-09-23: told
    /// when the game is classified, asked only when nobody classified it and the upload adds it.
    /// </summary>
    internal static class AdultMarksChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(AdultMarks.Shown(adult: true, declarable: false) && !AdultMarks.Open(adult: true, declarable: false),
                  "a classified game shows the box ticked and locked", "the person is told; the question is already answered");
            check(AdultMarks.Shown(adult: false, declarable: true) && AdultMarks.Open(adult: false, declarable: true),
                  "an unclassified game this upload adds offers the box", "the first publisher is the one with the say");
            check(!AdultMarks.Shown(adult: false, declarable: false),
                  "a game already on the site and unclassified shows nothing", "later translators have no say, and nothing is to be told");

            foreach (var source in new[] { "steam", "igdb", "contributor", "admin" })
            {
                string said = AdultMarks.Source(source);
                check(said.EndsWith(".") && said.IndexOf("contributor", StringComparison.OrdinalIgnoreCase) < 0,
                      $"Source({source}) is a sentence that never says 'contributor' alone",
                      "the project's rule: that word never stands on its own");
            }
        }
    }
}
