using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Which of the site's answers is the game in front of the person.
    ///
    /// The stake: a first publication is filed under the game picked here, and that is what every
    /// other machine searches with. The figures are the mod's, carried over on 2026-09-05 when the
    /// Manager started asking the same question.
    /// </summary>
    internal static class GameCandidatesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // The same Steam id on both sides is the strongest fact there is.
            check(GameCandidates.Confidence("367520", "Hollow Knight", "steam", "367520", "Hollow Knight")
                  >= GameCandidates.BestMatch,
                "the same Steam id makes a best match", "★ whatever else is known");

            // A game the site already holds is likelier than one it only heard of.
            check(GameCandidates.Confidence(null, "Some Game", "local", null, "Some Game")
                  > GameCandidates.Confidence(null, "Some Game", "igdb", null, "Some Game"),
                "the site's own catalogue outranks a game database", "it already carries translations");

            check(GameCandidates.Confidence(null, "Some Game", "local", null, "Some Game")
                  >= GameCandidates.BestMatch,
                "catalogue plus the same name is a best match", "30 + 20");

            // Names: equal beats contained beats unrelated, case aside.
            check(GameCandidates.Confidence(null, "some game", "rawg", null, "Some Game")
                  > GameCandidates.Confidence(null, "Some Game II", "rawg", null, "Some Game")
                  && GameCandidates.Confidence(null, "Some Game II", "rawg", null, "Some Game")
                  > GameCandidates.Confidence(null, "Other", "rawg", null, "Some Game"),
                "an equal name outranks a partial one, which outranks none", "case does not count");

            check(GameCandidates.Confidence(null, "Other", "rawg", null, "Some Game") == 0,
                "nothing in common scores nothing", "no mark, bottom of the list");

            // ── Marks and rows ───────────────────────────────────────────────
            check(GameCandidates.Mark(GameCandidates.BestMatch) == "★"
                  && GameCandidates.Mark(GameCandidates.LikelyMatch) == "☆"
                  && GameCandidates.Mark(GameCandidates.LikelyMatch - 1) == "",
                "★ from the best-match line, ☆ from the likely one, nothing below", "the mod's thresholds");

            check(GameCandidates.SourceLabel("local") == "catalog" && GameCandidates.SourceLabel("IGDB") == "igdb"
                  && GameCandidates.SourceLabel(null) == "",
                "the site's catalogue reads 'catalog', the rest keep their name", "'local' means nothing to a reader");

            check(GameCandidates.Row("Hollow Knight", "local", 80) == "Hollow Knight [catalog] ★"
                  && GameCandidates.Row("Other", null, 0) == "Other",
                "a row is the name, the source in brackets, the mark", "same row in both products");

            check(GameCandidates.Legend.Contains("★") && GameCandidates.Legend.Contains("[catalog]"),
                "the legend explains the two marks it uses", "a mark nobody explains is decoration");
        }
    }
}
