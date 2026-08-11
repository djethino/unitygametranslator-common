using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The two measures a player reads about a file, checked against the website's rules.
    ///
    /// Written from what App\Models\Translation does, not from the C# beside it — the website is
    /// the reference, and a check copied from the port would only prove the port equals itself.
    ///
    /// The case that matters most is SKIPPED: lines an author deliberately keeps as they are.
    /// The mod's own copy never received them, so the website was systematically more generous
    /// and the same file could carry different words in a browser and in a game.
    /// </summary>
    internal static class QualityChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // Coverage: (H+V+S) / (H+V+S+A).
            check(Cov(human: 5, validated: 0, skipped: 0, ai: 5) == 0.5,
                "half read is half covered", "five read, five still machine output");
            check(Cov(10, 0, 0, 0) == 1.0, "all read is full coverage", "nothing left unreviewed");
            check(Cov(0, 0, 0, 10) == 0.0, "none read is zero", "machine output nobody has been through");

            // ⚠ Skipped counts as settled. This is the divergence that existed.
            check(Cov(4, 0, 4, 2) == 0.8,
                "kept-as-is lines count as read", "deciding to keep a name IS reading the line");
            check(Cov(4, 0, 4, 2) > Cov(4, 0, 0, 2),
                "so a file with kept lines scores higher", "the mod used to drop them and score lower");

            // ⚠ The guard is on H+V+A, deliberately not on the denominator.
            check(Cov(0, 0, 7, 0) == null,
                "a file of nothing but kept lines has no coverage",
                "otherwise S/S would announce 100% for a file with no translation in it");
            check(Cov(0, 0, 0, 0) == null, "and an empty file has none either", "absence, not zero");

            // Completeness: settled / (settled + captured).
            check(Comp(5, 0, 0, 5, captured: 10) == 0.5, "half of what it met is settled", "ten of twenty");
            check(Comp(0, 0, 5, 5, 10) == 0.5, "kept lines count as settled too", "same rule, both measures");
            check(Comp(10, 0, 0, 0, 0) == 1.0, "nothing pending is complete", "no captures waiting");
            check(Comp(0, 0, 0, 0, 0) == null, "an empty file is not 0% complete", "it is nothing at all");

            // Capture-only: met text, settled none of it.
            check(Quality.IsCaptureOnly(0, 0, 0, 0, 12), "text met and nothing done is capture-only",
                "the game's own words handed back, not a translation in progress");
            check(!Quality.IsCaptureOnly(0, 0, 1, 0, 12), "one kept line is already a decision",
                "somebody looked at it");
            check(!Quality.IsCaptureOnly(0, 0, 0, 0, 0), "and an empty file is not capture-only",
                "nothing was captured either");

            // The floor: reading cannot be judged before writing.
            check(Stage(5, 0, 0, 0, captured: 20) == null,
                "a barely started file gets no stage", "two lines out of thirteen were once 'fully reviewed'");
            check(Stage(10, 0, 0, 0, 0) == ReviewStage.Reviewed, "a finished one does", "nothing pending");

            // Steps, in the website's order.
            check(Stage(0, 0, 0, 10, 0) == ReviewStage.Machine, "untouched machine output", "coverage 0");
            check(Stage(1, 0, 0, 9, 0) == ReviewStage.Started, "one line read starts the review", "coverage above 0");
            check(Stage(4, 0, 0, 6, 0) == ReviewStage.Advanced, "four in ten is well under way", "the 0.4 threshold");
            check(Stage(10, 0, 0, 0, 0) == ReviewStage.Reviewed, "all of it is read", "coverage 1");

            // The scenario the divergence produced, now settled the same way on both sides.
            check(Stage(6, 0, 4, 0, 0) == ReviewStage.Reviewed,
                "six read and four kept is fully reviewed",
                "the mod used to answer 'well under way' for the file the site called finished");
        }

        private static double? Cov(int human, int validated, int skipped, int ai) =>
            Quality.ReviewCoverage(human, validated, skipped, ai);

        private static double? Comp(int human, int validated, int skipped, int ai, int captured) =>
            Quality.Completeness(human, validated, skipped, ai, captured);

        private static ReviewStage? Stage(int human, int validated, int skipped, int ai, int captured) =>
            Quality.Stage(human, validated, skipped, ai, captured);
    }
}
