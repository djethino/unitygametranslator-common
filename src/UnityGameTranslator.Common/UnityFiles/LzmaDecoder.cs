using System;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// LZMA ("LZMA1"), as Unity writes a bundle's LZMA blocks: five property bytes (lc/lp/pb,
    /// dictionary size), then the range-coded stream, the output size known from the block table,
    /// with or without an end marker. Written after Igor Pavlov's reference decoder (LzmaSpec.cpp,
    /// public domain), decoding into the output array itself — it is the whole window.
    /// </summary>
    public static class LzmaDecoder
    {
        private const int NumStates = 12;
        private const int ProbInit = 1 << 10;
        private const int EndPosModelIndex = 14;
        private const int NumFullDistances = 1 << (EndPosModelIndex >> 1);
        private const int NumAlignBits = 4;
        private const int MatchMinLen = 2;

        public static byte[] Decode(byte[] input, int outputSize)
        {
            if (input.Length < 5) throw new UnityFileFormatException("LZMA: no properties");
            int d = input[0];
            if (d >= 9 * 5 * 5) throw new UnityFileFormatException($"LZMA: properties byte {d}");
            int lc = d % 9; d /= 9;
            int lp = d % 5;
            int pb = d / 5;
            uint dictSize = (uint)(input[1] | (input[2] << 8) | (input[3] << 16) | (input[4] << 24));
            if (dictSize < 4096) dictSize = 4096;
            return new Decoder(input, 5, lc, lp, pb, dictSize).Run(outputSize);
        }

        private sealed class Decoder
        {
            private readonly byte[] _in;
            private int _ip;
            private uint _range, _code;

            private readonly int _lc, _lp, _pb;
            private readonly uint _dictSize;
            private readonly ushort[] _literal;
            private readonly ushort[] _isMatch = Probs(NumStates << 4), _isRep = Probs(NumStates),
                _isRepG0 = Probs(NumStates), _isRepG1 = Probs(NumStates), _isRepG2 = Probs(NumStates),
                _isRep0Long = Probs(NumStates << 4);
            private readonly ushort[][] _posSlot = { Probs(1 << 6), Probs(1 << 6), Probs(1 << 6), Probs(1 << 6) };
            private readonly ushort[] _posDecoders = Probs(1 + NumFullDistances - EndPosModelIndex);
            private readonly ushort[] _align = Probs(1 << NumAlignBits);
            private readonly Length _len = new Length(), _repLen = new Length();

            private byte[] _out = Array.Empty<byte>();
            private int _op;

            public Decoder(byte[] input, int start, int lc, int lp, int pb, uint dictSize)
            {
                _in = input;
                _ip = start;
                _lc = lc; _lp = lp; _pb = pb;
                _dictSize = dictSize;
                _literal = Probs(0x300 << (lc + lp));
            }

            private static ushort[] Probs(int count)
            {
                var p = new ushort[count];
                for (int i = 0; i < count; i++) p[i] = ProbInit;
                return p;
            }

            private byte Next()
            {
                if (_ip >= _in.Length) throw new UnityFileFormatException("LZMA: the stream ends early");
                return _in[_ip++];
            }

            private void Normalize()
            {
                if (_range < (1u << 24)) { _range <<= 8; _code = (_code << 8) | Next(); }
            }

            private int Bit(ushort[] probs, int index)
            {
                uint v = probs[index];
                uint bound = (_range >> 11) * v;
                int bit;
                if (_code < bound) { v += ((1u << 11) - v) >> 5; _range = bound; bit = 0; }
                else { v -= v >> 5; _code -= bound; _range -= bound; bit = 1; }
                probs[index] = (ushort)v;
                Normalize();
                return bit;
            }

            private uint DirectBits(int count)
            {
                uint result = 0;
                do
                {
                    _range >>= 1;
                    _code -= _range;
                    uint t = 0u - (_code >> 31);
                    _code += _range & t;
                    if (_code == _range) throw new UnityFileFormatException("LZMA: corrupted direct bits");
                    Normalize();
                    result = (result << 1) + (t + 1);
                } while (--count > 0);
                return result;
            }

            private int Tree(ushort[] probs, int offset, int bits)
            {
                int m = 1;
                for (int i = 0; i < bits; i++) m = (m << 1) + Bit(probs, offset + m);
                return m - (1 << bits);
            }

            private int ReverseTree(ushort[] probs, int offset, int bits)
            {
                int m = 1, symbol = 0;
                for (int i = 0; i < bits; i++)
                {
                    int bit = Bit(probs, offset + m);
                    m = (m << 1) + bit;
                    symbol |= bit << i;
                }
                return symbol;
            }

            private sealed class Length
            {
                public readonly ushort[] Choice = Probs(2);
                public readonly ushort[] Low = Probs(16 << 3), Mid = Probs(16 << 3), High = Probs(1 << 8);
            }

            private int DecodeLength(Length l, int posState)
            {
                if (Bit(l.Choice, 0) == 0) return Tree(l.Low, posState << 3, 3);
                if (Bit(l.Choice, 1) == 0) return 8 + Tree(l.Mid, posState << 3, 3);
                return 16 + Tree(l.High, 0, 8);
            }

            private uint DecodeDistance(int len)
            {
                int lenState = Math.Min(len, 3);
                int posSlot = Tree(_posSlot[lenState], 0, 6);
                if (posSlot < 4) return (uint)posSlot;
                int directBits = (posSlot >> 1) - 1;
                uint dist = (uint)((2 | (posSlot & 1)) << directBits);
                if (posSlot < EndPosModelIndex)
                    // The reference indexes PosDecoders + dist - posSlot with m starting at 1.
                    dist += (uint)ReverseTree(_posDecoders, (int)dist - posSlot, directBits);
                else
                {
                    dist += DirectBits(directBits - NumAlignBits) << NumAlignBits;
                    dist += (uint)ReverseTree(_align, 0, NumAlignBits);
                }
                return dist;
            }

            public byte[] Run(int outputSize)
            {
                _out = new byte[outputSize];
                // The range decoder's start: a zero byte, then the code.
                if (Next() != 0) throw new UnityFileFormatException("LZMA: the stream does not start with zero");
                _range = 0xFFFFFFFF;
                _code = 0;
                for (int i = 0; i < 4; i++) _code = (_code << 8) | Next();
                if (_code == _range) throw new UnityFileFormatException("LZMA: corrupted start");

                int state = 0;
                uint rep0 = 0, rep1 = 0, rep2 = 0, rep3 = 0;
                int pbMask = (1 << _pb) - 1, lpMask = (1 << _lp) - 1;
                while (_op < outputSize)
                {
                    int posState = _op & pbMask;
                    if (Bit(_isMatch, (state << 4) + posState) == 0)
                    {
                        Literal(state, rep0, lpMask);
                        state = state < 4 ? 0 : state < 10 ? state - 3 : state - 6;
                        continue;
                    }

                    int len;
                    if (Bit(_isRep, state) != 0)
                    {
                        if (_op == 0) throw new UnityFileFormatException("LZMA: a repeat before any byte");
                        if (Bit(_isRepG0, state) == 0)
                        {
                            if (Bit(_isRep0Long, (state << 4) + posState) == 0)
                            {
                                state = state < 7 ? 9 : 11;
                                _out[_op] = _out[_op - rep0 - 1];
                                _op++;
                                continue;
                            }
                        }
                        else
                        {
                            uint dist;
                            if (Bit(_isRepG1, state) == 0) dist = rep1;
                            else
                            {
                                if (Bit(_isRepG2, state) == 0) dist = rep2;
                                else { dist = rep3; rep3 = rep2; }
                                rep2 = rep1;
                            }
                            rep1 = rep0;
                            rep0 = dist;
                        }
                        len = DecodeLength(_repLen, posState);
                        state = state < 7 ? 8 : 11;
                    }
                    else
                    {
                        rep3 = rep2; rep2 = rep1; rep1 = rep0;
                        len = DecodeLength(_len, posState);
                        state = state < 7 ? 7 : 10;
                        rep0 = DecodeDistance(len);
                        if (rep0 == 0xFFFFFFFF) break;   // the end marker
                        if (rep0 >= _dictSize || rep0 >= (uint)_op)
                            throw new UnityFileFormatException($"LZMA: distance {rep0} at {_op}");
                    }

                    len += MatchMinLen;
                    if (len > outputSize - _op) throw new UnityFileFormatException("LZMA: a match goes past the output");
                    for (int from = _op - (int)rep0 - 1, end = _op + len; _op < end; ) _out[_op++] = _out[from++];
                }
                if (_op != outputSize) throw new UnityFileFormatException($"LZMA: {_op} byte(s) out of {outputSize}");
                return _out;
            }

            private void Literal(int state, uint rep0, int lpMask)
            {
                int prev = _op > 0 ? _out[_op - 1] : 0;
                int litState = ((_op & lpMask) << _lc) + (prev >> (8 - _lc));
                int offset = 0x300 * litState;
                int symbol = 1;
                if (state >= 7)
                {
                    if (rep0 >= (uint)_op) throw new UnityFileFormatException("LZMA: a matched literal before its byte");
                    int match = _out[_op - (int)rep0 - 1];
                    do
                    {
                        int matchBit = (match >> 7) & 1;
                        match <<= 1;
                        int bit = Bit(_literal, offset + ((1 + matchBit) << 8) + symbol);
                        symbol = (symbol << 1) | bit;
                        if (matchBit != bit) break;
                    } while (symbol < 0x100);
                }
                while (symbol < 0x100) symbol = (symbol << 1) | Bit(_literal, offset + symbol);
                _out[_op++] = (byte)(symbol - 0x100);
            }
        }
    }
}
