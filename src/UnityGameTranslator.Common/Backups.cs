using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Why a copy of a translation was taken, when nobody asked for it.
    ///
    /// 🔴 **A closed list, and it is the most useful column of the whole screen.** Choosing between
    /// five dated copies is a lottery; choosing between "before installing somebody's translation"
    /// and "before writing a merge" is a decision. The act is known at the moment of writing and
    /// costs nothing to record — it was simply never recorded.
    /// </summary>
    public enum BackupReason
    {
        /// <summary>Somebody asked for this copy. No act caused it.</summary>
        Saved,

        /// <summary>A community translation was installed over the one that was here.</summary>
        Installed,

        /// <summary>The result of a merge was written.</summary>
        Merged,

        /// <summary>What came back from the browser editor was written.</summary>
        Edited,

        /// <summary>An update was downloaded from the site.</summary>
        Downloaded,

        /// <summary>The local translation was removed.</summary>
        Removed,

        /// <summary>Another copy was put back, and this was what stood there.</summary>
        Restored,

        /// <summary>
        /// Something replaced the file and did not say what. Never written on purpose: it is what
        /// a copy from an older version reads as, and what a caller that forgot to say gets.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// One copy of a translation, as a screen needs to describe it.
    ///
    /// ⚠ Everything here comes from the copy's own little `about` file, NOT from opening the
    /// translation beside it. Fifteen files of half a megabyte, parsed to draw a list, is a list
    /// that takes a second to appear in a game.
    /// </summary>
    public struct BackupEntry
    {
        /// <summary>Folder name, which is also the identity: `auto-20260817-143210`.</summary>
        public string Id;

        /// <summary>When it was taken. Local time, as the person experienced it.</summary>
        public DateTime At;

        public BackupReason Reason;

        /// <summary>Whose translation was involved, when the act involved somebody. May be null.</summary>
        public string? By;

        /// <summary>What the person called it. Only ever set on a saved copy.</summary>
        public string? Label;

        public int Lines;

        /// <summary>Of those, the ones a human wrote or settled. The figure that says "my work".</summary>
        public int ByHand;

        /// <summary>The lineage this copy belongs to — see <see cref="Backups.IsAnotherLineage"/>.</summary>
        public string? Uuid;

        /// <summary>Whether it carries the fonts and images the translation names.</summary>
        public bool WithAssets;

        public bool IsSaved => Reason == BackupReason.Saved;
    }

    /// <summary>
    /// How a translation's own history is kept — the same way in the mod and in the manager.
    ///
    /// 🔴 **Here because the mod exists without the manager**, today and after the manager ships.
    /// The mod is the only product present in every installation, so it owns the mechanism and the
    /// manager is a second window onto the same folders. What must never differ is the layout, the
    /// limits and the words: a copy taken in the game and read by the tool has to be the same thing.
    ///
    /// 🔴 **And because a write path forgot once already.** The mod's own comment, on the fourth
    /// place that replaces the file: *"This one overwrote a player's own work with a community
    /// version and left nothing behind — and the settings dialog was meanwhile promising a
    /// backup."* Nine call sites across two products each had to remember. The answer is not a
    /// longer list; it is that the copy belongs INSIDE the one write path, so a path added later
    /// cannot skip it.
    ///
    /// ⚠ **Decisions only, no file access and no serializer.** This library has no dependencies by
    /// design (see its csproj), and the two products disagree about their JSON. So the rules live
    /// here and the reading and writing lives in each product — exactly as <see cref="Badges"/>
    /// decides the chips and leaves the drawing alone.
    /// </summary>
    public static class Backups
    {
        /// <summary>The folder, inside the mod's own data folder for that game.</summary>
        public const string FolderName = "backups";

        /// <summary>What a copy's description file is called, inside its folder.</summary>
        public const string AboutFileName = "about.json";

        /// <summary>
        /// How many automatic copies are kept, oldest dropped.
        ///
        /// ⚠ **Five, not three.** One session can chain three replacements — install a community
        /// translation, merge it, then write back what the browser editor returned. At three, the
        /// state from BEFORE that session is already gone, which is precisely the one somebody
        /// reaches for when they realise the session went wrong.
        /// </summary>
        public const int AutomaticKept = 5;

        /// <summary>
        /// How many copies somebody may hold deliberately.
        ///
        /// 🔴 **Nothing is ever evicted from these.** Full means the button refuses and says so;
        /// making room is a decision, taken by the person who decided to keep them. Rotating them
        /// would delete work somebody explicitly chose to protect — the opposite of what they
        /// pressed the button for.
        /// </summary>
        public const int SavedKept = 10;

        /// <summary>Folders holding the assets a translation may name.</summary>
        public static readonly string[] AssetFolders = { "fonts", "images" };

        // ── Naming ────────────────────────────────────────────────────────

        /// <summary>
        /// The folder name for a new copy: `auto-20260817-143210` or `saved-20260817-143210`.
        ///
        /// ⚠ Sortable as text, and that is deliberate: the order on screen must not depend on a
        /// file timestamp, which a copy, a sync tool or an archive extraction rewrites.
        /// </summary>
        public static string NewId(BackupReason reason, DateTime at) =>
            (reason == BackupReason.Saved ? "saved-" : "auto-")
            + at.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>Whether a folder name is one of ours, and which family it belongs to.</summary>
        public static bool IsBackupFolder(string name, out bool saved)
        {
            saved = false;
            if (string.IsNullOrEmpty(name)) return false;

            if (name.StartsWith("saved-", StringComparison.Ordinal)) { saved = true; return true; }
            return name.StartsWith("auto-", StringComparison.Ordinal);
        }

        // ── What goes into a copy ─────────────────────────────────────────

        /// <summary>
        /// The asset files a copy must carry, relative to the mod's data folder.
        ///
        /// 🔴 **Only what the translation NAMES.** A translation file lists the images it puts in
        /// place (`_images[].file`) and the fonts it asks for, so the set is computable — which is
        /// what makes a copy of "the translation and its assets" possible at all rather than a copy
        /// of the whole folder.
        ///
        /// ⚠ **Generated atlases are never included.** They are rebuilt on demand from the font
        /// beside them, and they are the largest thing in that folder. Keeping them would multiply
        /// the size of every copy for something no one can lose.
        ///
        /// ⚠ Parsing stays with the caller: both products already read that file with their own
        /// JSON library, and this one has neither.
        /// </summary>
        /// <param name="imageFiles">Values of `_images[].file`, in any order. Nulls tolerated.</param>
        /// <param name="fontFiles">Font FILE names the translation asks for. Nulls tolerated.</param>
        public static List<string> AssetsToCopy(IEnumerable<string?>? imageFiles,
                                                IEnumerable<string?>? fontFiles)
        {
            var chosen = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Take(IEnumerable<string?>? names, string folder)
            {
                if (names == null) return;

                foreach (var name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    // ⚠ A bare file name, never a path. These come out of a translation file that
                    // anybody may edit, and a name walking up with ".." would have the copy reach
                    // outside the folder it is copying.
                    var bare = name!.Trim().Replace('\\', '/');
                    var slash = bare.LastIndexOf('/');
                    if (slash >= 0) bare = bare.Substring(slash + 1);

                    if (bare.Length == 0 || bare == "." || bare == "..") continue;

                    var relative = folder + "/" + bare;
                    if (seen.Add(relative)) chosen.Add(relative);
                }
            }

            Take(imageFiles, "images");
            Take(fontFiles, "fonts");

            return chosen;
        }

        // ── What rotates, and what refuses ────────────────────────────────

        /// <summary>
        /// Which automatic copies to drop, newest kept — the ids, oldest first.
        ///
        /// ⚠ Saved copies are not eligible and are not even looked at: they do not rotate.
        /// </summary>
        public static List<string> AutomaticToDrop(IEnumerable<BackupEntry> all)
        {
            var automatic = new List<BackupEntry>();
            foreach (var entry in all)
            {
                if (!entry.IsSaved) automatic.Add(entry);
            }

            automatic.Sort((a, b) => b.At.CompareTo(a.At));

            var drop = new List<string>();
            for (var i = AutomaticKept; i < automatic.Count; i++) drop.Add(automatic[i].Id);

            return drop;
        }

        /// <summary>How many of the deliberate slots are used.</summary>
        public static int SavedCount(IEnumerable<BackupEntry> all)
        {
            var count = 0;
            foreach (var entry in all)
            {
                if (entry.IsSaved) count++;
            }

            return count;
        }

        /// <summary>Whether another deliberate copy may be taken.</summary>
        public static bool CanSaveAnother(IEnumerable<BackupEntry> all) =>
            SavedCount(all) < SavedKept;

        /// <summary>
        /// Why the button is unavailable, or null when it is available.
        ///
        /// ⚠ Never a greyed control without words — the rule this project holds everywhere.
        /// </summary>
        public static string? WhyCannotSave(IEnumerable<BackupEntry> all) =>
            CanSaveAnother(all)
                ? null
                : $"{SavedKept} copies kept. Delete one to make room.";

        // ── Words ─────────────────────────────────────────────────────────

        /// <summary>
        /// What a row says about itself, in one line.
        ///
        /// 🔴 Plain international English: the mod and the manager ship no translations, so this is
        /// read as it stands by somebody whose fourth language it is. "Before installing a
        /// community translation", not "pre-install snapshot".
        /// </summary>
        /// <param name="by">Whose translation the act involved, already written as a mention.</param>
        public static string Describe(BackupReason reason, string? by = null)
        {
            var who = string.IsNullOrWhiteSpace(by) ? null : by!.Trim();

            switch (reason)
            {
                case BackupReason.Saved:
                    return "Saved by you";

                case BackupReason.Installed:
                    return who is null
                        ? "Before installing a community translation"
                        : "Before installing " + who + "'s translation";

                case BackupReason.Merged:
                    return "Before writing a merge";

                case BackupReason.Edited:
                    return "Before writing what came back from the browser";

                case BackupReason.Downloaded:
                    return who is null
                        ? "Before a downloaded update"
                        : "Before an update from " + who;

                case BackupReason.Removed:
                    return "When the translation was removed";

                case BackupReason.Restored:
                    return "Before another copy was put back";

                default:
                    return "Before something replaced the translation";
            }
        }

        /// <summary>Headings, so the two lists are named identically in both products.</summary>
        public const string SavedHeading = "Saved by you";

        public const string AutomaticHeading = "Replaced automatically";

        /// <summary>Said beside the automatic list, so nobody counts on one staying.</summary>
        public const string AutomaticNote = "the oldest goes on its own";

        /// <summary>
        /// Said at the top of the list. Copies never leave the game folder, and somebody looking at
        /// a list of their own work deserves to know that before they wonder.
        /// </summary>
        public const string PrivacyNote =
            "Nothing here is sent anywhere. These stay in this game's folder.";

        // ── The one mistake that cannot be undone ─────────────────────────

        /// <summary>
        /// Whether this copy belongs to a DIFFERENT translation from the one in place.
        ///
        /// 🔴 **The only restore that cannot be taken back with another click.** Putting back an
        /// earlier version of your own work costs you the newer version, which is itself kept. But
        /// restoring a copy of somebody else's translation replaces yours with a file that shares
        /// nothing with it — a different lineage, different lines, and the merge base to match.
        /// It has to be said on the row, beside the line count, not in small print.
        ///
        /// ⚠ False when either side is unknown: a copy from before uuids were recorded is not
        /// evidence of anything, and warning on silence teaches people to ignore the warning.
        /// </summary>
        public static bool IsAnotherLineage(string? backupUuid, string? currentUuid)
        {
            if (string.IsNullOrWhiteSpace(backupUuid) || string.IsNullOrWhiteSpace(currentUuid))
                return false;

            return !string.Equals(backupUuid!.Trim(), currentUuid!.Trim(),
                                  StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Said on such a row, and it names what it is rather than shouting.</summary>
        public const string AnotherLineageNote = "a different translation, not an earlier yours";

        // ── What is asked before an act that cannot be taken back ─────────

        /// <summary>
        /// The two questions asked before acting, worded HERE so the two products cannot drift.
        ///
        /// 🔴 One of them asked and the other did not: the mod confirmed a deletion, the manager
        /// removed the copy on the click. Two screens onto the same folder, disagreeing about
        /// whether losing work deserves a question — and the one that did not ask was the one
        /// where a mis-click is cheapest to make and dearest to notice.
        ///
        /// ⚠ Both name what stands to be lost IN FIGURES, which is the rule every confirmation in
        /// this project follows: "are you sure?" is a question nobody can answer usefully, because
        /// the person asking already knows what is at stake and the person answering does not.
        /// </summary>
        public const string ConfirmRestoreTitle = "Put this copy back?";

        /// <param name="lines">What the copy holds.</param>
        /// <param name="nowLines">What the game holds today — the thing being replaced.</param>
        /// <param name="when">When the copy was taken, already written the way the row writes it.</param>
        /// <param name="anotherLineage">Whether it belongs to a different translation entirely.</param>
        public static string ConfirmRestoreBody(int lines, int nowLines, string when,
                                                bool anotherLineage)
        {
            var body = $"This game will use the {lines}-line version from {when} instead of the "
                     + $"{nowLines} lines it holds now.\n\nWhat it holds now is kept as a backup, "
                     + "so you can come back to it.";

            // ⚠ Repeated here even though the row already says it: a confirmation is read by
            // somebody who has decided, and this is the last moment the decision can change.
            if (anotherLineage)
            {
                body += "\n\n⚠ This copy is " + AnotherLineageNote
                      + ". Its lines and its history are not yours.";
            }

            return body;
        }

        public const string ConfirmRestoreVerb = "Put it back";

        public const string ConfirmDeleteTitle = "Delete this copy?";

        /// <param name="what">The name it carries, or a phrase naming its date. Never blank.</param>
        public static string ConfirmDeleteBody(string what, int lines) =>
            $"This removes {what} and the {lines} lines it holds. It cannot be undone.\n\n"
            + "Nothing else is touched: the translation in the game stays exactly as it is.";

        public const string ConfirmDeleteVerb = "Delete";
    }
}
