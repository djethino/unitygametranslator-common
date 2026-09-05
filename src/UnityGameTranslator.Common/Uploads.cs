namespace UnityGameTranslator.Common
{
    /// <summary>What sending this file to the site would BECOME.</summary>
    public enum UploadAct
    {
        /// <summary>Nothing of this lineage is on the site: it is created, under this account's name.</summary>
        Upload,

        /// <summary>This account's own row — a Main or a branch — is replaced by this file.</summary>
        Update,

        /// <summary>Somebody else leads this lineage and takes contributions: the file becomes a branch for them to review.</summary>
        Contribute,

        /// <summary>
        /// The only way on: leave the lineage and publish this file as a translation of its own.
        /// Chosen when the lineage cannot take what this account would send — its Main works alone,
        /// is gone, or has closed since — never as a preference.
        /// </summary>
        Fork,
    }

    /// <summary>
    /// What one button does with a translation file, and the word on it — decided once for the
    /// mod, the Manager and whatever comes next.
    ///
    /// 🔴 **Written on 2026-09-05 because each product decided this on its own, and none of them
    /// had the whole list.** The site's determineOwnership is the authority and refuses correctly;
    /// the two clients each kept a copy of part of it. The mod knew "still the copy" and "the
    /// lineage is dead", the Manager knew "frozen" and "the Main is missing", neither read the
    /// Main's refusal of contributions at the moment it mattered — so both announced "Contribute"
    /// over a translation whose author works alone, and the server said no after the upload.
    ///
    /// ⚠ **Three facts decide, and they come from three places.** Where the file stands
    /// (<see cref="Publication"/>), whether this account's row is a branch, and the walls the
    /// server reports — its Main takes no contributions, is gone, has erased their account, has
    /// closed since. None of them is this library's to fetch; all of them are its to weigh.
    ///
    /// ⚠ **Null is "not asked", never "no."** Every wall is a nullable the server may not have
    /// sent; treating silence as a refusal would lock people out of a site that never spoke.
    ///
    /// 🔴 **Contributing and forking are decided in the game, and only there** (the user, 2026-09-05:
    /// "on décide de prendre son envol ou de contribuer, ça ne se fait pas à la chaîne sur plusieurs
    /// jeux"). The Manager creates a translation and updates this account's own row — Main or
    /// branch — and stops at the first act: it costs the person an effort on purpose, and it keeps
    /// a tool that lists twenty games from filing twenty contributions in a minute. The rule is here
    /// so that a tool which one day takes those acts inherits the same answers.
    /// </summary>
    public static class Uploads
    {
        /// <summary>
        /// The act, or null when there is nothing on this machine to send.
        /// </summary>
        /// <param name="publication">Where the file stands — see <see cref="Publications.Of"/>.</param>
        /// <param name="onABranch">This account's own row in the lineage is a contribution, not a translation of its own.</param>
        /// <param name="acceptsBranches">The lineage's Main takes contributions. Null when the server did not say.</param>
        /// <param name="mainMissing">The Main has been removed by its author. Null when not asked.</param>
        /// <param name="mainAbandoned">The Main is published and its owner's account is erased. Null when not asked.</param>
        /// <param name="branchFrozen">This account's branch sits on a Main that has closed since. Null when not asked.</param>
        public static UploadAct? ActOf(Publication publication, bool onABranch,
                                       bool? acceptsBranches, bool? mainMissing,
                                       bool? mainAbandoned, bool? branchFrozen)
        {
            switch (publication)
            {
                case Publication.NotDownloaded:
                    return null;

                case Publication.NeverPublished:
                    return UploadAct.Upload;

                case Publication.Published:
                    // A branch whose road has ended can only leave; a Main's row is always its own
                    // to replace, whatever became of the people contributing to it.
                    return onABranch && (branchFrozen == true || mainMissing == true || mainAbandoned == true)
                        ? UploadAct.Fork
                        : UploadAct.Update;

                default:
                    // Not yours: a contribution, if the lineage can take one. A headless lineage
                    // takes none — the server refuses to let the next upload inherit its following.
                    return acceptsBranches == false || mainMissing == true || mainAbandoned == true
                        ? UploadAct.Fork
                        : UploadAct.Contribute;
            }
        }

        /// <summary>
        /// The word on the button. The mod's, unchanged, because it is what every player has read
        /// first; a second vocabulary in the Manager was two names for one act.
        /// </summary>
        public static string Verb(UploadAct act)
        {
            switch (act)
            {
                case UploadAct.Update: return "Update";
                case UploadAct.Contribute: return "Contribute";
                case UploadAct.Fork: return "Fork";
                default: return "Upload";
            }
        }

        /// <summary>
        /// Why the natural act is closed, with the way on — or null when nothing closes it.
        ///
        /// ⚠ One wall at a time, the one that explains the most first: a Main that is gone makes
        /// its refusal of contributions beside the point. Each sentence is the fact, then what to
        /// do, in the words the mod already used for it.
        /// </summary>
        /// <param name="owner">Who leads the lineage, when known. Named when it can be — "somebody" leaves nowhere to look.</param>
        public static string? Wall(Publication publication, bool onABranch, string? owner,
                                   bool? acceptsBranches, bool? mainMissing,
                                   bool? mainAbandoned, bool? branchFrozen)
        {
            if (publication != Publication.NotYours && !(publication == Publication.Published && onABranch))
                return null;

            if (mainMissing == true)
            {
                return "The translation this contributes to has been removed by its author. Your "
                     + "lines are safe, and your copy is now the only one: Fork publishes it as "
                     + "your own version.";
            }

            if (mainAbandoned == true)
            {
                return "The account that owned this translation has been deleted, so no "
                     + "contribution will ever be read. The translation itself is still published "
                     + "and still works. Your lines are safe: Fork publishes them as your own version.";
            }

            if (onABranch)
            {
                return branchFrozen == true
                    ? "The translation you contribute to no longer accepts contributions, so this "
                      + "can no longer be sent. Your lines are safe: Fork keeps them and publishes "
                      + "them under your own name."
                    : null;
            }

            if (acceptsBranches == false)
            {
                string who = string.IsNullOrWhiteSpace(owner) ? "The author" : People.Mention(owner);
                return who + " works alone on this one and does not take contributions. Your "
                     + "lines are safe: Fork keeps them and publishes them under your own name.";
            }

            return null;
        }

        /// <summary>
        /// Whether this act is taken in the game and nowhere else — the first act in somebody
        /// else's lineage. Updating one's own row afterwards is not.
        /// </summary>
        public static bool DecidedInTheGame(UploadAct act) =>
            act == UploadAct.Contribute || act == UploadAct.Fork;

        /// <summary>Said by a tool that cannot take the act, after the wall when there is one.</summary>
        public const string OnlyInTheGame =
            "Contributing and forking are decided in the game: open it and choose there.";
    }
}
