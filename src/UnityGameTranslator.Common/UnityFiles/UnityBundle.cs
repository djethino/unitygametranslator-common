using System;
using System.Collections.Generic;
using System.IO;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// A Unity asset bundle ("UnityFS", format 6 to 8 — Unity 5.3 onwards, data.unity3d and the
    /// Addressables bundles included): its files, each given back decompressed on demand. Only the
    /// blocks a file covers are decompressed — a game's bundle can be gigabytes of textures.
    ///
    /// The older web containers of Unity 3.x to 5.2 are read too: "UnityWeb" (one LZMA stream) and
    /// "UnityRaw" (the same data, stored). Their directory and files are decompressed whole when the
    /// bundle is opened — they are small next to a UnityFS — and an entry is served from that copy.
    ///
    /// Refused, said: "UnityArchive" and anything whose tables do not read (an encrypted bundle among them).
    /// </summary>
    public sealed class UnityBundle
    {
        private const uint CompressionMask = 0x3F;
        private const uint BlocksInfoAtTheEnd = 0x80;
        private const uint BlockInfoNeedPaddingAtStart = 0x200;
        private const uint NodeIsSerializedFile = 4;

        private readonly UnityFileReader _reader;
        private readonly List<Block> _blocks = new List<Block>();
        private long _dataStart;
        // A UnityWeb / UnityRaw bundle: the whole decompressed data, standing for its only block.
        private byte[]? _legacyData;

        public sealed class Entry
        {
            internal Entry(string path, long offset, long size, uint flags) { Path = path; Offset = offset; Size = size; Flags = flags; }
            public string Path { get; }
            internal long Offset { get; }
            public long Size { get; }
            internal uint Flags { get; }
            /// <summary>A serialized file (objects), not a resource stream (.resS) nor a resource.</summary>
            public bool IsSerializedFile => (Flags & NodeIsSerializedFile) != 0;
        }

        private struct Block
        {
            public uint Uncompressed, Compressed, Flags;
            public long UncompressedStart, CompressedStart;
        }

        public int FormatVersion { get; private set; }
        /// <summary>The engine version written in the header ("2021.3.27f1"), or what the bundle put there ("0.0.0").</summary>
        public string EngineVersion { get; private set; } = "";
        public IReadOnlyList<Entry> Entries { get; private set; } = Array.Empty<Entry>();

        private UnityBundle(Stream stream) { _reader = new UnityFileReader(stream, bigEndian: true); }

        private static readonly string[] Signatures = { "UnityFS", "UnityWeb", "UnityRaw", "UnityArchive" };

        /// <summary>
        /// Whether the stream starts like a Unity bundle (it is left where it was). "UnityArchive" is
        /// recognised too: <see cref="Open(Stream)"/> then says it is not one this reader opens.
        /// </summary>
        public static bool LooksLikeOne(Stream stream)
        {
            long at = stream.Position;
            var head = new byte[16];
            int n = 0, got;
            while (n < head.Length && (got = stream.Read(head, n, head.Length - n)) > 0) n += got;
            stream.Position = at;
            foreach (var signature in Signatures)
            {
                if (n <= signature.Length || head[signature.Length] != 0) continue;
                if (System.Text.Encoding.ASCII.GetString(head, 0, signature.Length) == signature) return true;
            }
            return false;
        }

        public static UnityBundle Open(Stream stream)
        {
            var bundle = new UnityBundle(stream);
            bundle.ReadHeader();
            return bundle;
        }

        private void ReadHeader()
        {
            var r = _reader;
            r.Position = 0;
            string signature = r.Zstring(16);
            if (signature == "UnityWeb" || signature == "UnityRaw") { ReadLegacy(signature == "UnityWeb"); return; }
            if (signature != "UnityFS")
                throw new UnityFileFormatException($"a '{signature}' bundle, not one of UnityFS, UnityWeb and UnityRaw: not read");
            FormatVersion = (int)r.U32();
            if (FormatVersion < 6 || FormatVersion > 8)
                throw new UnityFileFormatException($"UnityFS format {FormatVersion}: not one this reader knows (6 to 8)");
            r.Zstring(64);                  // the player version family ("5.x.x")
            EngineVersion = r.Zstring(64);  // the engine revision ("2021.3.27f1")
            long size = r.I64();
            uint compressedInfo = r.U32(), uncompressedInfo = r.U32(), flags = r.U32();
            if (size > r.Length) throw new UnityFileFormatException($"the bundle says {size} byte(s), the file has {r.Length}");
            // An encrypted bundle (Unity's China builds) carries no mark this reader trusts: it fails
            // below, at its block table, and is said as a file not read.
            if (FormatVersion >= 7) r.Align(16);

            byte[] info;
            if ((flags & BlocksInfoAtTheEnd) != 0)
            {
                long back = r.Position;
                r.Position = r.Length - compressedInfo;
                info = r.Bytes((int)compressedInfo);
                r.Position = back;
            }
            else info = r.Bytes((int)compressedInfo);
            info = Decompress(info, (int)uncompressedInfo, flags & CompressionMask, "block table");

            var t = new UnityFileReader(new MemoryStream(info), bigEndian: true);
            t.Skip(16);   // hash of the uncompressed data
            int blockCount = t.Count(10);
            long uStart = 0, cStart = 0;
            for (int i = 0; i < blockCount; i++)
            {
                var b = new Block { Uncompressed = t.U32(), Compressed = t.U32(), Flags = t.U16(), UncompressedStart = uStart, CompressedStart = cStart };
                uStart += b.Uncompressed;
                cStart += b.Compressed;
                _blocks.Add(b);
            }
            int nodeCount = t.Count(20);
            var entries = new List<Entry>(nodeCount);
            for (int i = 0; i < nodeCount; i++)
            {
                long offset = t.I64(), length = t.I64();
                uint nodeFlags = t.U32();
                string path = t.Zstring(1024);
                if (offset < 0 || length < 0 || offset + length > uStart)
                    throw new UnityFileFormatException($"'{path}' lies outside the bundle's data");
                entries.Add(new Entry(path, offset, length, nodeFlags));
            }
            Entries = entries;

            if ((flags & BlockInfoNeedPaddingAtStart) != 0) r.Align(16);
            _dataStart = r.Position;
            if (_dataStart + cStart > r.Length)
                throw new UnityFileFormatException($"the blocks need {cStart} byte(s) from {_dataStart}, the file has {r.Length}");
        }

        /// <summary>
        /// One file of the bundle as a read-only, seekable stream: a block is decompressed when a read
        /// reaches it (the last one kept), so reading a file's object table and two fonts out of it
        /// costs those blocks, not the whole file.
        /// </summary>
        public Stream Open(Entry entry) => new EntryStream(this, entry);

        /// <summary>
        /// UnityWeb / UnityRaw (format 2 to 5): a big-endian header, the levels' sizes, then from the
        /// header's own size the data — one LZMA stream (a 13-byte header: properties, then the size)
        /// or the same bytes stored. Decompressed, it is a directory (a count, then for each file a
        /// name, an offset and a size) and the files, their offsets counted from the start of that data.
        /// </summary>
        private void ReadLegacy(bool compressed)
        {
            var r = _reader;
            string kind = compressed ? "UnityWeb" : "UnityRaw";
            FormatVersion = (int)r.U32();
            if (FormatVersion < 2 || FormatVersion > 5)
                throw new UnityFileFormatException($"{kind} format {FormatVersion}: not one this reader knows (2 to 5)");
            r.Zstring(64);                  // the player version family ("3.x.x")
            EngineVersion = r.Zstring(64);  // the engine revision ("3.4.2f2")
            if (FormatVersion >= 4) r.Skip(16 + 4);   // hash and checksum
            r.U32();                        // minimum streamed bytes
            uint headerSize = r.U32();
            r.U32();                        // levels to download before streaming
            int levels = r.Count(8);
            if (levels < 1) throw new UnityFileFormatException($"a {kind} bundle with {levels} level(s)");
            r.Skip((levels - 1) * 8L);      // the last level is the whole data
            uint compressedSize = r.U32(), uncompressedSize = r.U32();
            if (headerSize > r.Length || compressedSize > r.Length - headerSize)
                throw new UnityFileFormatException($"the bundle's data ({compressedSize} byte(s) from {headerSize}) lies outside the file ({r.Length})");
            if (uncompressedSize > int.MaxValue)
                throw new UnityFileFormatException($"the bundle's data is {uncompressedSize} byte(s): too large to hold");
            r.Position = headerSize;
            byte[] data = r.Bytes((int)compressedSize);

            if (compressed)
            {
                // Properties (5), the uncompressed size (8, little-endian; all ones when unknown), the stream.
                if (data.Length < 13) throw new UnityFileFormatException("UnityWeb: no LZMA header");
                ulong said = 0;
                for (int i = 12; i >= 5; i--) said = (said << 8) | data[i];
                if (said != ulong.MaxValue && said != uncompressedSize)
                    throw new UnityFileFormatException($"UnityWeb: the LZMA header says {said} byte(s), the bundle's header {uncompressedSize}");
                var stream = new byte[data.Length - 8];
                Buffer.BlockCopy(data, 0, stream, 0, 5);
                Buffer.BlockCopy(data, 13, stream, 5, data.Length - 13);
                data = LzmaDecoder.Decode(stream, (int)uncompressedSize);
            }
            else if (data.Length != uncompressedSize)
                throw new UnityFileFormatException($"UnityRaw: {data.Length} byte(s) stored, {uncompressedSize} announced");

            _legacyData = data;
            _blocks.Add(new Block { Uncompressed = (uint)data.Length, Compressed = (uint)data.Length });

            var t = new UnityFileReader(new MemoryStream(data), bigEndian: true);
            int count = t.Count(9);   // a name's end, an offset and a size, at least
            var entries = new List<Entry>(count);
            for (int i = 0; i < count; i++)
            {
                string path = t.Zstring(1024);
                long offset = t.U32(), length = t.U32();
                if (offset + length > data.Length) throw new UnityFileFormatException($"'{path}' lies outside the bundle's data");
                entries.Add(new Entry(path, offset, length, 0));
            }
            // The directory does not say which file holds objects: one that opens like a serialized file
            // does — a header at the wrong size too, so the caller says it rather than skipping it.
            for (int i = 0; i < entries.Count; i++)
            {
                var plain = entries[i];
                using (var inner = Open(plain))
                    if (SerializedFile.Probe(inner, out _) != SerializedProbe.NotSerialized)
                        entries[i] = new Entry(plain.Path, plain.Offset, plain.Size, NodeIsSerializedFile);
            }
            Entries = entries;
        }

        private byte[]? _cachedBlock;
        private int _cachedIndex = -1;

        /// <summary>The decompressed block <paramref name="index"/>.</summary>
        private byte[] BlockAt(int index)
        {
            if (_legacyData != null) return _legacyData;
            if (index == _cachedIndex && _cachedBlock != null) return _cachedBlock;
            var b = _blocks[index];
            _reader.Position = _dataStart + b.CompressedStart;
            var raw = _reader.Bytes((int)b.Compressed);
            _cachedBlock = Decompress(raw, (int)b.Uncompressed, b.Flags & CompressionMask, "block");
            _cachedIndex = index;
            return _cachedBlock;
        }

        /// <summary>The block holding byte <paramref name="position"/> of the bundle's data, by bisection.</summary>
        private int BlockOf(long position)
        {
            int lo = 0, hi = _blocks.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (_blocks[mid].UncompressedStart <= position) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        private sealed class EntryStream : Stream
        {
            private readonly UnityBundle _bundle;
            private readonly Entry _entry;
            private long _position;

            public EntryStream(UnityBundle bundle, Entry entry) { _bundle = bundle; _entry = entry; }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => _entry.Size;
            public override long Position
            {
                get => _position;
                set => _position = value < 0 ? throw new ArgumentOutOfRangeException(nameof(value)) : value;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int done = 0;
                while (done < count && _position < _entry.Size)
                {
                    long at = _entry.Offset + _position;
                    int index = _bundle.BlockOf(at);
                    var block = _bundle.BlockAt(index);
                    long inBlock = at - _bundle._blocks[index].UncompressedStart;
                    int n = (int)Math.Min(Math.Min(count - done, block.Length - inBlock), _entry.Size - _position);
                    if (n <= 0) throw new UnityFileFormatException($"'{_entry.Path}': no data at {_position}");
                    Buffer.BlockCopy(block, (int)inBlock, buffer, offset + done, n);
                    done += n;
                    _position += n;
                }
                return done;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? _position + offset : _entry.Size + offset;
                return _position;
            }

            public override void Flush() { }
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private static byte[] Decompress(byte[] data, int size, uint compression, string what)
        {
            switch (compression)
            {
                case 0:
                    if (data.Length != size) throw new UnityFileFormatException($"an uncompressed {what} of {data.Length} byte(s) announced as {size}");
                    return data;
                case 1: return LzmaDecoder.Decode(data, size);
                case 2:
                case 3: return Lz4Block.Decode(data, size);
                default: throw new UnityFileFormatException($"a {what} compressed with method {compression}: not one this reader knows");
            }
        }
    }
}
