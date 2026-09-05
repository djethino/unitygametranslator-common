namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Which of the games the site offers is the one in front of the person, before a translation
    /// is published under its name.
    ///
    /// 🔴 **A first publication names its game, and that name is what every other machine will
    /// search with.** The mod has asked the server since the start — by Steam id, then by name —
    /// and let the person pick from the answers; the Manager sent whatever the folder said and let
    /// the server create a game around it. A game created from a repack's folder name or a
    /// product called "Game" is a translation nobody else ever finds.
    ///
    /// ⚠ The site ranks nothing here: it returns what it knows (its own catalogue first, then the
    /// stores and the game databases) and the client puts the likeliest first. That order is the
    /// same decision in every product, so it is taken once. The figures were the mod's
    /// (UploadSetupPanel, 2026-09-05) and are carried over unchanged.
    /// </summary>
    public static class GameCandidates
    {
        /// <summary>From here a candidate is marked as the best match (★).</summary>
        public const int BestMatch = 50;

        /// <summary>From here a candidate is marked as a likely one (☆).</summary>
        public const int LikelyMatch = 20;

        /// <summary>The site's own catalogue — a game that already carries translations.</summary>
        public const string CatalogueSource = "local";

        /// <summary>
        /// How likely one answer from the site is to be the detected game. Higher is likelier.
        /// </summary>
        /// <param name="candidateSteamId">The answer's Steam id, when it has one.</param>
        /// <param name="candidateName">The answer's display name.</param>
        /// <param name="candidateSource">Where the site found it: "local", "steam", "igdb", "rawg".</param>
        /// <param name="detectedSteamId">The Steam id read on this machine, when there is one.</param>
        /// <param name="detectedName">The name read on this machine.</param>
        public static int Confidence(string? candidateSteamId, string? candidateName, string? candidateSource,
                                     string? detectedSteamId, string? detectedName)
        {
            int score = 0;

            // The strongest fact there is: the same store id on both sides.
            if (!string.IsNullOrEmpty(candidateSteamId) && !string.IsNullOrEmpty(detectedSteamId)
                && string.Equals(candidateSteamId, detectedSteamId, System.StringComparison.Ordinal))
            {
                score += 50;
            }

            // A game the site already holds translations for outranks one it only heard of.
            if (string.Equals(candidateSource, CatalogueSource, System.StringComparison.OrdinalIgnoreCase))
                score += 30;
            else if (string.Equals(candidateSource, "steam", System.StringComparison.OrdinalIgnoreCase))
                score += 20;

            if (!string.IsNullOrEmpty(detectedName) && !string.IsNullOrEmpty(candidateName))
            {
                string detected = detectedName!.Trim();
                string name = candidateName!.Trim();

                if (string.Equals(detected, name, System.StringComparison.OrdinalIgnoreCase))
                    score += 20;
                else if (name.IndexOf(detected, System.StringComparison.OrdinalIgnoreCase) >= 0
                         || detected.IndexOf(name, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 5;
            }

            return score;
        }

        /// <summary>The mark beside a candidate: ★ for the best match, ☆ for a likely one, nothing otherwise.</summary>
        public static string Mark(int confidence) =>
            confidence >= BestMatch ? "★" : confidence >= LikelyMatch ? "☆" : "";

        /// <summary>
        /// Where an answer came from, as a tag a person can read: the site's own catalogue is
        /// "catalog", every other source keeps its name.
        /// </summary>
        public static string SourceLabel(string? source)
        {
            if (string.IsNullOrEmpty(source)) return "";

            return string.Equals(source, CatalogueSource, System.StringComparison.OrdinalIgnoreCase)
                ? "catalog"
                : source!.ToLowerInvariant();
        }

        /// <summary>One candidate as a list row: name, its source in brackets, its mark.</summary>
        public static string Row(string? name, string? source, int confidence)
        {
            string label = SourceLabel(source);
            string mark = Mark(confidence);

            string row = name ?? "";
            if (label.Length > 0) row += " [" + label + "]";
            if (mark.Length > 0) row += " " + mark;
            return row;
        }

        /// <summary>What the marks mean, under the list — the mod's own line.</summary>
        public const string Legend =
            "Pick the matching game. ★ = best match • [catalog] = already known here, other tags = external game databases";
    }
}
