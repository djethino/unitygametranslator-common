using System;
using System.IO;
using System.Text;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>A Unity data file that is not what it claims, or not one this reader knows — said, never guessed past.</summary>
    public sealed class UnityFileFormatException : Exception
    {
        public UnityFileFormatException(string message) : base(message) { }
    }

    /// <summary>
    /// Reads Unity's binary files over any seekable stream: either byte order (a bundle header is
    /// big-endian, a serialized file says its own), zero-terminated strings, alignment to four.
    /// Every read past the end is a <see cref="UnityFileFormatException"/>, never a short value.
    /// </summary>
    public sealed class UnityFileReader
    {
        private readonly Stream _stream;
        private readonly byte[] _scratch = new byte[8];

        public UnityFileReader(Stream stream, bool bigEndian)
        {
            if (!stream.CanSeek) throw new ArgumentException("a seekable stream is needed", nameof(stream));
            _stream = stream;
            BigEndian = bigEndian;
        }

        public bool BigEndian { get; set; }

        public long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }

        public long Length => _stream.Length;

        public void Fill(byte[] buffer, int offset, int count)
        {
            int done = 0;
            while (done < count)
            {
                int n = _stream.Read(buffer, offset + done, count - done);
                if (n <= 0) throw new UnityFileFormatException($"the file ends {count - done} byte(s) early, at {Position}");
                done += n;
            }
        }

        public byte[] Bytes(int count)
        {
            if (count < 0 || count > Length - Position)
                throw new UnityFileFormatException($"{count} byte(s) asked at {Position}, {Length - Position} left");
            var bytes = new byte[count];
            Fill(bytes, 0, count);
            return bytes;
        }

        private byte[] Ordered(int size)
        {
            Fill(_scratch, 0, size);
            if (BigEndian == BitConverter.IsLittleEndian) Array.Reverse(_scratch, 0, size);
            return _scratch;
        }

        public byte U8() { Fill(_scratch, 0, 1); return _scratch[0]; }
        public bool Bool() => U8() != 0;
        public short I16() => BitConverter.ToInt16(Ordered(2), 0);
        public ushort U16() => BitConverter.ToUInt16(Ordered(2), 0);
        public int I32() => BitConverter.ToInt32(Ordered(4), 0);
        public uint U32() => BitConverter.ToUInt32(Ordered(4), 0);
        public long I64() => BitConverter.ToInt64(Ordered(8), 0);
        public ulong U64() => BitConverter.ToUInt64(Ordered(8), 0);
        public float F32() => BitConverter.ToSingle(Ordered(4), 0);
        public double F64() => BitConverter.ToDouble(Ordered(8), 0);

        /// <summary>A count read from the file: refused when negative or larger than what is left.</summary>
        public int Count(int elementSize = 1)
        {
            int n = I32();
            if (n < 0 || (long)n * Math.Max(1, elementSize) > Length - Position)
                throw new UnityFileFormatException($"a count of {n} at {Position - 4} does not fit the {Length - Position} byte(s) left");
            return n;
        }

        /// <summary>A UTF-8 string ended by a zero byte, refused past <paramref name="max"/> bytes.</summary>
        public string Zstring(int max = 4096)
        {
            var bytes = new MemoryStream();
            for (int i = 0; i < max; i++)
            {
                byte b = U8();
                if (b == 0) return Encoding.UTF8.GetString(bytes.ToArray());
                bytes.WriteByte(b);
            }
            throw new UnityFileFormatException($"no end to a string at {Position - max}");
        }

        public void Align(int to = 4)
        {
            long rest = Position % to;
            if (rest != 0) Position += to - rest;
        }

        public void Skip(long count)
        {
            if (count < 0 || count > Length - Position)
                throw new UnityFileFormatException($"{count} byte(s) skipped at {Position}, {Length - Position} left");
            Position += count;
        }
    }
}
