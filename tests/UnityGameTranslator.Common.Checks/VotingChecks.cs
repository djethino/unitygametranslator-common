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
