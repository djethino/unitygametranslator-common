using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What a provider will accept, learned by being refused.
    ///
    /// Every rung below costs a request, and every concession costs quality — a dropped
    /// temperature is a different translation. So the order matters: give up what the server
    /// NAMED first, and never give up anything on a refusal that was not about the request.
    /// </summary>
    internal static class NegotiationChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // Everything that decides a shape is in corpus/rules/negotiation.json. What stays here is the words for a log.
            var why = New();
            why.Concede("use max_completion_tokens", out string reason);
            check(reason != null && reason.IndexOf("max_completion_tokens", StringComparison.Ordinal) >= 0,
                "it says what it gave up", "a silent concession is one nobody can explain later");

            var shape = New();
            var said = shape.Concede("Unsupported parameter: response_format", true, out string told);
            check(said == Concession.ResponseFormat && told != null && told.IndexOf("response format", StringComparison.Ordinal) >= 0,
                "and says when it is the format that was refused", "the caller shows it, so it must be in words");
        }

        private static Negotiation New()
        {
            var negotiation = new Negotiation();
            negotiation.ForgetIfChanged("http://server|model");
            return negotiation;
        }
    }
}
