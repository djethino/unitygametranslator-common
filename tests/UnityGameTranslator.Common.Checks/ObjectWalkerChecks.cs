using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityGameTranslator.Common.UnityFiles;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Reading an object's values along a type tree (ObjectWalker), on bytes built here by hand: every
    /// kind of value, the path and owner it is given, the alignments, a list of classes, a list of
    /// strings, a reference, the registry of [SerializeReference] objects with and without a resolver —
    /// and above all that an object whose bytes are not its layout is REFUSED, never read in part.
    /// Called from <see cref="UnityFilesChecks.Run"/>.
    /// </summary>
    internal static class ObjectWalkerChecks
    {
        private sealed class Seen : IObjectVisitor
        {
            public readonly List<string> Lines = new List<string>();
            public void Value(in FieldSite s, object value) => Lines.Add($"{s.Path} [{s.Owner}.{s.Field}] = {value}");
            public void Pointer(in FieldSite s, int fileId, long pathId) => Lines.Add($"{s.Path} -> {fileId}/{pathId}");
            public void Skipped(in FieldSite s, long length) => Lines.Add($"{s.Path} skipped {length}");
        }

        // ---- nodes
        private static IEnumerable<TypeNode> Str(int d, string name) => new[]
        {
            new TypeNode(d, "string", name, -1, 0, 0x8001),
            new TypeNode(d + 1, "Array", "Array", -1, 1, 0x4001),
            new TypeNode(d + 2, "SInt32", "size", 4, 0, 0x1),
            new TypeNode(d + 2, "char", "data", 1, 0, 0x1),
        };

        private static TypeNode Int(int d, string name) => new TypeNode(d, "SInt32", name, 4, 0, 0x0);

        private static IReadOnlyList<TypeNode> ProbeTree()
        {
            var t = new List<TypeNode>
            {
                new TypeNode(0, "Probe", "Base", -1, 0, 0x8000),
                Int(1, "m_Count"),
            };
            t.AddRange(Str(1, "m_Title"));
            t.Add(new TypeNode(1, "UInt8", "m_Flag", 1, 0, 0x4000));
            // a list of classes: vector -> Array -> (size, data: Item { string name; int level })
            t.Add(new TypeNode(1, "vector", "items", -1, 0, 0x0));
            t.Add(new TypeNode(2, "Array", "Array", -1, 1, 0x1));
            t.Add(new TypeNode(3, "SInt32", "size", 4, 0, 0x1));
            t.Add(new TypeNode(3, "Item", "data", -1, 0, 0x1));
            t.AddRange(Str(4, "name"));
            t.Add(Int(4, "level"));
            // a list of strings
            t.Add(new TypeNode(1, "vector", "names", -1, 0, 0x0));
            t.Add(new TypeNode(2, "Array", "Array", -1, 1, 0x1));
            t.Add(new TypeNode(3, "SInt32", "size", 4, 0, 0x1));
            t.AddRange(Str(3, "data"));
            // bytes
            t.Add(new TypeNode(1, "vector", "blob", -1, 0, 0x0));
            t.Add(new TypeNode(2, "Array", "Array", -1, 1, 0x4001));
            t.Add(new TypeNode(3, "SInt32", "size", 4, 0, 0x1));
            t.Add(new TypeNode(3, "UInt8", "data", 1, 0, 0x1));
            // a reference
            t.Add(new TypeNode(1, "PPtr<Object>", "m_Target", 12, 0, 0x0));
            t.Add(new TypeNode(2, "SInt32", "m_FileID", 4, 0, 0x0));
            t.Add(new TypeNode(2, "SInt64", "m_PathID", 8, 0, 0x0));
            return t;
        }

        // ---- bytes
        private static void Pad(BinaryWriter w) { while (w.BaseStream.Length % 4 != 0) w.Write((byte)0); }

        private static void Text(BinaryWriter w, string s)
        {
            var b = Encoding.UTF8.GetBytes(s);
            w.Write(b.Length);
            w.Write(b);
            Pad(w);
        }

        private static byte[] ProbeBytes(bool extra = false)
        {
            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(7);                       // m_Count
            Text(w, "ab");                    // m_Title: 2 bytes, padded to four
            w.Write((byte)1); Pad(w);         // m_Flag, aligned
            w.Write(2);                       // items: two
            Text(w, "Sword"); w.Write(3);
            Text(w, "Shield"); w.Write(5);
            w.Write(2);                       // names: two
            Text(w, "one"); Text(w, "deux");
            w.Write(5); w.Write(new byte[] { 1, 2, 3, 4, 5 }); Pad(w);   // blob, aligned
            w.Write(1); w.Write(4242L);       // m_Target
            if (extra) w.Write(0xDEADBEEF);
            return ms.ToArray();
        }

        private static ObjectWalk Walk(IReadOnlyList<TypeNode> tree, byte[] bytes, IObjectVisitor v, ReferenceResolver? resolver = null, long? endOverride = null)
        {
            var reader = new UnityFileReader(new MemoryStream(bytes), bigEndian: false);
            return new ObjectWalker(tree, resolver).Walk(reader, 0, endOverride ?? bytes.Length, v, "Probe");
        }

        private static bool Refuses(Action walk, string containing)
        {
            try { walk(); return false; }
            catch (UnityFileFormatException ex) { return ex.Message.Contains(containing); }
        }

        public static void Run(Action<bool, string, string> check)
        {
            var tree = ProbeTree();
            var seen = new Seen();
            var done = Walk(tree, ProbeBytes(), seen);

            check(done.RegistryNotRead == null, "a whole object is read whole", "nothing said to be left unread");
            check(seen.Lines.SequenceEqual(new[]
                {
                    "m_Count [Probe.m_Count] = 7",
                    "m_Title [Probe.m_Title] = ab",
                    "m_Flag [Probe.m_Flag] = 1",
                    "items.Array[0].name [Item.name] = Sword",
                    "items.Array[0].level [Item.level] = 3",
                    "items.Array[1].name [Item.name] = Shield",
                    "items.Array[1].level [Item.level] = 5",
                    "names.Array[0] [Probe.names] = one",
                    "names.Array[1] [Probe.names] = deux",
                    "blob.Array skipped 5",
                    "m_Target -> 1/4242",
                }),
                "every value comes with its path, its class and its field",
                "a list keeps its field's name, a nested class's fields are its own: " + string.Join(" | ", seen.Lines));

            check(Refuses(() => Walk(tree, ProbeBytes(extra: true), new Seen()), "do(es) not account for") || Refuses(() => Walk(tree, ProbeBytes(extra: true), new Seen()), "does not account for"),
                "bytes left once the tree is walked refuse the object", "another layout, never read in part");
            var whole = ProbeBytes();
            check(Refuses(() => Walk(tree, whole, new Seen(), endOverride: whole.Length - 4), "reads past"),
                "an object that reads past its end is refused", "the size the file gives it is the layout's limit");
            check(Refuses(() => Walk(tree, whole.Take(whole.Length - 6).ToArray(), new Seen()), ""),
                "a truncated object is refused", "a count or a read that does not fit says so");
            var bigCount = (byte[])whole.Clone();
            // items' count sits after m_Count (4), m_Title (8), m_Flag (4): offset 16
            BitConverter.GetBytes(1000000).CopyTo(bigCount, 16);
            check(Refuses(() => Walk(tree, bigCount, new Seen()), "does not fit"),
                "a count larger than the bytes left is refused", "never a million elements out of a few bytes");

            RegistryChecks(check);
        }

        private static void RegistryChecks(Action<bool, string, string> check)
        {
            var tree = new List<TypeNode>
            {
                new TypeNode(0, "Holder", "Base", -1, 0, 0x8000),
                Int(1, "m_Id"),
                new TypeNode(1, "ManagedReferencesRegistry", "references", -1, 0, 0x0),
            };
            var foo = new List<TypeNode> { new TypeNode(0, "Foo", "Base", -1, 0, 0x0) };
            foo.AddRange(Str(1, "label"));

            var ms = new MemoryStream();
            var w = new BinaryWriter(ms);
            w.Write(9);                                  // m_Id
            w.Write(2);                                  // registry version
            w.Write(3);                                  // three entries
            w.Write(1L); Text(w, "Foo"); Text(w, "Game.Ns"); Text(w, "Assembly-CSharp"); Text(w, "hello");   // data: label
            w.Write(-1L); Text(w, ""); Text(w, ""); Text(w, "");                                            // no type: no data
            w.Write(2L); Text(w, "Foo"); Text(w, "Game.Ns"); Text(w, "Assembly-CSharp"); Text(w, "again");
            var bytes = ms.ToArray();

            var withResolver = new Seen();
            var read = Walk(tree, bytes, withResolver, (cls, ns, asm) => cls == "Foo" ? foo : null);
            check(read.RegistryNotRead == null && withResolver.Lines.SequenceEqual(new[]
                {
                    "m_Id [Probe.m_Id] = 9",
                    "references[1]<Foo>.label [Foo.label] = hello",
                    "references[2]<Foo>.label [Foo.label] = again",
                }),
                "the referenced objects are read with the class the resolver gives", string.Join(" | ", withResolver.Lines));

            var without = new Seen();
            var unread = Walk(tree, bytes, without);
            check(unread.RegistryNotRead != null && unread.RegistryNotRead.Contains("Game.Ns.Foo") && without.Lines.SequenceEqual(new[] { "m_Id [Probe.m_Id] = 9" }),
                "without a resolver the registry is SAID unread, with the first class it names", unread.RegistryNotRead ?? "silent");

            var unknown = new Seen();
            var notKnown = Walk(tree, bytes, unknown, (cls, ns, asm) => null);
            check(notKnown.RegistryNotRead != null && notKnown.RegistryNotRead.Contains("not known"),
                "a class the resolver does not know says so, and the object is not taken for whole", notKnown.RegistryNotRead ?? "silent");

            var old = (byte[])bytes.Clone();
            BitConverter.GetBytes(1).CopyTo(old, 4);
            check(Walk(tree, old, new Seen()).RegistryNotRead is string why && why.Contains("version 1"),
                "a registry in another version is said unread", "the layout of its entries is not known");
        }
    }
}
