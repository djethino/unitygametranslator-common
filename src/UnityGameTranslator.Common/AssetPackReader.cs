using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>One file listed in a pack's directory — what the pack DECLARES, before anything is read.</summary>
    public sealed class PackEntry
    {
        public string Name { get; internal set; } = "";

        /// <summary>The size the pack declares once unpacked. A claim: see <see cref="AssetPackReader.Open"/>.</summary>
        public long Size { get; internal set; }

        public long CompressedSize { get; internal set; }

        /// <summary>The checksum the pack declares for the unpacked content.</summary>
        public uint Crc32 { get; internal set; }

        /// <summary>How it is stored: 0 as is, 8 deflated — the two a pack may use.</summary>
        public int Method { get; internal set; }

        public bool Encrypted { get; internal set; }

        public bool IsFolder => Name.EndsWith("/", StringComparison.Ordinal);

        internal long LocalHeaderOffset { get; set; }
    }

    /// <summary>What a stream actually held — measured, never declared.</summary>
    public sealed class PackMeasure
    {
        public long Length { get; internal set; }

        /// <summary>SHA-256, upper-case hex. Empty when the read stopped past its limit.</summary>
        public string Sha256 { get; internal set; } = "";

        public uint Crc32 { get; internal set; }

        /// <summary>The first <see cref="AssetPacks.HeaderLength"/> bytes, for <see cref="AssetPacks.ContentMatches"/>.</summary>
        public byte[] Head { get; internal set; } = new byte[AssetPacks.HeaderLength];

        public int HeadCount { get; internal set; }

        /// <summary>The read went past its limit and stopped there: the content is larger than allowed.</summary>
        public bool Over { get; internal set; }
    }

    /// <summary>
    /// Reads a `.ugtpack` — a standard zip — with nothing but what every Unity runtime carries.
    ///
    /// 🔴 **Why not ZipArchive.** It lives in System.IO.Compression.dll, which a game's own folder
    /// often lacks (stripped builds); DeflateStream, which this uses, is the one the mod already runs
    /// on every test game, Mono and IL2CPP (its font atlas cache and word breaker). One reader then
    /// serves the mod AND the Manager, so both refuse exactly the same packs.
    ///
    /// ⚠ The format stays a plain zip on purpose (user, 2026-09-27): anybody can rename a pack to .zip
    /// and look inside before installing it, or build one by hand. What protects is what is checked
    /// on reading, not an unusual container.
    ///
    /// What is refused, whole: a file that is not a zip, a zip spread over several disks, ZIP64 (a
    /// font or a picture never needs four gigabytes), an encrypted entry, a compression other than
    /// deflate, and a directory pointing outside the file.
    /// </summary>
    public static class AssetPackReader
    {
        private const uint EndOfDirectory = 0x06054b50;
        private const uint DirectoryEntry = 0x02014b50;
        private const uint LocalEntry = 0x04034b50;

        /// <summary>The end record, then the longest comment a zip may carry after it.</summary>
        private const int EndSearchWindow = 22 + 0xFFFF;

        /// <summary>
        /// The pack's directory: every entry it declares. Throws <see cref="InvalidDataException"/>,
        /// with the reason in words, when the file cannot be read as a pack.
        /// </summary>
        public static List<PackEntry> Entries(Stream zip)
        {
            if (!zip.CanSeek || !zip.CanRead) throw new ArgumentException("A pack is read from a seekable stream.", nameof(zip));

            var length = zip.Length;
            if (length < 22) throw new InvalidDataException("Not a zip file.");

            // The end record sits in the last bytes, before an optional comment.
            var window = (int)Math.Min(length, EndSearchWindow);
            var tail = ReadAt(zip, length - window, window);

            var end = -1;
            for (var i = tail.Length - 22; i >= 0; i--)
            {
                if (U32(tail, i) == EndOfDirectory) { end = i; break; }
            }

            if (end < 0) throw new InvalidDataException("Not a zip file.");

            var disk = U16(tail, end + 4);
            var directoryDisk = U16(tail, end + 6);
            var onDisk = U16(tail, end + 8);
            var total = U16(tail, end + 10);
            var directorySize = U32(tail, end + 12);
            var directoryOffset = U32(tail, end + 16);

            if (disk != 0 || directoryDisk != 0 || onDisk != total)
                throw new InvalidDataException("A zip split over several files cannot be read.");

            if (total == 0xFFFF || directorySize == 0xFFFFFFFF || directoryOffset == 0xFFFFFFFF)
                throw new InvalidDataException("A ZIP64 archive cannot be read. Fonts and pictures never need one.");

            if ((long)directoryOffset + directorySize > length)
                throw new InvalidDataException("Damaged: its directory points outside the file.");

            var directory = ReadAt(zip, directoryOffset, (int)directorySize);
            var entries = new List<PackEntry>(total);
            var at = 0;

            for (var n = 0; n < total; n++)
            {
                if (at + 46 > directory.Length || U32(directory, at) != DirectoryEntry)
                    throw new InvalidDataException("Damaged: its directory is cut short.");

                var flags = U16(directory, at + 8);
                var method = U16(directory, at + 10);
                var crc = U32(directory, at + 16);
                var compressed = U32(directory, at + 20);
                var size = U32(directory, at + 24);
                var nameLength = U16(directory, at + 28);
                var extraLength = U16(directory, at + 30);
                var commentLength = U16(directory, at + 32);
                var local = U32(directory, at + 42);

                if (at + 46 + nameLength > directory.Length)
                    throw new InvalidDataException("Damaged: its directory is cut short.");

                if (compressed == 0xFFFFFFFF || size == 0xFFFFFFFF || local == 0xFFFFFFFF)
                    throw new InvalidDataException("A ZIP64 archive cannot be read. Fonts and pictures never need one.");

                entries.Add(new PackEntry
                {
                    // Names are read as UTF-8, which every tool writing zips today uses for anything
                    // beyond ASCII. A name decoded wrongly fails the name rules, it is never trusted.
                    Name = Encoding.UTF8.GetString(directory, at + 46, nameLength),
                    Size = size,
                    CompressedSize = compressed,
                    Crc32 = crc,
                    Method = method,
                    Encrypted = (flags & 1) != 0,
                    LocalHeaderOffset = local,
                });

                at += 46 + nameLength + extraLength + commentLength;
            }

            return entries;
        }

        /// <summary>
        /// The unpacked content of an entry — never more than one byte past what the pack declares,
        /// so a lie about the size is caught at that byte (<see cref="Measure"/> reports it as Over),
        /// and a few kilobytes can never unpack into a full disk.
        /// </summary>
        public static Stream Open(Stream zip, PackEntry entry)
        {
            if (entry.Encrypted) throw new InvalidDataException(entry.Name + " is encrypted.");
            if (entry.Method != 0 && entry.Method != 8)
                throw new InvalidDataException(entry.Name + " is compressed in a way UGT does not read.");

            var header = ReadAt(zip, entry.LocalHeaderOffset, 30);
            if (U32(header, 0) != LocalEntry) throw new InvalidDataException("Damaged: " + entry.Name + " is not where the directory says.");

            var dataStart = entry.LocalHeaderOffset + 30 + U16(header, 26) + U16(header, 28);
            if (dataStart + entry.CompressedSize > zip.Length)
                throw new InvalidDataException("Damaged: " + entry.Name + " runs past the end of the file.");

            Stream raw = new Slice(zip, dataStart, entry.CompressedSize);
            if (entry.Method == 8) raw = new DeflateStream(raw, CompressionMode.Decompress);

            return new Bounded(raw, entry.Size + 1);
        }

        /// <summary>
        /// Reads a stream to its end — or to one byte past <paramref name="limit"/>, then stops and says
        /// so. Length, SHA-256, CRC-32 and the first bytes, in one pass.
        /// </summary>
        public static PackMeasure Measure(Stream content, long limit)
        {
            var measure = new PackMeasure();
            var crc = 0xFFFFFFFFu;
            var buffer = new byte[81920];

            using (var sha = SHA256.Create())
            {
                int read;
                while ((read = content.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (measure.HeadCount < measure.Head.Length)
                    {
                        var take = Math.Min(measure.Head.Length - measure.HeadCount, read);
                        Array.Copy(buffer, 0, measure.Head, measure.HeadCount, take);
                        measure.HeadCount += take;
                    }

                    sha.TransformBlock(buffer, 0, read, null, 0);
                    crc = Crc32Step(crc, buffer, read);
                    measure.Length += read;

                    if (measure.Length > limit)
                    {
                        measure.Over = true;
                        return measure;
                    }
                }

                sha.TransformFinalBlock(new byte[0], 0, 0);
                measure.Sha256 = Hex(sha.Hash!);
            }

            measure.Crc32 = ~crc;
            return measure;
        }

        // ── Bytes ─────────────────────────────────────────────────────────

        private static readonly uint[] Crc32Table = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                var c = n;
                for (var k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                table[n] = c;
            }

            return table;
        }

        private static uint Crc32Step(uint crc, byte[] buffer, int count)
        {
            for (var i = 0; i < count; i++) crc = Crc32Table[(crc ^ buffer[i]) & 0xFF] ^ (crc >> 8);
            return crc;
        }

        private static string Hex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        private static byte[] ReadAt(Stream stream, long offset, int count)
        {
            var buffer = new byte[count];
            stream.Position = offset;

            var done = 0;
            while (done < count)
            {
                var read = stream.Read(buffer, done, count - done);
                if (read == 0) throw new InvalidDataException("Damaged: the file ends too early.");
                done += read;
            }

            return buffer;
        }

        private static int U16(byte[] b, int at) => b[at] | (b[at + 1] << 8);

        private static uint U32(byte[] b, int at) => (uint)(b[at] | (b[at + 1] << 8) | (b[at + 2] << 16) | (b[at + 3] << 24));

        /// <summary>A window of the pack file, read from its own position whatever else moved the stream.</summary>
        private sealed class Slice : Stream
        {
            private readonly Stream _inner;
            private readonly long _start;
            private readonly long _length;
            private long _read;

            public Slice(Stream inner, long start, long length)
            {
                _inner = inner;
                _start = start;
                _length = length;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var left = _length - _read;
                if (left <= 0) return 0;

                _inner.Position = _start + _read;
                var read = _inner.Read(buffer, offset, (int)Math.Min(count, left));
                _read += read;
                return read;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get => _read; set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        /// <summary>Hands out at most a set number of bytes, then reports the end — whatever lies beyond.</summary>
        private sealed class Bounded : Stream
        {
            private readonly Stream _inner;
            private long _left;

            public Bounded(Stream inner, long limit)
            {
                _inner = inner;
                _left = limit;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_left <= 0) return 0;

                var read = _inner.Read(buffer, offset, (int)Math.Min(count, _left));
                _left -= read;
                return read;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) _inner.Dispose();
                base.Dispose(disposing);
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }
}
