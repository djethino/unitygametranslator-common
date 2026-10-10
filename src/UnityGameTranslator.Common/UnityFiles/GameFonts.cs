using System;
using System.Collections.Generic;
using System.IO;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// The fonts a Unity game carries in its data files — Unity's Font objects (UI.Text, TextMesh,
    /// NGUI's dynamic fonts, and the source files of TextMesh Pro and UI Toolkit dynamic assets) —
    /// with the font file each embeds (Unity keeps the whole TTF/OTF in it, unless the developer
    /// unticked "Include Font Data"). Read-only on the game.
    ///
    /// One reader for the mod (the original font of a component, behind a replacement) and the
    /// Manager (export): the same file gives the same font in both (analyse/export-polices-jeu.md).
    /// </summary>
    public static class GameFonts
    {
        private static readonly string[] Wanted = { "m_Name", "m_FontData", "m_FontNames" };

        public sealed class Font
        {
            internal Font(string name, IReadOnlyList<string> fontNames, byte[]? data, string where, long pathId)
            { Name = name; FontNames = fontNames; Data = data; Where = where; PathId = pathId; }

            public string Name { get; }
            /// <summary>The names the font asks the system for (m_FontNames): its own family when imported from a file.</summary>
            public IReadOnlyList<string> FontNames { get; }
            /// <summary>The embedded font file; null when the font has none (a built-in or system font, or "Include Font Data" unticked).</summary>
            public byte[]? Data { get; }
            /// <summary>The data file it was read from (and, in a bundle, the file inside it).</summary>
            public string Where { get; }
            public long PathId { get; }

            /// <summary>The file's extension from its first bytes: ".otf" (CFF), ".ttc" (collection) or ".ttf".</summary>
            public string Extension
            {
                get
                {
                    if (Data == null || Data.Length < 4) return "";
                    uint tag = (uint)(Data[0] << 24 | Data[1] << 16 | Data[2] << 8 | Data[3]);
                    return tag == 0x4F54544F ? ".otf" : tag == 0x74746366 ? ".ttc" : ".ttf";
                }
            }
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
        }

        /// <summary>
        /// Every font in a game's data folder (the "&lt;Game&gt;_Data" folder), its bundles included.
        /// <paramref name="engineVersion"/>: the game's Unity version when known (the mod knows it);
        /// otherwise each file's own, then the folder's main file's.
        /// </summary>
        public static Reading FromDataFolder(string dataFolder, string? engineVersion = null)
        {
            var reading = new Reading();
            ulong fallback = UnityVersions.Key(engineVersion);
            if (fallback == 0) fallback = MainVersion(dataFolder);
            foreach (var path in Directory.GetFiles(dataFolder, "*", SearchOption.AllDirectories))
                ReadFile(path, fallback, reading, dataFolder);
            return reading;
        }

        /// <summary>The fonts of one data file (a serialized file or a bundle).</summary>
        public static Reading FromFile(string path, string? engineVersion = null)
        {
            var reading = new Reading();
            ReadFile(path, UnityVersions.Key(engineVersion), reading, Path.GetDirectoryName(path) ?? "");
            return reading;
        }

        /// <summary>The version written in globalgamemanagers, mainData or data.unity3d — the game's own.</summary>
        private static ulong MainVersion(string dataFolder)
        {
            foreach (var name in new[] { "globalgamemanagers", "mainData", "data.unity3d" })
            {
                var path = Path.Combine(dataFolder, name);
                if (!File.Exists(path)) continue;
                using (var stream = File.OpenRead(path))
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

        private static void ReadFile(string path, ulong fallback, Reading reading, string root)
        {
            string where = Relative(root, path);
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
                            if (!entry.IsSerializedFile) continue;
                            using (var inner = bundle.Open(entry))
                                ReadSerialized(inner, key != 0 ? key : fallback, reading, where + "/" + entry.Path);
                        }
                    }
                    else if (SerializedFile.LooksLikeOne(stream))
                        ReadSerialized(stream, fallback, reading, where);
                    // Anything else is not a Unity data file: nothing to say.
                }
            }
            catch (UnityFileFormatException ex) { reading.NotRead.Add(new KeyValuePair<string, string>(where, ex.Message)); }
            catch (IOException ex) { reading.NotRead.Add(new KeyValuePair<string, string>(where, ex.Message)); }
            catch (UnauthorizedAccessException ex) { reading.NotRead.Add(new KeyValuePair<string, string>(where, ex.Message)); }
        }

        private static void ReadSerialized(Stream stream, ulong fallback, Reading reading, string where)
        {
            var file = SerializedFile.Open(stream);
            ulong key = UnityVersions.Key(file.EngineVersion);
            if (key == 0) key = fallback;
            IReadOnlyList<TypeNode>? generated = null;
            bool generatedLooked = false;
            foreach (var info in file.Objects)
            {
                if (info.ClassId != FontLayouts.FontClassId) continue;
                var tree = file.KeptTreeOf(info);
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
                var fields = file.Fields(info, tree, Wanted);
                var names = new List<string>();
                if (fields.TryGetValue("m_FontNames", out var list) && list is List<object?> items)
                    foreach (var item in items) if (item is string s && s.Length > 0) names.Add(s);
                var data = fields.TryGetValue("m_FontData", out var bytes) ? bytes as byte[] : null;
                reading.Fonts.Add(new Font(fields.TryGetValue("m_Name", out var n) ? n as string ?? "" : "",
                    names, data != null && data.Length > 0 ? data : null, where, info.PathId));
            }
        }

        private static string Relative(string root, string path)
        {
            if (root.Length > 0 && path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return path.Substring(root.Length).TrimStart('\\', '/').Replace('\\', '/');
            return Path.GetFileName(path);
        }
    }
}
