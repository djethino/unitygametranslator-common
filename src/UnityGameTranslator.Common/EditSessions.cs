namespace UnityGameTranslator.Common
{
    /// <summary>What is happening to an edit session, in the words every product uses.</summary>
    public enum EditSessionStage
    {
        /// <summary>Uploading the file and asking the site for a page.</summary>
        Opening,

        /// <summary>Open, nothing has come back from the browser yet.</summary>
        Waiting,

        /// <summary>A save arrived and was written into the game.</summary>
        Applied,

        /// <summary>A save arrived and could not be written.</summary>
        Failed,

        /// <summary>Over: the page was closed, the site dropped it, or we ended it.</summary>
        Finished,
    }

    /// <summary>
    /// The rules of a browser edit session — the durations, the states and the words.
    ///
    /// 🔴 **Written because the two clients did not agree.** The mod kept a session alive every ten
    /// minutes and waited ninety seconds before calling a departure real; the manager did the same
    /// two things at five minutes and forty-five seconds. Nobody chose that: each was written on its
    /// own day and neither knew the other existed. Two answers to one question is not a preference,
    /// it is a bug waiting for the shorter one to be right.
    ///
    /// 🔴 **And the numbers belong to the SERVER, which is what nobody was looking at.** A session
    /// starts with fifteen minutes to live (`EditSessionToken::INITIAL_TTL_MINUTES`); any sign of
    /// life pushes the expiry back. A keepalive every ten minutes therefore left five minutes of
    /// margin on a fresh session — it works, and it is one server-side change away from not
    /// working, silently, for everybody mid-edit.
    ///
    /// ⚠ **PHP cannot consume this library**, so the site's TTL exists twice by necessity — as the
    /// badge words do. What is written below is a MIRROR of the server's number, not a second
    /// opinion: if `INITIAL_TTL_MINUTES` moves, this moves, and the checks pin the relationship
    /// rather than the value.
    /// </summary>
    public static class EditSessions
    {
        /// <summary>
        /// What the server gives a fresh session, in minutes — mirrored from
        /// `EditSessionToken::INITIAL_TTL_MINUTES`.
        /// </summary>
        public const int ServerInitialTtlMinutes = 15;

        /// <summary>
        /// How often to tell the site the session is still wanted, in seconds.
        ///
        /// ⚠ **Derived, not chosen**: a third of what the server grants, so two keepalives can be
        /// lost — a hiccup, a suspended laptop, a slow answer — before a session anybody is still
        /// working in disappears. Picking a number and hoping is what produced ten minutes against
        /// a fifteen-minute life.
        /// </summary>
        public const int KeepAliveSeconds = ServerInitialTtlMinutes * 60 / 3;

        /// <summary>
        /// How long to wait, in seconds, before believing the browser has really gone.
        ///
        /// ⚠ The site announces a departure for a page RELOAD as well as for a real close, so this
        /// is the time given to come back. The two clients disagreed at 45 and 90; the longer one
        /// wins, and the reason is that the two mistakes do not cost the same. Too short ends a
        /// session somebody is still working in — their next save has nowhere to land. Too long
        /// holds a slot on the site for another minute. One of those is somebody's work.
        /// </summary>
        public const int BrowserGraceSeconds = 90;

        /// <summary>
        /// How often to ask the site what changed, in seconds, when watching by polling rather than
        /// by a stream.
        ///
        /// ⚠ Only the manager polls, and that is a deliberate difference rather than an oversight:
        /// it works on a game that is CLOSED, where three seconds are invisible, and a stateless
        /// request repairs itself where a dropped stream has to be reconnected. The mod is inside a
        /// running game and applies a correction while somebody plays, so it streams. The transport
        /// differs; everything on this page does not.
        /// </summary>
        public const int PollSeconds = 3;

        /// <summary>What to say about a session, identically wherever it is said.</summary>
        public static string Describe(EditSessionStage stage, int applied)
        {
            switch (stage)
            {
                case EditSessionStage.Applied:
                    return applied == 1
                        ? "A change from the browser was written into the game."
                        : applied + " changes from the browser were written into the game.";

                case EditSessionStage.Failed:
                    return "A change arrived from the browser and could not be written.";

                case EditSessionStage.Finished:
                    return "The browser session has ended.";

                case EditSessionStage.Opening:
                    return "Opening the editor…";

                default:
                    return "Waiting for changes from the browser…";
            }
        }
    }
}
