namespace UnityGameTranslator.Common
{
    /// <summary>
    /// How a person is named on screen — the same way in every product.
    ///
    /// 🔴 **Written here because it was written four different ways.** A name appeared as
    /// <c>name</c>, <c>by name</c>, <c>@name</c> and <c>"name"</c> depending on which screen you
    /// were on, and only ONE of the three products ever said which one was you. Somebody reading
    /// the same fact in the mod, in the manager and on the site was reading three things.
    ///
    /// ⚠ **Composition only, never rendering.** What colour a name takes belongs to the product,
    /// exactly as it does for <see cref="Badges"/>. What must not differ is the string.
    ///
    /// ⚠ The website cannot consume this — it is PHP — so it re-keys the same rule in its language
    /// files. That is the trap that left the "Solo work" chip missing from the site for a day: a
    /// decision taken here does not travel there on its own.
    /// </summary>
    public static class People
    {
        /// <summary>
        /// A person, as written anywhere: <c>@name</c>, and <c>@name (you)</c> when it is the
        /// account signed in here.
        ///
        /// 🔴 **The mark is a WORD, never a colour.** A colour cannot be read by somebody who does
        /// not already know there is something to read, and this one has to survive a screenshot,
        /// a colour-blind reader and a row that is already using colour to say something else.
        ///
        /// ⚠ The at-sign goes on your own name too. One form, or the reader has to learn two —
        /// and the one place that would have dropped it is the place it matters most, where your
        /// name sits in a list beside other people's.
        /// </summary>
        /// <param name="name">The account name. Null or blank yields <see cref="Unknown"/>.</param>
        /// <param name="isYou">
        /// Whether this is the account signed in on THIS machine. False when nobody is signed in:
        /// an anonymous reader is not "not you", they are simply somebody with no name here, and
        /// nothing should be marked.
        /// </param>
        public static string Mention(string? name, bool isYou = false)
        {
            if (string.IsNullOrWhiteSpace(name)) return Unknown;

            return isYou ? "@" + name.Trim() + " (you)" : "@" + name.Trim();
        }

        /// <summary>
        /// What stands in for a name nobody sent. Never "you", never a blank: a missing author is
        /// a fact, and an empty space where a name belongs reads as a bug.
        /// </summary>
        public const string Unknown = "unknown";

        /// <summary>
        /// True when this name is the account signed in here — the one test, so that no screen
        /// invents its own.
        ///
        /// ⚠ Case-insensitive: the site accepts a name in the case its owner typed and echoes it
        /// back that way, and a mention that failed to match on capitalisation would tell somebody
        /// their own translation belonged to a stranger.
        /// </summary>
        public static bool IsYou(string? name, string? signedInAs)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(signedInAs))
                return false;

            return string.Equals(name.Trim(), signedInAs.Trim(),
                                 System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The same, in one call, for the common case where a caller holds both names.
        /// </summary>
        public static string MentionOf(string? name, string? signedInAs)
            => Mention(name, IsYou(name, signedInAs));
    }
}
