using System;
using System.Collections.Generic;
using System.IO;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Which installed font file carries a font name — read from the NAME TABLE inside each file,
    /// the one place a font says what it is called.
    ///
    /// 🔴 **Never guessed from the file's name.** "Segoe UI Historic" is seguihis.ttf, "Microsoft
    /// YaHei" msyh.ttc, "Leelawadee UI" LeelawUI.ttf: a file name derived from the font name (and a
    /// loose comparison of names) found none of them, so a translation naming such a font got no
    /// copy of it and its text was drawn with nothing (2026-10-02, a Syriac line). Only the
    /// collections (.ttc) were read by their name table then; every format is now.
    ///
    /// 🔴 **One rule for the mod that USES the font and the Manager that EXPORTS it** — the same
    /// file, or the pack carries a font the game never showed (<see cref="SystemFontNames"/>).
    ///
    /// The folders stay with each product (they depend on the machine); this reads only the files'
    /// directories and name tables — a system's font folder holds a thousand files, searched while a
    /// game starts.
    /// </summary>
    public static class FontFileNames
    {
        private const uint Ttcf = 0x74746366;   // 'ttcf'

        /// <summary>The worst <see cref="FindFace"/> rank: a typographic family only.</summary>
        public const int WorstRank = 3;

        private static readonly string[] SingleExtensions = { ".ttf", ".otf" };

        /// <summary>
        /// The face of a font file — a single font (face 0) or a collection — that best carries
        /// <paramref name="name"/>, -1 when none, with how well (<paramref name="rank"/>, 0 best):
        /// 0 — its full name (id 4) IS the name ("Yu Gothic UI Semibold");
        /// 1 — its family (id 1) is, and it is the Regular of that family;
        /// 2 — its family is, in another style: a family's Bold carries the same family name, and its
        ///     file may be listed before the Regular's ("Yu Gothic" gave the Bold, 2026-10-01);
        /// 3 — only its typographic family (id 16), shared by every weight ("Nirmala UI" names the
        ///     Semilight face too).
        /// </summary>
        public static int FindFace(Stream file, string name, out int rank)
        {
            rank = int.MaxValue;
            if (file == null || string.IsNullOrEmpty(name)) return -1;
            var header = ReadAt(file, 0, 12);
            if (header == null) return -1;

            long[] directories;
            if (ReadUInt32(header, 0) == Ttcf)
            {
                int count = (int)ReadUInt32(header, 8);
                var offsets = count > 0 && count < 4096 ? ReadAt(file, 12, count * 4) : null;
                if (offsets == null) return -1;
                directories = new long[count];
                for (int i = 0; i < count; i++) directories[i] = ReadUInt32(offsets, i * 4);
            }
            else directories = new long[] { 0 };   // a single font: its directory starts the file

            int best = -1;
            for (int i = 0; i < directories.Length && rank > 0; i++)
            {
                var table = NameTable(file, directories[i]);
                if (table == null) continue;
                int r = Has(table, 4, name) ? 0
                      : Has(table, 1, name) ? (IsRegular(table) ? 1 : 2)
                      : Has(table, 16, name) ? WorstRank
                      : int.MaxValue;
                if (r < rank) { rank = r; best = i; }
            }
            return best;
        }

        /// <summary>Whether the file is a collection (.ttc): several faces, each taken out to be used alone.</summary>
        public static bool IsCollection(Stream file)
        {
            var header = file == null ? null : ReadAt(file, 0, 4);
            return header != null && ReadUInt32(header, 0) == Ttcf;
        }

        /// <summary>
        /// The installed file for a font name, and its face — null when none.
        ///
        /// 1. A file the name NAMES: a translation may refer to a font by its file ("comicbd"), and the
        ///    names <see cref="SystemFontNames.Candidates"/> derives are tried as .ttf/.otf files first.
        /// 2. Otherwise the file whose name table carries the name, the best rank of ALL the folders'
        ///    files — never the first one listed (a family's Bold comes before its Regular).
        /// </summary>
        /// <param name="collections">Whether a face inside a collection may answer: only for a caller
        /// that takes the face out of it — a file carried as it is must be a single font.</param>
        public static string? FindFile(string name, IEnumerable<string> folders, bool collections, out int face)
        {
            face = -1;
            if (string.IsNullOrEmpty(name) || folders == null) return null;
            var dirs = new List<string>();
            foreach (var dir in folders) if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir)) dirs.Add(dir);

            var candidates = SystemFontNames.Candidates(name);
            foreach (var dir in dirs)
                foreach (var candidate in candidates)
                    foreach (var extension in SingleExtensions)
                    {
                        string path = Path.Combine(dir, candidate + extension);
                        if (File.Exists(path)) { face = 0; return path; }
                    }

            string? bestFile = null;
            int bestFace = -1, bestRank = int.MaxValue;
            foreach (var dir in dirs)
            {
                IEnumerable<string> files;
                try { files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories); }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { continue; }
                foreach (var file in files)
                {
                    string extension = Path.GetExtension(file);
                    bool single = Array.Exists(SingleExtensions, x => string.Equals(x, extension, StringComparison.OrdinalIgnoreCase));
                    if (!single && !(collections && string.Equals(extension, ".ttc", StringComparison.OrdinalIgnoreCase))) continue;
                    try
                    {
                        using (var stream = File.OpenRead(file))
                        {
                            int found = FindFace(stream, name, out int rank);
                            if (found < 0 || rank >= bestRank) continue;
                            bestFile = file; bestFace = found; bestRank = rank;
                        }
                    }
                    // A file we may not open is a font we cannot carry; the search goes on.
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
                    if (bestRank == 0) break;
                }
                if (bestRank == 0) break;
            }
            face = bestFace;
            return bestFile;
        }

        private static bool Has(byte[] table, int id, string name) =>
            NamesOf(table, id).Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Its subfamily (id 2) is the plain style, in the words fonts use for it.</summary>
        private static bool IsRegular(byte[] table) =>
            NamesOf(table, 2).Exists(n => n.Equals("Regular", StringComparison.OrdinalIgnoreCase)
                                       || n.Equals("Normal", StringComparison.OrdinalIgnoreCase)
                                       || n.Equals("Book", StringComparison.OrdinalIgnoreCase)
                                       || n.Equals("Roman", StringComparison.OrdinalIgnoreCase));

        private static byte[]? NameTable(Stream file, long dir)
        {
            var sfnt = ReadAt(file, dir, 12);
            if (sfnt == null) return null;
            int numTables = ReadUInt16(sfnt, 4);
            var records = ReadAt(file, dir + 12, numTables * 16);
            if (records == null) return null;
            for (int t = 0; t < numTables; t++)
            {
                int r = t * 16;
                if (records[r] == 'n' && records[r + 1] == 'a' && records[r + 2] == 'm' && records[r + 3] == 'e')
                    return ReadAt(file, ReadUInt32(records, r + 8), (int)ReadUInt32(records, r + 12));
            }
            return null;
        }

        /// <summary>The strings a name table holds under <paramref name="id"/>, every platform.</summary>
        private static List<string> NamesOf(byte[] table, int id)
        {
            var names = new List<string>();
            if (table.Length < 6) return names;
            int count = ReadUInt16(table, 2), strings = ReadUInt16(table, 4);
            for (int i = 0; i < count; i++)
            {
                int r = 6 + i * 12;
                if (r + 12 > table.Length) break;
                int platform = ReadUInt16(table, r), nameId = ReadUInt16(table, r + 6);
                int length = ReadUInt16(table, r + 8), at = strings + ReadUInt16(table, r + 10);
                if (nameId != id || at + length > table.Length) continue;
                string? n = platform == 3 || platform == 0 ? System.Text.Encoding.BigEndianUnicode.GetString(table, at, length)
                          : platform == 1 ? System.Text.Encoding.ASCII.GetString(table, at, length)
                          : null;
                if (!string.IsNullOrEmpty(n) && !names.Contains(n!)) names.Add(n!);
            }
            return names;
        }

        private static byte[]? ReadAt(Stream s, long offset, int length)
        {
            if (length < 0 || offset < 0 || offset + length > s.Length) return null;
            s.Position = offset;
            var b = new byte[length];
            int read = 0;
            while (read < length)
            {
                int n = s.Read(b, read, length - read);
                if (n <= 0) return null;
                read += n;
            }
            return b;
        }

        private static uint ReadUInt32(byte[] b, int o) => (uint)(b[o] << 24 | b[o + 1] << 16 | b[o + 2] << 8 | b[o + 3]);
        private static int ReadUInt16(byte[] b, int o) => b[o] << 8 | b[o + 1];
    }
}
