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
            var start = New();
            check(start.TokenField == "max_tokens", "the cap goes in max_tokens first",
                "every compatible server understands it; the newer name is accepted and IGNORED by some");
            check(start.UnusedTokenField == "max_completion_tokens", "and the other name is taken off",
                "sending both is itself rejected");
            check(start.SendTemperature, "temperature is sent", "translation wants it at zero, not at a default");
            check(start.ReasoningEffort == "none", "and reasoning is asked to stand down", "the best rung");
            check(start.SendSeed, "a seed may be sent",
                "only a retranslation ever asks for one — ordinary translation wants the same answer twice");

            // Only a refusal about our request may cost us anything.
            check(Negotiation.IsAboutOurRequest(400) && Negotiation.IsAboutOurRequest(422),
                "400 and 422 are about what we sent", "the server read it and would not have it");
            check(!Negotiation.IsAboutOurRequest(401) && !Negotiation.IsAboutOurRequest(404)
                  && !Negotiation.IsAboutOurRequest(429) && !Negotiation.IsAboutOurRequest(500),
                "a wrong key, a wrong address, a rate limit and an outage are not",
                "giving up temperature because a server was down would degrade every later line");

            // The named field goes first, whatever else might also be wrong.
            var named = New();
            check(named.Concede("Unsupported parameter: use max_completion_tokens", out _)
                  && named.TokenField == "max_completion_tokens",
                "the server names a field, so that is what we change",
                "several things can be wrong at once and only the named one is certain");
            check(named.ReasoningEffort == "none", "without touching the reasoning rung yet",
                "one variable per attempt or we learn nothing");

            var temp = New();
            check(temp.Concede("temperature does not support 0.0 with this model", out _) && !temp.SendTemperature,
                "a refused temperature is dropped", "its own default applies, and the line still gets translated");
            check(temp.SendSeed, "and it takes the seed down with it only if the seed was named",
                "one variable per attempt, or the next refusal teaches us nothing");

            // The seed is conceded BEFORE the temperature on purpose.
            var seeded = New();
            check(seeded.Concede("Unrecognized request argument supplied: seed", out _)
                  && !seeded.SendSeed && seeded.SendTemperature,
                "a refused seed is dropped, and the temperature is kept",
                "the variation comes from the temperature; the seed only makes it reproducible where honoured");

            // Nothing named: walk down the ladder, one rung at a time.
            var ladder = New();
            check(ladder.Concede("bad request", out _) && ladder.ReasoningEffort == "low",
                "with nothing named, reasoning steps down", "none, then low");
            check(ladder.Concede("bad request", out _) && ladder.ReasoningEffort == null,
                "then the field goes away entirely", "some models reject the parameter outright");
            check(!ladder.Concede("bad request", out _), "and then there is nothing left to give",
                "the failure was never about our parameters");

            // Everything at once, on one unhappy model.
            var all = New();
            check(all.Concede("max_completion_tokens is required", out _)
                  && all.Concede("temperature is not supported", out _)
                  && all.Concede("reasoning_effort invalid", out _)
                  && all.TokenField == "max_completion_tokens" && !all.SendTemperature
                  && all.ReasoningEffort == "low",
                "three concessions leave a request that still asks for a translation",
                "each one costs something, none of them costs the answer");

            // A concession belongs to one provider and one model.
            var moved = New();
            moved.Concede("temperature is not supported", out _);
            moved.ForgetIfChanged("http://elsewhere|other-model");
            check(moved.SendTemperature && moved.TokenField == "max_tokens" && moved.ReasoningEffort == "none",
                "changing provider or model forgets everything",
                "what one server accepts says nothing about the next, and carrying it over degrades a model that never needed it");

            var same = New();
            same.Concede("temperature is not supported", out _);
            same.ForgetIfChanged("http://server|model");
            check(!same.SendTemperature, "but the same pair keeps what it learned",
                "otherwise every line would pay for the same discovery");

            // The reason is written for whoever reads a log.
            var why = New();
            why.Concede("use max_completion_tokens", out string reason);
            check(reason != null && reason.IndexOf("max_completion_tokens", StringComparison.Ordinal) >= 0,
                "and it says what it gave up", "a silent concession is one nobody can explain later");
        }

        private static Negotiation New()
        {
            var negotiation = new Negotiation();
            negotiation.ForgetIfChanged("http://server|model");
            return negotiation;
        }
    }
}
