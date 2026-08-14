using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The rules of a browser edit session.
    ///
    /// ⚠ These pin RELATIONSHIPS, not values. The numbers may all move — the server may grant more
    /// or less time — and what must survive is that a keepalive comfortably outruns the expiry and
    /// that a page reload is not mistaken for somebody leaving. Pinning 300 and 90 would only prove
    /// that nobody had edited the file.
    /// </summary>
    internal static class EditSessionsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            int ttl = EditSessions.ServerInitialTtlMinutes * 60;

            // 🔴 THE case. The mod kept a session alive every ten minutes against a fifteen-minute
            // life: it worked, with one lost keepalive between it and a session vanishing under
            // somebody mid-edit.
            check(EditSessions.KeepAliveSeconds * 2 < ttl,
                "two keepalives can be lost without losing the session",
                "a hiccup or a suspended laptop must not end somebody's work");

            check(EditSessions.KeepAliveSeconds > 0 && EditSessions.KeepAliveSeconds < ttl,
                "and a keepalive is sent well within the life it renews",
                "sending it after the expiry renews nothing at all");

            // ⚠ A reload announces a departure exactly as a real close does.
            check(EditSessions.BrowserGraceSeconds >= 60,
                "a page reload has time to come back before the session is given up",
                "ending too early leaves somebody's next save with nowhere to land");

            check(EditSessions.BrowserGraceSeconds < EditSessions.KeepAliveSeconds,
                "but a departure is settled long before the next keepalive",
                "otherwise a session already abandoned is kept alive once more for nothing");

            check(EditSessions.PollSeconds > 0 && EditSessions.PollSeconds <= 10,
                "polling is often enough to feel immediate and rare enough to be free",
                "it runs against somebody else's server, on a game that is closed");

            // ── The words ─────────────────────────────────────────────────────
            foreach (EditSessionStage stage in Enum.GetValues(typeof(EditSessionStage)))
            {
                check(EditSessions.Describe(stage, 1).Length > 0,
                    $"{stage} is said in words", "a state nobody can read is a state nobody has");
            }

            check(EditSessions.Describe(EditSessionStage.Applied, 1)
                  != EditSessions.Describe(EditSessionStage.Applied, 3),
                "one change and three changes do not read the same",
                "a count that never changes is a count nobody trusts");

            // ── The marker ────────────────────────────────────────────────────
            // 🔴 The one that matters: silence must not be read as "nobody is editing".
            check(EditSessions.MarkerIsLive(null),
                "a marker the site could not be asked about counts as live",
                "reading silence as dead opens a second session over somebody's work");

            check(EditSessions.MarkerIsLive(true) && !EditSessions.MarkerIsLive(false),
                "and the site's answer is followed when there is one",
                "a marker whose session the site has forgotten must not block for ever");

            // ⚠ The suffix is APPENDED to the translation's name. Two things depend on that: one
            // marker per game (so two games stay editable at once), and the manager's uninstall
            // sweep picking it up by prefix instead of by a list somebody has to maintain.
            check(EditSessions.MarkerSuffix.StartsWith(".", StringComparison.Ordinal)
                  && EditSessions.MarkerSuffix.Length > 1,
                "the marker is a suffix, not a file name of its own",
                "a fixed name would be one marker for the whole machine, and invisible to the sweep");

            check(EditSessions.MarkerHolderField != EditSessions.MarkerKeyField
                  && EditSessions.MarkerKeyField != EditSessions.MarkerOpenedField
                  && EditSessions.MarkerHolderField != EditSessions.MarkerOpenedField,
                "its three fields are named apart",
                "two writers reading one name for two things is how a marker starts lying");

            check(EditSessions.HolderName(EditSessions.EditSessionHolder.Game)
                  != EditSessions.HolderName(EditSessions.EditSessionHolder.Manager),
                "the game and the manager are named differently",
                "the question is only answerable if it says WHO is holding the session");

            foreach (EditSessions.EditSessionHolder holder in
                     Enum.GetValues(typeof(EditSessions.EditSessionHolder)))
            {
                var question = EditSessions.ConflictQuestion(holder, "at 14:32", 0);
                check(question.Contains(EditSessions.HolderName(holder)) && question.Contains("14:32"),
                    $"the question about {holder} names it and when it started",
                    "'a session is already open' with neither is not a question anybody can answer");
            }

            // 🔴 Saves the browser made and nobody fetched exist in the session and NOWHERE else.
            var withWork = EditSessions.ConflictQuestion(EditSessions.EditSessionHolder.Manager,
                                                         "at 14:32", 3);
            check(withWork.Contains("3") && withWork.Length
                  > EditSessions.ConflictQuestion(EditSessions.EditSessionHolder.Manager, "at 14:32", 0).Length,
                "unfetched saves are counted in the question",
                "ending a session silently drops work the browser said was saved");

            check(EditSessions.ConflictQuestion(EditSessions.EditSessionHolder.Game, "now", 1)
                  != EditSessions.ConflictQuestion(EditSessions.EditSessionHolder.Game, "now", 2),
                "one and several do not read the same",
                "a sentence that does not agree with its number reads as a machine talking");
        }
    }
}
