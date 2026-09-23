using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>What became of a retranslation somebody asked for.</summary>
    public enum RetranslateOutcome
    {
        /// <summary>A different translation came back.</summary>
        Replaced,

        /// <summary>The backend kept answering the same thing. Nothing changed.</summary>
        Unchanged,

        /// <summary>Nothing usable came back. The previous translation stands.</summary>
        Failed,
    }

    /// <summary>The verdict of <see cref="Retranslation.Run"/>.</summary>
    public readonly struct RetranslateResult
    {
        public RetranslateResult(RetranslateOutcome outcome, string? value)
        {
            Outcome = outcome;
            Value = value;
        }

        public RetranslateOutcome Outcome { get; }

        /// <summary>The new translation when Replaced; otherwise the previous one (null if there was none).</summary>
        public string? Value { get; }
    }

    /// <summary>
    /// Asking again for a line somebody did not like — same instructions, a different draw.
    ///
    /// ⚠ In the socle since 2026-09-23: the Manager answers the browser editor's Retranslate when
    /// the game is closed, and must reach the same verdicts the game would.
    /// </summary>
    public static class Retranslation
    {
        /// <summary>
        /// How many times to ask. A translation service answers the same text the same way, so it
        /// is asked once; a model is asked up to the attempts the settings allow.
        /// </summary>
        public static int Rounds(bool translationService, int attemptsAllowed) =>
            translationService ? 1 : LineTranslation.ClampAttempts(attemptsAllowed);

        /// <summary>
        /// The seed for one round. A configured seed is offset by the round, never used as-is: a
        /// single fixed seed would redraw the very answer being rejected, every round, forever.
        /// Left unset, each round draws its own — variation without reproducibility.
        /// </summary>
        public static int SeedFor(int? configured, int round, Func<int> draw) =>
            configured.HasValue ? unchecked(configured.Value + round) : draw();

        /// <summary>
        /// Ask up to <paramref name="rounds"/> times until the answer differs from the one rejected.
        /// </summary>
        /// <param name="key">The line as keyed, placeholders included.</param>
        /// <param name="hadEntry">Whether a translation existed — without one, any answer is new.</param>
        /// <param name="previous">The translation being rejected.</param>
        /// <param name="ask">One round: its index in, the restored translation out (null when nothing came back).</param>
        public static RetranslateResult Run(string key, bool hadEntry, string? previous, int rounds,
                                            Func<int, string?> ask)
        {
            bool sameAnswerAgain = false;

            for (int round = 0; round < rounds; round++)
            {
                string? candidate = ask(round);
                if (string.IsNullOrEmpty(candidate)) continue;

                // A refusal must never replace an existing translation: stored as kept-as-is, it
                // would swap the line for its own source text — a loss dressed up as a decision.
                if (Answers.Read(candidate) != AnswerKind.Translation) continue;

                if (Placeholders.Invented(key, candidate!).Count > 0) continue;

                if (hadEntry && string.Equals(candidate, previous, StringComparison.Ordinal))
                {
                    sameAnswerAgain = true;
                    continue;
                }

                return new RetranslateResult(RetranslateOutcome.Replaced, candidate);
            }

            return new RetranslateResult(sameAnswerAgain ? RetranslateOutcome.Unchanged : RetranslateOutcome.Failed,
                                         previous);
        }

        /// <summary>How an outcome is spelt on the wire (<c>POST …/retranslation</c>, field <c>outcome</c>).</summary>
        public static string Word(RetranslateOutcome outcome)
        {
            switch (outcome)
            {
                case RetranslateOutcome.Replaced: return "replaced";
                case RetranslateOutcome.Unchanged: return "unchanged";
                default: return "failed";
            }
        }
    }

    /// <summary>Whether a request from the browser editor is answered, and if not, why.</summary>
    public enum PageRequestVerdict
    {
        /// <summary>Translate it and send the answer back.</summary>
        Answer,

        /// <summary>The page asked again with the same id (it re-emits while waiting). Already handled.</summary>
        Duplicate,

        /// <summary>A retranslation of this line is already on its way.</summary>
        AlreadyPending,

        /// <summary>No line named, or a metadata key (they start with '_'), which is never content.</summary>
        NotALine,

        /// <summary>Translation is switched off in this game's settings.</summary>
        TranslationOff,

        /// <summary>The line is not in the translation file being edited.</summary>
        NotInFile,
    }

    /// <summary>
    /// What the holder of a browser edit session keeps of the page's per-line requests: which ids
    /// it has seen, and which request each line in flight answers.
    ///
    /// 🔴 **The guards on an AI call a web page can trigger**, and why they are one class shared by
    /// the mod and the Manager rather than two copies (2026-09-23). The request comes from whoever
    /// holds the page; the call runs on this machine with this machine's backend — possibly a paid
    /// key. So:
    /// <list type="bullet">
    /// <item>🔴 the line must be IN THE FILE. Without that, the page could have any text of ten
    /// thousand characters translated: the key turned into a free proxy, and a way in for prompt
    /// injection. With it, the model only ever sees what the game itself would have sent;</item>
    /// <item>the same id is answered once — the page re-emits every ~30 s while it waits;</item>
    /// <item>one request per line in flight — repeated clicks do not multiply calls;</item>
    /// <item>nothing is asked when translation is off in the game's settings.</item>
    /// </list>
    /// The answer is always a PROPOSAL: the page stages it for its own Save, nothing is written.
    ///
    /// Thread-safe: requests arrive on a network thread and answers leave from a worker.
    /// </summary>
    public sealed class PageRetranslations
    {
        /// <summary>How many recent ids are remembered — enough to cover a page re-emitting its waiting rows.</summary>
        public const int RememberedIds = 32;

        private readonly object _lock = new object();
        private readonly Queue<string> _seenIds = new Queue<string>();
        private readonly Dictionary<string, string> _idByKey = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>
        /// Decide on one request, and when the verdict is <see cref="PageRequestVerdict.Answer"/>,
        /// remember it until <see cref="Settle"/> or <see cref="Forget"/>.
        /// </summary>
        /// <param name="id">The page's request id — stable across its re-emissions.</param>
        /// <param name="key">The line asked for.</param>
        /// <param name="translationEnabled">See <see cref="LineTranslation.IsEnabled"/>.</param>
        /// <param name="fileHasLine">Whether the translation file holds this line.</param>
        public PageRequestVerdict Admit(string? id, string? key, bool translationEnabled, Func<string, bool> fileHasLine)
        {
            if (string.IsNullOrEmpty(key) || key![0] == '_') return PageRequestVerdict.NotALine;

            lock (_lock)
            {
                if (!string.IsNullOrEmpty(id))
                {
                    if (_seenIds.Contains(id!)) return PageRequestVerdict.Duplicate;
                    _seenIds.Enqueue(id!);
                    while (_seenIds.Count > RememberedIds) _seenIds.Dequeue();
                }

                if (!translationEnabled) return PageRequestVerdict.TranslationOff;
                if (!fileHasLine(key)) return PageRequestVerdict.NotInFile;
                if (_idByKey.ContainsKey(key)) return PageRequestVerdict.AlreadyPending;

                _idByKey[key] = id ?? "";
                return PageRequestVerdict.Answer;
            }
        }

        /// <summary>
        /// A retranslation of this line has ended: the id the page is waiting on, or null when the
        /// line was not asked for by the page (the in-game editor asks too).
        /// </summary>
        public string? Settle(string key)
        {
            lock (_lock)
            {
                if (!_idByKey.TryGetValue(key, out var id)) return null;
                _idByKey.Remove(key);
                return id;
            }
        }

        /// <summary>The request could not even be submitted: forget it, nothing will answer.</summary>
        public void Forget(string key)
        {
            lock (_lock) { _idByKey.Remove(key); }
        }

        /// <summary>The session ended.</summary>
        public void Clear()
        {
            lock (_lock)
            {
                _seenIds.Clear();
                _idByKey.Clear();
            }
        }
    }
}
