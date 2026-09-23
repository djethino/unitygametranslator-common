using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>One turn of a chat request, as an OpenAI-compatible server takes it.</summary>
    public sealed class ChatMessage
    {
        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }

        /// <summary>"system", "user" or "assistant".</summary>
        public string Role { get; }

        public string Content { get; }
    }

    /// <summary>
    /// Send one chat request and return what the model said, or null when nothing usable came back
    /// (the server refused, did not answer, or answered with nothing).
    ///
    /// ⚠ The transport is the caller's: each product has its own HTTP stack, its own negotiation
    /// state and its own way of saying a server is unreachable. What is asked, and what is made of
    /// the answer, is not — that is this file.
    /// </summary>
    public delegate string? ChatSend(IReadOnlyList<ChatMessage> messages, double temperature, int maxTokens, int? seed);

    /// <summary>One answer that was refused, and why — what somebody needs to settle the line by hand.</summary>
    public sealed class FailedAttempt
    {
        public string Value = "";
        public List<string> Errors = new List<string>();
    }

    /// <summary>What became of one line sent to a backend.</summary>
    public enum LineOutcome
    {
        /// <summary>A usable translation came back; <see cref="LineAnswer.Text"/> holds it, restored.</summary>
        Translated,

        /// <summary>The model answered with the skip marker alone: this line is not to be translated.</summary>
        Declined,

        /// <summary>Nothing translatable in the text (empty, or whitespace only). Nobody was asked.</summary>
        NothingToSend,

        /// <summary>Longer than <see cref="Limits.AiTextLength"/>. Nobody was asked.</summary>
        TooLong,

        /// <summary>The request itself failed — no answer to judge. Worth asking again later.</summary>
        NoAnswer,

        /// <summary>The answer carried the skip marker without being it. Discarded.</summary>
        Unusable,

        /// <summary>Every attempt broke a placeholder. <see cref="LineAnswer.Attempts"/> says how.</summary>
        Refused,
    }

    /// <summary>The result of <see cref="LineTranslation.AskModel"/> or <see cref="LineTranslation.CheckServiceAnswer"/>.</summary>
    public sealed class LineAnswer
    {
        public LineOutcome Outcome;

        /// <summary>The translation, restored (markup, line breaks, padding back), when <see cref="Outcome"/> is Translated.</summary>
        public string? Text;

        /// <summary>
        /// The last thing the model said, cleaned but not restored — whatever the outcome. For a
        /// report that shows a refused or unusable answer; never for filing.
        /// </summary>
        public string? Said;

        /// <summary>Every answer refused on the way, in order.</summary>
        public List<FailedAttempt> Attempts = new List<FailedAttempt>();

        /// <summary>How many requests the line cost.</summary>
        public int Requests;

        /// <summary>The accepted answer only passed once its missing trailing line breaks were put back.</summary>
        public bool Repaired;

        /// <summary>The model wrapped its answer (quotes, a preamble, a note) and it had to be cleaned.</summary>
        public bool NeededCleaning;
    }

    /// <summary>
    /// How one line is asked of a model: which instructions, how warm each attempt is, how many.
    ///
    /// ⚠ The mod's settings read into plain values, so the same question can be asked outside a
    /// running game. <see cref="LineTranslation.ClampTemperature"/> and
    /// <see cref="LineTranslation.ClampAttempts"/> turn the raw file values into these.
    /// </summary>
    public sealed class ModelJob
    {
        /// <summary>The system prompt for a text carrying these placeholders, of this kind.</summary>
        public Func<Prompts.Markers, TextType, string> Instructions = (_, __) => "";

        /// <summary>Temperature of the first attempt and of the corrective exchange.</summary>
        public double Temperature;

        /// <summary>Seed of the first attempt, or null to send none.</summary>
        public int? Seed;

        /// <summary>
        /// Floor for the temperature of the last-resort attempt — a fresh request, reinforced, that
        /// must leave the deterministic basin that failed twice.
        /// </summary>
        public double RepairTemperature;

        /// <summary>Seed of every attempt after the first.</summary>
        public int? RepairSeed;

        /// <summary>How many requests the line may cost at most.</summary>
        public int Attempts = Placeholders.MaxAttempts;

        /// <summary>Told before each request, with its index (0 for the first) and the total. Optional.</summary>
        public Action<int, int>? OnAttempt;
    }

    /// <summary>
    /// Asking a backend for one line, and judging what comes back — the part of translation that
    /// is the same whoever asks.
    ///
    /// 🔴 **In the socle because there are now three askers.** The mod translating a game; the
    /// Manager's bench scoring models against the instructions a game sends; and the Manager
    /// answering the browser editor's Retranslate while the game is closed (2026-09-23). The bench
    /// had its own copy of the attempt loop and it had already drifted — no variation, a clean the
    /// mod did not do. A second copy for the editor would have been a third.
    ///
    /// ⚠ **Pure by contract.** No HTTP, no configuration, no clock, no log: the request goes out
    /// through the <see cref="ChatSend"/> it is handed, the settings come in a <see cref="ModelJob"/>.
    /// What to do with a refusal — remember it, show it, put a previous value back — is the caller's.
    /// </summary>
    public static class LineTranslation
    {
        /// <summary>Temperatures clamped to what an OpenAI-compatible server accepts.</summary>
        public static double ClampTemperature(double value)
        {
            if (double.IsNaN(value) || value < 0.0) return 0.0;
            return value > 2.0 ? 2.0 : value;
        }

        /// <summary>Attempts as an actually usable number, whatever the file says: 1 to 10.</summary>
        public static int ClampAttempts(int value)
        {
            if (value < 1) return 1;
            return value > 10 ? 10 : value;
        }

        /// <summary>
        /// Whether live translation runs: a backend is selected AND switched on.
        ///
        /// ⚠ Both halves are required. The flag alone says yes with the backend set to "none",
        /// where nothing can answer; the backend alone could not be switched off at all.
        /// </summary>
        public static bool IsEnabled(bool enableAi, string? backend) =>
            enableAi && backend != "none";

        /// <summary>
        /// Whether the backend is a translation service (Google, DeepL) rather than a model: it
        /// takes no instructions, and the same text always comes back the same way.
        /// </summary>
        public static bool IsTranslationService(string? backend) => backend == "google" || backend == "deepl";

        /// <summary>
        /// Which placeholders a text about to be sent actually carries.
        ///
        /// ⚠ Read from the text itself, never from what the caller happened to extract. The mod
        /// counted the numbers IT had lifted, which is right for a text just captured and wrong for
        /// a text that is already a cache key — a retranslation, or the Manager, who only has the
        /// key: its [!v*0] was lifted long ago, so the instructions stopped announcing it while the
        /// validation went on demanding it back.
        /// </summary>
        public static Prompts.Markers MarkersOf(string toSend)
        {
            return new Prompts.Markers
            {
                LineBreaks = toSend.Contains(Backends.LineBreak),
                Tags = toSend.Contains(Markup.PlaceholderPrefix),
                Numbers = toSend.Contains("[!v*"),
                Variables = toSend.Contains("[!STR*"),
            };
        }

        /// <summary>
        /// Translate one line with a model: a plain request; then, if the answer broke a
        /// placeholder, a corrective exchange carrying the failed answer back with targeted
        /// feedback; then a fresh request without it — to break the anchoring — with the required
        /// sequences spelt into the instructions and at least the repair temperature.
        ///
        /// ⚠ **The answer is cleaned before it is judged**, while its line breaks are still tokens
        /// (2026-09-23). A model that wraps its answer in quotation marks is not one that broke a
        /// rule; judging it raw failed lines a game can use. And cleaning after the line breaks
        /// were restored trimmed the trailing ones away. The failed answer is sent back to the model
        /// as it SAID it, not as it was cleaned: that is what it is being corrected on.
        /// </summary>
        /// <param name="text">The line as it is keyed — numbers and variables already placeholders.</param>
        public static LineAnswer AskModel(string text, ModelJob job, ChatSend send)
        {
            var result = new LineAnswer();

            if (string.IsNullOrEmpty(text))
            {
                result.Outcome = LineOutcome.NothingToSend;
                return result;
            }

            if (text.Length > Limits.AiTextLength)
            {
                result.Outcome = LineOutcome.TooLong;
                return result;
            }

            PreparedText prepared = Backends.Prepare(text);
            if (prepared.NothingToSend)
            {
                result.Outcome = LineOutcome.NothingToSend;
                return result;
            }

            // ⚠ Sent as it is. Reasoning is turned off through the request's reasoning_effort field
            // (see Negotiation), never by appending a marker to the text: a model treats such a
            // marker as content and TRANSLATES it, leaving "/inga_tänkningar"-style residue glued
            // to the result — measured on every model tested, including ones that do not reason.
            string toSend = prepared.ToSend;
            // Classified as the game wrote it — line breaks and markup are part of what makes a text
            // a paragraph rather than a label.
            string instructions = job.Instructions(MarkersOf(toSend), Prompts.Classify(text));
            int maxTokens = Math.Max(200, text.Length * 2);

            // Placeholders plus the game's own delimiters around them. None → a single attempt,
            // nothing to validate.
            List<string> frozen = Placeholders.FrozenSequences(toSend);
            bool needsValidation = frozen.Count > 0;

            string? failedAsSaid = null;
            List<string> errors = new List<string>();
            int attempts = ClampAttempts(job.Attempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                job.OnAttempt?.Invoke(attempt, attempts);

                var messages = new List<ChatMessage>();
                double temperature = job.Temperature;
                int? seed = attempt == 0 ? job.Seed : job.RepairSeed;

                if (attempt == 0)
                {
                    messages.Add(new ChatMessage("system", instructions));
                    messages.Add(new ChatMessage("user", toSend));
                }
                else if (attempt == 1)
                {
                    // The context changed, so the answer can: temperature 0 on an identical
                    // request would return the identical broken answer.
                    messages.Add(new ChatMessage("system", instructions));
                    messages.Add(new ChatMessage("user", toSend));
                    messages.Add(new ChatMessage("assistant", failedAsSaid ?? ""));
                    messages.Add(new ChatMessage("user", Placeholders.Correction(errors ?? new List<string>(), frozen)));
                }
                else
                {
                    temperature = Math.Max(job.RepairTemperature, job.Temperature);
                    messages.Add(new ChatMessage("system", instructions + "\n" + Placeholders.MandatorySequences(frozen)));
                    messages.Add(new ChatMessage("user", toSend));
                }

                string? said = send(messages, temperature, maxTokens, seed);
                result.Requests = attempt + 1;

                // A transport failure is not a refused translation: retrying the same request here
                // is pointless, the caller decides when a new try makes sense.
                if (said == null)
                {
                    result.Outcome = LineOutcome.NoAnswer;
                    return result;
                }

                string answer = Answers.Clean(said);
                result.Said = answer;
                result.NeededCleaning |= !string.Equals(answer, said.Trim(), StringComparison.Ordinal);

                // Refusal, translation, or neither — see Answers.Read.
                switch (Answers.Read(answer))
                {
                    case AnswerKind.Skip:
                        result.Outcome = LineOutcome.Declined;
                        result.Text = Answers.SkipMarker;
                        return result;
                    case AnswerKind.Unusable:
                        result.Outcome = LineOutcome.Unusable;
                        return result;
                }

                if (!needsValidation || Placeholders.Accepts(toSend, answer, frozen, out errors))
                {
                    result.Outcome = LineOutcome.Translated;
                    result.Text = Backends.Restore(prepared, answer);
                    return result;
                }

                // The repair a game makes for itself — and it has to pass the full check on its own.
                string? mended = Placeholders.RepairTrailingBreaks(toSend, answer);
                if (mended != null && Placeholders.Accepts(toSend, mended, frozen, out _))
                {
                    result.Outcome = LineOutcome.Translated;
                    result.Repaired = true;
                    result.Text = Backends.Restore(prepared, mended);
                    return result;
                }

                failedAsSaid = said;
                result.Attempts.Add(new FailedAttempt { Value = answer, Errors = new List<string>(errors ?? new List<string>()) });
            }

            // Every attempt broke a placeholder. Never keep a corrupted line: it would replace the
            // text on screen and travel to everyone on upload.
            result.Outcome = LineOutcome.Refused;
            return result;
        }

        /// <summary>
        /// Judge what a translation service (Google, DeepL) answered for a prepared text.
        ///
        /// No retry: these services take no instructions, so there is nothing to correct. But a
        /// broken result must never be kept, and the deterministic trailing-break repair applies
        /// first, as for a model. Nothing is cleaned: a service returns a translation and nothing
        /// else, so anything removed would be text.
        /// </summary>
        public static LineAnswer CheckServiceAnswer(PreparedText prepared, string? answer)
        {
            var result = new LineAnswer { Requests = 1 };

            if (string.IsNullOrEmpty(answer))
            {
                result.Outcome = LineOutcome.NoAnswer;
                return result;
            }

            List<string> frozen = Placeholders.FrozenSequences(prepared.ToSend);
            if (frozen.Count > 0 && !Placeholders.Accepts(prepared.ToSend, answer!, frozen, out var errors))
            {
                string? mended = Placeholders.RepairTrailingBreaks(prepared.ToSend, answer!);
                if (mended == null || !Placeholders.Accepts(prepared.ToSend, mended, frozen, out _))
                {
                    result.Outcome = LineOutcome.Refused;
                    result.Attempts.Add(new FailedAttempt { Value = answer!, Errors = new List<string>(errors) });
                    return result;
                }

                result.Repaired = true;
                answer = mended;
            }

            result.Outcome = LineOutcome.Translated;
            result.Text = Backends.Restore(prepared, answer!);
            return result;
        }
    }
}
