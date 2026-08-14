using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The switch that says which copy an editor is about to change.
    ///
    /// ⚠ It appears in a game, in a browser and in the manager, and somebody who learns it in one
    /// must not have to relearn it in the next. These cases pin what each position means and when
    /// it can be chosen — the part that would otherwise drift into three dialects of one control.
    ///
    /// The stake is plain: choosing the wrong side publishes something somebody meant to keep to
    /// themselves, or leaves untouched something they meant to share.
    /// </summary>
    internal static class EditScopeChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── A browser on its own ──────────────────────────────────────────
            var alone = EditScope.Sides(hasLocalFile: false, canReachMachine: false, signedIn: true,
                                        publishedByThisAccount: true, publishedBySomebodyElse: false);

            check(!Find(alone, EditSide.Local).Available
                  && Find(alone, EditSide.Local).Block == SideBlock.NoMachineHere,
                "a page opened on its own cannot reach this machine",
                "there is no game and no manager on the other end to write the file");
            check(Find(alone, EditSide.Server).Available,
                "but the published version is reachable from anywhere", "it is on the site");
            check(!Find(alone, EditSide.Both).Available,
                "and Both needs both", "one of the two is out of reach, so the pair is");

            // ── A game, signed out ────────────────────────────────────────────
            var anonymous = EditScope.Sides(hasLocalFile: true, canReachMachine: true, signedIn: false,
                                            publishedByThisAccount: false, publishedBySomebodyElse: false);

            check(Find(anonymous, EditSide.Local).Available,
                "translating for yourself needs no account", "the file is on your machine");
            check(Find(anonymous, EditSide.Server).Block == SideBlock.SignedOut,
                "the published side has no meaning without a name", "and says so rather than staying silent");

            // ── Signed in, nothing published yet ──────────────────────────────
            var fresh = EditScope.Sides(hasLocalFile: true, canReachMachine: true, signedIn: true,
                                        publishedByThisAccount: false, publishedBySomebodyElse: false);

            check(Find(fresh, EditSide.Server).Block == SideBlock.NothingPublished,
                "with nothing published there is nothing to change on the server",
                "sending would CREATE something, which is publishing — a separate act");

            // ⚠ Somebody else's lineage is a different refusal from an empty one.
            var theirs = EditScope.Sides(hasLocalFile: true, canReachMachine: true, signedIn: true,
                                         publishedByThisAccount: false, publishedBySomebodyElse: true);

            check(Find(theirs, EditSide.Server).Block == SideBlock.SomebodyElses,
                "somebody else's translation is not ours to rewrite",
                "and that is not the same message as having published nothing");

            // ── Everything in place ───────────────────────────────────────────
            var mine = EditScope.Sides(hasLocalFile: true, canReachMachine: true, signedIn: true,
                                       publishedByThisAccount: true, publishedBySomebodyElse: false);

            check(Find(mine, EditSide.Local).Available && Find(mine, EditSide.Server).Available
                  && Find(mine, EditSide.Both).Available,
                "owning the published version opens all three", "nothing is out of reach");

            // ── Falling back ──────────────────────────────────────────────────
            check(EditScope.Default(mine, EditSide.Server) == EditSide.Server,
                "what was asked for is honoured when it is possible", "no surprise");

            // ⚠ THE case. Asking for the server side where it is impossible must land on the local
            // one, never the reverse.
            check(EditScope.Default(fresh, EditSide.Server) == EditSide.Local,
                "an impossible side falls back to the cautious one",
                "falling the other way would publish something somebody meant to keep");

            check(EditScope.Default(alone, EditSide.Local) == EditSide.Server,
                "and with no machine to reach, the published side is what is left",
                "the fallback is what is possible, not a fixed preference");

            // ── Every refusal says why ────────────────────────────────────────
            foreach (SideBlock block in Enum.GetValues(typeof(SideBlock)))
            {
                if (block == SideBlock.None) continue;

                check(EditScope.Explain(block).Length > 0,
                    $"a {block} block explains itself",
                    "a greyed control with no reason reads as a broken window");
            }

            // ── The words are the same everywhere ─────────────────────────────
            foreach (EditSide side in Enum.GetValues(typeof(EditSide)))
            {
                check(EditScope.Name(side).Length > 0 && EditScope.Effect(side).Length > 0,
                    $"the {side} side is named and its effect stated",
                    "somebody moves this switch to learn what a save means");
            }

            check(EditScope.Effect(EditSide.Local).IndexOf("Nothing is published", StringComparison.Ordinal) >= 0,
                "the local side promises nothing leaves the machine",
                "that promise is the reason somebody picks it");

            // ── The pictures ──────────────────────────────────────────────────
            //
            // ⚠ Pinned as literals rather than compared to one another. The small form of this
            // control carries three marks and NO words: if one silently changed side, the only
            // thing left saying where a button writes would be wrong, and nothing else in any of
            // the three products would notice.
            check(EditScope.Mark(EditSide.Server) == "cloud",
                "the published side is the cloud", "what is published lives away from this machine");
            check(EditScope.Mark(EditSide.Local) == "display",
                "this machine is the screen", "the one in front of whoever is reading");
            check(EditScope.Mark(EditSide.Both) == "link",
                "both is the two linked", "it is the pair, not a third place");
        }

        private static SideStanding Find(SideStanding[] sides, EditSide side)
        {
            foreach (var candidate in sides)
                if (candidate.Side == side) return candidate;

            return new SideStanding { Side = side, Block = SideBlock.None };
        }
    }
}
