using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Which game a name search was actually about.
    ///
    /// 🔴 **The defect these cases hold the door on could WRITE A FILE INTO A GAME.** A game with no
    /// Steam id is looked up by name, the site matches loosely, and one request can describe several
    /// games. Neither product filtered the answer back: everything that came back was counted as
    /// this game's, and one of those translations could be offered for install.
    ///
    /// ⚠ The second case is what makes the rule worth writing rather than obvious: keeping only
    /// exact matches would LOSE "Foo: Deluxe Edition" for a folder called "Foo" — the very situation
    /// loose matching exists for.
    /// </summary>
    internal static class GameNamesChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // A Steam id is exact: whatever came back is the right game, nothing to arbitrate.
            var byId = GameNames.Which(new List<string> { "Cat" }, null);
            check(byId.Chosen.Count == 1 && !byId.Ambiguous,
                "a single answer is kept whatever was asked",
                "nothing to choose between");

            // The ordinary loose match, and the reason loose matching exists at all.
            var widened = GameNames.Which(new List<string> { "Foo: Deluxe Edition" }, "Foo");
            check(widened.Chosen.Count == 1 && !widened.Ambiguous,
                "a single loose candidate is this game",
                "a folder is not always named as the site names it");

            // An exact name wins over its neighbours, and the answer is then not ambiguous.
            var exact = GameNames.Which(new List<string> { "Cattails", "Cat", "Cat Quest II" }, "Cat");
            check(exact.Chosen.Count == 1 && exact.Chosen[0] == 1 && !exact.Ambiguous,
                "an exact name wins",
                "being called exactly that is not a coincidence");

            // Case and stray spaces are not a difference anybody means.
            var casing = GameNames.Which(new List<string> { "Cattails", "  cat " }, "CAT");
            check(casing.Chosen.Count == 1 && casing.Chosen[0] == 1,
                "case and surrounding spaces are not a difference",
                "the same name written differently");

            // 🔴 Several loose candidates, none exact: everything is kept AND the caller is told.
            var muddle = GameNames.Which(new List<string> { "Cattails", "Cat Quest II" }, "Cat ");
            check(muddle.Chosen.Count == 2 && muddle.Ambiguous,
                "an unresolved name keeps everything and says so",
                "never guess, never hide");

            // Two games carrying the same exact name: both kept, and it is not an ambiguity — the
            // name answered, the site simply has two games wearing it.
            var twins = GameNames.Which(new List<string> { "Cat", "Cat" }, "Cat");
            check(twins.Chosen.Count == 2 && !twins.Ambiguous,
                "two exact namesakes are both kept",
                "the name answered; there are two");

            // Nothing came back: nothing to choose, and nothing to warn about.
            var none = GameNames.Which(new List<string>(), "Cat");
            check(none.Chosen.Count == 0 && !none.Ambiguous,
                "no candidate is not an ambiguity",
                "an empty answer is an answer");
        }
    }
}
