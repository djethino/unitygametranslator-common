using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The five bands of the quality bar, and the words that name them.
    ///
    /// ⚠ These cases exist because this exact drift has happened three times. They check the two
    /// things a second copy always gets wrong: the ORDER, and whether the word matches the one the
    /// website ships in nineteen languages.
    /// </summary>
    internal static class CompositionChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var bands = Composition.Bands();

            check(bands.Length == 5, "there are five bands and no more",
                "a sixth would have no colour and no denominator");

            // ── The order IS the rule ─────────────────────────────────────────
            //
            // Settled first, still-to-do last, so the grey ends the bar and its length reads as the
            // work left without any arithmetic. Reordering silently rewrites every bar in three
            // products at once.
            check(bands[0] == TagBand.Human && bands[1] == TagBand.Validated
                  && bands[2] == TagBand.Machine && bands[3] == TagBand.Skipped
                  && bands[4] == TagBand.Captured,
                "and they come in the bar's order, grey last",
                "the length of the tail is what says how much is left");

            // ── The words the website ships ───────────────────────────────────
            //
            // Taken from lang/en.json (progress.human, .validated, .ai, .skipped, .capture). They
            // cannot be read from here — the site is PHP — so they are written out, which is
            // exactly what makes a divergence visible instead of merely possible.
            check(Composition.Name(TagBand.Human) == "Human", "Human", "progress.human");
            check(Composition.Name(TagBand.Validated) == "Validated", "Validated", "progress.validated");
            check(Composition.Name(TagBand.Machine) == "AI", "AI", "progress.ai");
            check(Composition.Name(TagBand.Skipped) == "Kept as is", "Kept as is", "progress.skipped");
            check(Composition.Name(TagBand.Captured) == "Captured", "Captured", "progress.capture");

            // ── The letters, and the one that must stay empty ─────────────────
            check(Composition.Letter(TagBand.Human) == "H"
                  && Composition.Letter(TagBand.Validated) == "V"
                  && Composition.Letter(TagBand.Machine) == "A"
                  && Composition.Letter(TagBand.Skipped) == "S",
                "the four tags carry the letters the editors show",
                "a fifth vocabulary for a fact that already has one");

            check(Composition.Letter(TagBand.Captured).Length == 0,
                "and a captured line carries none",
                "it holds no tag: inventing one teaches a tag the file does not contain");

            // ── Each band explains itself ─────────────────────────────────────
            foreach (var band in bands)
            {
                check(Composition.Name(band).Trim().Length > 0 && Composition.Effect(band).Trim().Length > 0,
                    band + " is named and explained",
                    "a coloured band nobody can name is a decoration");
            }

            // ── The stage words live in ONE place ─────────────────────────────
            //
            // 🔴 They were moved to Quality.StageName after existing twice, and the Manager still
            // held a private copy that had drifted back to "Review well under way" — the idiom the
            // socle replaced. Named here rather than merely agreed upon.
            check(Quality.StageName(ReviewStage.Advanced) == "Review in progress",
                "an advanced review is 'Review in progress'",
                "'well under way' is transparent to a native and opaque to everybody else");

            check(Quality.StageName(ReviewStage.Reviewed) == "Fully reviewed"
                  && Quality.StageName(ReviewStage.Started) == "Review started"
                  && Quality.StageName(ReviewStage.Machine) == "Machine translation",
                "and the other three are the words both products show",
                "a second copy of a verdict is a second chance to disagree with yourself");
        }
    }
}
