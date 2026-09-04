using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Which of the games a name search brought back is the one that was asked about.
    ///
    /// 🔴 **The defect this answers, and it could WRITE A FILE INTO A GAME.** A game bought outside
    /// Steam has no id, so it is looked up by NAME — and the site matches loosely (`name LIKE %…%`)
    /// on purpose, because a game folder is not always named as the site names it: "Foo" against
    /// "Foo: Deluxe Edition". One request therefore describes several games, and neither the mod nor
    /// the Manager filtered the answer back onto the game they had asked about. Everything that came
    /// back was counted as this game's — "N translations are published for this game" — and one of
    /// them could be offered for install.
    ///
    /// The rule, in order:
    ///
    ///  1. **An exact name wins.** Case and surrounding spaces ignored, nothing else: a game called
    ///     exactly what we asked for is not a coincidence.
    ///  2. **A single candidate wins.** "Foo" resolving only to "Foo: Deluxe Edition" is the
    ///     ordinary case loose matching exists for, and dropping it would lose translations that
    ///     genuinely belong to this game.
    ///  3. **Several loose candidates and none exact: keep them all, and SAY SO.** Picking one would
    ///     repeat the old fault with better odds; dropping them would hide work that may well be
    ///     this game's. What changes is that the caller is told, instead of being handed a pile.
    ///
    /// ⚠ **In the socle because both products face it.** The mod resolves its own game the same way
    /// the Manager resolves fifty; a rule answered twice is a rule that ends up answered differently,
    /// and here the disagreement would be about which game a file gets written into.
    /// </summary>
    public static class GameNames
    {
        /// <summary>What was kept, and whether it still describes more than one game.</summary>
        public sealed class Match
        {
            public Match(IReadOnlyList<int> chosen, bool ambiguous)
            {
                Chosen = chosen;
                Ambiguous = ambiguous;
            }

            /// <summary>Indices into the candidates, in the order they were given.</summary>
            public IReadOnlyList<int> Chosen { get; }

            /// <summary>
            /// Several games match loosely and none exactly. Nothing was dropped — a caller must
            /// simply not present the result as being about one game.
            /// </summary>
            public bool Ambiguous { get; }
        }

        /// <param name="candidates">The names that came back, in the server's order.</param>
        /// <param name="asked">
        /// The name searched with, or null when the game was found by Steam id — an id is exact, so
        /// whatever came back is the right game and there is nothing to choose between.
        /// </param>
        public static Match Which(IReadOnlyList<string> candidates, string asked)
        {
            var all = new List<int>();
            for (var i = 0; i < candidates.Count; i++) all.Add(i);

            if (candidates.Count <= 1) return new Match(all, false);

            if (!string.IsNullOrWhiteSpace(asked))
            {
                var wanted = asked.Trim();
                var exact = new List<int>();

                for (var i = 0; i < candidates.Count; i++)
                {
                    var name = candidates[i];
                    if (name == null) continue;

                    if (string.Equals(name.Trim(), wanted, StringComparison.OrdinalIgnoreCase))
                        exact.Add(i);
                }

                if (exact.Count > 0) return new Match(exact, false);
            }

            return new Match(all, true);
        }
    }
}
