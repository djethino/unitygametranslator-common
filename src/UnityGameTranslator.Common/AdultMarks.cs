namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The "Adults only" box of a first publication, in the words the mod and the Manager both show.
    ///
    /// 🔴 **Decided with the owner on 2026-09-23** (root analyse/adult-declaration-at-publish.md): the
    /// mark is about the GAME, and its authority runs admin > Steam > IGDB > the author of its first
    /// translation. So the publish screens tell the person when the game is classified — ticked and
    /// locked, with who says so — and ask only when this upload adds the game to the site and nobody
    /// classified it. The site answers which (<c>GET /games/adult</c>); these are the sentences.
    ///
    /// ⚠ Here rather than in each product: one fact, one wording — the site's game page says the same
    /// source in its own twenty languages.
    /// </summary>
    public static class AdultMarks
    {
        /// <summary>The box's words.</summary>
        public const string Box = "Adults only";

        /// <summary>Under the box when it is offered: what ticking it does, and what it does not.</summary>
        public const string WhatItDoes =
            "Hides the game from the website's lists, unless a visitor chooses to see adult games. The translation works the same.";

        /// <summary>
        /// Under the box when the game is classified: who says so. <paramref name="source"/> is the
        /// site's word — steam, igdb, contributor (the author of its first translation), admin.
        /// </summary>
        public static string Source(string? source)
        {
            switch (source)
            {
                case "steam": return "Classified by Steam.";
                case "igdb": return "Classified by IGDB.";
                case "contributor": return "Declared by the author of its first translation.";
                case "admin": return "Set by a moderator.";
                default: return "Classified for adults only.";
            }
        }

        /// <summary>Whether the box is shown at all: classified (locked) or offered (open).</summary>
        public static bool Shown(bool adult, bool declarable) => adult || declarable;

        /// <summary>Whether the person may tick it: nobody classified the game, and this upload adds it.</summary>
        public static bool Open(bool adult, bool declarable) => !adult && declarable;
    }
}
