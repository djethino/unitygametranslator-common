using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>What a stream starts like, as <see cref="SerializedFile.Probe"/> says it.</summary>
    public enum SerializedProbe
    {
        /// <summary>Not a serialized file: nothing to say about it.</summary>
        NotSerialized,

        /// <summary>A serialized file whose size is the size it declares.</summary>
        Serialized,

        /// <summary>The header of a serialized file, in a file of another size: cut short, or not the file it was.</summary>
        SizeDisagrees,
    }

    /// <summary>
    /// A Unity serialized file (.assets, sharedassets, level files, globalgamemanagers, a bundle's
    /// CAB-…): its header, its types (with their type tree when the build kept them) and its table of
    /// objects. Formats 9 to 22 and later (Unity 3.5 onwards); an object is read from the stream when
    /// asked, never the whole file.
    /// </summary>
    public sealed class SerializedFile
    {
        private readonly UnityFileReader _reader;
        private readonly List<SerializedType> _types = new List<SerializedType>();
        private readonly List<ObjectInfo> _objects = new List<ObjectInfo>();

        public sealed class SerializedType
        {
            internal SerializedType(int classId, IReadOnlyList<TypeNode>? tree) { ClassId = classId; Tree = tree; }
            public int ClassId { get; }
            /// <summary>The type tree kept in the file; null when stripped or not read (see <see cref="TreeRefused"/>).</summary>
            public IReadOnlyList<TypeNode>? Tree { get; }
        }

        public readonly struct ObjectInfo
        {
            internal ObjectInfo(long pathId, long start, long size, int classId, int typeIndex)
            { PathId = pathId; Start = start; Size = size; ClassId = classId; TypeIndex = typeIndex; }
            public long PathId { get; }
            public long Start { get; }
            public long Size { get; }
            public int ClassId { get; }
            internal int TypeIndex { get; }
        }

        public int Format { get; private set; }

        /// <summary>The byte order of the file's values — its objects' too (the header says it once).</summary>
        public bool BigEndian { get; private set; }
        /// <summary>The engine version the file says ("2021.3.27f1"); empty or "0.0.0" when it says none.</summary>
        public string EngineVersion { get; private set; } = "";
        public bool TypeTreesKept { get; private set; }
        /// <summary>Why a kept type tree was not used (a string this table does not know), or null.</summary>
        public string? TreeRefused { get; private set; }
        public IReadOnlyList<ObjectInfo> Objects => _objects;

        /// <summary>One file this one points into: a reference (PPtr) whose file id is N + 1 names entry N.</summary>
        public sealed class ExternalFile
        {
            internal ExternalFile(string pathName, string guid, int type) { PathName = pathName; Guid = guid; Type = type; }

            /// <summary>The path Unity wrote ("archive:/CAB-…/CAB-…", "globalgamemanagers.assets", "Library/unity default resources").</summary>
            public string PathName { get; }

            /// <summary>The 16 bytes written beside it, as 32 hexadecimal digits.</summary>
            public string Guid { get; }

            /// <summary>Unity's kind of external (0 for an asset file of the player).</summary>
            public int Type { get; }
        }

        /// <summary>
        /// The files this one points into, in the order the file lists them. A reference's file id 0 is
        /// THIS file; id N is entry N − 1 here. Read the first time it is asked, from where the object
        /// table ended — a caller that never asks (the fonts) is not made to depend on it — so the
        /// stream must still be open then. <see cref="UnityFileFormatException"/> when the table does not read.
        /// </summary>
        public IReadOnlyList<ExternalFile> Externals => _externals ?? (_externals = ReadExternals());

        private long _afterObjects;
        private List<ExternalFile>? _externals;

        private SerializedFile(Stream stream) { _reader = new UnityFileReader(stream, bigEndian: true); }

        /// <summary>
        /// Whether the stream is a serialized file: <see cref="Probe"/> says
        /// <see cref="SerializedProbe.Serialized"/>. A header that is valid but whose size is wrong
        /// answers false here — a caller that must tell it from "not one at all" asks <see cref="Probe"/>.
        /// </summary>
        public static bool LooksLikeOne(Stream stream) => Probe(stream, out _) == SerializedProbe.Serialized;

        /// <summary>
        /// What the stream starts like, said in three answers instead of a yes and a no: a game's
        /// folder holds many other files (nothing to say), serialized files, and — the case a boolean
        /// hid — a file whose header IS a serialized file's but that is not the size it says it is
        /// (cut short by a failed copy, a half-finished update). The last one is a file somebody must
        /// be told about, not one to skip silently.
        ///
        /// Read without throwing; the stream is left where it was. <paramref name="detail"/> says what
        /// disagrees, for the third answer only.
        /// </summary>
        public static SerializedProbe Probe(Stream stream, out string? detail)
        {
            detail = null;
            long at = stream.Position;
            try
            {
                if (stream.Length < 32) return SerializedProbe.NotSerialized;
                var head = new byte[48];
                int n = 0;
                stream.Position = 0;
                while (n < head.Length) { int got = stream.Read(head, n, head.Length - n); if (got <= 0) break; n += got; }
                uint format = BigU32(head, 8);
                if (format < 9 || format > 64) return SerializedProbe.NotSerialized;
                long fileSize, dataOffset, metadata;
                if (format >= 22)
                {
                    if (n < 48) return SerializedProbe.NotSerialized;
                    metadata = BigU32(head, 20);
                    fileSize = (long)BigU64(head, 24);
                    dataOffset = (long)BigU64(head, 32);
                }
                else
                {
                    metadata = BigU32(head, 0);
                    fileSize = BigU32(head, 4);
                    dataOffset = BigU32(head, 12);
                }

                // A header that holds together under the size IT declares: what the file would be if whole.
                bool holdsTogether = fileSize > 0 && dataOffset <= fileSize && metadata > 0 && metadata < fileSize && head[16] <= 1;
                if (!holdsTogether) return SerializedProbe.NotSerialized;
                if (fileSize == stream.Length) return SerializedProbe.Serialized;

                detail = $"the header of a serialized file (format {format}) says {fileSize} byte(s), the file has {stream.Length}";
                return SerializedProbe.SizeDisagrees;
            }
            finally { stream.Position = at; }
        }

        private static uint BigU32(byte[] b, int at) => (uint)(b[at] << 24 | b[at + 1] << 16 | b[at + 2] << 8 | b[at + 3]);
        private static ulong BigU64(byte[] b, int at) => ((ulong)BigU32(b, at) << 32) | BigU32(b, at + 4);

        public static SerializedFile Open(Stream stream)
        {
            var file = new SerializedFile(stream);
            file.ReadMetadata();
            return file;
        }

        private void ReadMetadata()
        {
            var r = _reader;
            r.Position = 0;
            r.U32();
            uint fileSize32 = r.U32();
            Format = (int)r.U32();
            uint dataOffset32 = r.U32();
            if (Format < 9) throw new UnityFileFormatException($"serialized file format {Format}: older than this reader knows (9)");
            bool bigEndian = r.U8() != 0;
            r.Skip(3);
            long dataOffset = dataOffset32;
            if (Format >= 22)
            {
                r.U32();
                r.I64();
                dataOffset = r.I64();
                r.I64();
            }
            r.BigEndian = bigEndian;
            BigEndian = bigEndian;

            EngineVersion = Format >= 7 ? r.Zstring(64) : "";
            if (Format >= 8) r.I32();   // target platform
            TypeTreesKept = Format < 13 || r.Bool();

            int typeCount = r.Count(4);
            for (int i = 0; i < typeCount; i++) _types.Add(ReadType(isReference: false));

            bool bigIds = Format >= 7 && Format < 14 && r.I32() != 0;
            int objectCount = r.Count(12);
            for (int i = 0; i < objectCount; i++)
            {
                long pathId;
                if (Format >= 14) { r.Align(); pathId = r.I64(); }
                else pathId = bigIds ? r.I64() : r.I32();
                long start = (Format >= 22 ? r.I64() : r.U32()) + dataOffset;
                long size = r.U32();
                int typeId = r.I32();
                int classId;
                int typeIndex;
                if (Format < 16)
                {
                    classId = r.U16();
                    typeIndex = _types.FindIndex(t => t.ClassId == typeId);
                }
                else
                {
                    if (typeId < 0 || typeId >= _types.Count) throw new UnityFileFormatException($"object {pathId} names type {typeId} of {_types.Count}");
                    classId = _types[typeId].ClassId;
                    typeIndex = typeId;
                }
                if (Format < 11) r.U16();
                if (Format >= 11 && Format < 17) r.I16();
                if (Format == 15 || Format == 16) r.U8();
                if (start < 0 || size < 0 || start + size > r.Length)
                    throw new UnityFileFormatException($"object {pathId} lies outside the file");
                _objects.Add(new ObjectInfo(pathId, start, size, classId, typeIndex));
            }
            // What follows (script types, externals, reference types, user information) is not needed
            // to read an object; the externals are read when somebody asks (Externals).
            _afterObjects = r.Position;
        }

        private List<ExternalFile> ReadExternals()
        {
            var r = _reader;
            long at = r.Position;
            try
            {
                r.Position = _afterObjects;

                // Script types: (file index, id) per script, from format 11. Only passed over here.
                if (Format >= 11)
                {
                    int scripts = r.Count(8);
                    for (int i = 0; i < scripts; i++)
                    {
                        r.I32();
                        if (Format >= 14) { r.Align(); r.I64(); }
                        else r.I32();
                    }
                }

                int count = r.Count(1);
                var list = new List<ExternalFile>(count);
                for (int i = 0; i < count; i++)
                {
                    r.Zstring(256);                       // an empty string, written by the editor
                    var guid = r.Bytes(16);
                    int type = r.I32();
                    string path = r.Zstring(1024);
                    var hex = new StringBuilder(32);
                    foreach (byte b in guid) hex.Append(b.ToString("x2"));
                    list.Add(new ExternalFile(path, hex.ToString(), type));
                }
                return list;
            }
            finally { r.Position = at; }
        }

        private SerializedType ReadType(bool isReference)
        {
            var r = _reader;
            int classId = r.I32();
            if (Format >= 16) r.Bool();                   // stripped
            short scriptIndex = Format >= 17 ? r.I16() : (short)-1;
            if (Format >= 13)
            {
                if ((isReference && scriptIndex >= 0) || (Format < 16 && classId < 0) || (Format >= 16 && classId == 114))
                    r.Skip(16);                           // script id
                r.Skip(16);                               // type hash
            }
            IReadOnlyList<TypeNode>? tree = null;
            if (TypeTreesKept)
            {
                if (Format < 12 && Format != 10)
                    throw new UnityFileFormatException($"type trees in the format of serialized file {Format}: not read");
                tree = ReadTypeTree();
                if (Format >= 21)
                {
                    if (isReference) { r.Zstring(); r.Zstring(); r.Zstring(); }
                    else { int n = r.Count(4); r.Skip(4L * n); }
                }
            }
            return new SerializedType(classId, tree);
        }

        private IReadOnlyList<TypeNode>? ReadTypeTree()
        {
            var r = _reader;
            int nodeCount = r.Count(24);
            int stringSize = r.Count();
            var raw = new (int depth, byte flags, uint type, uint name, int size, uint meta)[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                r.U16();                                  // the field's own version
                int depth = r.U8();
                byte flags = r.U8();
                uint type = r.U32(), name = r.U32();
                int size = r.I32();
                r.I32();                                  // index
                uint meta = r.U32();
                if (Format >= 19) r.U64();                // reference type hash
                raw[i] = (depth, flags, type, name, size, meta);
            }
            var local = r.Bytes(stringSize);
            var nodes = new TypeNode[nodeCount];
            for (int i = 0; i < nodeCount; i++)
            {
                string? type = Name(local, raw[i].type), name = Name(local, raw[i].name);
                if (type == null || name == null)
                {
                    // A common string newer than the generated table: this tree is not used, the
                    // generated layout is (said by the caller through TreeRefused).
                    TreeRefused = $"a type tree names common string {(type == null ? raw[i].type : raw[i].name) & 0x7FFFFFFF}, unknown to this table";
                    return null;
                }
                nodes[i] = new TypeNode(raw[i].depth, type, name, raw[i].size, raw[i].flags, raw[i].meta);
            }
            return nodes;
        }

        private static string? Name(byte[] local, uint offset)
        {
            if ((offset & 0x80000000) != 0) return FontLayouts.CommonString(offset & 0x7FFFFFFF);
            if (offset >= local.Length) return null;
            int end = (int)offset;
            while (end < local.Length && local[end] != 0) end++;
            return System.Text.Encoding.UTF8.GetString(local, (int)offset, end - (int)offset);
        }

        /// <summary>The type tree the file kept for an object, or null.</summary>
        public IReadOnlyList<TypeNode>? KeptTreeOf(ObjectInfo info) =>
            info.TypeIndex >= 0 && info.TypeIndex < _types.Count ? _types[info.TypeIndex].Tree : null;

        /// <summary>
        /// Reads the named top-level fields of an object with <paramref name="tree"/>; see
        /// <see cref="TypeTreeWalker.Fields"/>.
        /// </summary>
        public Dictionary<string, object?> Fields(ObjectInfo info, IReadOnlyList<TypeNode> tree, ICollection<string> wanted,
                                                  ICollection<string>? measured = null) =>
            Fields(info, tree, wanted, measured, null, out _);

        /// <summary>
        /// As above, with the string fields named in <paramref name="raw"/> given as bytes (see
        /// <see cref="TypeTreeWalker.Fields(ICollection{string}, ICollection{string}, ICollection{string})"/>)
        /// and <paramref name="end"/>, where the last field read ended: a caller whose tree is the
        /// whole of the layout can check the object ends there.
        /// </summary>
        public Dictionary<string, object?> Fields(ObjectInfo info, IReadOnlyList<TypeNode> tree, ICollection<string> wanted,
                                                  ICollection<string>? measured, ICollection<string>? raw, out long end)
        {
            _reader.Position = info.Start;
            var walker = new TypeTreeWalker(_reader, tree);
            var fields = walker.Fields(wanted, measured ?? Array.Empty<string>(), raw ?? Array.Empty<string>());
            end = walker.Position;
            if (end > info.Start + info.Size)
                throw new UnityFileFormatException($"object {info.PathId} read past its {info.Size} byte(s): not the layout it was written with");
            return fields;
        }
    }
}
