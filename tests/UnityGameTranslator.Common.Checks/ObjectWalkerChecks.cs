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

        private sealed class WantsBytes : IObjectVisitor, IObjectBytesVisitor
        {
            public readonly List<string> Lines = new List<string>();
            public void Value(in FieldSite s, object value) => Lines.Add($"{s.Path} = {value}");
            public void Pointer(in FieldSite s, int fileId, long pathId) => Lines.Add($"{s.Path} -> {fileId}/{pathId}");
            public void Skipped(in FieldSite s, long length) => Lines.Add($"{s.Path} skipped {length}");
            public bool Wants(in FieldSite s, int length) => true;
            public void Bytes(in FieldSite s, byte[] bytes) => Lines.Add($"{s.Path} bytes {bytes.Length}");
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

            // a visitor that asks for the bytes of a one-byte array gets them, and the walk goes on after them as after a skip
            var wanting = new WantsBytes();
            Walk(tree, ProbeBytes(), wanting);
            check(wanting.Lines.Contains("blob.Array bytes 5") && !wanting.Lines.Any(l => l.Contains("skipped")) && wanting.Lines.Last() == "m_Target -> 1/4242",
                "a visitor that wants a byte array's bytes receives them, and the fields after it are still read", string.Join(" | ", wanting.Lines));

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

            // A tree that is only the START of the layout (a MonoBehaviour's header): read as far as it goes.
            var head = new List<TypeNode> { new TypeNode(0, "Probe", "Base", -1, 0, 0x8000), Int(1, "m_Count") };
            head.AddRange(Str(1, "m_Title"));
            var headSeen = new Seen();
            var reader = new UnityFileReader(new MemoryStream(whole), bigEndian: false);
            new ObjectWalker(head).Walk(reader, 0, whole.Length, headSeen, "Probe", wholeObject: false);
            check(headSeen.Lines.SequenceEqual(new[] { "m_Count [Probe.m_Count] = 7", "m_Title [Probe.m_Title] = ab" })
                  && Refuses(() => Walk(head, whole, new Seen()), "does not account for"),
                "a tree that is only the start of the layout is read as far as it goes, and only when asked", "the header of a MonoBehaviour, before the fields its class declares");
            check(Refuses(() => new ObjectWalker(head).Walk(new UnityFileReader(new MemoryStream(whole), bigEndian: false), 0, 6, new Seen(), "Probe", wholeObject: false), "reads past"),
                "even then a header that does not fit the object is refused", "the size is still the limit");

            // A class with no field to serialize holds no byte: it is read as nothing, not refused.
            var empty = new List<TypeNode>
            {
                new TypeNode(0, "Probe", "Base", -1, 0, 0x8000), Int(1, "m_Count"),
                new TypeNode(1, "Marker", "m_Marker", -1, 0, 0x0), Int(1, "m_After"),
            };
            var keptEmpty = new List<TypeNode>(empty);
            keptEmpty[2] = new TypeNode(1, "Marker", "m_Marker", 0, 0, 0x0);
            var keptSeen = new Seen();
            Walk(keptEmpty, new byte[] { 5, 0, 0, 0, 6, 0, 0, 0 }, keptSeen);
            check(keptSeen.Lines.Count == 3, "the same, as a type tree a file kept writes it (size 0)", string.Join(" | ", keptSeen.Lines));
            var emptySeen = new Seen();
            Walk(empty, new byte[] { 5, 0, 0, 0, 6, 0, 0, 0 }, emptySeen);
            check(emptySeen.Lines.SequenceEqual(new[] { "m_Count [Probe.m_Count] = 5", "m_Marker skipped 0", "m_After [Probe.m_After] = 6" }),
                "a class with no field to serialize holds no byte", string.Join(" | ", emptySeen.Lines));
            var unknown = new List<TypeNode> { new TypeNode(0, "Probe", "Base", -1, 0, 0x8000), new TypeNode(1, "Mystery", "m_X", -2, 0, 0x0) };
            check(Refuses(() => Walk(unknown, new byte[4], new Seen()), "has no size"),
                "a type that names a size this reader cannot decode is still refused", "only a class with no fields is nothing");

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

            var other = (byte[])bytes.Clone();
            BitConverter.GetBytes(3).CopyTo(other, 4);
            check(Walk(tree, other, new Seen()).RegistryNotRead is string why && why.Contains("version 3"),
                "a registry in a version this reader does not know is said unread", "the layout of its entries is not known");

            // Version 1: no count, no ids — entries one after the other, the first is 0, a "Terminus" ends the list.
            var ms1 = new MemoryStream();
            var w1 = new BinaryWriter(ms1);
            w1.Write(9);
            w1.Write(1);
            Text(w1, "Foo"); Text(w1, "Game.Ns"); Text(w1, "Assembly-CSharp"); Text(w1, "first");
            Text(w1, "Foo"); Text(w1, "Game.Ns"); Text(w1, "Assembly-CSharp"); Text(w1, "second");
            Text(w1, "Terminus"); Text(w1, "UnityEngine.DMAT"); Text(w1, "FAKE_ASM");
            var v1 = new Seen();
            var read1 = Walk(tree, ms1.ToArray(), v1, (cls, ns, asm) => cls == "Foo" ? foo : null);
            check(read1.RegistryNotRead == null && v1.Lines.SequenceEqual(new[]
                {
                    "m_Id [Probe.m_Id] = 9",
                    "references[0]<Foo>.label [Foo.label] = first",
                    "references[1]<Foo>.label [Foo.label] = second",
                }),
                "a registry in version 1 is read: ids are positions from 0, the Terminus entry ends it", string.Join(" | ", v1.Lines));

            var unterminated = ms1.ToArray();
            Array.Resize(ref unterminated, unterminated.Length - 40);
            bool refused = false;
            try { Walk(tree, unterminated, new Seen(), (cls, ns, asm) => cls == "Foo" ? foo : null); }
            catch (UnityFileFormatException) { refused = true; }
            check(refused, "a version 1 registry that does not end is refused, not read past the object", refused ? "refused" : "read on");
        }
    }
}
