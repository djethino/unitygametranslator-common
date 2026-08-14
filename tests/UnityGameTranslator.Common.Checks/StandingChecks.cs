using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The four questions a screen answers about a translation, and who may write.
    ///
    /// ⚠ Most of these pin cases a well-meaning tidy-up would break: refusing the anonymous person
    /// a merge because "merging is a sync feature", or letting the manager write into a game set up
    /// by somebody else because "it is only the local file".
    /// </summary>
    internal static class StandingChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // ── Writing the local file ────────────────────────────────────────
            check(Standings.MayWriteLocally(AccountStanding.Anonymous),
                "somebody with no account may change their own copy",
                "they can diverge exactly like a branch, and merging writes nothing but the file here");

            check(Standings.MayWriteLocally(AccountStanding.Ours),
                "and so may the account the game uses", "the ordinary case");

            check(!Standings.MayWriteLocally(AccountStanding.SomebodyElses),
                "but not a game set up under another account",
                "one must not break, by inattention, what another user of this computer put in place");

            // ── Writing to the server ─────────────────────────────────────────
            check(!Standings.MayWriteToServer(AccountStanding.Anonymous),
                "publishing needs a name", "it becomes visible and it carries yours");

            check(Standings.MayWriteToServer(AccountStanding.Ours),
                "the game's own account may publish", "that is what being signed in is for");

            check(!Standings.MayWriteToServer(AccountStanding.SomebodyElses),
                "and another account may not publish for this game",
                "it would file the work under a name nobody chose");

            // ── Every refusal says how to get out of it ───────────────────────
            check(Standings.ExplainRefusal(AccountStanding.SomebodyElses, toServer: false)
                      .IndexOf("open the game", StringComparison.OrdinalIgnoreCase) >= 0,
                "the wrong-account refusal names where the account is changed",
                "'not your account' on its own leaves somebody stuck");

            check(Standings.ExplainRefusal(AccountStanding.Anonymous, toServer: true)
                      .IndexOf("no account", StringComparison.OrdinalIgnoreCase) >= 0,
                "and the sign-in refusal says what still works without one",
                "otherwise it reads as 'you can do nothing', which is false");

            check(Standings.ExplainRefusal(AccountStanding.Anonymous, toServer: false).Length == 0
                  && Standings.ExplainRefusal(AccountStanding.Ours, toServer: true).Length == 0,
                "nothing refused, nothing explained", "a reason shown beside a live button is noise");

            // ── The roles keep the project's words ────────────────────────────
            //
            // 🔴 There is no "Contributor" among them, and there must never be one. Becoming a
            // Branch IS contributing; a second word for it would put two names on one thing across
            // three products. Where the word is useful it goes beside the other — "Contribute
            // (branch)" — never alone.
            foreach (LineageRole role in Enum.GetValues(typeof(LineageRole)))
            {
                check(role.ToString().IndexOf("Contributor", StringComparison.OrdinalIgnoreCase) < 0,
                    $"{role} is not named 'contributor'",
                    "Main, Branch and Fork are the vocabulary, everywhere, in every language");
            }
        }
    }
}
