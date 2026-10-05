using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The game confirmed on this machine against the game the site files the translation under.
    ///
    /// The stake (user, 2026-10-05): a Main can be moved by its owner at any time, rightly or as a
    /// joke. Followed in silence, every machine holding the translation would change game, and
    /// Community would offer the translations of the wrong one. A contribution, meanwhile, follows
    /// its Main and waits for its author to confirm.
    /// </summary>
    internal static class GameChoicesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var card = new LineageGame(12, "Lost Echo", "500", 81, null);

            // ── The same game, however it was named ───────────────────────────
            check(GameChoices.Same(new GameChoice("local", "12", "Lost Echo"), card),
                "the card itself is the same game", "picked from the site's catalogue");
            check(GameChoices.Same(new GameChoice("steam", "500", "Lost Echo"), card),
                "the Steam id the card holds is the same game", "picked as a Steam hit before the card existed");
            check(GameChoices.Same(new GameChoice("igdb", "81", "Lost Echo"), card),
                "the IGDB id the card holds is the same game", "picked as an IGDB hit");
            check(!GameChoices.Same(new GameChoice("local", "13", "Lost Echo"), card),
                "another card is another game, whatever its title", "homonyms exist");
            check(!GameChoices.Same(new GameChoice("rawg", "81", "Lost Echo"), card),
                "an id is read in its own source only", "IGDB 81 is not RAWG 81");

            // ── The chip on the game's title ──────────────────────────────────
            var detected = GameChoices.IdentityBadge(hasName: true, confirmed: false, onTheSite: false);
            check(detected is { Text: "Detected", Tone: BadgeTone.Attention, Kind: BadgeKind.GameIdentity },
                "a name only read from the files says Detected, in yellow", "not confirmed by anybody yet");
            var confirmedChip = GameChoices.IdentityBadge(hasName: true, confirmed: true, onTheSite: false);
            check(confirmedChip is { Text: "Confirmed", Tone: BadgeTone.Good },
                "a game the player chose says Confirmed, in green", "chosen here, not yet fixed by the site");
            check(GameChoices.IdentityBadge(true, true, onTheSite: true) is null
                  && GameChoices.IdentityBadge(true, false, onTheSite: true) is null,
                "nothing once a translation of it is on the site", "the site fixes the game from then on");
            check(GameChoices.IdentityBadge(hasName: false, confirmed: false, onTheSite: false) is null,
                "nothing without a name to qualify", "\"No game detected\" needs no chip");
            check(!string.IsNullOrEmpty(detected?.Tip) && !string.IsNullOrEmpty(confirmedChip?.Tip),
                "each says what it means on hover", "a chip's tip is never empty");

            // ── An answer of the list against the game already confirmed ────────
            var held = new GameChoice("igdb", "137192", "Buried Stars");
            var folded = new System.Collections.Generic.Dictionary<string, string>
                { ["steam"] = "1025960", ["igdb"] = "137192" };

            check(GameChoices.Holds(held, "igdb", "137192", null),
                "the answer it was confirmed from holds it", "nothing to apply");
            check(GameChoices.Holds(held, "steam", "1025960", folded),
                "the same game clicked on its Steam row holds it too",
                "🔴 the reported defect: Apply (1) stayed after clicking the game already confirmed");
            check(!GameChoices.Holds(held, "steam", "1754810", new System.Collections.Generic.Dictionary<string, string> { ["steam"] = "1754810" }),
                "another game's row does not", "its soundtrack is another Steam app");
            check(!GameChoices.Holds(held, "rawg", "137192", new System.Collections.Generic.Dictionary<string, string> { ["rawg"] = "137192" }),
                "an id is read in its own source only here too", "IGDB 137192 is not RAWG 137192");
            check(!GameChoices.Holds(null, "igdb", "137192", null),
                "nothing confirmed holds nothing", "every answer is then something to apply");

            // ── The line under the game's name ────────────────────────────────
            var mine = new GameChoice("local", "13", "Crystal Dragon");
            check(GameChoices.Banner(mine, card) == "On the website, this translation is for Lost Echo.",
                "a difference is said, naming the site's game", "the wording validated on 2026-10-05");
            check(GameChoices.Banner(new GameChoice("local", "12", "Lost Echo"), card) == null,
                "nothing is said when they agree", "a notice that always shows becomes wallpaper");
            check(GameChoices.Banner(null, card) == null && GameChoices.Banner(mine, null) == null,
                "nothing is said when either side is unknown", "unknown is not a difference");

            // ── Nothing confirmed yet: the site's game is taken; a change never is ─
            check(GameChoices.Adopt(null, card)?.Id == "12" && GameChoices.Adopt(null, card)?.Source == "local",
                "an installation that confirmed nothing takes the site's game", "it already accepted it by downloading");
            check(GameChoices.Adopt(mine, card) == mine,
                "a confirmed game is never replaced by the site's", "a move is asked about, not adopted");
            check(GameChoices.ConfirmBody("Lost Echo") == "This game will be named Lost Echo. Community will show translations for Lost Echo.",
                "the confirmation names the consequence", "validated on 2026-10-05");

            // ── A contribution waits for the switch; a Main's owner does not ──
            var account = new AccountFacts { Online = true, SignedIn = true };
            var differing = new LocalFacts { Lines = 3, LocalChanges = 1, ConfirmedGame = mine };

            var branchServer = new ServerFacts { Checked = true, Exists = true, IsOwner = true, Role = LineageRole.Branch, Game = card, GameSwitchPending = true, Hash = "b" };
            var onBranch = Uploads.Button(Standings.From(differing, branchServer, account), differing, branchServer, account);
            check(onBranch.Act == UploadAct.Update && onBranch.Closed == "Switch game to keep contributing."
                  && onBranch.Wall == "On the website, this translation is for Lost Echo. Switch game to keep contributing.",
                "a branch whose Main moved waits for Switch game", "the way out beside the button, the whole wall where no line states the fact");

            var mainServer = new ServerFacts { Checked = true, Exists = true, IsOwner = true, Role = LineageRole.Main, Game = card, Hash = "m" };
            var onMain = Uploads.Button(Standings.From(differing, mainServer, account), differing, mainServer, account);
            check(onMain.Act == UploadAct.Update && onMain.Closed == null,
                "a Main's owner is never held", "they moved it");

            var stranger = new ServerFacts { Checked = true, Exists = true, IsOwner = false, Game = card, AcceptsBranches = true, Hash = "m" };
            var first = Uploads.Button(Standings.From(differing, stranger, account), differing, stranger, account);
            check(first.Act == UploadAct.Contribute && first.Closed == "Switch game to keep contributing.",
                "a first contribution from another game waits too", "the site would refuse it (game_changed)");

            var followed = new LocalFacts { Lines = 3, LocalChanges = 1, ConfirmedGame = GameChoices.Of(card) };
            var after = Uploads.Button(Standings.From(followed, branchServer, account), followed, branchServer, account);
            check(after.Closed == null,
                "once switched, the branch contributes again", "the upload names the game; the site lifts the hold");
        }
    }
}
