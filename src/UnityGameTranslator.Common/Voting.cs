namespace UnityGameTranslator.Common
{
    /// <summary>Why the arrows are not offered. <see cref="RateBlock.None"/> means they are.</summary>
    public enum RateBlock
    {
        /// <summary>They are offered.</summary>
        None,

        /// <summary>Nobody is signed in. A vote has to belong to someone.</summary>
        SignedOut,

        /// <summary>Nothing of this lineage is published, so there is nothing to rate.</summary>
        NothingPublished,

        /// <summary>It is yours. The server refuses this too — it is not only a matter of taste.</summary>
        YourOwn,

        /// <summary>
        /// Nobody here has actually used it yet. The one condition the server cannot see, and the
        /// one that makes a rating mean anything.
        /// </summary>
        NotUsedYet,
    }

    /// <summary>
    /// Who may rate a translation, decided the same way wherever the arrows appear.
    ///
    /// ⚠ **The first three mirror the server** (`Translation::canBeVotedBy`): signed in, published,
    /// and never your own. Drawing live arrows the server will refuse with a 403 is how a product
    /// teaches somebody that its buttons lie.
    ///
    /// 🔴 **The fourth is ours, and it is the point.** A rating is worth something only from
    /// somebody who has run the translation. The mod counts the lines it has actually put on
    /// screen; the manager can only see that the file has met text at some point, which is weaker
    /// and is why it is passed in rather than decided here. What must NOT happen is a screen that
    /// picks between translations nobody has played offering to rate them — a vote cast on a title
    /// card measures nothing.
    /// </summary>
    public static class Voting
    {
        /// <param name="signedIn">An account is signed in.</param>
        /// <param name="published">There is a published translation to rate.</param>
        /// <param name="isYourOwn">The published one belongs to this account.</param>
        /// <param name="hasUsedIt">
        /// This machine has actually run it. The mod counts lines shown this session; the manager
        /// can only tell that the local file has met text at all. Both are honest where they are;
        /// neither is guessed here.
        /// </param>
        public static RateBlock Rating(bool signedIn, bool published, bool isYourOwn, bool hasUsedIt)
        {
            if (!published) return RateBlock.NothingPublished;
            if (!signedIn) return RateBlock.SignedOut;
            if (isYourOwn) return RateBlock.YourOwn;
            if (!hasUsedIt) return RateBlock.NotUsedYet;

            return RateBlock.None;
        }

        /// <summary>
        /// What to say instead of the arrows. Empty when they are shown.
        ///
        /// ⚠ Never "you cannot vote". Each of these is actionable or at least explains itself, and
        /// a dead arrow with no reason is the thing this replaces.
        /// </summary>
        public static string Explain(RateBlock block)
        {
            switch (block)
            {
                case RateBlock.SignedOut:
                    return "Sign in to rate this translation.";
                case RateBlock.NothingPublished:
                    return "Nothing is published for this game yet — there is nothing to rate.";
                case RateBlock.YourOwn:
                    return "You cannot rate your own translation.";
                case RateBlock.NotUsedYet:
                    return "Play with it a little, then rate it.";
                default:
                    return "";
            }
        }
    }
}
