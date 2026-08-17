using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// How a translation's own history is kept, checked against the rule rather than the code.
    ///
    /// ⚠ The stake: this is the last copy of work that exists nowhere else. A rotation that drops
    /// one too many, a deliberate copy evicted to make room, or a restore that silently swaps one
    /// lineage for another are all losses nobody can undo — and none of the three would throw.
    /// </summary>
    internal static class BackupsChecks
    {
        private static BackupEntry Auto(string stamp) => new BackupEntry
        {
            Id = "auto-" + stamp,
            At = DateTime.ParseExact(stamp, "yyyyMMdd-HHmmss",
                                     System.Globalization.CultureInfo.InvariantCulture),
            Reason = BackupReason.Installed,
        };

        private static BackupEntry Saved(string stamp) => new BackupEntry
        {
            Id = "saved-" + stamp,
            At = DateTime.ParseExact(stamp, "yyyyMMdd-HHmmss",
                                     System.Globalization.CultureInfo.InvariantCulture),
            Reason = BackupReason.Saved,
        };

        public static void Run(Action<bool, string, string> check)
        {
            // ── Naming ────────────────────────────────────────────────────
            var at = new DateTime(2026, 8, 17, 14, 32, 10);

            check(Backups.NewId(BackupReason.Saved, at) == "saved-20260817-143210"
                  && Backups.NewId(BackupReason.Installed, at) == "auto-20260817-143210",
                "the family is in the folder name",
                "a reader of the folder, and a rotation, must tell them apart without opening anything");

            check(string.CompareOrdinal(Backups.NewId(BackupReason.Saved, at.AddSeconds(-1)),
                                        Backups.NewId(BackupReason.Saved, at)) < 0,
                "names sort in the order they were taken",
                "ordering on a file timestamp breaks the day something copies the folder");

            check(Backups.IsBackupFolder("auto-20260817-143210", out var savedA) && !savedA
                  && Backups.IsBackupFolder("saved-20260817-143210", out var savedB) && savedB
                  && !Backups.IsBackupFolder("fonts", out _)
                  && !Backups.IsBackupFolder("", out _),
                "only our own folders are recognised",
                "sweeping a folder we did not create is how somebody's own files disappear");

            // ── Rotation ──────────────────────────────────────────────────
            var five = new List<BackupEntry>
            {
                Auto("20260817-100000"), Auto("20260817-110000"), Auto("20260817-120000"),
                Auto("20260817-130000"), Auto("20260817-140000"),
            };

            check(Backups.AutomaticToDrop(five).Count == 0,
                $"{Backups.AutomaticKept} automatic copies drop nothing",
                "the limit is a ceiling, not a target");

            var six = new List<BackupEntry>(five) { Auto("20260817-150000") };
            var dropped = Backups.AutomaticToDrop(six);

            check(dropped.Count == 1 && dropped[0] == "auto-20260817-100000",
                "the sixth drops the OLDEST, not the newest",
                "dropping the wrong end would keep the copy nobody wants and lose the one they do");

            // 🔴 The rule that protects work somebody chose to protect.
            var mixed = new List<BackupEntry>(six);
            for (var i = 0; i < 12; i++) mixed.Add(Saved("202608" + (10 + i) + "-090000"));

            var droppedMixed = Backups.AutomaticToDrop(mixed);
            var touchedSaved = false;
            foreach (var id in droppedMixed)
            {
                if (id.StartsWith("saved-", StringComparison.Ordinal)) touchedSaved = true;
            }

            check(!touchedSaved,
                "rotation never touches a copy somebody saved",
                "even twelve of them, past the limit: evicting one is deleting what they protected");

            // ── The ceiling refuses rather than evicts ────────────────────
            var full = new List<BackupEntry>();
            for (var i = 0; i < Backups.SavedKept; i++) full.Add(Saved("202608" + (10 + i) + "-090000"));

            check(!Backups.CanSaveAnother(full) && Backups.WhyCannotSave(full) is { Length: > 0 },
                "full means refused, with a reason in words",
                "a greyed button without words is a dead end; a silent eviction is worse");

            check(Backups.CanSaveAnother(five) && Backups.WhyCannotSave(five) is null,
                "automatic copies do not fill the deliberate slots",
                "the two families are counted apart or the ceiling arrives early");

            // ── What a copy carries ───────────────────────────────────────
            var assets = Backups.AssetsToCopy(new[] { "title.png", (string?)null, "  " },
                                              new[] { "arial.ttf" });

            check(assets.Count == 2 && assets.Contains("images/title.png")
                  && assets.Contains("fonts/arial.ttf"),
                "only the assets the translation names, in their own folders",
                "copying the whole folder would carry the generated atlases, the largest thing in it");

            var escaping = Backups.AssetsToCopy(new[] { "../../config.json", "sub/dir/x.png" }, null);

            check(escaping.Count == 2
                  && escaping.Contains("images/config.json") && escaping.Contains("images/x.png"),
                "a name that walks up is reduced to its file name",
                "these come out of a file anybody may edit; a path would let a copy reach outside");

            var twice = Backups.AssetsToCopy(new[] { "a.png", "A.PNG" }, null);
            check(twice.Count == 1, "the same asset is taken once", "case differs, the file does not");

            // ── Words ─────────────────────────────────────────────────────
            check(Backups.Describe(BackupReason.Installed, "@Seniorito")
                      .Contains("@Seniorito"),
                "the act names whose translation was involved",
                "'before installing a translation' is a date with extra steps; a name is a memory");

            check(Backups.Describe(BackupReason.Installed) != Backups.Describe(BackupReason.Merged)
                  && Backups.Describe(BackupReason.Unknown) is { Length: > 0 },
                "every act reads differently, and the unknown one still says something",
                "two rows that read alike are two rows nobody can choose between");

            // ── The mistake that cannot be undone ─────────────────────────
            check(Backups.IsAnotherLineage("aaa", "bbb"),
                "a copy from another lineage is flagged",
                "restoring it replaces your work with a file that shares nothing with it");

            check(!Backups.IsAnotherLineage("aaa", "AAA"),
                "the same lineage in another case is not another lineage",
                "a uuid echoed back differently would accuse every copy of being foreign");

            check(!Backups.IsAnotherLineage(null, "bbb") && !Backups.IsAnotherLineage("aaa", null),
                "an unknown uuid accuses nobody",
                "warning on silence teaches people to ignore the warning");
        }
    }
}
