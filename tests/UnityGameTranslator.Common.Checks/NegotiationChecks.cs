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

            // The count of attempts is the count of concessions that exist, walked here rather than trusted: a body that names every
            // field, refused again and again, until nothing is left to give up
            var all = New();
            int made = 0;
            while (all.Concede("max_completion_tokens stream_options seed temperature reasoning", false, out _) == Concession.Made && made < 100) made++;
            check(made > 0 && made < 100 && Negotiation.MaxAttempts == made + 1,
                $"the attempts are the concessions that exist, and the one that works ({made} + 1)",
                "a literal here is a count somebody forgets to move when one more thing can be given up");

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
