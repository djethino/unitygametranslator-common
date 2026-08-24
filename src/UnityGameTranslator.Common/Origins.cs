namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Where a fork came from, and how much it was handed.
    ///
    /// ⚠ <see cref="Lines"/> is a SNAPSHOT taken at the instant of the fork and never recomputed.
    /// The original keeps growing afterwards, so asking the question later answers a different one.
    /// That is what lets the figure stay true as the fork grows past it.
    ///
    /// ⚠ <see cref="Author"/> is read live rather than stored, so a rename follows; the account can
    /// also be gone, and the credit then stands without a name rather than not at all.
    /// </summary>
    public struct Origin
    {
        /// <summary>The account this was taken from. Null when it is no longer on the site.</summary>
        public readonly string? Author;

        /// <summary>
        /// How many lines were received, counted once at the fork. Null when the server did not
        /// say — an older one, or a row written before the column existed.
        /// </summary>
        public readonly int? Lines;

        public Origin(string? author, int? lines)
        {
            Author = author;
            Lines = lines;
        }
    }

    /// <summary>
    /// A fork's provenance, said the same way wherever it is read.
    ///
    /// 🔴 **Here because forking ERASES the link and only this survives it.** The mod severs a
    /// fork's tie to the lineage it left — it has to, or it would keep offering to merge from a
    /// translation it just walked away from — and that severed the provenance with it: somebody's
    /// three thousand lines became somebody else's starting point with nothing on screen to say so.
    /// The site fixed that with the <c>origin_*</c> columns and a component of its own; the mod and
    /// the Manager showed nothing at all, so the same file credited its source in a browser and
    /// credited nobody in the game it came from.
    ///
    /// 🔴 **The only fact in the strip that is FILIATION rather than measurement.** Coverage,
    /// completeness and the review stage are computed and can be argued with; this one is recorded
    /// in the database at the moment the fork happens. It is also the only way to read the tree of
    /// contributions once a lineage has been left.
    ///
    /// ⚠ Plain international English: the mod and the Manager ship no translations, so whatever is
    /// written here is what a Polish, Brazilian or Korean player reads. The website keys its own
    /// wording (<c>translation.forked_from*</c>) in nineteen languages and says the same thing —
    /// a decision taken here does not travel there on its own.
    /// </summary>
    public static class Origins
    {
        /// <summary>
        /// The chip itself: <c>Forked from @alice</c>.
        ///
        /// ⚠ **"Forked", not "Started from"**, although the website's sentence opens that way. The
        /// three roles a translation can hold are Main, Branch and Fork in all three products, and
        /// this chip sits in the strip right beside the role — spending the one word everybody has
        /// already learnt is what makes the pair readable without reading.
        ///
        /// ⚠ The name goes through <see cref="People.Mention"/> like every other name on screen,
        /// so the row's own author and the author it came from are written the same way.
        /// </summary>
        public static string Name(Origin origin)
        {
            if (string.IsNullOrWhiteSpace(origin.Author))
                return "Forked from a removed account";

            return "Forked from " + People.Mention(origin.Author);
        }

        /// <summary>
        /// What it means, for a tooltip: who it came from, how much of it, and that the two have
        /// been separate translations ever since.
        ///
        /// ⚠ The last sentence is not decoration. Without it "Forked from @alice" reads as a live
        /// link — somebody would expect an update from Alice to reach them, which is precisely what
        /// forking gave up.
        /// </summary>
        public static string Effect(Origin origin)
        {
            string separate = " It has been a separate translation ever since.";

            if (string.IsNullOrWhiteSpace(origin.Author))
                return "Started from work whose account is no longer on the site." + separate;

            string who = "Started from " + People.Mention(origin.Author) + "'s work";

            if (!origin.Lines.HasValue || origin.Lines.Value <= 0)
                return who + "." + separate;

            string much = origin.Lines.Value == 1 ? "1 line of it" : origin.Lines.Value + " lines of it";

            return who + ", " + much + " at the time." + separate;
        }
    }
}
