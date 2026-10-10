using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityGameTranslator.Common.UnityFiles;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Reading a game's own files for its fonts (UnityFiles): the decompressors against streams
    /// another implementation wrote, the version keys, the generated Font layouts, and a serialized
    /// file built here byte by byte. The full proof — every font of the bench's players and of the
    /// test games equal to AssetsTools.NET's — runs on the bench (banc-unity, fontextract-proto
    /// compare), out of this repository.
    /// </summary>
    internal static class UnityFilesChecks
    {
        // Written by Python's lzma module (liblzma), as Unity stores a block: the five property bytes,
        // then the stream (the header's size field removed). Name, presets, size, SHA-256 prefix of the
        // original, the stream.
        private static readonly (string name, int size, string sha, string stream)[] LzmaVectors =
        {
            ("text, lc3", 2080, "630258f4b3d48de6", "XQAAgAAAKhoIogMlZvFLeMWiBf8u5tnSIBqtNPjiHehBNvrcBmm7POQQNCcJ67Nm4+03mO2SrdUnRQgwXl1xMUidrAMbo2iONwriD9vr/FkOXmUfyN/pL9GziDbObIt2WFczxsMZnDAFS11MQyuf/7hj4AA="),
            ("text, lc0 lp2", 2080, "630258f4b3d48de6", "bAAAEAAAKhoIogPDjni95SaaZoj9NAmS3vAyRkzmD5GnjKAnCqvWh+/E67h6WwoDdn4HZiXIWvSwTQDqvWcgpaoD9WcnMKCelma68L80kZEavASoq9uH93gz3dJEuYfhZav8IfAO5NQd+T3m77mQBt//xQigAA=="),
            ("random, lc3", 600, "e534507a26496831", "XQAAgAAASQqQI7bAME0e62betgX4fce/BhKNkPtDmzIz/VQtdUbuNRQ1xptnSaKtAk1G/vX0+razPS8q9ZlvplIG/ZUJljvlKd60icw+0FUC8TtR0bishmUFUrr9uiWYx+lWgtYd7P5d9jgGSOgPO8ZMDJf8VMUWP47XgOmpEtWbbcGTHk6hkemLp4FpZmkkWped9TDC4TD52XbjZ4fiUg70CBW/jRnhqSDFBTl5jUcBsgkw/g3t3HMPndnbZJaFhTL1wa/bpQvsEhmU8Pp48A/0daKv0ho4UDHhirKCQNz1hyIIBKiNXem690k92fOACg1nYJLM2pjZ6CTsvT/I2geaveRVuXn8J7X3QQnLdfu891WOAoiJzCX4J1rNxd82sjyh2F1g2pqLTRi0VHfjBabEolNNp4phTV5qWx82ztNdC/n8dgskEZvw2x7NuZteXDRD/oKYn+98FkEtiso1mOMnrCQP3pknEikhOtdXBBcsSzV+2vQyQGZV4MFRWYy5t9+4XtqYMvc8h7QpfuUxxMM+xWZOAL9xZ8orL2AhbI4zXpBpqS5v7KBKXvTDPe3BylDW5jC10WiChQQ2EDXpVlCdOrffL2QZaf/ZuVpbcOkXTECpInYZDeCz+YKTLG5om4gD1JjqSR+DmYDwKm/nZOVHQ2Ws82dDFE5eWwiBd/k359Yjsv4vd7eMFOZhM6Bjr1PymEdKJwNF8Fp7wDI12DB8OaoQ8oDvdEIv4fL1thbbfqZ3Iey40x3m7MjrGTR3oxoDgEuECixXIzU2gmF3Gp8+RZf//rAo5gs4ErzGUi9fBZLmIXIEtX/4BvDth6ZEEHr///KRwAA="),
            ("random, lc0 lp2", 600, "e534507a26496831", "bAAAEAAASQqQI7bANbpZKZdSKw5OsElF4YifEslZad3c5cIThpvvoMXPgR10m9L0ArGVvkg4rBzZGtDVzmQ4ylfU5auoAWyI578kjtf9i5v/o1adOgsPlueuJRU6Oikc4nmDqFp6duoUXUvPdkg6tDnhjhN4nGw8nrhdwjbGIVSzUPkIN1UhEOuMy7MzJP3NV2O+t0l4A447GrgdkVwFv5Y36ehWA5McjTtc4r5JrNDG4NeLR3VRFegIO5znVumRwoyHAvHYlvxcLtel5VQXvZwX5t1TAIeuKzMEYqNfTV6UcJVbWPfbV24aa4Pvp7V9A1nRuZ1IP0KsspyyUhhiuwpWvzr0hsHMHVDh+5JWmOO2nPwTqvZrv9Q7zOGAYPIHS3UItw2NAAmKBHKI9Uv6AOEc2KD8o1z41iwP52rlbQkBxVhNnoFeDIOgenR7/neIUKq5jFyoEHqTDcdO6hFPU/XQ3OYSCqnpTs4m+kPp993m9y8tMp8M2I8mmPwvSIGcc3hlvvSCm0aV/RjJ2MDcuBL9Kh8UX3yCfyCZuJMlwsMdRSF44a/JdI7NHefFyGXkJYI3hkBu0unHPOxUz78FUOWthLcxWF7YmWckvXDKZZRLe1xWbLkLj+bUdB7BK0RvbrZ2UEFloSDfsKPXWRHVu3zFLR2kmYtKwqS7J3D8MQRYymB3In2MuAFYnLYvzvUBdy7jOQugk3CxXJ1ucXEqBLMLikbY+sczo8sEGsTqHau+b65nFKi4seerPxtzanc/9NzQTBljGdNNihMBbeU5ot6a/PHGLdvA+VkwK2vcRVxeKyF9LJ4oy+a3g8jK/LWcuLHa/z//122AAA=="),
            ("runs, lc3", 4545, "7c6314cd1fb9f023", "XQAAgAAAIO/7v/6jsV7l+D+yqiZV+Gge/tKEegFlsLc0PG3gjfC/Q74CzbzBV8+GKMtmoJBC4YTPS3Yt2GSmoKZ/cY4Tlg4Hpf35//bJ1JYl/DxfgJC9jsjb4llfft9GrF5R+Q/MlimzIATpiVU4GsYCTq6I8cysAhu7B0yf9/9XoE4+uEXLkuJrYmX28p5eUltQ0SxB5bnT1bQtUcw6/S96plfvopFtg52kJIorm3uwjvzA7Q5AhfsNAb6KK4Mq5+sLqSVUqpeSfW8yn7evQHClEEHctztRfJfYStEs7359xtOHBa4gGINyP/P+ZoiqDxUytSp91pzNIz/LjPHPkd6L3xan4U9u56fq+v//k/5iAA=="),
            ("runs, lc0 lp2", 4545, "7c6314cd1fb9f023", "bAAAEAAAIO/7v/6jsV7l+D+yqiZV+Gge/tKEg+L2/skWgMX1XrTtsWrOqFNwTk+Cb4GcTg5T/QpbG98iFtlBwue/IwjfbhcTt+tuMHn+/n5CQUcG2peRA7JRVbGclLqj5JYOw9PplHIlwE0zvD+gBfmx5jSbUF95xBPgkLZHXKFmIAy2DCVyGfUd35qMoxLnGu4TU60r+OeAWHCnx1KTuTCo9vwhpcf+DHrcwl6jRjQi0+dB1HFzRhk5o4g9fexPe6XfRFtnda4PX8kDxw4tOMFoxvU/FkiFsZRzhyyNk7cPqNwxvNhbqKdRSaZJJkN9Az+6JVB632Z7TnDZuw54nxo2xVl+KsKPomTNEQSDZg39/iaIjEpzkh7XFVK3S89MgUaDdIhwiXgdwQ+yKln7//LEIMA="),
        };

        private static string Sha(byte[] b)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(b)).Replace("-", "").Substring(0, 16).ToLowerInvariant();
        }

        private static bool Refuses(Func<byte[]> decode)
        {
            try { decode(); return false; }
            catch (UnityFileFormatException) { return true; }
        }

        public static void Run(Action<bool, string, string> check)
        {
            foreach (var v in LzmaVectors)
            {
                var stream = Convert.FromBase64String(v.stream);
                check(Sha(LzmaDecoder.Decode(stream, v.size)) == v.sha,
                    $"LZMA decodes a stream liblzma wrote ({v.name})", "a bundle compressed with LZMA gives back its files byte for byte");
            }
            var records = Records();
            foreach (var (name, stream) in new[] { ("lc3", UnityFilesVectors.RecordsLc3), ("lc0 lp2 pb0", UnityFilesVectors.RecordsLp2) })
            {
                var decoded = LzmaDecoder.Decode(Convert.FromBase64String(stream), records.Length);
                check(Sha(decoded) == Sha(records),
                    $"LZMA decodes records alike, with repeats after matches ({name})", "the paths text and noise never take — a broken state update passed every other vector");
            }

            var broken = Convert.FromBase64String(LzmaVectors[0].stream);
            broken[40] ^= 0x5A;
            check(Refuses(() => LzmaDecoder.Decode(broken, LzmaVectors[0].size)) || Sha(LzmaDecoder.Decode(broken, LzmaVectors[0].size)) != LzmaVectors[0].sha,
                "a damaged LZMA stream is refused or gives other bytes, never the original", "the check above could not pass on a decoder that ignores its input");
            check(Refuses(() => LzmaDecoder.Decode(Convert.FromBase64String(LzmaVectors[0].stream), LzmaVectors[0].size + 100)),
                "an LZMA stream shorter than the size announced is refused", "a block table that lies is said, not padded with zeros");

            // LZ4: "abc" then a match of 20 at distance 3 (overlapping its own output), then "z".
            var lz4 = new byte[] { 0x3F, (byte)'a', (byte)'b', (byte)'c', 3, 0, 1, 0x10, (byte)'z' };
            var expected = new StringBuilder();
            for (int i = 0; i < 23; i++) expected.Append("abc"[i % 3]);
            expected.Append('z');
            check(Encoding.ASCII.GetString(Lz4Block.Decode(lz4, 24)) == expected.ToString(),
                "LZ4 copies an overlapping match and its extended length", "a match shorter than its distance is the common case in text");
            check(Refuses(() => Lz4Block.Decode(new byte[] { 0x3F, (byte)'a', 9, 0, 1 }, 24)),
                "an LZ4 match reaching before the start is refused", "offset 9 after one byte");

            check(UnityVersions.Key("2021.3.27f1") < UnityVersions.Key("2021.3.28f1")
                  && UnityVersions.Key("6000.0.84f1") > UnityVersions.Key("2023.2.22f1")
                  && UnityVersions.Key("2018.4.36f1") > UnityVersions.Key("2018.4.36b2")
                  && UnityVersions.Key("2021.3.27f1c1") == UnityVersions.Key("2021.3.27f1"),
                "Unity versions sort as releases do", "a layout is chosen by the last version at or before the game's");
            check(UnityVersions.Key("0.0.0") == 0 && UnityVersions.Key("") == 0 && UnityVersions.Key("x.y") == 0,
                "a version a bundle leaves blank is no version", "the caller then uses the game's own");
            check(UnityVersions.Text(UnityVersions.Key("6000.7.0b4")) == "6000.7.0b4", "a version key reads back as its text", "it names the version in the journal");

            var font2021 = FontLayouts.For(UnityVersions.Key("2021.3.27f1"), out bool newer2021);
            check(font2021 != null && !newer2021 && Has(font2021, "m_FontData") && Has(font2021, "m_FontNames") && Has(font2021, "m_Name"),
                "a Font layout exists for a version the table knows, with the fields read", "m_Name, m_FontData, m_FontNames");
            FontLayouts.For(UnityVersions.Key("9000.1.0f1"), out bool newer9000);
            check(newer9000, "a version newer than the table is said to be", "the closest layout below is used, and may be wrong");
            check(FontLayouts.For(UnityVersions.Key("2.6.0f1"), out _) == null, "a version older than any Font has no layout", "nothing guessed");
            check(FontLayouts.CommonString(0) == "AABB" && FontLayouts.CommonString(5) == "AnimationClip",
                "an embedded type tree's common string is found at its offset", "names are counted in bytes, each ended by a zero");

            BuiltFile(check);
        }

        /// <summary>The records <see cref="UnityFilesVectors"/> compress: the same generator as the Python that wrote them.</summary>
        internal static byte[] Records()
        {
            uint x = 12345;
            var output = new List<byte>();
            for (int i = 0; i < 1500; i++)
            {
                x = (x * 1103515245 + 12345) & 0x7FFFFFFF;
                uint r = x >> 16;
                output.Add((byte)(i & 0xFF));
                output.Add((byte)((i >> 8) & 0xFF));
                output.Add((byte)(r & 0x0F));
                output.Add(0);
                output.Add((r & 0x10) != 0 ? (byte)0x7F : (byte)0x80);
                output.Add((r & 0x300) == 0 ? (byte)(r & 0xFF) : (byte)0x20);
            }
            return output.ToArray();
        }

        private static bool Has(IReadOnlyList<TypeNode> tree, string name)
        {
            foreach (var n in tree) if (n.Depth == 1 && n.Name == name) return true;
            return false;
        }

        /// <summary>
        /// A serialized file of format 22 written here, its Font object laid out with the 2021.3 table
        /// and no type tree (a release build): the font file comes back whole, and its names.
        /// </summary>
        private static void BuiltFile(Action<bool, string, string> check)
        {
            var tree = FontLayouts.For(UnityVersions.Key("2021.3.27f1"), out _)!;
            var fontFile = new byte[] { 0x00, 0x01, 0x00, 0x00, 0x42, 0x43, 0x44 };
            var obj = new MemoryStream();
            var w = new BinaryWriter(obj);
            // Write the object along the tree: each top-level field with a neutral value.
            WriteObject(w, tree, new Dictionary<string, object>
            {
                ["m_Name"] = "Bench Font",
                ["m_FontData"] = fontFile,
                ["m_FontNames"] = new[] { "Bench Family" },
            });
            var objBytes = obj.ToArray();

            var meta = new MemoryStream();
            var m = new BinaryWriter(meta);
            m.Write(Encoding.ASCII.GetBytes("2021.3.27f1\0"));
            m.Write(19);          // platform
            m.Write((byte)0);     // no type trees
            m.Write(1);           // one type
            m.Write(FontLayouts.FontClassId);
            m.Write((byte)0);     // not stripped
            m.Write((short)-1);   // no script
            m.Write(new byte[16]);
            m.Write(1);           // one object
            const int headerSize = 48;
            while ((headerSize + meta.Length) % 4 != 0) m.Write((byte)0);
            m.Write(7L);          // path id
            m.Write(0L);          // start in the data
            m.Write((uint)objBytes.Length);
            m.Write(0);           // type index
            m.Write(0);           // script types
            m.Write(0);           // externals
            m.Write(0);           // reference types
            m.Write((byte)0);     // user information
            var metaBytes = meta.ToArray();

            long dataOffset = (headerSize + metaBytes.Length + 15) / 16 * 16;
            var file = new MemoryStream();
            var f = new BinaryWriter(file);
            void Big32(uint v) { f.Write((byte)(v >> 24)); f.Write((byte)(v >> 16)); f.Write((byte)(v >> 8)); f.Write((byte)v); }
            void Big64(ulong v) { Big32((uint)(v >> 32)); Big32((uint)v); }
            Big32(0); Big32(0); Big32(22); Big32(0);
            f.Write((byte)0); f.Write(new byte[3]);
            Big32((uint)metaBytes.Length); Big64((ulong)(dataOffset + objBytes.Length)); Big64((ulong)dataOffset); Big64(0);
            f.Write(metaBytes);
            while (file.Length < dataOffset) f.Write((byte)0);
            f.Write(objBytes);
            file.Position = 0;

            check(SerializedFile.LooksLikeOne(file), "a format 22 file is recognised by its header", "its sizes agree with the stream");
            var read = SerializedFile.Open(file);
            check(read.Objects.Count == 1 && read.Objects[0].ClassId == FontLayouts.FontClassId && read.Objects[0].PathId == 7,
                "the object table is read", "path id, class, place");
            var fields = read.Fields(read.Objects[0], tree, new[] { "m_Name", "m_FontData", "m_FontNames" });
            check(fields["m_Name"] as string == "Bench Font"
                  && fields["m_FontData"] is byte[] data && Sha(data) == Sha(fontFile)
                  && fields["m_FontNames"] is List<object?> names && names.Count == 1 && names[0] as string == "Bench Family",
                "a Font without type tree gives back its name, its whole file and its family", "what the export writes and the UI.Text backup names");
            check(!SerializedFile.LooksLikeOne(new MemoryStream(Encoding.ASCII.GetBytes(new string('x', 200)))),
                "a file that is not a serialized file is not taken for one", "a game's folder holds DLLs, videos, configs");
        }

        /// <summary>Writes neutral values along a tree, the named top-level fields with the given ones.</summary>
        private static void WriteObject(BinaryWriter w, IReadOnlyList<TypeNode> tree, Dictionary<string, object> values)
        {
            int i = 1;
            while (i < tree.Count)
            {
                var node = tree[i];
                values.TryGetValue(node.Name, out var value);
                i = WriteNode(w, tree, i, value);
            }
        }

        // Writes node i (and its subtree) with value, or zeros / empty arrays; returns the next sibling's index.
        private static int WriteNode(BinaryWriter w, IReadOnlyList<TypeNode> tree, int i, object? value)
        {
            var node = tree[i];
            int end = i + 1;
            while (end < tree.Count && tree[end].Depth > node.Depth) end++;
            if (node.Type == "string") WriteBytes(w, Encoding.UTF8.GetBytes(value as string ?? ""));
            else if (end - i > 1 && tree[i + 1].IsArray)
            {
                if (value is byte[] bytes) WriteBytes(w, bytes);
                else if (value is string[] strings)
                {
                    w.Write(strings.Length);
                    foreach (var s in strings) WriteBytes(w, Encoding.UTF8.GetBytes(s));
                }
                else w.Write(0);
            }
            else if (end - i == 1) w.Write(new byte[Math.Max(0, node.ByteSize)]);
            else for (int k = i + 1; k < end; ) k = WriteNode(w, tree, k, null);
            if (node.AlignsAfter || (end - i > 1 && tree[i + 1].AlignsAfter)) Pad(w);
            return end;
        }

        private static void WriteBytes(BinaryWriter w, byte[] bytes)
        {
            w.Write(bytes.Length);
            w.Write(bytes);
            Pad(w);
        }

        private static void Pad(BinaryWriter w)
        {
            while (w.BaseStream.Length % 4 != 0) w.Write((byte)0);
        }
    }
}
