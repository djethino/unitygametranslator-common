namespace UnityGameTranslator.Common
{
    /// <summary>Where a translation stands in its reading, as a step rather than a mark.</summary>
    public enum ReviewStage
    {
        /// <summary>Translated, nobody has read it back yet.</summary>
        Machine,

        /// <summary>Someone has started reading it.</summary>
        Started,

        /// <summary>Well under way.</summary>
        Advanced,

        /// <summary>Read from end to end.</summary>
        Reviewed,
    }

    /// <summary>
    /// How much of a translation exists, and how much of it a human has read.
    ///
    /// ⚠ THE WEBSITE IS THE REFERENCE. It computes these server-side for every published file, and
    /// this is a port of App\Models\Translation — not an interpretation of it. A player who opens
    /// the same file in a browser and in a game has to be told the same thing, and the two are
    /// free to disagree only if nobody notices.
    ///
    /// ⚠ They DID disagree. The mod's copy never received the SKIPPED count — lines an author
    /// deliberately keeps as they are, proper nouns and brand names, which is ordinary in a game.
    /// The website counts them as read and as settled; the mod dropped them entirely. Since the
    /// website's figures are therefore always the more generous, the same file could read "fully
    /// reviewed" in the browser and "review well under way" in the game, and the completeness gate
    /// below could open on one side and not the other.
    ///
    /// ⚠ The website is PHP, so this cannot be shared with it as code — only kept faithful to it.
    /// Anything changed here changes on both sides or it is a new divergence.
    ///
    /// Two counts that are NOT interchangeable, and mixing them up is how this goes wrong:
    ///  · settled = H + V + S + A — every line whose fate is decided;
    ///  · translated = H + V + A — the lines that actually carry a translation.
    /// Captured lines are neither: text the mod has met in game and nobody has dealt with yet.
    /// </summary>
    public static class Quality
    {
        /// <summary>
        /// Below this share of translated work, no stage is shown at all.
        ///
        /// Two lines translated out of thirteen met in game were once labelled "fully reviewed".
        /// Writing comes before reading: there has to be a translation before "how well was it
        /// read" means anything.
        /// </summary>
        public const double TranslationFloor = 0.9;

        /// <summary>
        /// How much of it a human has settled: (H+V+S) / (H+V+S+A). Null when nothing is
        /// translated — an absence of coverage, which is not a coverage of zero.
        ///
        /// ⚠ The guard is on H+V+A and NOT on the denominator, exactly as the website has it, and
        /// that asymmetry is deliberate: a file holding nothing but skipped lines would otherwise
        /// divide S by S and announce a coverage of 100% for a file with no translation in it.
        /// </summary>
        public static double? ReviewCoverage(int human, int validated, int skipped, int ai)
        {
            if (human + validated + ai == 0) return null;

            return (double)(human + validated + skipped) / (human + validated + skipped + ai);
        }

        /// <summary>
        /// How much of what the file has MET in game is settled: settled / (settled + captured).
        ///
        /// Captured lines are the honest denominator — text the mod ran into and nobody has dealt
        /// with. The size of a whole game is unknowable, so it is never the denominator of
        /// anything. Null when the file holds nothing at all.
        /// </summary>
        public static double? Completeness(int human, int validated, int skipped, int ai, int captured)
        {
            int settled = human + validated + skipped + ai;
            if (settled + captured == 0) return null;

            return (double)settled / (settled + captured);
        }

        /// <summary>
        /// A file that has met text in game and settled none of it.
        ///
        /// Not "a translation at zero": no translation was attempted. That distinction is what
        /// somebody needs before downloading — one is work in progress, the other is the game's
        /// own text handed back unchanged.
        /// </summary>
        public static bool IsCaptureOnly(int human, int validated, int skipped, int ai, int captured) =>
            human + validated + skipped + ai == 0 && captured > 0;

        /// <summary>
        /// Where a translation stands. Null when there is nothing to say — nothing translated, or
        /// too much of what the file met still waiting (see <see cref="TranslationFloor"/>).
        ///
        /// ⚠ A step, never a mark, and it carries no verdict. Every translation starts as machine
        /// output because that is how the mod works; naming that a failing grade tells a newcomer
        /// their starting point is worthless.
        ///
        /// ⚠ Returns the step, not a sentence. The website says it in nineteen languages and the
        /// mod says it in one — wording belongs to whoever is doing the talking.
        /// </summary>
        public static ReviewStage? Stage(int human, int validated, int skipped, int ai, int captured)
        {
            double? completeness = Completeness(human, validated, skipped, ai, captured);
            if (completeness.HasValue && completeness.Value < TranslationFloor) return null;

            double? coverage = ReviewCoverage(human, validated, skipped, ai);
            if (!coverage.HasValue) return null;

            if (coverage.Value >= 1.0) return ReviewStage.Reviewed;
            if (coverage.Value >= 0.4) return ReviewStage.Advanced;
            if (coverage.Value > 0.0) return ReviewStage.Started;

            return ReviewStage.Machine;
        }

        /// <summary>
        /// What a stage is called, in the words used everywhere.
        ///
        /// ⚠ Moved here on 2026-08-14 because it existed TWICE, identical to the character, in the
        /// mod's TranslationQuality and the manager's QualityBar. Two copies of a verdict are two
        /// chances to tell somebody their file is "fully reviewed" on one screen and something
        /// else on the next — and the rule that produces the stage was already shared, so only the
        /// wording was free to drift.
        /// </summary>
        /// 🔴 **Plain, international English.** These words are read in the mod and in the manager,
        /// and NEITHER has translations — an English word is all a Polish or Brazilian player will
        /// ever get here. "Review well under way" stood in this spot and is exactly the kind of
        /// idiom that is transparent to a native and opaque to everybody else. The website is
        /// translated and keys its own wording separately, so it is unaffected by what is chosen
        /// here: this is the untranslated surface, and it has to be readable at a school level.
        public static string StageName(ReviewStage stage)
        {
            switch (stage)
            {
                case ReviewStage.Reviewed: return "Fully reviewed";
                case ReviewStage.Advanced: return "Review in progress";
                case ReviewStage.Started: return "Review started";
                default: return "Machine translation";
            }
        }
    }
}
