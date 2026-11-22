using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityGameTranslator.Common.UnityFiles;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The older Unity containers, "UnityRaw" and "UnityWeb" (Unity 3.x to 5.2), built here byte by
    /// byte from their public description (UnityPy, AssetStudio): a big-endian header, the levels'
    /// sizes, then a directory and its files, stored or in one LZMA stream. The LZMA stream is the one
    /// liblzma wrote for the very same directory and serialized file (see <see cref="WebStream"/>).
    /// </summary>
    internal static class LegacyBundleChecks
    {
        private const string SerializedName = "CAB-0123.assets";
        private const string ResourceName = "CAB-0123.resS";
        private static readonly byte[] Resource = Encoding.ASCII.GetBytes("resource bytes, not a serialized file");

        // The directory then the two files, as the data of a legacy bundle. Offsets count from its start.
        internal static byte[] Payload(byte[] serialized)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            void Big32(uint v) { w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
            int directory = 4 + SerializedName.Length + 1 + 8 + ResourceName.Length + 1 + 8;
            Big32(2);
            w.Write(Encoding.ASCII.GetBytes(SerializedName + "\0")); Big32((uint)directory); Big32((uint)serialized.Length);
            w.Write(Encoding.ASCII.GetBytes(ResourceName + "\0")); Big32((uint)(directory + serialized.Length)); Big32((uint)Resource.Length);
            w.Write(serialized);
            w.Write(Resource);
            return ms.ToArray();
        }

        /// <summary>The header of format <paramref name="version"/>, then the data it announces.</summary>
        internal static byte[] Bundle(string signature, int version, byte[] stored, uint uncompressedSize, int levels = 1)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            void Big32(uint v) { w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
            w.Write(Encoding.ASCII.GetBytes(signature + "\0"));
            Big32((uint)version);
            w.Write(Encoding.ASCII.GetBytes("3.x.x\0"));
            w.Write(Encoding.ASCII.GetBytes("3.5.7f6\0"));
            if (version >= 4) { w.Write(new byte[16]); Big32(0xDEADBEEF); }
            Big32(0);                       // minimum streamed bytes
            long headerSizeAt = ms.Position;
            Big32(0);                       // header size, patched below
            Big32(1);                       // levels to download before streaming
            Big32((uint)levels);
            for (int i = 0; i < levels - 1; i++) { Big32(7); Big32(9); }   // the inner levels, not read
            Big32((uint)stored.Length); Big32(uncompressedSize);
            if (version >= 2) Big32(0);     // complete file size
            if (version >= 3) Big32(0);     // file info header size
            uint headerSize = (uint)ms.Position;
            ms.Position = headerSizeAt;
            Big32(headerSize);
            ms.Position = headerSize;
            w.Write(stored);
            return ms.ToArray();
        }

        /// <summary>The payload of <see cref="Payload"/>, as liblzma wrote it (LZMA1, preset 9) with Unity's 13-byte header.</summary>
        private const string WebStream =
            "XQAAAATNAQAAAAAAAAAAZ/7o49JfWFXEgdhDOJJiUstiQYvGoyHRjiju4ME9NG9hq1hFzo+pLlKG7y4eBKWDWqXa+64Vw9Y8tiTSzNYHR+zKTBFyRHS+SEkdhdBN/HjC55qPWbvHgq0EqHNc2yYxAVHh0cwt1nmRdJpiGCsDwQzwKX4v+ft42D1qS49e0I9HkLuuHntXBCPRqtWtQ2Y1rPy6N1VMj8hPds+GUZgdhEuUWcgzDntl7uzfogPk9a4CgP6JUgli1C19pYUasKVpgtle2r0T4lmW/eepea95X7bcav/ebzVg";

        internal static void Run(Action<bool, string, string> check, byte[] serialized, byte[] fontFile)
        {
            var payload = Payload(serialized);
            if (Environment.GetEnvironmentVariable("UGT_DUMP_LEGACY") is string dump) File.WriteAllBytes(dump, payload);

            foreach (var (version, levels) in new[] { (2, 1), (3, 2), (5, 1) })
            {
                var raw = Bundle("UnityRaw", version, payload, (uint)payload.Length, levels);
                Verify(check, raw, serialized, $"UnityRaw format {version}, {levels} level(s)");
            }

            if (WebStream.Length > 0)
            {
                var stream = Convert.FromBase64String(WebStream);
                foreach (var version in new[] { 3, 4 })
                    Verify(check, Bundle("UnityWeb", version, stream, (uint)payload.Length), serialized, $"UnityWeb format {version}");

                var lying = (byte[])stream.Clone();
                lying[5] ^= 1;   // the size the LZMA header announces
                check(Refuses(() => UnityBundle.Open(new MemoryStream(Bundle("UnityWeb", 3, lying, (uint)payload.Length)))),
                    "a UnityWeb bundle whose LZMA header and bundle header disagree on the size is refused", "one of the two lies");
                var damaged = (byte[])stream.Clone();
                damaged[damaged.Length / 2] ^= 0x5A;
                check(Refuses(() => ReadAll(Bundle("UnityWeb", 3, damaged, (uint)payload.Length))) || !ReadAll(Bundle("UnityWeb", 3, damaged, (uint)payload.Length)).SequenceEqual(serialized),
                    "a damaged UnityWeb stream is refused or gives other bytes, never the original", "the checks above could not pass on a reader that ignores the data");
            }

            var good = Bundle("UnityRaw", 3, payload, (uint)payload.Length);
            check(Refuses(() => UnityBundle.Open(new MemoryStream(Bundle("UnityRaw", 3, payload, (uint)payload.Length + 1)))),
                "a UnityRaw bundle whose data is not the size it announces is refused", "stored bytes cannot be another length");
            check(Refuses(() => UnityBundle.Open(new MemoryStream(good, 0, good.Length - 5))),
                "a UnityRaw bundle cut short is refused", "the header announces data the file does not have");
            check(Refuses(() => UnityBundle.Open(new MemoryStream(Bundle("UnityRaw", 9, payload, (uint)payload.Length)))),
                "a UnityRaw format this reader does not know is refused", "9 is no format of the web containers");
            var outside = (byte[])payload.Clone();
            outside[4 + SerializedName.Length + 1 + 4] = 0xFF;   // the first file's size
            check(Refuses(() => UnityBundle.Open(new MemoryStream(Bundle("UnityRaw", 3, outside, (uint)outside.Length)))),
                "a file the directory puts outside the data is refused", "said, not read past the end");

            var archive = new MemoryStream(Encoding.ASCII.GetBytes("UnityArchive\0" + new string('x', 100)));
            bool looks = UnityBundle.LooksLikeOne(archive);
            string? archiveSaid = null;
            try { UnityBundle.Open(archive); } catch (UnityFileFormatException ex) { archiveSaid = ex.Message; }
            check(looks && archiveSaid != null && archiveSaid.Contains("UnityArchive"),
                "a UnityArchive bundle is recognised and refused by name", "its reader is not written: the file is said, not skipped");

            // In a data folder: the walk of a game's fonts opens the stored bundle like any other.
            string folder = Path.Combine(Path.GetTempPath(), "ugt-check-legacy-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllBytes(Path.Combine(folder, "old.unity3d"), good);
                var index = GameFonts.FromDataFolder(folder, "2021.3.27f1", withData: true);
                check(index.Fonts.Count == 1 && index.NotRead.Count == 0 && index.Fonts[0].Data != null && index.Fonts[0].Data!.SequenceEqual(fontFile),
                    "a game's font held in an older web bundle is found and read", "such a game is no longer left without its fonts");
            }
            finally { Directory.Delete(folder, true); }
        }

        private static void Verify(Action<bool, string, string> check, byte[] bundleBytes, byte[] serialized, string what)
        {
            var stream = new MemoryStream(bundleBytes);
            check(UnityBundle.LooksLikeOne(stream) && stream.Position == 0, $"{what}: recognised, the stream left where it was", "the walk asks before it opens");
            var bundle = UnityBundle.Open(stream);
            check(bundle.EngineVersion == "3.5.7f6" && bundle.Entries.Count == 2 && bundle.Entries[0].Path == SerializedName && bundle.Entries[1].Path == ResourceName,
                $"{what}: the engine version and the directory are read", "names in the order written");
            check(bundle.Entries[0].IsSerializedFile && !bundle.Entries[1].IsSerializedFile,
                $"{what}: the serialized file is told from a resource file", "only objects' files are walked");
            check(Read(bundle, bundle.Entries[0]).SequenceEqual(serialized) && Read(bundle, bundle.Entries[1]).SequenceEqual(Resource),
                $"{what}: each entry reads back byte for byte", "what a game's text and fonts are taken from");
            using (var inner = bundle.Open(bundle.Entries[0]))
            {
                inner.Position = 10;
                var middle = new byte[20];
                int n = inner.Read(middle, 0, 20);
                check(n == 20 && middle.SequenceEqual(serialized.Skip(10).Take(20)) && SerializedFile.Open(bundle.Open(bundle.Entries[0])).Objects.Count == 1,
                    $"{what}: an entry is seekable and opens as a serialized file", "the object walker reads it in place");
            }
        }

        private static byte[] Read(UnityBundle bundle, UnityBundle.Entry entry)
        {
            using (var s = bundle.Open(entry))
            {
                var all = new byte[entry.Size];
                int done = 0, n;
                while (done < all.Length && (n = s.Read(all, done, all.Length - done)) > 0) done += n;
                return all;
            }
        }

        private static byte[] ReadAll(byte[] bundleBytes)
        {
            var bundle = UnityBundle.Open(new MemoryStream(bundleBytes));
            return Read(bundle, bundle.Entries[0]);
        }

        private static bool Refuses(Action action)
        {
            try { action(); return false; }
            catch (UnityFileFormatException) { return true; }
        }
        private static bool Refuses(Func<byte[]> action)
        {
            try { action(); return false; }
            catch (UnityFileFormatException) { return true; }
        }
    }
}
