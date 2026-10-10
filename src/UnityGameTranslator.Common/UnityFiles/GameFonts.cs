using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// The fonts a Unity game carries in its data files — Unity's Font objects (UI.Text, TextMesh,
    /// NGUI's dynamic fonts, and the source files of TextMesh Pro and UI Toolkit dynamic assets) —
    /// with the font file each embeds (Unity keeps the whole TTF/OTF in it, unless the developer
    /// unticked "Include Font Data"). Read-only on the game.
    ///
    /// One reader for the mod (the original font of a component, behind a replacement; Extract) and
    /// the Manager (Extract): the same file gives the same font in both (analyse/export-polices-jeu.md).
    ///
    /// Two ways to read: <see cref="FromDataFolder"/> with every font's bytes (the bench), or an INDEX
    /// (withData false: each font's length only — a game can carry dozens of large fonts, nobody asked
    /// for them all) and then <see cref="ReadData"/> for the one wanted.
    /// </summary>
    public static class GameFonts
    {
        private static readonly string[] Named = { "m_Name", "m_FontNames" };
        private static readonly string[] WithData = { "m_Name", "m_FontData", "m_FontNames" };
        private static readonly string[] DataMeasured = { "m_FontData" };

        public sealed class Font
        {
            internal Font(string name, IReadOnlyList<string> fontNames, byte[]? data, int dataLength, string file, string? inner, long pathId)
            { Name = name; FontNames = fontNames; Data = data; DataLength = dataLength; File = file; Inner = inner; PathId = pathId; }

            public string Name { get; }
            /// <summary>The names the font asks the system for (m_FontNames): its own family when imported from a file.</summary>
            public IReadOnlyList<string> FontNames { get; }
            /// <summary>The embedded font file when it was read; null in an index, or when the font has none.</summary>
            public byte[]? Data { get; }
            /// <summary>The embedded file's length; 0 when the font has none (a built-in or system font, or "Include Font Data" unticked).</summary>
            public int DataLength { get; }
            public bool HasFile => DataLength > 0;
            /// <summary>The data file it is in, relative to the data folder, with '/'.</summary>
            public string File { get; }
            /// <summary>Inside a bundle, the file within it; null for a serialized file on disk.</summary>
            public string? Inner { get; }
            public long PathId { get; }
            /// <summary>Where it was read: the data file, and in a bundle the file inside it.</summary>
            public string Where => Inner == null ? File : File + "/" + Inner;

            /// <summary>The file's extension from its first bytes: ".otf" (CFF), ".ttc" (collection) or ".ttf"; "" when not read.</summary>
            public string Extension => ExtensionOf(Data);
        }

        /// <summary>A font file's extension from its first bytes: ".otf", ".ttc" or ".ttf"; "" for nothing.</summary>
        public static string ExtensionOf(byte[]? data)
        {
            if (data == null || data.Length < 4) return "";
            uint tag = (uint)(data[0] << 24 | data[1] << 16 | data[2] << 8 | data[3]);
            return tag == 0x4F54544F ? ".otf" : tag == 0x74746366 ? ".ttc" : ".ttf";
        }

        public sealed class Reading
        {
            public List<Font> Fonts { get; } = new List<Font>();
            /// <summary>Unity data files that could not be read, and why — said, never skipped silently.</summary>
            public List<KeyValuePair<string, string>> NotRead { get; } = new List<KeyValuePair<string, string>>();
            /// <summary>
            /// Versions newer than the generated layouts knew, read with the closest one below — a
            /// newer Unity may have changed Font (each said once by the caller).
            /// </summary>
            public HashSet<string> NewerThanTable { get; } = new HashSet<string>();
            /// <summary>The data folder's fingerprint when it was read (<see cref="Stamp"/>).</summary>
            public string Stamp { get; set; } = "";
        }

        /// <summary>
        /// Every font in a game's data folder (the "&lt;Game&gt;_Data" folder), its bundles included.
        /// <paramref name="engineVersion"/>: the game's Unity version when known (the mod knows it);
        /// otherwise each file's own, then the folder's main file's. <paramref name="withData"/> false:
        /// an index, each font's length only. <paramref name="progress"/>: files done, of how many —
        /// called from the reading thread.
        /// </summary>
        public static Reading FromDataFolder(string dataFolder, string? engineVersion = null, bool withData = true, Action<int, int>? progress = null)
        {
            var reading = new Reading();
            var files = Directory.GetFiles(dataFolder, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            reading.Stamp = StampOf(dataFolder, files);
            ulong fallback = UnityVersions.Key(engineVersion);
            if (fallback == 0) fallback = MainVersion(dataFolder);
            for (int i = 0; i < files.Length; i++)
            {
                ReadFile(files[i], fallback, reading, dataFolder, withData, null);
                progress?.Invoke(i + 1, files.Length);
            }
            return reading;
        }

        /// <summary>The fonts of one data file (a serialized file or a bundle).</summary>
        public static Reading FromFile(string path, string? engineVersion = null)
        {
            var reading = new Reading();
            ReadFile(path, UnityVersions.Key(engineVersion), reading, Path.GetDirectoryName(path) ?? "", true, null);
            return reading;
        }

        /// <summary>
        /// The embedded file of one font of an index, read again from its data file; null when it has
        /// none. <see cref="UnityFileFormatException"/> (or an IO error) when the file no longer reads —
        /// the game was updated since the index: the caller indexes again.
        /// </summary>
        public static byte[]? ReadData(string dataFolder, Font font, string? engineVersion = null)
        {
            var reading = new Reading();
            ulong fallback = UnityVersions.Key(engineVersion);
            if (fallback == 0) fallback = MainVersion(dataFolder);
            string path = Path.Combine(dataFolder, font.File.Replace('/', Path.DirectorySeparatorChar));
            ReadFile(path, fallback, reading, dataFolder, true, font);
            if (reading.NotRead.Count > 0) throw new UnityFileFormatException(reading.NotRead[0].Value);
            foreach (var f in reading.Fonts)
                if (f.PathId == font.PathId && f.Inner == font.Inner) return f.Data;
            throw new UnityFileFormatException($"font '{font.Name}' (#{font.PathId}) is no longer in {font.Where}");
        }

        /// <summary>
        /// A fingerprint of a data folder — each file's relative path, size and last write — that
        /// changes when the game is updated: an index is redone only then.
        /// </summary>
        public static string Stamp(string dataFolder)
        {
            var files = Directory.GetFiles(dataFolder, "*", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            return StampOf(dataFolder, files);
        }

        private static string StampOf(string dataFolder, string[] files)
        {
            var text = new StringBuilder();
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                text.Append(Relative(dataFolder, file)).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
            }
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>The version written in globalgamemanagers, mainData or data.unity3d — the game's own.</summary>
        private static ulong MainVersion(string dataFolder)
        {
            foreach (var name in new[] { "globalgamemanagers", "mainData", "data.unity3d" })
            {
                var path = Path.Combine(dataFolder, name);
                if (!System.IO.File.Exists(path)) continue;
                using (var stream = System.IO.File.OpenRead(path))
                {
                    if (UnityBundle.LooksLikeOne(stream))
                    {
                        ulong key = UnityVersions.Key(UnityBundle.Open(stream).EngineVersion);
                        if (key != 0) return key;
                    }
                    else if (SerializedFile.LooksLikeOne(stream))
                    {
                        ulong key = UnityVersions.Key(SerializedFile.Open(stream).EngineVersion);
                        if (key != 0) return key;
                    }
                }
            }
            return 0;
        }

        // only: a font of an index, the one wanted (ReadData) — its bundle entry alone is opened.
        private static void ReadFile(string path, ulong fallback, Reading reading, string root, bool withData, Font? only)
        {
            string file = Relative(root, path);
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (UnityBundle.LooksLikeOne(stream))
                    {
                        var bundle = UnityBundle.Open(stream);
                        ulong key = UnityVersions.Key(bundle.EngineVersion);
                        foreach (var entry in bundle.Entries)
                        {
                            if (!entry.IsSerializedFile || (only != null && entry.Path != only.Inner)) continue;
                            using (var inner = bundle.Open(entry))
                                ReadSerialized(inner, key != 0 ? key : fallback, reading, file, entry.Path, withData, only);
                        }
                    }
                    else if (SerializedFile.LooksLikeOne(stream))
                        ReadSerialized(stream, fallback, reading, file, null, withData, only);
                    // Anything else is not a Unity data file: nothing to say.
                }
            }
            catch (UnityFileFormatException ex) { reading.NotRead.Add(new KeyValuePair<string, string>(file, ex.Message)); }
            catch (IOException ex) { reading.NotRead.Add(new KeyValuePair<string, string>(file, ex.Message)); }
            catch (UnauthorizedAccessException ex) { reading.NotRead.Add(new KeyValuePair<string, string>(file, ex.Message)); }
        }

        private static void ReadSerialized(Stream stream, ulong fallback, Reading reading, string file, string? inner, bool withData, Font? only)
        {
            var serialized = SerializedFile.Open(stream);
            ulong key = UnityVersions.Key(serialized.EngineVersion);
            if (key == 0) key = fallback;
            IReadOnlyList<TypeNode>? generated = null;
            bool generatedLooked = false;
            foreach (var info in serialized.Objects)
            {
                if (info.ClassId != FontLayouts.FontClassId || (only != null && info.PathId != only.PathId)) continue;
                var tree = serialized.KeptTreeOf(info);
                if (tree == null)
                {
                    if (!generatedLooked)
                    {
                        generatedLooked = true;
                        generated = FontLayouts.For(key, out bool newer);
                        if (newer) reading.NewerThanTable.Add(UnityVersions.Text(key));
                        if (generated == null)
                            throw new UnityFileFormatException(key == 0
                                ? "fonts in a file that names no Unity version, and none was given"
                                : $"no Font layout for Unity {UnityVersions.Text(key)}");
                    }
                    tree = generated!;
                }
                var fields = withData ? serialized.Fields(info, tree, WithData) : serialized.Fields(info, tree, Named, DataMeasured);
                var names = new List<string>();
                if (fields.TryGetValue("m_FontNames", out var list) && list is List<object?> items)
                    foreach (var item in items) if (item is string s && s.Length > 0) names.Add(s);
                byte[]? data = null;
                int length = 0;
                if (withData)
                {
                    data = fields.TryGetValue("m_FontData", out var bytes) ? bytes as byte[] : null;
                    if (data != null && data.Length == 0) data = null;
                    length = data?.Length ?? 0;
                }
                else if (fields.TryGetValue("m_FontData", out var measured) && measured is int n && n > 0) length = n;
                reading.Fonts.Add(new Font(fields.TryGetValue("m_Name", out var nm) ? nm as string ?? "" : "",
                    names, data, length, file, inner, info.PathId));
            }
        }

        private static string Relative(string root, string path)
        {
            if (root.Length > 0 && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            return Path.GetFileName(path);
        }

        // ── An index kept on disk ────────────────────────────────────────────────────────────────

        private const string IndexHeader = "ugt-game-fonts 1";

        /// <summary>
        /// Writes an index (fonts without their bytes) with the data folder's stamp: a line per font,
        /// tab-separated, each text escaped — the library reads and writes no JSON.
        /// </summary>
        public static void SaveIndex(Reading index, string path)
        {
            var text = new StringBuilder();
            text.Append(IndexHeader).Append('\n');
            text.Append("stamp\t").Append(index.Stamp).Append('\n');
            foreach (var f in index.Fonts)
            {
                text.Append("font\t").Append(Escape(f.File)).Append('\t').Append(Escape(f.Inner ?? "")).Append('\t')
                    .Append(f.PathId).Append('\t').Append(f.DataLength).Append('\t').Append(Escape(f.Name));
                foreach (var n in f.FontNames) text.Append('\t').Append(Escape(n));
                text.Append('\n');
            }
            foreach (var v in index.NewerThanTable) text.Append("newer\t").Append(Escape(v)).Append('\n');
            foreach (var n in index.NotRead) text.Append("notread\t").Append(Escape(n.Key)).Append('\t').Append(Escape(n.Value)).Append('\n');
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            System.IO.File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        }

        /// <summary>An index written by <see cref="SaveIndex"/>; null when there is none, or it is not one (the caller indexes again).</summary>
        public static Reading? LoadIndex(string path)
        {
            if (!System.IO.File.Exists(path)) return null;
            var lines = System.IO.File.ReadAllText(path, Encoding.UTF8).Split('\n');
            if (lines.Length == 0 || lines[0] != IndexHeader) return null;
            var index = new Reading();
            for (int i = 1; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;
                var parts = lines[i].Split('\t');
                switch (parts[0])
                {
                    case "stamp" when parts.Length == 2: index.Stamp = parts[1]; break;
                    case "font" when parts.Length >= 6 && long.TryParse(parts[3], out var pathId) && int.TryParse(parts[4], out var length):
                        var names = new List<string>();
                        for (int k = 6; k < parts.Length; k++) names.Add(Unescape(parts[k]));
                        string inner = Unescape(parts[2]);
                        index.Fonts.Add(new Font(Unescape(parts[5]), names, null, length, Unescape(parts[1]), inner.Length == 0 ? null : inner, pathId));
                        break;
                    case "newer" when parts.Length == 2: index.NewerThanTable.Add(Unescape(parts[1])); break;
                    case "notread" when parts.Length == 3: index.NotRead.Add(new KeyValuePair<string, string>(Unescape(parts[1]), Unescape(parts[2]))); break;
                    default: return null;   // a line this version does not know: not trusted, read again
                }
            }
            return index.Stamp.Length == 0 ? null : index;
        }

        private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string Unescape(string s)
        {
            if (s.IndexOf('\\') < 0) return s;
            var b = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] != '\\' || i + 1 == s.Length) { b.Append(s[i]); continue; }
                char c = s[++i];
                b.Append(c == 't' ? '\t' : c == 'n' ? '\n' : c == 'r' ? '\r' : c);
            }
            return b.ToString();
        }
    }
}
