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
    /// The publish button as a screen draws it: what it would do, the word on it, the line under
    /// it, and why it is closed when it is.
    ///
    /// ⚠ <see cref="Hint"/> is a sentence for the interface's own translation; a username never
    /// goes through that, so it travels apart in <see cref="Mention"/> and the screen appends it.
    /// The one hint that already embeds a name — the wall — says so with
    /// <see cref="HintIsTranslatable"/> false.
    /// </summary>
    public struct UploadButton
    {
        /// <summary>What pressing it would do; null when there is nothing on this machine to send.</summary>
        public UploadAct? Act;

        /// <summary>The word on the button. "Sync" when both sides moved: that exchange comes first.</summary>
        public string Verb;

        /// <summary>What pressing it does, said under the button while it is open.</summary>
        public string Hint;

        /// <summary>Whether <see cref="Hint"/> may go through the interface's translation.</summary>
        public bool HintIsTranslatable;

        /// <summary>A username the screen appends to <see cref="Hint"/>, as data, or null.</summary>
        public string? Mention;

        /// <summary>Why it cannot act, said under the button instead of the hint — or null when it can.</summary>
        public string? Closed;

        /// <summary>
        /// The wall in full — the fact, then the way out — when a wall is what turned the act
        /// into a fork; null otherwise. For a screen with no status card beside the button (the
        /// upload window, a tool's list), where <see cref="Hint"/> may carry the way out alone.
        /// </summary>
        public string? Wall;

        public bool Enabled => Closed == null;
    }

    /// <summary>
    /// A wall, in two halves: the fact that closed the road, and the way out. Where the eye reads
    /// the fact first (a status card) it gets the fact; beside the button it gets the way out;
    /// alone in a corner or a window, both — so the same event is never told twice in two
    /// wordings on one screen.
    /// </summary>
    public struct WallText
    {
        public string Fact;
        public string WayOut;
        public string Whole => Fact + " " + WayOut;
    }

    /// <summary>The four walls, each written once. The words every product reads.</summary>
    internal static class Walls
    {
        public static readonly WallText MainMissing = new WallText
        {
            Fact = "The Main was removed by its author.",
            WayOut = "Contributions cannot be sent there any more. Fork keeps your lines as your own version.",
        };

        public static readonly WallText MainAbandoned = new WallText
        {
            Fact = "The account behind the Main was deleted. The Main is still published and still works.",
            WayOut = "Contributions will not be read any more. Fork keeps your lines as your own version.",
        };

        public static readonly WallText BranchFrozen = new WallText
        {
            Fact = "The Main no longer accepts contributions.",
            WayOut = "Contributions cannot be sent there any more. Fork keeps your lines as your own version.",
        };

        /// <param name="owner">Who leads the lineage, when known. Named when it can be — "somebody" leaves nowhere to look.</param>
        public static WallText WorksAlone(string? owner) => new WallText
        {
            Fact = (string.IsNullOrWhiteSpace(owner) ? "The author" : People.Mention(owner))
                 + " works alone on this one and does not take contributions.",
            WayOut = "Fork keeps your lines as your own version.",
        };
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
        /// Why the natural act is closed, with the way on — or null when nothing closes it. The
        /// two halves of <see cref="WallOf"/> in one sentence, for a place that shows nothing else
        /// about it (a corner notification, a tool's list).
        /// </summary>
        public static string? Wall(Publication publication, bool onABranch, string? owner,
                                   bool? acceptsBranches, bool? mainMissing,
                                   bool? mainAbandoned, bool? branchFrozen)
            => WallOf(publication, onABranch, owner, acceptsBranches, mainMissing, mainAbandoned, branchFrozen)?.Whole;

        /// <summary>
        /// The wall in two halves — the fact, and the way out — or null when nothing closes the road.
        ///
        /// ⚠ One wall at a time, the one that explains the most first: a Main that is gone makes
        /// its refusal of contributions beside the point.
        /// </summary>
        /// <param name="owner">Who leads the lineage, when known. Named when it can be — "somebody" leaves nowhere to look.</param>
        public static WallText? WallOf(Publication publication, bool onABranch, string? owner,
                                       bool? acceptsBranches, bool? mainMissing,
                                       bool? mainAbandoned, bool? branchFrozen)
        {
            if (publication != Publication.NotYours && !(publication == Publication.Published && onABranch))
                return null;

            if (mainMissing == true) return Walls.MainMissing;
            if (mainAbandoned == true) return Walls.MainAbandoned;

            if (onABranch)
                return branchFrozen == true ? Walls.BranchFrozen : (WallText?)null;

            if (acceptsBranches == false) return Walls.WorksAlone(owner);

            return null;
        }

        /// <summary>
        /// Whether this act is taken in the game and nowhere else — the first act in somebody
        /// else's lineage. Updating one's own row afterwards is not.
        /// </summary>
        public static bool DecidedInTheGame(UploadAct act) =>
            act == UploadAct.Contribute || act == UploadAct.Fork;

        /// <summary>
        /// Why the act cannot be taken right now, or null when it can.
        ///
        /// 🔴 **A refusal known BEFORE the click is said before the click.** Every product here
        /// offers publishing from more than one place — the mod from its panel and from the corner
        /// notification, the Manager from a game's card — and each place used to work out for
        /// itself whether the act was open. One of them did not: the notification's Contribute
        /// button opened the upload window unconditionally, and somebody with no account learnt
        /// they needed one only after filling it in and pressing Upload.
        ///
        /// ⚠ **It answers with the REASON, never with a bool**, and that is the whole point: a
        /// control that cannot act has to say why, right there. A caller that only wants to know
        /// whether it may act asks whether this is null.
        ///
        /// ⚠ **Order matters, most-explaining first.** "There is nothing to send" outranks "you
        /// are not signed in": telling somebody to sign in for an upload that would be empty sends
        /// them round a loop that ends in the same place.
        ///
        /// ⚠ **A fork asks for neither the network nor an account**, so the last three do not
        /// apply to it: it is local from end to end — a new lineage on this machine, nothing sent
        /// — and it is precisely the way on when the walls above are up.
        /// </summary>
        /// <param name="act">What would be taken; see <see cref="ActOf"/>.</param>
        /// <param name="lines">How many lines the file holds. Nothing to send is nothing to send.</param>
        /// <param name="untouchedCopy">A fork still holding, line for line, the file it came from.</param>
        /// <param name="online">Whether this install talks to the site at all.</param>
        /// <param name="signedIn">Whether an account is in place here.</param>
        /// <param name="inSync">Whether what is here is already what is published.</param>
        public static string? ClosedReason(UploadAct act, int lines, bool untouchedCopy,
                                           bool online, bool signedIn, bool inSync)
        {
            if (lines == 0) return "Nothing to upload";

            // The fact, then the way out. Naming the author would need a lookup nobody has after
            // a fork — the lineage is gone — and the sentence works without it.
            if (untouchedCopy)
                return "This copy is unchanged. Translate or correct a line to publish it as yours.";

            if (act == UploadAct.Fork) return null;

            if (!online) return "Offline mode: nothing is sent";
            if (!signedIn) return "Login required";
            if (inSync) return "Up to date — nothing to send";

            return null;
        }

        /// <summary>
        /// The publish button, whole, from where the translation stands and the facts around it.
        ///
        /// 🔴 **Written because two screens of the mod derived it for themselves** (2026-09-16):
        /// the main screen's Actions row and the upload window's mode check each composed
        /// <see cref="ActOf"/>, <see cref="Wall"/> and <see cref="ClosedReason"/> with their own
        /// glue — the verb, the hint, when "Sync" replaces the verb, which wall to show — and the
        /// corner notification did it a third way. One composition, held by the corpus.
        ///
        /// ⚠ **"Sync" comes before any act.** When both sides moved, the button settles that
        /// exchange first, whatever the act would have been; the act is still returned, since it
        /// is what follows.
        /// </summary>
        public static UploadButton Button(Standing standing, LocalFacts local, ServerFacts server, AccountFacts account)
        {
            bool onABranch = Standings.OnABranch(standing);
            var act = ActOf(standing.Publication, onABranch, server.AcceptsBranches,
                            server.MainMissing, server.MainAbandoned, server.BranchFrozen);
            var taken = act ?? UploadAct.Upload;
            bool bothMoved = standing.Sync == SyncDirection.Merge;

            string hint;
            bool translatable = true;
            string? mention = null;
            string? wholeWall = null;

            if (bothMoved)
            {
                hint = "Both local (" + local.LocalChanges + " changes) and server were updated. Click to sync.";
            }
            else if (taken == UploadAct.Fork)
            {
                // The wall in the socle's words. On a screen whose status card already states the
                // fact — an author looking at their own published branch, which is exactly when
                // StatusCards.Notice speaks — the button says only the way out, which this button
                // now is; anywhere else, the fact too. A wall may name the Main's owner, so it is
                // written as it is.
                string? owner = string.IsNullOrEmpty(server.MainUsername) ? server.Uploader : server.MainUsername;
                var wall = WallOf(standing.Publication, onABranch, owner, server.AcceptsBranches,
                                  server.MainMissing, server.MainAbandoned, server.BranchFrozen);
                bool cardStatesTheFact = standing.Publication == Publication.Published && server.IsOwner;
                translatable = wall == null;
                wholeWall = wall?.Whole;
                hint = wall == null
                    ? "Leave this translation and publish your lines as your own"
                    : cardStatesTheFact ? wall.Value.WayOut : wall.Value.Whole;
            }
            else if (taken == UploadAct.Update)
            {
                // Say WHICH kind of change is pending, otherwise an update offered after a mere
                // font or exclusion edit looks like the mod lost track of what was synced.
                string id = server.SiteId is int site ? " #" + site : "";
                if (local.LocalChanges > 0)
                    hint = "Update" + id + " (" + local.LocalChanges + " local changes)";
                else if (local.MetadataDirty)
                    hint = "Update" + id + " — settings changed (fonts, images, exclusions)";
                else
                    hint = "Update your translation" + id;
            }
            else if (taken == UploadAct.Contribute)
            {
                hint = "Contribute as a branch to";
                mention = server.Uploader;
            }
            else
            {
                hint = "Create a new translation";
            }

            // A fork that has not been touched holds somebody else's file, line for line — and
            // only while it has never been published: the marker travels inside the file, so
            // whoever downloads a fork carries it too, in a lineage where other rules answer.
            bool untouchedCopy = !server.Exists && local.ForkStillTheCopy;

            return new UploadButton
            {
                Act = act,
                Verb = bothMoved ? "Sync" : Verb(taken),
                Hint = hint,
                HintIsTranslatable = translatable,
                Mention = mention,
                Closed = ClosedReason(taken, local.Lines, untouchedCopy, account.Online, account.SignedIn,
                                      standing.Sync == SyncDirection.InSync),
                Wall = wholeWall,
            };
        }

        /// <summary>Said by a tool that cannot take the act, after the wall when there is one.</summary>
        public const string OnlyInTheGame =
            "Contributing and forking are decided in the game: open it and choose there.";
    }
}
