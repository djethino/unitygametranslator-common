using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Finding out what a provider will actually accept, by being refused.
    ///
    /// ⚠ Nothing here is guessed from a URL or a model name. People point this at local servers,
    /// proxies and gateways whose names say nothing about what they take, so the only reliable
    /// source is the refusal itself.
    ///
    /// ⚠ No JSON, no HTTP, on purpose — and it costs nothing, because a refusal is read by looking
    /// for a field name in the error text. This decides the SHAPE of a request; each program writes
    /// that shape in its own JSON and sends it with its own client. That split is what lets the mod
    /// (Newtonsoft, netstandard2.0) and the manager (System.Text.Json, .NET 8) hold one policy
    /// between them.
    ///
    /// ⚠ The manager had none of this: it sent one fixed shape and gave up. A provider refusing
    /// that shape scored every model at zero for a reason that was never about the models.
    /// </summary>
    public sealed class Negotiation
    {
        /// <summary>
        /// One request per thing that can still be given up on, plus the one that works.
        /// </summary>
        public const int MaxAttempts = 5;

        /// <summary>
        /// What to ask for reasoning, from best to last resort.
        ///
        /// Accepted values differ per provider: "none" works on Ollama, vLLM, LM Studio and recent
        /// Grok, while OpenAI only takes it from gpt-5.1 on and some models cannot turn reasoning
        /// off at all. "low" is the common denominator — it does not remove reasoning, it keeps it
        /// small. null means send no such field, for the ones that reject it outright.
        /// </summary>
        private static readonly string[] EffortLadder = { "none", "low", null };

        private int _rung;
        private bool _useMaxCompletionTokens;
        private bool _omitTemperature;
        private bool _omitSeed;
        private string _learnedFor;

        /// <summary>
        /// Forget everything when the provider or the model changes.
        ///
        /// What one server accepts says nothing about the next, and carrying a concession across
        /// would silently degrade a model that never needed it — a dropped temperature is a
        /// different translation, not a different request.
        /// </summary>
        public void ForgetIfChanged(string providerAndModel)
        {
            string key = providerAndModel ?? string.Empty;
            if (string.Equals(_learnedFor, key, StringComparison.Ordinal)) return;

            _learnedFor = key;
            _rung = 0;
            _useMaxCompletionTokens = false;
            _omitTemperature = false;
            _omitSeed = false;
        }

        /// <summary>
        /// The field to put the token cap in.
        ///
        /// ⚠ max_tokens is what every OpenAI-compatible server understands; OpenAI's reasoning
        /// models are the exception and demand max_completion_tokens. Sending the newer name by
        /// default is worse than useless — Ollama accepts it and IGNORES it, measured, which
        /// silently removes the cap altogether.
        /// </summary>
        public string TokenField => _useMaxCompletionTokens ? "max_completion_tokens" : "max_tokens";

        /// <summary>
        /// The other one, which must be taken OFF the request. Never send both: OpenAI rejects it.
        /// </summary>
        public string UnusedTokenField => _useMaxCompletionTokens ? "max_tokens" : "max_completion_tokens";

        /// <summary>False once a provider has refused our temperature. Its own default then applies.</summary>
        public bool SendTemperature => !_omitTemperature;

        /// <summary>
        /// False once a provider has refused a seed.
        ///
        /// ⚠ A seed is only ever sent when the caller asks for a DIFFERENT answer to a question
        /// already asked — a retranslation the human did not like. Ordinary translation sends none:
        /// it wants the same answer every time, which is what temperature 0 already gives.
        ///
        /// ⚠ Being accepted is not being honoured. Several servers take the field and ignore it, and
        /// nothing in the response says so. The variation must therefore never RELY on the seed —
        /// it comes from the temperature, and the seed only makes it reproducible where supported.
        /// </summary>
        public bool SendSeed => !_omitSeed;

        /// <summary>What to ask for reasoning, or null to send no such field at all.</summary>
        public string ReasoningEffort => EffortLadder[_rung];

        /// <summary>
        /// Whether a refusal says anything about the request we sent.
        ///
        /// ⚠ Only these two. A 401, a 404, a 429 or a 5xx says nothing about our parameters, and
        /// giving one up on that basis would degrade every later translation to work around a
        /// server that was merely down or a key that was merely wrong.
        /// </summary>
        public static bool IsAboutOurRequest(int statusCode) => statusCode == 400 || statusCode == 422;

        /// <summary>
        /// Give up on whatever the refusal points at — one thing per call, so the next attempt
        /// changes exactly one variable.
        ///
        /// The field the server NAMED comes first, before blaming the reasoning ladder: several of
        /// these can be wrong at once on the same model, and the named one is the only certainty.
        ///
        /// Returns false when there is nothing left to concede, which means the failure was never
        /// about our parameters.
        /// </summary>
        /// <param name="reason">What was given up and why, for whoever keeps a log.</param>
        public bool Concede(string errorBody, out string reason)
        {
            string body = errorBody ?? string.Empty;

            if (!_useMaxCompletionTokens
                && body.IndexOf("max_completion_tokens", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _useMaxCompletionTokens = true;
                reason = "provider wants max_completion_tokens instead of max_tokens";
                return true;
            }

            // Before the temperature, deliberately: a body naming both costs us the variation itself
            // if we give up the temperature, and only its reproducibility if we give up the seed.
            if (!_omitSeed && body.IndexOf("seed", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _omitSeed = true;
                reason = "provider rejects a seed — dropping it, the temperature still varies the answer";
                return true;
            }

            if (!_omitTemperature && body.IndexOf("temperature", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                _omitTemperature = true;
                reason = "provider rejects our temperature — dropping it, its own default applies";
                return true;
            }

            if (_rung + 1 < EffortLadder.Length)
            {
                _rung++;
                reason = EffortLadder[_rung] == null
                    ? "reasoning_effort rejected, sending no reasoning parameter at all"
                    : "reasoning_effort rejected, falling back to " + EffortLadder[_rung];
                return true;
            }

            reason = null;
            return false;
        }
    }
}
