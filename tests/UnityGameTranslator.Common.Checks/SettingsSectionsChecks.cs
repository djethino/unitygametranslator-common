using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The six settings sections that travel inside a translation file: one table, read the same
    /// way by everything that writes a file, reads one back, or shows one on a screen.
    ///
    /// 🔴 **The stake is a list that exists twice.** The mod BUILDS a translation file by walking
    /// <see cref="SettingsSections.All"/> and asking <see cref="SettingsSections.JsonKey"/> for
    /// each name — so a section added here is written by everybody, at once. It READ one back with
    /// a hand-written branch per key, which is a second copy of the same list and nothing compared
    /// them: a seventh section would have been written and never read, in silence.
    ///
    /// ⚠ **The round trip is the case that matters**, and it is the one nothing could state before
    /// <see cref="SettingsSections.SectionOf"/> existed: a name that survives being turned into a
    /// file key and back is a name a reader can be driven by.
    ///
    /// ⚠ These cases walk <c>All</c> rather than naming six things, on purpose — a seventh section
    /// is covered the day it is added, which is the whole point of the table being here.
    /// </summary>
    internal static class SettingsSectionsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var all = SettingsSections.All;

            check(all.Length > 0,
                $"the table names {all.Length} section(s)",
                "an empty table would make every case below pass without asking anything");

            // ── One name each, one key each ───────────────────────────────
            var names = new HashSet<string>(StringComparer.Ordinal);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var duplicateNames = new List<string>();
            var duplicateKeys = new List<string>();

            foreach (string section in all)
            {
                if (!names.Add(section)) duplicateNames.Add(section);

                string key = SettingsSections.JsonKey(section);
                if (key != null && !keys.Add(key)) duplicateKeys.Add(key);
            }

            check(duplicateNames.Count == 0,
                "no section is named twice",
                "a screen listing them would show one row for two things");

            check(duplicateKeys.Count == 0,
                "no two sections share a file key",
                "the second one written into the file would erase the first, and reading it back could only guess");

            // ── Every section has a key, and it is a metadata key ─────────
            var withoutKey = new List<string>();
            var notUnderscored = new List<string>();

            foreach (string section in all)
            {
                string key = SettingsSections.JsonKey(section);
                if (string.IsNullOrEmpty(key)) { withoutKey.Add(section); continue; }
                if (!key.StartsWith("_", StringComparison.Ordinal)) notUnderscored.Add(key);
            }

            check(withoutKey.Count == 0,
                withoutKey.Count == 0
                    ? "every section has a file key"
                    : "NO FILE KEY: " + string.Join(", ", withoutKey.ToArray()),
                "a section with no key is built by the writer and dropped on the floor");

            check(notUnderscored.Count == 0,
                notUnderscored.Count == 0
                    ? "and every key is an underscore key"
                    : "NOT A METADATA KEY: " + string.Join(", ", notUnderscored.ToArray()),
                "anything not starting with _ is read as a translated line, so the section would arrive as somebody's text");

            // ── 🔴 The round trip: name → key → name ──────────────────────
            var lost = new List<string>();
            foreach (string section in all)
            {
                string back = SettingsSections.SectionOf(SettingsSections.JsonKey(section));
                if (!string.Equals(back, section, StringComparison.Ordinal))
                    lost.Add($"{section} -> {SettingsSections.JsonKey(section)} -> {back ?? "(none)"}");
            }

            check(lost.Count == 0,
                lost.Count == 0
                    ? "a section survives being written as a key and read back"
                    : "THE ROUND TRIP LOSES: " + string.Join(", ", lost.ToArray()),
                "reading a file is driven by this; a key the table cannot name back is a section written and never read");

            // ── What is NOT one of ours ───────────────────────────────────
            // A translation file carries plenty of other underscore keys, and the lines themselves.
            foreach (string stranger in new[] { "_uuid", "_source", "_game", "_local_changes", "Play", "" })
            {
                check(SettingsSections.SectionOf(stranger) == null,
                    $"\"{stranger}\" is not a settings section",
                    "claiming one would hand another part of the file, or somebody's line, to a section owner");
            }

            check(SettingsSections.SectionOf(null) == null,
                "and neither is nothing at all",
                "the reader asks about every key it meets; it must not have to check first");

            check(SettingsSections.JsonKey("not_a_section") == null,
                "an unknown name has no file key",
                "the writer would otherwise put a section under a null key");

            // ── Every section is sayable ──────────────────────────────────
            // A seventh added without a label would show as its own internal name on screen.
            var unnamed = new List<string>();
            var undescribed = new List<string>();

            foreach (string section in all)
            {
                if (SettingsSections.Name(section) == section) unnamed.Add(section);
                if (string.IsNullOrEmpty(SettingsSections.Description(section))) undescribed.Add(section);
            }

            check(unnamed.Count == 0,
                unnamed.Count == 0
                    ? "every section has a label of its own"
                    : "SHOWN AS ITS INTERNAL NAME: " + string.Join(", ", unnamed.ToArray()),
                "the fallback exists for a section arriving from a newer product, not for ours");

            check(undescribed.Count == 0,
                undescribed.Count == 0
                    ? "and a line saying what replacing it costs"
                    : "NOTHING SAID ABOUT: " + string.Join(", ", undescribed.ToArray()),
                "a section name alone does not let anybody decide whether to take it");

            // ── The one alias, pinned ─────────────────────────────────────
            // TranslationFiles.ImagesSection is this table's images row under the name the backup
            // code reads it by. Two spellings for one string is how no backup ever carried an image.
            check(TranslationFiles.ImagesSection == SettingsSections.ImagesKey,
                "the backup code's images key is this table's images key",
                "they were once two spellings, and the backups silently held no image at all");
        }
    }
}
