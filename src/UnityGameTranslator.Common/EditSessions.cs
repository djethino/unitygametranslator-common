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

        // ── The marker: how two programs on one machine avoid editing the same file ──────────

        /// <summary>
        /// Suffix of the file that says a session is open, appended to the translation's own name.
        ///
        /// ⚠ **Appended, not a name of its own**, and that is load-bearing twice over. It ties the
        /// marker to the translation it belongs to, so a machine with five modded games has five
        /// independent markers and editing two games at once stays possible — which the site was
        /// built for, up to twelve at a time. And it makes the manager's uninstall sweep pick it up
        /// for free: that sweep files everything starting with the translation's name under
        /// "Translation", so no list of names has to be kept in step with this.
        /// </summary>
        public const string MarkerSuffix = ".editsession";

        /// <summary>Field names inside the marker. Written here so two writers cannot drift.</summary>
        public const string MarkerHolderField = "holder";
        public const string MarkerKeyField = "mod_key";
        public const string MarkerOpenedField = "opened_at";

        /// <summary>
        /// Which program opened the session.
        ///
        /// ⚠ Stored rather than deduced. A marker with no name would let each product assume the
        /// other wrote it, and the one case that matters — the manager finding ITS OWN session after
        /// a crash — is exactly the one that assumption gets wrong.
        /// </summary>
        public enum EditSessionHolder
        {
            /// <summary>The mod, from inside the game.</summary>
            Game,

            /// <summary>The manager, with the game closed.</summary>
            Manager,
        }

        /// <summary>How a holder is named to somebody, in either product.</summary>
        public static string HolderName(EditSessionHolder holder)
        {
            return holder == EditSessionHolder.Manager ? "the manager" : "the game";
        }

        /// <summary>
        /// What to ask before taking over a session somebody else's window left open.
        ///
        /// ⚠ **Ask, never decide.** Both sessions hold a copy of the file taken when they opened,
        /// and each one saves the whole thing back: ending the other silently would throw away work
        /// somebody may be typing at that moment. The question names WHO and WHEN because those are
        /// the two facts that let somebody answer it.
        /// </summary>
        /// <param name="other">The program holding the session — never the one asking.</param>
        /// <param name="whenText">
        /// When it was opened, already written out. Formatting a time needs the culture and the
        /// clock of the machine, neither of which belongs in a rules library.
        /// </param>
        /// <param name="pendingChanges">
        /// Saves made in the browser that the holder never fetched. ⚠ Those exist NOWHERE else —
        /// until somebody downloads them, the session is the only copy — so the answer says what
        /// becomes of them rather than leaving somebody to guess.
        /// </param>
        public static string ConflictQuestion(EditSessionHolder other, string whenText,
                                              int pendingChanges)
        {
            var question = "A browser editing session for this game is already open, started from "
                         + HolderName(other) + " " + whenText + ". Only one can run at a time, "
                         + "because each one saves the whole translation back and the last to save "
                         + "would erase the other.";

            if (pendingChanges > 0)
            {
                question += pendingChanges == 1
                    ? " One change saved in the browser was never taken into the game; it will be "
                    + "written before the session ends, so nothing is lost."
                    : " " + pendingChanges + " changes saved in the browser were never taken into "
                    + "the game; they will be written before the session ends, so nothing is lost.";
            }

            return question + " End it and open yours?";
        }

        /// <summary>
        /// Whether a marker still means anything, given what the site said about its key.
        ///
        /// 🔴 **The server answers this, never a timeout.** A marker is a pointer; only the site
        /// knows whether the session behind it is alive, and it already says so by refusing an
        /// unknown key. Inventing an age at which a marker "must" be stale would either free a
        /// session somebody is still working in, or block one that died an hour ago — and the file
        /// gives no way to tell those apart.
        ///
        /// ⚠ **Not asked yet is treated as ALIVE**, which is the same way round the site itself
        /// decides whether a game has gone quiet: "an unknown state must behave exactly like a live
        /// one". The two mistakes are not worth the same. Reading silence as "dead" opens a second
        /// session over one somebody is typing in, and the loser is whoever saves first. Reading it
        /// as "alive" costs a question that turns out to be unnecessary — and a session cannot be
        /// opened while the site is unreachable anyway, so nothing is being blocked that would have
        /// worked.
        /// </summary>
        /// <param name="siteStillKnowsTheKey">
        /// What the site answered for the key in the marker. Null when it could not be asked.
        /// </param>
        public static bool MarkerIsLive(bool? siteStillKnowsTheKey)
        {
            return siteStillKnowsTheKey ?? true;
        }
    }
}
