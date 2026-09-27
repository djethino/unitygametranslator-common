using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The pack reader against what .NET's own zip writer produces, and against zips forged to lie.
    ///
    /// ⚠ The reader exists so the mod does not need System.IO.Compression.dll; it must therefore read
    /// every ordinary zip exactly as the standard reader would — or somebody's hand-made pack, legitimate
    /// by decision (2026-09-27), would be refused for nothing.
    /// </summary>
    internal static class AssetPackReaderChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var big = new byte[200_000];
            new Random(7).NextBytes(big);
            var text = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("compressible ", 5000)));

            var zip = Build(archive =>
            {
                Add(archive, "manifest.json", Encoding.UTF8.GetBytes("{\"format\":1}"), CompressionLevel.Optimal);
                Add(archive, "images/标题.png", big, CompressionLevel.NoCompression);
                Add(archive, "fonts/a.ttf", text, CompressionLevel.Optimal);
                archive.CreateEntry("images/");
            }, comment: "made by hand");

            var entries = AssetPackReader.Entries(zip);
            check(entries.Select(e => e.Name).SequenceEqual(new[] { "manifest.json", "images/标题.png", "fonts/a.ttf", "images/" })
                  && entries.Single(e => e.Name == "images/").IsFolder,
                "a zip written by .NET is listed entry by entry, names in any script, folders marked",
                "a pack built by hand with any ordinary tool must be read like one the Manager exported");

            foreach (var (name, content) in new[] { ("images/标题.png", big), ("fonts/a.ttf", text) })
            {
                var entry = entries.Single(e => e.Name == name);
                using var stream = AssetPackReader.Open(zip, entry);
                var measure = AssetPackReader.Measure(stream, entry.Size);

                check(!measure.Over && measure.Length == content.Length && measure.Crc32 == entry.Crc32
                      && measure.Sha256 == Hex(SHA256.HashData(content)),
                    $"{name} unpacks to exactly what was put in, its checksum the one declared",
                    "stored and deflated alike — the two ways every zip tool writes");
            }

            // Reading entries out of order, the stream moved between them.
            var font = entries.Single(e => e.Name == "fonts/a.ttf");
            var image = entries.Single(e => e.Name == "images/标题.png");
            using (var first = AssetPackReader.Open(zip, font))
            using (var second = AssetPackReader.Open(zip, image))
            {
                var a = new byte[10];
                first.Read(a, 0, 10);
                AssetPackReader.Measure(second, image.Size);
                var rest = AssetPackReader.Measure(first, font.Size);
                check(rest.Length == text.Length - 10,
                    "two entries read in turn from one file each keep their place",
                    "the file is shared; each entry reads from its own offset");
            }

            // A size that lies: declared 16, holding 50 000.
            var lying = Build(archive => Add(archive, "fonts/bomb.ttf", new byte[50_000], CompressionLevel.Optimal));
            DeclareSize(lying, "fonts/bomb.ttf", 16);
            var bomb = AssetPackReader.Entries(lying).Single();
            using (var stream = AssetPackReader.Open(lying, bomb))
            {
                var measure = AssetPackReader.Measure(stream, bomb.Size);
                check(bomb.Size == 16 && measure.Over && measure.Length == 17,
                    "an entry larger than it declares is stopped one byte past its declaration",
                    "a few kilobytes that unpack to terabytes are caught at the first byte too many");
            }

            // Encrypted, or compressed another way: refused on opening, with the reason.
            var odd = Build(archive => Add(archive, "fonts/x.ttf", text, CompressionLevel.Optimal));
            var oddEntry = AssetPackReader.Entries(odd).Single();
            oddEntry.GetType().GetProperty("Encrypted")!.SetValue(oddEntry, true);
            check(Throws(() => AssetPackReader.Open(odd, oddEntry)),
                "an encrypted entry is refused",
                "its content cannot be checked, so it cannot be written");

            oddEntry.GetType().GetProperty("Encrypted")!.SetValue(oddEntry, false);
            oddEntry.GetType().GetProperty("Method")!.SetValue(oddEntry, 12);
            check(Throws(() => AssetPackReader.Open(odd, oddEntry)),
                "an entry compressed another way than deflate is refused",
                "nothing but stored and deflated is ever unpacked");

            // Not a zip, a truncated zip, a zip whose directory points outside it.
            check(Throws(() => AssetPackReader.Entries(new MemoryStream(Encoding.UTF8.GetBytes("MZ this is a program")))),
                "a file that is not a zip is refused", "renamed to .ugtpack, it is still not a pack");

            var whole = Build(archive => Add(archive, "fonts/a.ttf", text, CompressionLevel.Optimal)).ToArray();
            check(Throws(() => AssetPackReader.Entries(new MemoryStream(whole.Take(whole.Length / 2).ToArray()))),
                "a pack cut in half is refused", "a download that stopped halfway must not install half a font");

            var outside = (byte[])whole.Clone();
            var end = LastIndexOf(outside, new byte[] { 0x50, 0x4B, 0x05, 0x06 });
            BitConverter.GetBytes(0x7FFFFFFFu).CopyTo(outside, end + 16);
            check(Throws(() => AssetPackReader.Entries(new MemoryStream(outside))),
                "a directory pointing outside the file is refused", "reading there would read whatever the forger chose");
        }

        private static MemoryStream Build(Action<ZipArchive> fill, string? comment = null)
        {
            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            {
                if (comment != null) archive.Comment = comment;
                fill(archive);
            }

            stream.Position = 0;
            return stream;
        }

        private static void Add(ZipArchive archive, string name, byte[] content, CompressionLevel level)
        {
            using var stream = archive.CreateEntry(name, level).Open();
            stream.Write(content, 0, content.Length);
        }

        /// <summary>Rewrites the size an archive declares for one entry, in both its headers.</summary>
        private static void DeclareSize(MemoryStream archive, string entryName, uint size)
        {
            var bytes = archive.GetBuffer();
            var name = Encoding.UTF8.GetBytes(entryName);

            for (var i = 0; i + 46 < archive.Length; i++)
            {
                if (bytes[i] != 0x50 || bytes[i + 1] != 0x4B) continue;

                if (bytes[i + 2] == 3 && bytes[i + 3] == 4 && BitConverter.ToUInt16(bytes, i + 26) == name.Length
                    && bytes.AsSpan(i + 30, name.Length).SequenceEqual(name))
                    BitConverter.GetBytes(size).CopyTo(bytes, i + 22);
                else if (bytes[i + 2] == 1 && bytes[i + 3] == 2 && BitConverter.ToUInt16(bytes, i + 28) == name.Length
                         && bytes.AsSpan(i + 46, name.Length).SequenceEqual(name))
                    BitConverter.GetBytes(size).CopyTo(bytes, i + 24);
            }
        }

        private static int LastIndexOf(byte[] haystack, byte[] needle)
        {
            for (var i = haystack.Length - needle.Length; i >= 0; i--)
                if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle)) return i;
            return -1;
        }

        private static bool Throws(Action act)
        {
            try { act(); return false; }
            catch (InvalidDataException) { return true; }
        }

        private static string Hex(byte[] bytes) => Convert.ToHexString(bytes);
    }
}
