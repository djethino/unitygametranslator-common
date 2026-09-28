using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>One file an export carries: its name in the pack, and where it is read from.</summary>
    public sealed class ExportFile
    {
        public ExportFile(string entryName, string sourcePath, AssetKind kind)
        {
            EntryName = entryName;
            SourcePath = sourcePath;
            Kind = kind;
        }

        /// <summary>"fonts/NotoSans.ttf", "images/title.png".</summary>
        public string EntryName { get; }

        public string SourcePath { get; }

        public AssetKind Kind { get; }
    }

    /// <summary>What an export would carry — counted before anything is written.</summary>
    public sealed class ExportPlan
    {
        public ExportPlan(IReadOnlyList<ExportFile> files, IReadOnlyList<ImageDefinition> images)
        {
            Files = files;
            Images = images;
        }

        public IReadOnlyList<ExportFile> Files { get; }

        /// <summary>The image settings the pack's manifest carries — each with its picture in <see cref="Files"/>.</summary>
        public IReadOnlyList<ImageDefinition> Images { get; }

        public int Fonts => Files.Count(f => f.Kind == AssetKind.Font);

        public int ImageCount => Files.Count(f => f.Kind == AssetKind.Image);

        public bool IsEmpty => Files.Count == 0;
    }

    /// <summary>
    /// Writes a `.ugtpack` — the other half of <see cref="AssetPackReader"/>, in the socle so the mod
    /// and UGT Manager make the SAME pack from the same game (user, 2026-09-28: the mod exports too).
    ///
    /// 🔴 **Only what the translation USES** (user, 2026-09-27): an image goes with its setting; a font
    /// goes when a font setting or rule names it as Custom. A font left in fonts/ that nothing picks is
    /// noise to whoever receives the pack. System fonts go only when asked for — their licences are the
    /// sharer's to check (<see cref="AssetPacks.ShareNotice"/>).
    ///
    /// ⚠ Without ZipArchive, for the reader's reason: a stripped game may lack System.IO.Compression.dll,
    /// while DeflateStream — constructed exactly as the mod's atlas cache does — runs on every test game.
    /// A plain zip: stored or deflated, UTF-8 names, no ZIP64 (fonts and pictures never need it).
    ///
    /// ⚠ The manifest is written here as text: the socle has no JSON library, and one writer is what
    /// keeps the two products' manifests identical. Products still READ the translation with their own
    /// JSON and hand over what they read.
    /// </summary>
    public static class AssetPackWriter
    {
        /// <summary>Under the packs folder: where the mod writes its exports. The mod reads packs from the
        /// packs folder itself, not below it, so an export is never offered back for import.</summary>
        public const string ExportedFolder = "exported";

        /// <summary>Why there is nothing to export — the same words in both products.</summary>
        public const string NothingToExport = "This game's translation uses no added font or image.";

        /// <summary>
        /// Whether an export always carries this font file: a Custom font the translation uses. A copy of a
        /// System font goes only when the System fonts are asked for; a file marked as the game's never goes.
        /// </summary>
        public static bool IsExported(string fileName, IEnumerable<string> fontReferences) =>
            fontReferences.Any(r => FontReferences.Order(r)[0] == FontSource.Custom
                                    && AssetPacks.IsFontFileFor(fileName, FontReferences.Name(r)));

        /// <summary>
        /// Every file the export carries, read from the mod's data folder.
        /// </summary>
        /// <param name="fontReferences">Every font reference the translation applies, as written (origin mark included).</param>
        /// <param name="definitions">The translation's image settings, in file order.</param>
        /// <param name="systemFonts">Installed fonts asked for: the name the translation uses, and the file.</param>
        public static ExportPlan Plan(string dataFolder, IReadOnlyList<string> fontReferences,
                                      IEnumerable<ImageDefinition> definitions,
                                      IEnumerable<KeyValuePair<string, string>> systemFonts)
        {
            var files = new List<ExportFile>();
            var images = new List<ImageDefinition>();

            foreach (var definition in definitions)
            {
                var file = definition.File;
                if (file == null || !AssetPacks.IsSafeFileName(file) || !AssetPacks.IsImageFile(file)) continue;

                var source = Path.Combine(Path.Combine(dataFolder, AssetPacks.ImagesFolder), file);
                if (!File.Exists(source)) continue;

                files.Add(new ExportFile(AssetPacks.ImagesFolder + "/" + file, source, AssetKind.Image));
                images.Add(definition);
            }

            var packed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fontsFolder = Path.Combine(dataFolder, AssetPacks.FontsFolder);
            if (Directory.Exists(fontsFolder))
            {
                foreach (var path in Directory.GetFiles(fontsFolder).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                {
                    var name = Path.GetFileName(path);
                    if (!AssetPacks.IsFontFile(name) || !AssetPacks.IsSafeFileName(name)) continue;
                    if (!IsExported(name, fontReferences)) continue;

                    files.Add(new ExportFile(AssetPacks.FontsFolder + "/" + name, path, AssetKind.Font));
                    packed.Add(name);
                }
            }

            // Written under the name the translation uses, so the mod receiving the pack finds it among its
            // own fonts before looking at the system.
            foreach (var system in systemFonts)
            {
                var name = system.Key + Path.GetExtension(system.Value).ToLowerInvariant();
                if (!AssetPacks.IsSafeFileName(name) || !AssetPacks.IsFontFile(name) || !packed.Add(name)) continue;
                if (!File.Exists(system.Value)) continue;

                files.Add(new ExportFile(AssetPacks.FontsFolder + "/" + name, system.Value, AssetKind.Font));
            }

            return new ExportPlan(files, images);
        }

        /// <summary>The manifest, as JSON text.</summary>
        /// <param name="targetLanguage">The language the pictures' text is in — the translation's target; left out when not settled.</param>
        public static string ManifestJson(string gameName, string? steamId, string madeBy, string? targetLanguage,
                                          IEnumerable<ImageDefinition> images)
        {
            var json = new StringBuilder();
            json.Append("{\n");
            json.Append("  ").Append(Quote(PackManifest.FormatField)).Append(": ").Append(AssetPacks.Format).Append(",\n");

            json.Append("  ").Append(Quote(PackManifest.GameField)).Append(": { ")
                .Append(Quote(PackManifest.GameNameField)).Append(": ").Append(Quote(gameName));
            if (!string.IsNullOrWhiteSpace(steamId))
                json.Append(", ").Append(Quote(PackManifest.SteamIdField)).Append(": ").Append(Quote(steamId!));
            json.Append(" },\n");

            json.Append("  ").Append(Quote(PackManifest.MadeByField)).Append(": ").Append(Quote(madeBy)).Append(",\n");

            if (Languages.IsSettled(targetLanguage))
                json.Append("  ").Append(Quote(PackManifest.TargetLanguageField)).Append(": ").Append(Quote(targetLanguage!)).Append(",\n");

            json.Append("  ").Append(Quote(PackManifest.ImagesField)).Append(": [");
            var first = true;
            foreach (var image in images)
            {
                json.Append(first ? "\n    { " : ",\n    { ");
                first = false;

                var firstField = true;
                foreach (var field in image.Fields())
                {
                    if (!firstField) json.Append(", ");
                    firstField = false;
                    json.Append(Quote(field.Key)).Append(": ")
                        .Append(field.Value is double number ? Number(number) : Quote((string)field.Value));
                }

                json.Append(" }");
            }

            json.Append(first ? "]\n" : "\n  ]\n");
            json.Append("}\n");
            return json.ToString();
        }

        /// <summary>
        /// The export's file name: the game, and the moment — "LONESTAR assets 2026-09-28 14-32.ugtpack".
        ///
        /// 🔴 Dated rather than replaced or numbered (user, 2026-09-28): a pack already sent to somebody
        /// must not be overwritten by a newer state of the game, and "(2)" says nothing about which is
        /// which. Two exports in the same minute are the same moment, and the second replaces the first.
        /// </summary>
        public static string FileName(string gameName, DateTime when)
        {
            // A character no system accepts in a file name becomes a space, and spaces run together once.
            var clean = new StringBuilder();
            foreach (var c in gameName)
            {
                var kept = c < 32 || "\\/:*?\"<>|".IndexOf(c) >= 0 ? ' ' : c;
                if (kept == ' ' && clean.Length > 0 && clean[clean.Length - 1] == ' ') continue;
                clean.Append(kept);
            }

            var game = clean.ToString().Trim().TrimEnd('.');
            if (game.Length == 0) game = "Game";

            var name = game + " assets " + when.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture) + AssetPacks.Extension;
            return AssetPacks.IsSafeFileName(name) ? name : "Game assets " + when.ToString("yyyy-MM-dd HH-mm", CultureInfo.InvariantCulture) + AssetPacks.Extension;
        }

        /// <summary>
        /// Writes the pack: every file, then the manifest. Throws on a file that cannot be read — the caller
        /// writes to a temporary file and keeps the previous one on failure.
        /// </summary>
        public static void Write(Stream output, IEnumerable<ExportFile> files, string manifestJson)
        {
            var entries = new List<Written>();
            var offset = 0L;
            var stamp = DosTime(DateTime.Now);

            foreach (var file in files)
                offset += WriteEntry(output, file.EntryName, File.ReadAllBytes(file.SourcePath), stamp, offset, entries);

            offset += WriteEntry(output, AssetPacks.ManifestName, new UTF8Encoding(false).GetBytes(manifestJson), stamp, offset, entries);

            if (entries.Count >= 0xFFFF) throw new InvalidDataException("Too many files for one pack.");

            var directoryStart = offset;
            var directorySize = 0L;
            foreach (var entry in entries)
            {
                var header = new List<byte>();
                U32(header, 0x02014b50);
                U16(header, 20);               // made by: 2.0
                U16(header, 20);               // needed: 2.0
                U16(header, 0x0800);           // names in UTF-8
                U16(header, entry.Method);
                U32(header, stamp);
                U32(header, entry.Crc);
                U32(header, entry.Compressed);
                U32(header, entry.Size);
                U16(header, entry.Name.Length);
                U16(header, 0);                // extra
                U16(header, 0);                // comment
                U16(header, 0);                // disk
                U16(header, 0);                // internal attributes
                U32(header, 0);                // external attributes
                U32(header, entry.Offset);
                header.AddRange(entry.Name);

                output.Write(header.ToArray(), 0, header.Count);
                directorySize += header.Count;
            }

            if (directoryStart + directorySize > uint.MaxValue) throw new InvalidDataException("A pack over 4 GB cannot be written.");

            var end = new List<byte>();
            U32(end, 0x06054b50);
            U16(end, 0);
            U16(end, 0);
            U16(end, entries.Count);
            U16(end, entries.Count);
            U32(end, (uint)directorySize);
            U32(end, (uint)directoryStart);
            U16(end, 0);
            output.Write(end.ToArray(), 0, end.Count);
            output.Flush();
        }

        private sealed class Written
        {
            public byte[] Name = new byte[0];
            public int Method;
            public uint Crc;
            public uint Compressed;
            public uint Size;
            public uint Offset;
        }

        private static long WriteEntry(Stream output, string name, byte[] data, uint stamp, long offset, List<Written> entries)
        {
            if (offset > uint.MaxValue || data.LongLength > uint.MaxValue)
                throw new InvalidDataException("A pack over 4 GB cannot be written.");

            var crc = ~AssetPackReader.Crc32Step(0xFFFFFFFFu, data, data.Length);

            byte[] deflated;
            using (var memory = new MemoryStream())
            {
                using (var deflate = new DeflateStream(memory, CompressionMode.Compress, leaveOpen: true))
                    deflate.Write(data, 0, data.Length);
                deflated = memory.ToArray();
            }

            // Fonts are often already compressed and pictures always are: stored when deflate gains nothing.
            var method = deflated.Length < data.Length ? 8 : 0;
            var body = method == 8 ? deflated : data;

            var entry = new Written
            {
                Name = Encoding.UTF8.GetBytes(name),
                Method = method,
                Crc = crc,
                Compressed = (uint)body.Length,
                Size = (uint)data.Length,
                Offset = (uint)offset,
            };

            var header = new List<byte>();
            U32(header, 0x04034b50);
            U16(header, 20);
            U16(header, 0x0800);
            U16(header, method);
            U32(header, stamp);
            U32(header, crc);
            U32(header, entry.Compressed);
            U32(header, entry.Size);
            U16(header, entry.Name.Length);
            U16(header, 0);
            header.AddRange(entry.Name);

            output.Write(header.ToArray(), 0, header.Count);
            output.Write(body, 0, body.Length);
            entries.Add(entry);

            return header.Count + body.Length;
        }

        /// <summary>MS-DOS date and time, as a zip stores them: date in the high half, time in the low.</summary>
        private static uint DosTime(DateTime t)
        {
            var year = Math.Max(1980, t.Year);
            var date = ((year - 1980) << 9) | (t.Month << 5) | t.Day;
            var time = (t.Hour << 11) | (t.Minute << 5) | (t.Second / 2);
            return ((uint)date << 16) | (uint)time;
        }

        private static void U16(List<byte> b, int v)
        {
            b.Add((byte)v);
            b.Add((byte)(v >> 8));
        }

        private static void U32(List<byte> b, uint v)
        {
            b.Add((byte)v);
            b.Add((byte)(v >> 8));
            b.Add((byte)(v >> 16));
            b.Add((byte)(v >> 24));
        }

        private static string Number(double value) =>
            Math.Abs(value) < 1e15 && value == Math.Floor(value)
                ? ((long)value).ToString(CultureInfo.InvariantCulture)
                : value.ToString("R", CultureInfo.InvariantCulture);

        private static string Quote(string text)
        {
            var sb = new StringBuilder(text.Length + 2);
            sb.Append('"');
            foreach (var c in text)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }

            return sb.Append('"').ToString();
        }
    }
}
