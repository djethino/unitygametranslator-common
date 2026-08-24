namespace UnityGameTranslator.Common
{
    /// <summary>
    /// One band of the quality bar — what a line is, once somebody has dealt with it or not.
    ///
    /// ⚠ The ORDER is the bar's order and it is part of the rule: settled first, still-to-do last,
    /// so the grey always ends the bar and its length reads as the work left without arithmetic.
    /// </summary>
    public enum TagBand
    {
        /// <summary>Written by a person.</summary>
        Human,

        /// <summary>Read by a person, who stood behind what the machine wrote.</summary>
        Validated,

        /// <summary>The machine's, with nobody's word on it. Also covers a line carrying no tag.</summary>
        Machine,

        /// <summary>A person ruled this line must not be translated. Settled, not translated.</summary>
        Skipped,

        /// <summary>Met in game and nobody has dealt with it yet. Neither settled nor translated.</summary>
        Captured,
    }

    /// <summary>
    /// What a translation is MADE OF, in words — one set for the whole ecosystem.
    ///
    /// 🔴 **Written on 2026-08-24 because the same five bands were named three different ways.**
    /// The website and the mod agreed — Human · Validated · AI · Kept as is · Captured — and the
    /// Manager's own key said <c>human · reviewed · AI · kept as is · not done yet</c>. Same bar,
    /// same colours, same file, and a reader moving from one window to the other had to work out
    /// that "reviewed" and "Validated" were the same band and that "not done yet" was the grey.
    ///
    /// ⚠ **This is the third time this exact drift happens.** <see cref="Quality.StageName"/> was
    /// moved here for it, and the Manager still carried a private copy that had drifted back to
    /// "Review well under way" — the very wording the socle replaced for being idiom. A rule that
    /// lives in one place stops drifting; one that is merely agreed upon does not.
    ///
    /// ⚠ **The website cannot consume this** — it is PHP, and it is translated into nineteen
    /// languages, so it keys the same words itself under <c>progress.*</c>. A change here does not
    /// travel there on its own.
    ///
    /// ⚠ Plain international English: the mod and the Manager ship no translations.
    /// </summary>
    public static class Composition
    {
        /// <summary>The five bands, in the order every bar draws them.</summary>
        public static TagBand[] Bands()
        {
            return new[]
            {
                TagBand.Human, TagBand.Validated, TagBand.Machine,
                TagBand.Skipped, TagBand.Captured,
            };
        }

        /// <summary>
        /// What a band is called.
        ///
        /// ⚠ "AI" and not "Machine": the enum names the concept, this names the band on screen, and
        /// the screen word is the one the website has always used and the one players say.
        /// </summary>
        public static string Name(TagBand band)
        {
            switch (band)
            {
                case TagBand.Human: return "Human";
                case TagBand.Validated: return "Validated";
                case TagBand.Machine: return "AI";
                case TagBand.Skipped: return "Kept as is";
                default: return "Captured";
            }
        }

        /// <summary>
        /// The one-letter form, which is the vocabulary the editors, the merge screens and the
        /// contribution counts all already use.
        ///
        /// ⚠ Empty for <see cref="TagBand.Captured"/>, and that is not an oversight: a captured
        /// line carries NO tag. Inventing a letter for it would put a fifth chip in grids that
        /// only ever hold four, and teach a tag the file does not contain.
        /// </summary>
        public static string Letter(TagBand band)
        {
            switch (band)
            {
                case TagBand.Human: return "H";
                case TagBand.Validated: return "V";
                case TagBand.Machine: return "A";
                case TagBand.Skipped: return "S";
                default: return "";
            }
        }

        /// <summary>
        /// What a band means, for a tooltip or a key nobody has met before.
        ///
        /// ⚠ Says what happened to the line, never why. An S can mean "a proper noun stays as it
        /// is" or "I will deal with it later", and we cannot read an author's intent.
        /// </summary>
        public static string Effect(TagBand band)
        {
            switch (band)
            {
                case TagBand.Human: return "Written by a person.";
                case TagBand.Validated: return "The machine wrote it and a person accepted it.";
                case TagBand.Machine: return "The machine's, with nobody's word on it yet.";
                case TagBand.Skipped: return "A person decided this line stays as the game wrote it.";
                default: return "Met in game and not dealt with yet.";
            }
        }
    }
}
