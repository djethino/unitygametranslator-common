namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The game a player confirmed for this installation — kept in `config.json` (`game_choice`),
    /// never in the translation file — as a publish-list answer: its source and its id there, and
    /// the title to show.
    /// </summary>
    public sealed class GameChoice
    {
        public GameChoice(string source, string id, string name)
        {
            Source = source;
            Id = id;
            Name = name;
        }

        /// <summary>"local" (a card of the site), "steam", "igdb" or "rawg" — as `game_pick` names them.</summary>
        public string Source { get; }

        /// <summary>The id in that source: the card's id, a Steam app id, an IGDB or RAWG id.</summary>
        public string Id { get; }

        /// <summary>The title shown for it.</summary>
        public string Name { get; }

        /// <summary>The choice as a publication sends it (`game_pick`).</summary>
        public GameCandidates.Pick AsPick() => new GameCandidates.Pick(Source, Id);
    }

    /// <summary>The game a lineage is filed under on the site, as `check-uuid` names it (`game`).</summary>
    public sealed class LineageGame
    {
        public LineageGame(long id, string name, string? steamId, long? igdbId, long? rawgId)
        {
            Id = id;
            Name = name;
            SteamId = steamId;
            IgdbId = igdbId;
            RawgId = rawgId;
        }

        /// <summary>The card's id on the site.</summary>
        public long Id { get; }

        public string Name { get; }
        public string? SteamId { get; }
        public long? IgdbId { get; }
        public long? RawgId { get; }
    }

    /// <summary>
    /// The game of this installation against the game its translation is filed under on the site —
    /// decided once for the mod and the Manager.
    ///
    /// 🔴 **A move made on the site is never followed in silence** (user, 2026-10-05: "imagine un
    /// petit plaisantin qui fait une trad et piège les gens en changeant le nom plus tard pour
    /// s'amuser"). Followed blindly, the game would change on every machine holding the translation,
    /// and Community would offer the translations of the wrong game. So the confirmed game stays
    /// until the player confirms the other one; until then one line under the game's name says so
    /// (<see cref="Banner"/>), with one act (<see cref="SwitchVerb"/>) and a confirmation.
    ///
    /// ⚠ **When nothing was confirmed yet, the site's game is taken as it stands** (<see cref="Adopt"/>):
    /// an installation that downloaded or published a translation before the choice existed has
    /// already accepted that game. The question is only ever asked about a CHANGE.
    ///
    /// ⚠ Wording validated by the user on 2026-10-05; plain international English, one verb.
    /// </summary>
    public static class GameChoices
    {
        /// <summary>The one act, on the line under the game's name.</summary>
        public const string SwitchVerb = "Switch game";

        /// <summary>The confirmation's title.</summary>
        public const string ConfirmTitle = "Switch game?";

        /// <summary>The confirmation's button.</summary>
        public const string ConfirmVerb = "Switch";

        /// <summary>What the switch does, in the confirmation: the consequence, named.</summary>
        public static string ConfirmBody(string name) =>
            "This game will be named " + name + ". Community will show translations for " + name + ".";

        /// <summary>
        /// Said before a publication when nothing identifies the game (`games/adult` answered
        /// `identified: false`): the site would refuse it, so it is said before the click — the same
        /// sentence the site answers with (`game_not_found`).
        /// </summary>
        public const string NotIdentified =
            "This game could not be identified. Search for it by title, or paste its Steam ID or Steam link, and pick it in the list.";

        /// <summary>Whether the confirmed game IS the lineage's game — by the card, or by a store id the card holds.</summary>
        public static bool Same(GameChoice choice, LineageGame game)
        {
            switch (choice.Source)
            {
                case GameCandidates.CatalogueSource: return choice.Id == Invariant(game.Id);
                case "steam": return !string.IsNullOrEmpty(game.SteamId) && choice.Id == game.SteamId;
                case "igdb": return game.IgdbId.HasValue && choice.Id == Invariant(game.IgdbId.Value);
                case "rawg": return game.RawgId.HasValue && choice.Id == Invariant(game.RawgId.Value);
                default: return false;
            }
        }

        /// <summary>
        /// Whether the line under the game's name is due: a game is confirmed here, the site files
        /// the translation under a game, and they are not the same. False when either is unknown.
        /// </summary>
        public static bool Differs(GameChoice? choice, LineageGame? game) =>
            choice != null && game != null && !Same(choice, game);

        /// <summary>The line under the game's name, or null when nothing differs.</summary>
        public static string? Banner(GameChoice? choice, LineageGame? game) =>
            Differs(choice, game) ? "On the website, this translation is for " + game!.Name + "." : null;

        /// <summary>
        /// The confirmed game after a translation was taken from the site or published: the
        /// lineage's game when nothing was confirmed here, the confirmed one otherwise (a difference
        /// is asked about, never adopted).
        /// </summary>
        public static GameChoice? Adopt(GameChoice? choice, LineageGame? game) =>
            choice ?? (game == null ? null : Of(game));

        /// <summary>The lineage's game as a choice — what Switch game writes.</summary>
        public static GameChoice Of(LineageGame game) =>
            new GameChoice(GameCandidates.CatalogueSource, Invariant(game.Id), game.Name);

        private static string Invariant(long value) =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
