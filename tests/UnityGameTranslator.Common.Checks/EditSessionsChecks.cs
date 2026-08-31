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

            // ── How the holder is written down ────────────────────────────────
            // 🔴 Three writers now put this fact in a file or a request body: the mod's marker,
            // the manager's marker, and the request that opens a session. They each had their own
            // copy of the spelling before this existed.
            foreach (EditSessions.EditSessionHolder holder in
                     Enum.GetValues(typeof(EditSessions.EditSessionHolder)))
            {
                check(EditSessions.ParseHolder(EditSessions.Serialize(holder)) == holder,
                    $"{holder} survives being written down and read back",
                    "a holder that changes on the way through is a session changing owner");
            }

            check(EditSessions.Serialize(EditSessions.EditSessionHolder.Game)
                  != EditSessions.Serialize(EditSessions.EditSessionHolder.Manager),
                "and the two are not written the same",
                "one spelling for both would make the field say nothing at all");

            // ⚠ Markers already on disk were written with .ToString() — "Game" and "Manager".
            // Reading must stay case-insensitive or every one of them changes owner on upgrade.
            check(EditSessions.ParseHolder("Manager") == EditSessions.EditSessionHolder.Manager
                  && EditSessions.ParseHolder("MANAGER") == EditSessions.EditSessionHolder.Manager
                  && EditSessions.ParseHolder("Game") == EditSessions.EditSessionHolder.Game,
                "the spellings already written on disk still read correctly",
                "markers written before this existed must not change hands on an update");

            // 🔴 Absent is not unknown — it is a mod or a manager published before the field
            // existed, and it must be given a session rather than refused over a label.
            check(EditSessions.ParseHolder(null) == EditSessions.EditSessionHolder.Game
                  && EditSessions.ParseHolder("") == EditSessions.EditSessionHolder.Game
                  && EditSessions.ParseHolder("  ") == EditSessions.EditSessionHolder.Game
                  && EditSessions.ParseHolder("gestionnaire") == EditSessions.EditSessionHolder.Game,
                "anything unrecognised reads as the game",
                "a client that says nothing predates the field; refusing it would break what shipped");

            // ── Presence, when it is shown by polling ─────────────────────────
            check(EditSessions.PresenceTtlSeconds >= EditSessions.PollSeconds * 3,
                "several polls can be lost before anybody is called absent",
                "one dropped request must not put out a light that says work is arriving");

            // 🔴 The bound that matters, and it is about somebody's work rather than about a
            // number: what the page calls "Saved" is not "applied", so the absence has to show
            // before the next save — seconds, not minutes.
            check(EditSessions.PresenceTtlSeconds < EditSessions.KeepAliveSeconds,
                "absence shows far sooner than the session's own renewal",
                "learning after a keepalive that nobody was listening is learning after the saves");

            // ⚠ The editor page refreshes its own view on a cycle of its own. A presence that
            // expires between two of its readings makes the light blink for no reason.
            check(EditSessions.PresenceTtlSeconds >= 10,
                "and it outlives one refresh of the page that displays it",
                "a light that flickers is a light nobody reads");

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

            // ⚠ The marker is a file anybody with an account on the machine can write, so what
            // comes out of it is data. Mirrors the site's own Str::random(64) check.
            check(EditSessions.IsPlausibleKey(new string('a', 64))
                  && EditSessions.IsPlausibleKey("0123456789" + new string('Z', 54)),
                "a 64-character alphanumeric key is accepted",
                "that is exactly what the site issues");

            check(!EditSessions.IsPlausibleKey(new string('a', 63))
                  && !EditSessions.IsPlausibleKey(new string('a', 65))
                  && !EditSessions.IsPlausibleKey("")
                  && !EditSessions.IsPlausibleKey(null),
                "and nothing of another length is",
                "a key is a fixed shape; anything else was not issued by the site");

            check(!EditSessions.IsPlausibleKey(new string('a', 60) + "/../")
                  && !EditSessions.IsPlausibleKey(new string('a', 63) + " ")
                  && !EditSessions.IsPlausibleKey(new string('a', 63) + "é"),
                "nor anything carrying a character a key never has",
                "the value ends up in a URL, and a planted marker must not decide what it says");
        }
    }
}
