using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// A pack written by the socle, read back by the socle's own reader — what the mod's export and the
    /// Manager's make, and what both open. Real files in a temporary folder: a zip is bytes on disk.
    /// </summary>
    internal static class AssetPackWriterChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var root = Path.Combine(Path.GetTempPath(), "ugt-writer-" + Guid.NewGuid().ToString("N"));
            try
            {
                var fonts = Path.Combine(root, AssetPacks.FontsFolder);
                var images = Path.Combine(root, AssetPacks.ImagesFolder);
                Directory.CreateDirectory(fonts);
                Directory.CreateDirectory(images);

                File.WriteAllBytes(Path.Combine(fonts, "Used.ttf"), Repeated("used font ", 4000));
                File.WriteAllBytes(Path.Combine(fonts, "Left.otf"), Repeated("tried once ", 100));
                File.WriteAllBytes(Path.Combine(images, "标题.png"), Random(3000));
                var system = Path.Combine(root, "system-candara.ttf");
                File.WriteAllBytes(system, Repeated("system ", 500));

                ImageDefinition Def(string sprite, string file) =>
                    ImageDefinition.Read(n => n == "sprite_name" ? sprite : n == "file" ? file : n == "pivot_x" ? (object)0.5 : null)!;

                var references = new[] { "[Custom] Used", "[Game] LiberationSans SDF", "Arial" };
                var plan = AssetPackWriter.Plan(root, references, new[] { Def("标题", "标题.png"), Def("Gone", "gone.png") },
                                                new[] { new KeyValuePair<string, string>("Candara", system) });

                check(plan.Fonts == 2 && plan.ImageCount == 1
                      && plan.Files.Any(f => f.EntryName == "fonts/Used.ttf")
                      && plan.Files.All(f => f.EntryName != "fonts/Left.otf")
                      && plan.Files.Any(f => f.EntryName == "fonts/Candara.ttf")
                      && plan.Images.Single().Sprite == "标题",
                    "an export carries the Custom fonts used, the System fonts asked for, and each image whose file is there",
                    "a font nothing picks is noise; a setting whose picture is missing would point at nothing");

                var manifest = AssetPackWriter.ManifestJson("Sample \"Game\"", "367520", "checks", "fr", plan.Images);
                var pack = Path.Combine(root, "out" + AssetPacks.Extension);
                using (var output = File.Create(pack))
                    AssetPackWriter.Write(output, plan.Files, manifest);

                using (var zip = File.OpenRead(pack))
                {
                    var entries = AssetPackReader.Entries(zip);
                    var same = plan.Files.All(file =>
                    {
                        var entry = entries.SingleOrDefault(e => e.Name == file.EntryName);
                        if (entry == null) return false;
                        PackMeasure measured;
                        using (var content = AssetPackReader.Open(zip, entry)) measured = AssetPackReader.Measure(content, entry.Size);
                        var original = File.ReadAllBytes(file.SourcePath);
                        return !measured.Over && measured.Length == original.Length && measured.Crc32 == entry.Crc32
                               && measured.Crc32 == Crc32(original);
                    });

                    check(same && entries.Count == plan.Files.Count + 1,
                        "a written pack reads back whole through the socle's reader, names in any script, every byte checked",
                        "the mod and the Manager open what either of them exports");

                    string text;
                    using (var content = AssetPackReader.OpenByName(zip, AssetPacks.ManifestName))
                    using (var reader = new StreamReader(content, Encoding.UTF8))
                        text = reader.ReadToEnd();

                    check(text.Contains("\"name\": \"Sample \\\"Game\\\"\"") && text.Contains("\"steam_id\": \"367520\"")
                          && text.Contains("\"target_language\": \"fr\"") && text.Contains("\"pivot_x\": 0.5")
                          && text.Contains("\"sprite_name\": \"标题\""),
                        "the manifest names the game, the language and every image setting, escaped",
                        "one writer: both products' manifests are the same text");

                    var deflated = entries.Single(e => e.Name == "fonts/Used.ttf");
                    var stored = entries.Single(e => e.Name == "images/标题.png");
                    check(deflated.Method == 8 && deflated.CompressedSize < deflated.Size && stored.Method == 0,
                        "what compresses is deflated, what does not is stored",
                        "pictures are already compressed: deflating them again only costs time");
                }

                // Which System fonts an export may offer: bare references only, a copy in fonts/ first.
                File.WriteAllBytes(Path.Combine(fonts, "Candara.ttf"), Repeated("copy ", 10));
                var offered = AssetPackWriter.SystemFonts(
                    new[] { "Candara", "Arial", "MS Gothic", "[Custom] Used", "[Game] LiberationSans SDF", "Nowhere" }, root,
                    stem => stem == "Arial" ? "C:\\Windows\\Fonts\\arial.ttf" : stem == "MS Gothic" ? "C:\\Windows\\Fonts\\msgothic.ttc" : null);

                check(offered.Select(c => c.Reference).SequenceEqual(new[] { "Arial", "Candara", "MS Gothic", "Nowhere" })
                      && offered.Single(c => c.Reference == "Candara").Path == Path.Combine(fonts, "Candara.ttf")
                      && offered.Single(c => c.Reference == "Arial").Includable
                      && offered.Single(c => c.Reference == "MS Gothic").Why == AssetPackWriter.CollectionNotSupported
                      && offered.Single(c => c.Reference == "Nowhere").Why == AssetPackWriter.NotOnThisComputer,
                    "only bare references are System fonts; a copy in fonts/ goes first; a .ttc or a missing font says why",
                    "[Custom] and [Game] name files that are not the system's to carry");

                // The one list of font folders (audit 2026-09-28): what each search used to miss.
                var windows = SystemFontFolders.For(SystemFontFolders.Os.Windows, "C:\\Windows", "C:\\Users\\u\\AppData\\Local");
                var linux = SystemFontFolders.For(SystemFontFolders.Os.Linux, home: "/home/u");
                var linuxXdg = SystemFontFolders.For(SystemFontFolders.Os.Linux, home: "/home/u", xdgDataHome: "/data");
                var mac = SystemFontFolders.For(SystemFontFolders.Os.MacOs, home: "/Users/u");
                check(windows.Count == 2 && windows[1].EndsWith(Path.Combine("Microsoft", "Windows", "Fonts"))
                      && linux.Contains("/home/u/.local/share/fonts") && linux.Contains("/home/u/.fonts") && linux.Contains("/usr/local/share/fonts")
                      && linuxXdg.Contains("/data/fonts")
                      && mac.Contains("/System/Library/Fonts/Supplemental") && mac.Contains("/Users/u/Library/Fonts"),
                    "the installed fonts include each user's own, on every system",
                    "four lists disagreed: a font found by one search was missing from the next");

                var when = new DateTime(2026, 9, 28, 14, 32, 5);
                check(AssetPackWriter.FileName("LONESTAR", when) == "LONESTAR assets 2026-09-28 14-32.ugtpack"
                      && AssetPackWriter.FileName("Who: \"Me\"?", when) == "Who Me assets 2026-09-28 14-32.ugtpack"
                      && AssetPackWriter.FileName("龙胤立志传", when) == "龙胤立志传 assets 2026-09-28 14-32.ugtpack",
                    "an export is named after the game and the minute, in a name every system can write",
                    "dated, never overwriting a pack already sent, never an unreadable (2)");
            }
            finally
            {
                try { Directory.Delete(root, true); } catch (IOException) { }
            }
        }

        /// <summary>CRC-32 computed here, bit by bit, from the zip specification — not the library's table.</summary>
        private static uint Crc32(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in data)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }

            return ~crc;
        }

        private static byte[] Repeated(string text, int times) =>
            Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(text, times)));

        private static byte[] Random(int length)
        {
            var bytes = new byte[length];
            new Random(7).NextBytes(bytes);
            return bytes;
        }
    }
}
