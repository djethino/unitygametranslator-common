using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// What one button does with a translation file.
    ///
    /// 🔴 The defect these hold the door on: both clients announced "Contribute" over a
    /// translation whose author works alone, and the server refused after the upload. Each kept
    /// part of the rule; none had the list. Now the list is here, and the act a tool may not take
    /// is named as such rather than opened.
    /// </summary>
    internal static class UploadsChecks
    {
        private static UploadAct? Act(Publication publication, bool onABranch = false,
                                      bool? accepts = null, bool? missing = null,
                                      bool? abandoned = null, bool? frozen = null) =>
            Uploads.ActOf(publication, onABranch, accepts, missing, abandoned, frozen);

        private static string? Wall(Publication publication, bool onABranch = false,
                                    bool? accepts = null, bool? missing = null,
                                    bool? abandoned = null, bool? frozen = null) =>
            Uploads.Wall(publication, onABranch, "djeitinho", accepts, missing, abandoned, frozen);

        public static void Run(Action<bool, string, string> check)
        {
            check(Act(Publication.NeverPublished) == UploadAct.Upload,
                "nothing published is uploaded", "it creates a lineage under this name");

            check(Act(Publication.Published) == UploadAct.Update,
                "one's own Main is updated", "whatever became of its contributors");

            check(Act(Publication.Published, onABranch: true) == UploadAct.Update,
                "one's own branch is updated too", "the first act was taken; the rest is upkeep");

            check(Act(Publication.NotDownloaded) == null,
                "nothing here means nothing to send", "an act needs a file");

            // ── Somebody else's lineage ──────────────────────────────────────
            check(Act(Publication.NotYours, accepts: true) == UploadAct.Contribute,
                "a Main that takes contributions gets one", "the file becomes a branch for review");

            // ⚠ Silence is not a refusal: an older site never says, and locking people out of it
            // would be inventing a no.
            check(Act(Publication.NotYours) == UploadAct.Contribute,
                "a Main that was not asked is not a refusal", "null is 'not asked', never 'no'");

            // 🔴 The case both clients got wrong.
            check(Act(Publication.NotYours, accepts: false) == UploadAct.Fork,
                "a Main that works alone leaves one way on: Fork", "the server refuses a branch there");

            check(Act(Publication.NotYours, accepts: true, missing: true) == UploadAct.Fork
                  && Act(Publication.NotYours, accepts: true, abandoned: true) == UploadAct.Fork,
                "a headless lineage takes no contribution", "nobody would ever read it");

            // ── A branch whose road ended ────────────────────────────────────
            check(Act(Publication.Published, onABranch: true, frozen: true) == UploadAct.Fork
                  && Act(Publication.Published, onABranch: true, missing: true) == UploadAct.Fork
                  && Act(Publication.Published, onABranch: true, abandoned: true) == UploadAct.Fork,
                "a branch on a closed, gone or abandoned Main can only fork", "the same wall from the other side");

            check(Act(Publication.Published, onABranch: false, frozen: true, missing: true) == UploadAct.Update,
                "a Main's own row is never walled", "the walls describe what a branch sits on");

            // ── Words ────────────────────────────────────────────────────────
            check(Uploads.Verb(UploadAct.Upload) == "Upload" && Uploads.Verb(UploadAct.Update) == "Update"
                  && Uploads.Verb(UploadAct.Contribute) == "Contribute" && Uploads.Verb(UploadAct.Fork) == "Fork",
                "the verbs are the mod's", "one vocabulary, three products");

            check(Wall(Publication.NotYours, accepts: false)!.Contains("@djeitinho")
                  && Wall(Publication.NotYours, accepts: false)!.Contains("Fork"),
                "the solo-work wall names who and the way on", "a wall without a door is a dead end");

            check(Wall(Publication.NotYours, accepts: true) == null && Wall(Publication.Published) == null
                  && Wall(Publication.NeverPublished, accepts: false) == null,
                "no wall where nothing is closed", "a sentence on an open door reads as a refusal");

            check(Wall(Publication.Published, onABranch: true, frozen: true)!.Contains("no longer accepts"),
                "a frozen branch is told it closed since", "nothing on its side changed, so it has to be said");

            // The one that explains the most, first: gone beats closed.
            check(Wall(Publication.NotYours, accepts: false, missing: true)!.Contains("removed"),
                "a Main that is gone outranks one that works alone", "there is nobody to work alone");

            // 🔴 The first act in somebody else's lineage is taken in the game, and nowhere else.
            check(Uploads.DecidedInTheGame(UploadAct.Contribute) && Uploads.DecidedInTheGame(UploadAct.Fork)
                  && !Uploads.DecidedInTheGame(UploadAct.Upload) && !Uploads.DecidedInTheGame(UploadAct.Update),
                "contributing and forking are the game's acts", "creating and updating are anybody's");

            WhyItIsClosed(check);
        }

        /// <summary>
        /// The refusal every screen that offers publishing has to say BEFORE the click.
        ///
        /// 🔴 It lives here because more than one place offers the act, and one of them used to
        /// work it out for itself — badly: the mod's corner notification opened the upload window
        /// with no account in place, and the refusal only arrived once the form had been filled in.
        /// </summary>
        private static void WhyItIsClosed(Action<bool, string, string> check)
        {
            string Closed(UploadAct act, int lines = 40, bool copy = false,
                          bool online = true, bool signedIn = true, bool inSync = false) =>
                Uploads.ClosedReason(act, lines, copy, online, signedIn, inSync);

            check(Closed(UploadAct.Update) == null,
                "an update with work to send is open",
                "the ordinary case: signed in, online, something to say");

            check(Closed(UploadAct.Contribute, signedIn: false) == "Login required",
                "contributing asks for an account, and says so",
                "this is the one that was found out only after the window was filled in");

            check(Closed(UploadAct.Update, online: false) == "Offline mode - upload disabled",
                "nothing is sent from an install that talks to nobody",
                "the account is beside the point when the network is off");

            check(Closed(UploadAct.Update, inSync: true) == "Up to date — nothing to send",
                "and nothing is sent when there is nothing to send",
                "an offer to publish what is already published reads as the mod losing track");

            // 🔴 A fork is local from end to end — a new lineage on this machine, nothing sent —
            // and it is the way ON when the walls are up. Asking it for an account or a network
            // would close the one door that answers them.
            check(Closed(UploadAct.Fork, online: false, signedIn: false) == null,
                "a fork asks for neither the network nor an account",
                "it writes one local file, and it is what a closed lineage leaves open");

            check(Closed(UploadAct.Fork, lines: 0) == "No translations to upload",
                "but a fork of nothing is still nothing",
                "the two refusals above it are about the file, not about sending it");

            check(Closed(UploadAct.Fork, copy: true).StartsWith("This copy is unchanged"),
                "and a copy nobody has touched is not somebody's work",
                "publishing it puts a second identical entry on the site under a new name");

            // ⚠ Order, and it is the one thing a reader would get wrong: telling somebody to sign
            // in for an upload that would be empty sends them round a loop back to the same place.
            check(Closed(UploadAct.Contribute, lines: 0, signedIn: false) == "No translations to upload",
                "the emptier reason is said first",
                "signing in would not change the answer");
        }
    }
}
