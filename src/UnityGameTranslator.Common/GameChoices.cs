namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The game a player confirmed for this installation — kept in `config.json` (`game_choice`),
    /// never in the translation file — as a publish-list answer: its source and its id there, and
    /// the title to show.
    /// </summary>
    public sealed class GameChoice
    {
        public GameChoice(string source, string id, string name,
                          System.Collections.Generic.IReadOnlyList<string>? otherNames = null)
        {
            Source = source;
            Id = id;
            Name = name;
            OtherNames = OtherNamesOf(otherNames);
        }

        /// <summary>
        /// The names the stores give the game besides its title (`other_names`, user 2026-10-06) —
        /// display only, never part of what identifies it. Empty when none is known.
        /// </summary>
        public System.Collections.Generic.IReadOnlyList<string> OtherNames { get; }

        /// <summary>The title as every screen shows it: "侠影录 (Legacy of Shadows)".</summary>
        public string Title => GameCandidates.TitleWithOtherNames(Name, OtherNames);

        internal static System.Collections.Generic.IReadOnlyList<string> OtherNamesOf(System.Collections.Generic.IEnumerable<string>? names)
        {
            var kept = new System.Collections.Generic.List<string>();
            if (names == null) return kept;
            foreach (var name in names)
            {
                if (!string.IsNullOrWhiteSpace(name)) kept.Add(name.Trim());
            }
            return kept;
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
        public LineageGame(long id, string name, string? steamId, long? igdbId, long? rawgId,
                           System.Collections.Generic.IReadOnlyList<string>? otherNames = null)
        {
            Id = id;
            Name = name;
            SteamId = steamId;
            IgdbId = igdbId;
            RawgId = rawgId;
            OtherNames = GameChoice.OtherNamesOf(otherNames);
        }

        /// <summary>The card's id on the site.</summary>
        public long Id { get; }

        public string Name { get; }

        /// <summary>The card's names in the other stores (`other_names`) — display only.</summary>
        public System.Collections.Generic.IReadOnlyList<string> OtherNames { get; }

        /// <summary>The title as every screen shows it: "侠影录 (Legacy of Shadows)".</summary>
        public string Title => GameCandidates.TitleWithOtherNames(Name, OtherNames);
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

        /// <summary>
        /// The chip on the game's title: "Detected" (yellow) while the name is only read from the
        /// game's files, "Confirmed" (green) once the player chose it — and nothing once a
        /// translation of it is on the site, which fixes the game (user, 2026-10-05: "un badge sur
        /// les titres tant qu'ils sont pas publish, detected/confirmed … detected en jaune et
        /// confirmed en vert"). Nothing either when there is no name to qualify.
        /// </summary>
        /// <param name="hasName">Whether a name is shown at all.</param>
        /// <param name="confirmed">Whether a game is confirmed here (`game_choice`).</param>
        /// <param name="onTheSite">Whether this game's translation is on the site — published or downloaded.</param>
        public static Badge? IdentityBadge(bool hasName, bool confirmed, bool onTheSite)
        {
            if (!hasName || onTheSite) return null;

            return confirmed
                ? new Badge { Kind = BadgeKind.GameIdentity, Text = "Confirmed", Tone = BadgeTone.Good,
                              Tip = "Chosen on this computer. Fixed by the website once the translation is published." }
                : new Badge { Kind = BadgeKind.GameIdentity, Text = "Detected", Tone = BadgeTone.Attention,
                              Tip = "Read from the game's files. Not confirmed yet." };
        }

        /// <summary>
        /// The line under a game's title when its name, only detected, describes several games on
        /// the site (<see cref="GameNames.Match.Ambiguous"/>) — namesakes, or loose matches none of
        /// which is exact — or null when there is nothing to say: a game confirmed here is known,
        /// and a name that answered once is not in doubt (user, 2026-10-05: "si le détecté semble
        /// être conflictuel il faut un message … pour demander de confirmer").
        /// </summary>
        public static string? Namesakes(GameChoice? confirmed, string? detectedName, bool ambiguous)
        {
            if (confirmed != null || !ambiguous || string.IsNullOrWhiteSpace(detectedName)) return null;
            return "Several games are named " + detectedName!.Trim() + ". Confirm which one with Change.";
        }

        /// <summary>
        /// Whether an answer of the site's game list IS the game already confirmed — by the answer's
        /// own source and id, or by any id the answer gathers from the others (`ids`, by source: the
        /// site folds the hits of one game into one row).
        ///
        /// 🔴 **By the game, never by the row's source** (user, 2026-10-05: "il me laisse apply (1)
        /// même si je … reclick sur celui d'avant"). A game confirmed from its IGDB answer, clicked
        /// again on its Steam row or its card, is the same game — nothing to apply.
        /// </summary>
        /// <param name="ids">The answer's ids by source ("local", "steam", "igdb", "rawg"); null when unknown.</param>
        public static bool Holds(GameChoice? held, string? source, string? id,
                                 System.Collections.Generic.IReadOnlyDictionary<string, string>? ids)
        {
            if (held is null) return false;
            if (held.Source == source && held.Id == id) return true;

            return ids != null && ids.TryGetValue(held.Source, out var same) && same == held.Id;
        }

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
            Differs(choice, game) ? "On the website, this translation is for " + game!.Title + "." : null;

        /// <summary>
        /// The confirmed game after a translation was taken from the site or published: the
        /// lineage's game when nothing was confirmed here, the confirmed one otherwise (a difference
        /// is asked about, never adopted).
        /// </summary>
        public static GameChoice? Adopt(GameChoice? choice, LineageGame? game) =>
            choice ?? (game == null ? null : Of(game));

        /// <summary>The lineage's game as a choice — what Switch game writes.</summary>
        public static GameChoice Of(LineageGame game) =>
            new GameChoice(GameCandidates.CatalogueSource, Invariant(game.Id), game.Name, game.OtherNames);

        /// <summary>
        /// The confirmed game with the other names the site now gives it — when the site files the
        /// translation under that SAME game (<see cref="Same"/>), and only its names change: the
        /// title confirmed, its source and its id stay. Null when there is nothing to refresh, so a
        /// caller writes only a real change.
        ///
        /// ⚠ Not a move: names are display (user, 2026-10-06), and a game the site files the
        /// translation under is the confirmed one by its id. A DIFFERENT game is still only ever
        /// said (<see cref="Banner"/>), never taken.
        /// </summary>
        public static GameChoice? WithNamesOf(GameChoice? choice, LineageGame? game)
        {
            if (choice == null || game == null || !Same(choice, game)) return null;

            // The card's title is one of its names here when the choice was titled otherwise (a
            // Steam pick confirmed as 侠影录, a card now titled Legacy of Shadows): both are kept.
            var names = new System.Collections.Generic.List<string>(game.OtherNames);
            names.Insert(0, game.Name);
            var others = new System.Collections.Generic.List<string>();
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal) { GameNames.Flat(choice.Name) };
            foreach (var name in names)
            {
                var flat = GameNames.Flat(name);
                if (flat.Length > 0 && seen.Add(flat)) others.Add(name);
            }

            if (others.Count == choice.OtherNames.Count && System.Linq.Enumerable.SequenceEqual(others, choice.OtherNames))
                return null;

            return new GameChoice(choice.Source, choice.Id, choice.Name, others);
        }

        private static string Invariant(long value) =>
            value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
