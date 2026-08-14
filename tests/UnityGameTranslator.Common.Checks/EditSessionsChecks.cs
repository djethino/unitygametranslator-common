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
        }
    }
}
