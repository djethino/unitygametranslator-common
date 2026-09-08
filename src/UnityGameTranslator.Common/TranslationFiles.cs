using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The files a translation keeps beside itself, and the one section of it two products read
    /// by name. Names only — the socle never touches a disk; whoever reads or writes these files
    /// does so with their own I/O, but spells nothing.
    ///
    /// 🔴 Why these names live here and nowhere else. The mod spelled the ancestor's name five
    /// times; four said <c>translations.json.ancestor</c> and the fifth — the one that cleaned up
    /// after a fork — said <c>translations.ancestor.json</c>. That file never existed, so it was
    /// never deleted, and the in-memory clear written inside the same <c>if</c> never ran either:
    /// every fork went on counting its own lines against the ancestor of the lineage it had left.
    /// Nothing threw and nothing logged. In the same family, the backups of the mod AND the manager
    /// read a translation's images from <c>_images</c> while the file writes them under
    /// <c>_image_replacements</c>, so a "saved" copy carried the fonts and never an image, in three
    /// products at once. A name written once cannot disagree with itself.
    /// </summary>
    public static class TranslationFiles
    {
        /// <summary>The translation of one game, in that game's mod data folder.</summary>
        public const string Name = "translations.json";

        /// <summary>
        /// Appended to the translation's own name: the snapshot of the file as it stood at the last
        /// sync, which is what makes a three-way comparison possible at all.
        /// </summary>
        public const string AncestorSuffix = ".ancestor";

        /// <summary>
        /// Appended likewise: the Main as it stood at the last merge from it, kept by a branch.
        /// A separate file on purpose — feeding the ordinary ancestor to a Main→branch merge would
        /// read every line the branch owns as a remote deletion.
        /// </summary>
        public const string MainAncestorSuffix = ".mainancestor";

        /// <summary>The ancestor beside a translation. The suffix is APPENDED to the whole path, never substituted into it.</summary>
        public static string AncestorOf(string translationPath)
        {
            if (translationPath == null) throw new ArgumentNullException(nameof(translationPath));
            return translationPath + AncestorSuffix;
        }

        /// <summary>The upstream ancestor beside a translation, same rule.</summary>
        public static string MainAncestorOf(string translationPath)
        {
            if (translationPath == null) throw new ArgumentNullException(nameof(translationPath));
            return translationPath + MainAncestorSuffix;
        }

        /// <summary>
        /// The section of the translation file that lists the images it puts in place. Each entry
        /// names its file under <see cref="ImageFileField"/>; entries written by earlier versions
        /// may still use one of <see cref="ImageFileLegacyFields"/>, and readers accept those too.
        ///
        /// ⚠ An ALIAS, not a second spelling: it is the images row of
        /// <see cref="SettingsSections.JsonKey"/>, kept under this name because the code that reads
        /// it is looking for images and not for a settings section — and because the two fields
        /// below only make sense beside it. The string itself is written once, in that table.
        /// </summary>
        public const string ImagesSection = SettingsSections.ImagesKey;

        /// <summary>Field of an image entry holding the file name, relative to the mod's <c>images/</c> folder.</summary>
        public const string ImageFileField = "file";

        /// <summary>Older spellings of <see cref="ImageFileField"/>, still found in game folders.</summary>
        public static readonly string[] ImageFileLegacyFields = { "replacement_file", "original_file" };
    }
}
