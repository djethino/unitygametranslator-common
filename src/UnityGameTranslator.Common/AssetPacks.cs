using System;

namespace UnityGameTranslator.Common
{
    /// <summary>What an asset file is for: a font the mod can offer, or a picture it puts in place.</summary>
    public enum AssetKind
    {
        Font,
        Image,
    }

    /// <summary>
    /// The `.ugtpack` format — fonts and replacement images for one game, in one file — and which
    /// file of a game's mod folder is a font or an image at all.
    ///
    /// A pack is a zip under another extension, the way an .apk is: renamed so that it is opened by
    /// the program that understands its layout rather than unpacked by hand into the wrong folder.
    ///
    /// <code>
    /// manifest.json              the format, the game it was made from, the image definitions
    /// fonts/&lt;name&gt;.ttf|.otf|.ttc   made available to the mod, never assigned (that is chosen in the mod)
    /// images/&lt;name&gt;.png          the pictures the definitions name
    /// </code>
    ///
    /// 🔴 **What is decided here is what makes a pack safe to open.** A pack comes from somebody
    /// else and is unpacked into a game folder, next to code the game loads. So an entry is read only
    /// when it is EXACTLY one of the two folders plus a bare file name of an accepted kind: no
    /// subfolder, no "..", no drive, nothing a Windows file system would rename. Anything else is not
    /// extracted at all, which makes an entry reaching outside the folder impossible by construction
    /// rather than caught by a check that has to be right every time.
    ///
    /// ⚠ The zip itself is read by each product with its own library; this file only answers "what
    /// is this name". Design and the user's decisions: analyse/manager-onglet-assets.md (root).
    /// </summary>
    public static class AssetPacks
    {
        /// <summary>The extension a pack carries — the one thing that says "open me with UGT Manager".</summary>
        public const string Extension = ".ugtpack";

        /// <summary>The one file at the top of a pack.</summary>
        public const string ManifestName = "manifest.json";

        /// <summary>
        /// The layout this reader understands. A pack stating a higher one was made by a newer tool
        /// and is refused whole rather than half understood.
        /// </summary>
        public const int Format = 1;

        public const string FontsFolder = "fonts";
        public const string ImagesFolder = "images";

        /// <summary>
        /// Font SOURCES. Generated atlases sit in the same folder and are never one of these: they
        /// are rebuilt from the font beside them (see <see cref="Backups.AssetsToCopy"/>).
        /// </summary>
        public static readonly string[] FontExtensions = { ".ttf", ".otf", ".ttc" };

        /// <summary>What the mod's own export writes, and what a definition's `file` names.</summary>
        public static readonly string[] ImageExtensions = { ".png" };

        /// <summary>The folder of a kind, inside the mod's data folder and inside a pack.</summary>
        public static string FolderOf(AssetKind kind) => kind == AssetKind.Font ? FontsFolder : ImagesFolder;

        /// <summary>Whether this file name is a font source.</summary>
        public static bool IsFontFile(string? name) => HasExtension(name, FontExtensions);

        /// <summary>Whether this file name is a replacement image.</summary>
        public static bool IsImageFile(string? name) => HasExtension(name, ImageExtensions);

        /// <summary>
        /// What a file dropped on its own is, by its name — null when it is neither, and a `.ugtpack`
        /// is not "neither": ask <see cref="IsPack"/> first.
        /// </summary>
        public static AssetKind? KindOfFile(string? name)
        {
            if (IsFontFile(name)) return AssetKind.Font;
            if (IsImageFile(name)) return AssetKind.Image;
            return null;
        }

        /// <summary>Whether this file name is a pack.</summary>
        public static bool IsPack(string? name) =>
            name != null && name.EndsWith(Extension, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Reads the name of an entry inside a pack: true only for `fonts/&lt;font&gt;` or
        /// `images/&lt;png&gt;`, with the bare file name. Every other entry — the manifest included —
        /// answers false and is not extracted.
        /// </summary>
        public static bool TryEntry(string? entryName, out AssetKind kind, out string bare)
        {
            kind = AssetKind.Font;
            bare = "";
            if (string.IsNullOrEmpty(entryName)) return false;

            // Both separators: a zip made on Windows by some tools writes backslashes.
            var name = entryName!.Replace('\\', '/');
            var slash = name.IndexOf('/');
            if (slash <= 0 || name.IndexOf('/', slash + 1) >= 0) return false;

            var folder = name.Substring(0, slash);
            var file = name.Substring(slash + 1);
            if (!IsSafeFileName(file)) return false;

            if (string.Equals(folder, FontsFolder, StringComparison.Ordinal) && IsFontFile(file))
            {
                kind = AssetKind.Font;
            }
            else if (string.Equals(folder, ImagesFolder, StringComparison.Ordinal) && IsImageFile(file))
            {
                kind = AssetKind.Image;
            }
            else
            {
                return false;
            }

            bare = file;
            return true;
        }

        /// <summary>
        /// Whether a name can be written as one file inside a folder, on every system a game runs on.
        ///
        /// ⚠ Judged by Windows' rules everywhere: a pack made on Linux is opened on Windows, and a name
        /// Windows rewrites (a trailing dot, a reserved device name) is a name that lands somewhere
        /// else than where it was checked.
        /// </summary>
        public static bool IsSafeFileName(string? name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            if (name == "." || name == "..") return false;
            if (name!.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal)) return false;
            if (name.StartsWith(" ", StringComparison.Ordinal)) return false;

            foreach (var c in name)
            {
                if (c < 32 || "\\/:*?\"<>|".IndexOf(c) >= 0) return false;
            }

            var stem = name;
            var dot = stem.IndexOf('.');
            if (dot >= 0) stem = stem.Substring(0, dot);

            foreach (var reserved in ReservedNames)
            {
                if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase)) return false;
            }

            return true;
        }

        /// <summary>Names Windows keeps for devices, whatever extension follows them.</summary>
        private static readonly string[] ReservedNames =
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
        };

        private static bool HasExtension(string? name, string[] extensions)
        {
            if (string.IsNullOrEmpty(name)) return false;

            foreach (var extension in extensions)
            {
                if (name!.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
