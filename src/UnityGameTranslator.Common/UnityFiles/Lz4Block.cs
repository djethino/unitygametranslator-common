using System;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// The LZ4 block format (no frame), as Unity compresses a bundle's blocks with LZ4 and LZ4HC —
    /// both decode the same. The output size is known from the block table.
    /// </summary>
    public static class Lz4Block
    {
        public static byte[] Decode(byte[] input, int outputSize)
        {
            var output = new byte[outputSize];
            int ip = 0, op = 0;
            while (ip < input.Length)
            {
                int token = input[ip++];

                int literals = token >> 4;
                if (literals == 15) literals += ExtraLength(input, ref ip);
                if (literals > input.Length - ip || literals > outputSize - op)
                    throw new UnityFileFormatException("LZ4: a literal run goes past the end");
                Buffer.BlockCopy(input, ip, output, op, literals);
                ip += literals;
                op += literals;
                if (ip == input.Length) break;   // the last sequence has no match

                if (ip + 2 > input.Length) throw new UnityFileFormatException("LZ4: the block ends inside an offset");
                int offset = input[ip] | (input[ip + 1] << 8);
                ip += 2;
                if (offset == 0 || offset > op) throw new UnityFileFormatException($"LZ4: offset {offset} at {op}");

                int length = token & 15;
                if (length == 15) length += ExtraLength(input, ref ip);
                length += 4;
                if (length > outputSize - op) throw new UnityFileFormatException("LZ4: a match goes past the output");
                // Byte by byte: a match may overlap what it copies (offset smaller than length).
                for (int from = op - offset, end = op + length; op < end; ) output[op++] = output[from++];
            }
            if (op != outputSize) throw new UnityFileFormatException($"LZ4: {op} byte(s) out of {outputSize}");
            return output;
        }

        private static int ExtraLength(byte[] input, ref int ip)
        {
            int extra = 0, b;
            do
            {
                if (ip >= input.Length) throw new UnityFileFormatException("LZ4: the block ends inside a length");
                b = input[ip++];
                extra += b;
            } while (b == 255);
            return extra;
        }
    }
}
