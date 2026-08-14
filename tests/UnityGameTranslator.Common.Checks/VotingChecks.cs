using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Who may rate a translation.
    ///
    /// ⚠ Three of these four refusals are the SERVER's, restated on the client so the arrows are
    /// never drawn for a request that would come back 403. If the server's rule changes, these
    /// cases are what should fail first.
    /// </summary>
    internal static class VotingChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            check(Voting.Rating(signedIn: true, published: true, isYourOwn: false, hasUsedIt: true)
                  == RateBlock.None,
                "somebody signed in who has played somebody else's published translation may rate it",
                "that is the whole case the arrows exist for");

            check(Voting.Rating(signedIn: false, published: true, isYourOwn: false, hasUsedIt: true)
                  == RateBlock.SignedOut,
                "signed out, the arrows are not drawn", "a vote has to belong to someone");

            check(Voting.Rating(signedIn: true, published: true, isYourOwn: true, hasUsedIt: true)
                  == RateBlock.YourOwn,
                "nobody rates their own translation",
                "the server refuses it too — drawing the arrow would promise a 403");

            check(Voting.Rating(signedIn: true, published: true, isYourOwn: false, hasUsedIt: false)
                  == RateBlock.NotUsedYet,
                "and nobody rates what they have not run",
                "a vote cast on something never played measures nothing");

            // ⚠ Order matters. Nothing published wins over every other reason: telling somebody to
            // sign in so they can rate something that does not exist sends them down a dead end.
            check(Voting.Rating(signedIn: false, published: false, isYourOwn: false, hasUsedIt: false)
                  == RateBlock.NothingPublished,
                "with nothing published, that is the reason given",
                "not 'sign in', which would be an invitation to nowhere");

            // 🔴 THE case that is easy to get wrong by being over-careful. Holding a branch of
            // somebody's lineage does NOT make their Main yours: the server checks the owner of
            // the translation being rated, and that is them. Refusing here would silence the
            // people who have worked with it most.
            check(Voting.Rating(signedIn: true, published: true, isYourOwn: false, hasUsedIt: true)
                  == RateBlock.None,
                "a branch author may rate the Main it contributes to",
                "the Main is public and belongs to somebody else — the server allows it");

            // ── The picture, which has to be the same in three products ───────
            check(Voting.CountLabel(3) == "+3" && Voting.CountLabel(-2) == "-2",
                "a positive count carries its sign", "it is what says the reception was good");

            check(Voting.CountLabel(0) == "0",
                "and zero does NOT", "'+0' reads as a positive vote when it is the absence of any");

            check(Voting.CountTone(5) == BadgeTone.Good && Voting.CountTone(-5) == BadgeTone.Wrong
                  && Voting.CountTone(0) == BadgeTone.Quiet,
                "the count is coloured by its sign", "same three as the website's");

            // ⚠ The one signal saying "you already voted". A filled arrow, nothing else.
            check(Voting.ArrowTone(1, 1) == BadgeTone.Good,
                "your own up-vote fills the up arrow", "that IS how somebody sees they have voted");
            check(Voting.ArrowTone(-1, -1) == BadgeTone.Wrong,
                "and your down-vote fills the down one", "same picture, other end");
            check(Voting.ArrowTone(1, -1) == BadgeTone.Plain && Voting.ArrowTone(1, null) == BadgeTone.Plain,
                "an arrow you did not choose stays quiet",
                "two lit arrows would say you voted twice, which nobody can");

            check(Voting.ArrowTip(1, 1).IndexOf("withdraw", StringComparison.Ordinal) >= 0,
                "clicking the arrow you chose withdraws the vote",
                "that is the server's behaviour, and nothing else on screen says it");

            foreach (RateBlock block in Enum.GetValues(typeof(RateBlock)))
            {
                bool spoken = Voting.Explain(block).Length > 0;

                check(block == RateBlock.None ? !spoken : spoken,
                    block == RateBlock.None
                        ? "no refusal, nothing to explain"
                        : $"a {block} refusal says why",
                    "a dead arrow with no reason is what this replaces");
            }
        }
    }
}
