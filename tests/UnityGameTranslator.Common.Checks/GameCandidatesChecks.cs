using System;
using System.Collections.Generic;

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

            // Foretales (2026-10-06): the add-ons Steam lists under a title that only CONTAINS the
            // name come after the row that IS the name, and are no likely match.
            int game = GameCandidates.Confidence("1170080", "Foretales", "igdb", null, "Foretales");
            int artbook = GameCandidates.Confidence("2080350", "Foretales - Artbook", "steam", null, "Foretales");
            check(game > artbook, "the exact name with the game's Steam id outranks an add-on that contains it", $"{game} > {artbook}");
            check(GameCandidates.Mark(artbook) == "" && GameCandidates.Mark(game) != "",
                "an add-on is no likely match; the game is", $"'{GameCandidates.Mark(artbook)}' / '{GameCandidates.Mark(game)}'");
            check(GameCandidates.Confidence("1", "Some Game", "igdb", null, "Some Game")
                  == GameCandidates.Confidence("1", "Some Game", "steam", null, "Some Game"),
                "the source alone counts for nothing", "the Steam id it carries does, whoever gives it");

            // ── The names the stores give a game besides its title, after it in brackets ──
            var row = GameCandidates.Row("Legacy of Shadows", "local", 0, new List<string> { "侠影录" });
            check(row == "Legacy of Shadows (侠影录) [catalog]",
                "a card's other names follow its title, in brackets", row);
            check(GameCandidates.Row("Foretales", "igdb", 0, new List<string>()) == "Foretales [igdb]",
                "no other name, nothing added", "an empty list writes nothing");

            // ── A list row's picture: fills its frame, or whole over its blur ──
            check(GameCandidates.FillsFrame(264, 352) && GameCandidates.FillsFrame(300, 450),
                "a cover fills its portrait frame", "IGDB 264x352, Steam capsule 300x450");
            check(!GameCandidates.FillsFrame(460, 215) && !GameCandidates.FillsFrame(1920, 1080),
                "a store header or a screenshot is shown whole", "cropped, it kept its middle third");
            check(!GameCandidates.FillsFrame(500, 500) && !GameCandidates.FillsFrame(0, 0),
                "a square, or a picture of no size, is not cropped", "taller than wide only");

            // ── What tells two games of one title apart ──────────────────────
            var ids = new System.Collections.Generic.Dictionary<string, string> { ["local"] = "12", ["igdb"] = "81", ["steam"] = "500" };
            check(GameCandidates.Facts(ids, 2025, new[] { "Studio A" }, new[] { "Studio A", "House B" })
                  == "Steam 500 · IGDB 81 · 2025 · Studio A / House B",
                "a row says its store ids, its year and who made and published it",
                "the site's list says the same; the card's own number is not a store's; a publisher that also made it is said once");
            check(GameCandidates.Facts(null, null, null, null) == "",
                "nothing known, nothing said", "an empty line rather than a guess");
            check(GameCandidates.Facts(null, null, null, new[] { "House B" }) == "House B",
                "a publisher alone is still said", "often the only maker a store names");

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

            // ── The hit taken, sent back as it was given (game_pick) ─────────
            var steamPick = GameCandidates.PickOf("steam", 0, "3863760");
            check(steamPick != null && steamPick.Source == "steam" && steamPick.Id == "3863760",
                "a Steam hit is named by its Steam id", "it carries no other id");

            var igdbPick = GameCandidates.PickOf("IGDB", 376372, "3863760");
            check(igdbPick != null && igdbPick.Source == "igdb" && igdbPick.Id == "376372",
                "an IGDB hit is named by its IGDB id, even when it knows the Steam id",
                "the site resolves the Steam id from the IGDB answer itself");

            check(GameCandidates.PickOf("local", 37, null)?.Id == "37"
                  && GameCandidates.PickOf("steam", 0, null) == null
                  && GameCandidates.PickOf("rawg", 0, null) == null
                  && GameCandidates.PickOf("somewhere", 5, null) == null,
                "a hit without a usable id, or from an unknown source, is no pick", "never a guessed pair");

            // ── Warned before sending, never refused here ────────────────────
            check(GameCandidates.DifferentGame("3863760", null, "3604960", null)?.Contains("3604960") == true
                  && GameCandidates.DifferentGame("3863760", null, "3863760", null) == null,
                "two Steam ids that differ are said, the same one is not", "the site decides (a demo reads its own id)");

            check(GameCandidates.DifferentGame(null, "Legacy of Shadows", null, "Spyro: Shadow Legacy") != null,
                "a title that is not a form of the name read is said", "the case that filed a translation under another game");

            check(GameCandidates.DifferentGame(null, "LONESTAR", null, "LoneStar: The Game") == null
                  && GameCandidates.DifferentGame(null, "Hollow Knight", null, "hollow-knight") == null
                  && GameCandidates.DifferentGame(null, null, null, "Anything") == null,
                "a product name that is a form of the title, or nothing read, says nothing",
                "case, spaces and punctuation aside");

            check(GameNames.Flat("龙胤立志传") == "龙胤立志传" && GameNames.Flat("Lone Star: The Game!") == "lonestarthegame",
                "names are compared on letters and digits of any script", "the site's flatten, the same rule");

            check(GameCandidates.NothingFound.Contains("Steam ID"),
                "an empty list says what to try next", "the search box takes ids too");

            // Without an account the list is the catalogue alone (2026-10-05): its empty answer
            // says so and gives the way to the rest, and the list with an account does not.
            check(GameCandidates.NothingFoundFor(stores: true) == GameCandidates.NothingFound
                  && GameCandidates.NothingFoundFor(stores: false).Contains("Sign in")
                  && GameCandidates.NothingFoundFor(stores: false).Contains("Steam ID")
                  && !GameCandidates.NothingFound.Contains("Sign in"),
                "a search without an account says the list is the catalogue alone, and how to search all games",
                "the stores are asked for an account only");
        }
    }
}
