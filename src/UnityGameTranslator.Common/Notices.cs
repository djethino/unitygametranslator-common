namespace UnityGameTranslator.Common
{
    /// <summary>
    /// What is waiting between a translation and the site, as the product that watches it reports
    /// it — the six facts a corner notification is written from. Each is already the product's
    /// verdict; this only carries them.
    /// </summary>
    public struct SyncWork
    {
        /// <summary>Signed in, and the server has not yet said what this account is to this lineage — nothing may be written in the second person yet.</summary>
        public bool WaitingForAccount;

        /// <summary>Lines captured or edited here, not sent yet.</summary>
        public bool HasLocalChanges;

        /// <summary>Fonts, images, exclusions, variables — settings that travel with the translation — changed, not sent.</summary>
        public bool HasMetadataChanges;

        /// <summary>A newer version waits on the site and nothing here conflicts.</summary>
        public bool HasServerUpdate;

        /// <summary>Both sides moved.</summary>
        public bool NeedsMerge;

        /// <summary>Branch only: its Main published something since the last merge from it.</summary>
        public bool HasMainUpdate;

        /// <summary>Main only: contributions never reviewed, or changed since.</summary>
        public int BranchesPendingReview;

        /// <summary>Anything at all worth showing.</summary>
        public bool Any => !WaitingForAccount
                           && (HasLocalChanges || HasMetadataChanges || HasServerUpdate
                               || NeedsMerge || HasMainUpdate || BranchesPendingReview > 0);
    }

    /// <summary>What the notification's one button does, when it has one.</summary>
    public enum SyncAction
    {
        /// <summary>No button: the notification offers a choice instead, or nothing.</summary>
        None,
        /// <summary>Settle both sides.</summary>
        Sync,
        /// <summary>Take the newer published version.</summary>
        Download,
        /// <summary>Merge what the Main published into this branch.</summary>
        MergeFromMain,
        /// <summary>Send this account's own changes to its own row.</summary>
        Update,
        /// <summary>Open the contributions waiting on this Main.</summary>
        Review,
        /// <summary>Two buttons instead of one: contribute (branch) or go independent (fork).</summary>
        ChooseBranchOrFork,
    }

    /// <summary>
    /// The corner notification about a translation, whole: whether it shows, what it says, what
    /// its button does, and — when the choice is between contributing and forking — which of the
    /// two doors is open and why the other is not.
    ///
    /// ⚠ <see cref="Message"/> is a sentence for the interface's own translation; a username
    /// never goes through that, so it travels apart in <see cref="Mention"/> and the screen
    /// appends it. <see cref="Wall"/> already names somebody and is written as it is.
    /// </summary>
    public struct SyncNotice
    {
        public bool Show;
        public string Message;
        public string? Mention;
        public SyncAction Action;

        /// <summary>The word on the button, when <see cref="Action"/> is one.</summary>
        public string? Verb;

        /// <summary>Contribute is offered — the lineage takes contributions.</summary>
        public bool OffersBranch;

        /// <summary>Why contributing cannot be taken right now, or null when it can.</summary>
        public string? BranchClosed;

        /// <summary>Why forking cannot be taken right now, or null when it can.</summary>
        public string? ForkClosed;

        /// <summary>Why the lineage takes no contribution — said instead of offering one — or null.</summary>
        public string? Wall;
    }

    /// <summary>
    /// What a screen says, unasked, about a translation — decided once so that a corner of the
    /// game, a card and a row in a tool cannot say different things about the same fact.
    /// </summary>
    public static class Notices
    {
        /// <summary>
        /// The corner notification, from what is waiting, where the translation stands, and the
        /// facts it stands on.
        ///
        /// 🔴 **Written because the mod's corner did this on its own, with its own glue over the
        /// socle's rules** (2026-09-16): which act was offered, whether contributing was open, and
        /// the words — a chain of two hundred lines beside a main screen that answered the same
        /// questions its own way. One sequence, most urgent first: a conflict, then a version
        /// waiting on the site, then one waiting upstream, then this machine's unsent lines,
        /// then its unsent settings, then contributions waiting on a Main.
        ///
        /// ⚠ **Somebody else's lineage is offered a CHOICE, never an act**: contribute (branch) or
        /// go independent (fork). Whether contributing is on the table is the socle's
        /// (<see cref="Uploads.ActOf"/>); when it is not, the wall says which one it is, in the
        /// same words the main screen and the Manager show. The two doors are then judged by
        /// <see cref="Uploads.ClosedReason"/>, separately: a fork asks for neither the network nor
        /// an account, and stays open precisely when the other door is shut.
        ///
        /// ⚠ A message with a count keeps it inline — the interface's translation placeholders
        /// numbers — and the person it names travels apart in <see cref="SyncNotice.Mention"/>.
        /// </summary>
        public static SyncNotice Sync(SyncWork work, Standing standing, LocalFacts local, ServerFacts server, AccountFacts account)
        {
            var notice = new SyncNotice { Show = work.Any, Message = "", Action = SyncAction.None };
            if (!work.Any) return notice;

            bool onABranch = Standings.OnABranch(standing);
            var offered = Uploads.ActOf(standing.Publication, onABranch, server.AcceptsBranches,
                                        server.MainMissing, server.MainAbandoned, server.BranchFrozen);
            bool canBranch = offered == UploadAct.Contribute;
            bool ours = server.IsOwner;

            // The count of what moved on the site, when the client has it: the one thing somebody
            // asks before deciding — "what changes?" — and a sentence that only says "moved" sends
            // them to look. Numbers stay inline: the pipeline placeholders them.
            string onTheSite = server.LinesChanged is int changed ? " (" + changed + " lines)" : "";

            if (work.NeedsMerge)
            {
                if (ours) notice.Message = "Changed here (" + local.LocalChanges + ") and on the site" + onTheSite + ". Sync needed.";
                else { notice.Message = "Sync needed — translation updated" + onTheSite + " by"; notice.Mention = server.Uploader; }
                notice.Action = SyncAction.Sync;
                notice.Verb = "Sync";
            }
            else if (work.HasServerUpdate)
            {
                // Ours, Main or branch alike: it is our OWN published version that moved — another
                // machine, or the site editor. Somebody else's: the Main we downloaded from moved.
                if (ours) notice.Message = "Update available on the site" + onTheSite + ".";
                else { notice.Message = "Translation updated" + onTheSite + " by"; notice.Mention = server.Uploader; }
                notice.Action = SyncAction.Download;
                notice.Verb = "Download";
            }
            else if (work.HasMainUpdate)
            {
                // Genuinely upstream: merged into the branch, never downloaded over it.
                notice.Message = "The Main was updated by";
                notice.Mention = server.Uploader;
                notice.Action = SyncAction.MergeFromMain;
                notice.Verb = "Update";
            }
            else if (work.HasLocalChanges)
            {
                if (ours)
                {
                    notice.Message = local.LocalChanges + " local changes to upload.";
                    notice.Action = SyncAction.Update;
                    notice.Verb = "Update";
                }
                else
                {
                    notice.Message = "You changed " + local.LocalChanges + " line(s). Share them?";
                    notice.Action = SyncAction.ChooseBranchOrFork;
                }
            }
            else if (work.HasMetadataChanges)
            {
                if (ours)
                {
                    notice.Message = "Translation settings changed — not uploaded yet";
                    notice.Action = SyncAction.Update;
                    notice.Verb = "Update";
                }
                else
                {
                    notice.Message = "You changed translation settings. Share them?";
                    notice.Action = SyncAction.ChooseBranchOrFork;
                }
            }
            else if (work.BranchesPendingReview > 0)
            {
                // Last, and rightly so: nothing degrades while it waits. But a contribution nobody
                // ever hears about is a contributor lost.
                notice.Message = work.BranchesPendingReview + " contribution(s) waiting for your review";
                notice.Action = SyncAction.Review;
                notice.Verb = "Review";
            }
            else
            {
                notice.Message = local.LocalChanges + " local changes";
                notice.Action = SyncAction.Sync;
                notice.Verb = "Sync";
            }

            if (notice.Action == SyncAction.ChooseBranchOrFork)
            {
                // A fork that still is the copy it came from — only while nothing of it is published.
                bool untouchedCopy = !server.Exists && local.ForkStillTheCopy;

                notice.OffersBranch = canBranch;
                notice.BranchClosed = Uploads.ClosedReason(UploadAct.Contribute, local.Lines, untouchedCopy,
                                                           account.Online, account.SignedIn, inSync: false);
                notice.ForkClosed = Uploads.ClosedReason(UploadAct.Fork, local.Lines, untouchedCopy,
                                                         account.Online, account.SignedIn, inSync: false);
                notice.Wall = canBranch
                    ? null
                    : Uploads.Wall(standing.Publication, onABranch,
                                   string.IsNullOrEmpty(server.MainUsername) ? server.Uploader : server.MainUsername,
                                   server.AcceptsBranches, server.MainMissing, server.MainAbandoned, server.BranchFrozen);
            }

            return notice;
        }
    }
}
