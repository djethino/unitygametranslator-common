using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// Where a value was met in an object: its path, its node, and the class and field it belongs to.
    /// </summary>
    /// <remarks>
    /// <para><see cref="Path"/> is concrete: <c>items.Array[3].name</c> — a field's name, a dot, a list
    /// as <c>Array[index]</c>, a referenced object as <c>references.Array[7]</c> (the registry's entries
    /// are <c>references[rid]&lt;Class&gt;</c>). A caller that groups values by field generalises the
    /// indices itself.</para>
    /// <para><see cref="Owner"/> and <see cref="Field"/> say whose field it is: the class that declares
    /// it (the root's own name for the object's first level, the type name of the class holding it
    /// below) and the field's name — a list's elements keep their list's.</para>
    /// </remarks>
    public readonly struct FieldSite
    {
        public FieldSite(string path, TypeNode node, string owner, string field)
        { Path = path; Node = node; Owner = owner; Field = field; }

        public string Path { get; }
        public TypeNode Node { get; }
        public string Owner { get; }
        public string Field { get; }
    }

    /// <summary>
    /// What an <see cref="ObjectWalker"/> hands over, value by value, keeping nothing: an object of a
    /// game can hold millions of texts and a caller decides what is worth a copy.
    ///
    /// ⚠ Everything received before <see cref="ObjectWalker.Walk"/> returns is provisional: when it
    /// throws (an object whose bytes are not its layout) the values already given belong to an object
    /// that is refused, and are to be dropped.
    /// </summary>
    public interface IObjectVisitor
    {
        /// <summary>A boolean, a number, or a string (as UTF-8 text).</summary>
        void Value(in FieldSite site, object value);

        /// <summary>A reference to another object (PPtr): its file id (0 is this file; N is entry N − 1 of <see cref="SerializedFile.Externals"/>) and its object id.</summary>
        void Pointer(in FieldSite site, int fileId, long pathId);

        /// <summary>Bytes passed over without being kept (an array of one-byte values, a type of a size this reader does not decode).</summary>
        void Skipped(in FieldSite site, long length);
    }

    /// <summary>
    /// The type tree of the class a referenced object (<c>[SerializeReference]</c>) is of, or null when
    /// the caller cannot say — which makes the registry <see cref="ObjectWalker.RegistryNotRead"/>.
    /// </summary>
    public delegate IReadOnlyList<TypeNode>? ReferenceResolver(string className, string nameSpace, string assembly);

    /// <summary>What a walk did, beyond the values it gave.</summary>
    public sealed class ObjectWalk
    {
        internal ObjectWalk(string? registryNotRead) { RegistryNotRead = registryNotRead; }

        /// <summary>
        /// Null when the whole object was read. Otherwise why the object's <c>[SerializeReference]</c>
        /// registry, the last thing in it, was NOT read: no resolver, or a referenced class the resolver
        /// does not know. The fields before it were given; what the registry holds was not.
        /// </summary>
        public string? RegistryNotRead { get; }
    }

    /// <summary>
    /// Reads an object's values along a type tree — the file's own, a generated layout of
    /// <see cref="ObjectLayouts"/>, or one built from a class's declaration — and hands every value to a
    /// visitor with its path: primitives, strings, references, lists, nested classes, and the registry
    /// of <c>[SerializeReference]</c> objects.
    ///
    /// 🔴 **An object whose bytes are not its layout is refused, never read in part.** A count that does
    /// not fit, a read past the end, bytes left over once the tree is walked: all are a
    /// <see cref="UnityFileFormatException"/> naming what disagrees. The only partial reading is the
    /// registry without a resolver, and it is said (<see cref="ObjectWalk.RegistryNotRead"/>).
    ///
    /// One walker per tree: its index of the tree is built once and the walker is reused for every
    /// object of that class. Not thread-safe.
    /// </summary>
    public sealed class ObjectWalker
    {
        private readonly Shape _root;
        private readonly ReferenceResolver? _resolver;
        private readonly Dictionary<IReadOnlyList<TypeNode>, Shape> _shapes = new Dictionary<IReadOnlyList<TypeNode>, Shape>();

        private sealed class Shape
        {
            public readonly IReadOnlyList<TypeNode> Nodes;
            public readonly int[][] Children;

            public Shape(IReadOnlyList<TypeNode> nodes)
            {
                if (nodes.Count == 0) throw new UnityFileFormatException("an empty type tree");
                Nodes = nodes;
                var lists = new List<int>[nodes.Count];
                var stack = new List<int>();
                for (int i = 0; i < nodes.Count; i++)
                {
                    lists[i] = new List<int>();
                    while (stack.Count > 0 && nodes[stack[stack.Count - 1]].Depth >= nodes[i].Depth) stack.RemoveAt(stack.Count - 1);
                    if (stack.Count > 0)
                    {
                        if (nodes[i].Depth != nodes[stack[stack.Count - 1]].Depth + 1)
                            throw new UnityFileFormatException($"type tree node {i} jumps from depth {nodes[stack[stack.Count - 1]].Depth} to {nodes[i].Depth}");
                        lists[stack[stack.Count - 1]].Add(i);
                    }
                    else if (i != 0) throw new UnityFileFormatException("a type tree with two roots");
                    stack.Add(i);
                }
                Children = new int[nodes.Count][];
                for (int i = 0; i < nodes.Count; i++) Children[i] = lists[i].ToArray();
            }
        }

        public ObjectWalker(IReadOnlyList<TypeNode> tree, ReferenceResolver? resolver = null)
        {
            _root = new Shape(tree);
            _shapes[tree] = _root;
            _resolver = resolver;
        }

        // The state of one walk, kept off the instance so a walker can be reused.
        private sealed class Run
        {
            public UnityFileReader R = null!;
            public IObjectVisitor V = null!;
            public string? RegistryNotRead;
        }

        /// <summary>
        /// Walks the object that starts at <paramref name="start"/> and ends at <paramref name="end"/>
        /// in the reader's stream, handing over every value.
        /// </summary>
        /// <param name="rootOwner">The name of the object's own class, the owner of its first-level fields.</param>
        /// <param name="wholeObject">
        /// True (the rule): the tree is the object's whole layout and the object must end where it ends.
        /// False: the tree is only the START of the layout (a MonoBehaviour's header, before the
        /// fields the game's code declares) — the reading stops after it and what follows is not asked
        /// about. Still refused when the header itself does not fit the object.
        /// </param>
        public ObjectWalk Walk(UnityFileReader reader, long start, long end, IObjectVisitor visitor, string rootOwner, bool wholeObject = true)
        {
            var run = new Run { R = reader, V = visitor };
            reader.Position = start;

            var kids = _root.Children[0];
            foreach (int kid in kids)
            {
                Field(run, _root, kid, _root.Nodes[kid].Name, rootOwner, _root.Nodes[kid].Name);
                if (run.RegistryNotRead != null) break;
            }

            // The registry is the last thing in an object; when it was not read the rest is not either.
            if (run.RegistryNotRead == null)
            {
                if (reader.Position > end)
                    throw new UnityFileFormatException($"the object reads past its end by {reader.Position - end} byte(s): not the layout it was written with");
                if (wholeObject && reader.Position != end)
                    throw new UnityFileFormatException($"the object has {end - reader.Position} byte(s) the layout does not account for");
            }
            return new ObjectWalk(run.RegistryNotRead);
        }

        private void Field(Run run, Shape shape, int index, string path, string owner, string field)
        {
            var node = shape.Nodes[index];
            var kids = shape.Children[index];
            var site = new FieldSite(path, node, owner, field);
            var r = run.R;

            if (node.IsArray || node.Type == "TypelessData")
            {
                ReadArray(run, shape, index, path, owner, field);
            }
            else if (node.Type == "string" && kids.Length == 1 && shape.Nodes[kids[0]].IsArray)
            {
                // A string is a counted array of bytes; read as such, given as text.
                var bytes = r.Bytes(r.Count());
                if (shape.Nodes[kids[0]].AlignsAfter) r.Align();
                run.V.Value(site, Encoding.UTF8.GetString(bytes));
            }
            else if (node.Type == "ManagedReferencesRegistry")
            {
                ReadRegistry(run, path, node, owner, field);
            }
            else if (node.Type.StartsWith("PPtr<", StringComparison.Ordinal) && kids.Length == 2)
            {
                int fileId = r.I32();
                long pathId = shape.Nodes[kids[1]].ByteSize == 4 ? r.I32() : r.I64();
                run.V.Pointer(site, fileId, pathId);
            }
            else if (kids.Length > 0)
            {
                foreach (int kid in kids)
                {
                    var child = shape.Nodes[kid];
                    // A list keeps its field's owner and name; anything else is a field of this class.
                    bool keeps = node.IsArray || child.IsArray;
                    Field(run, shape, kid, path.Length == 0 ? child.Name : path + "." + child.Name,
                          keeps ? owner : node.Type, keeps ? field : child.Name);
                    if (run.RegistryNotRead != null) return;
                }
            }
            else
            {
                Primitive(run, node, site);
            }

            if (node.AlignsAfter) r.Align();
        }

        private void ReadArray(Run run, Shape shape, int index, string path, string owner, string field)
        {
            var node = shape.Nodes[index];
            var kids = shape.Children[index];
            var r = run.R;
            if (kids.Length != 2) throw new UnityFileFormatException($"array '{node.Name}' has {kids.Length} children");

            var element = shape.Nodes[kids[1]];
            bool leaf = shape.Children[kids[1]].Length == 0;
            int size = leaf ? PrimitiveSize(element) : 0;
            int count = r.Count(Math.Max(1, size));

            if (leaf && size == 1)
            {
                // An array of one-byte values (a texture, a font file): passed over, said.
                r.Skip(count);
                run.V.Skipped(new FieldSite(path, node, owner, field), count);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Field(run, shape, kids[1], path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", owner, field);
                if (run.RegistryNotRead != null) return;
            }
        }

        private void ReadRegistry(Run run, string path, TypeNode node, string owner, string field)
        {
            var r = run.R;
            var site = new FieldSite(path, node, owner, field);

            int version = r.I32();
            if (version != 2)
            {
                run.RegistryNotRead = $"a registry of references in version {version}, not the one this reader knows (2)";
                return;
            }

            int count = r.Count(8);
            for (int i = 0; i < count; i++)
            {
                long rid = r.I64();
                string cls = ReadAlignedString(r), ns = ReadAlignedString(r), assembly = ReadAlignedString(r);

                // An entry that names no type (Unity's null and terminus references) holds no data.
                if (cls.Length == 0) continue;

                var tree = _resolver?.Invoke(cls, ns, assembly);
                if (tree == null)
                {
                    run.RegistryNotRead = _resolver == null
                        ? $"a registry of {count} reference(s) and no way to read their classes (first: {ns}.{cls})"
                        : $"the registry names {ns}.{cls} ({assembly}), whose class is not known";
                    return;
                }

                if (!_shapes.TryGetValue(tree, out var shape)) _shapes[tree] = shape = new Shape(tree);

                string entry = path + "[" + rid.ToString(CultureInfo.InvariantCulture) + "]<" + cls + ">";
                foreach (int kid in shape.Children[0])
                    Field(run, shape, kid, entry + "." + shape.Nodes[kid].Name, cls, shape.Nodes[kid].Name);
                if (run.RegistryNotRead != null) return;
            }
        }

        private static string ReadAlignedString(UnityFileReader r)
        {
            string text = Encoding.UTF8.GetString(r.Bytes(r.Count()));
            r.Align();
            return text;
        }

        private static bool IsKnownPrimitive(string type)
        {
            switch (type)
            {
                case "bool": case "char": case "SInt8": case "UInt8": case "SInt16": case "UInt16": case "short": case "unsigned short":
                case "int": case "SInt32": case "UInt32": case "unsigned int": case "float": case "Type*":
                case "SInt64": case "UInt64": case "long long": case "unsigned long long": case "double": case "FileSize":
                    return true;
                default: return false;
            }
        }

        private static int PrimitiveSize(TypeNode node)
        {
            switch (node.Type)
            {
                case "bool": case "char": case "SInt8": case "UInt8": return 1;
                case "SInt16": case "UInt16": case "short": case "unsigned short": return 2;
                case "int": case "SInt32": case "UInt32": case "unsigned int": case "float": case "Type*": return 4;
                case "SInt64": case "UInt64": case "long long": case "unsigned long long": case "double": case "FileSize": return 8;
                default: return node.ByteSize > 0 ? node.ByteSize : -1;
            }
        }

        private static void Primitive(Run run, TypeNode node, in FieldSite site)
        {
            var r = run.R;
            int size = PrimitiveSize(node);

            // A class with no field to serialize is a node with no children and no size (-1, or 0 in a type
            // tree a file kept): it holds no byte. A size below -1 names nothing this reader decodes: refused.
            if (size <= 0 && node.ByteSize >= -1 && node.Type != "string" && !IsKnownPrimitive(node.Type))
            {
                run.V.Skipped(site, 0);
                return;
            }

            if (size <= 0) throw new UnityFileFormatException($"field '{node.Name}' of type '{node.Type}' has no size this reader knows");

            switch (node.Type)
            {
                case "bool": run.V.Value(site, r.Bool()); return;
                case "char": case "UInt8": run.V.Value(site, r.U8()); return;
                case "SInt8": run.V.Value(site, (sbyte)r.U8()); return;
                case "SInt16": case "short": run.V.Value(site, r.I16()); return;
                case "UInt16": case "unsigned short": run.V.Value(site, r.U16()); return;
                case "int": case "SInt32": run.V.Value(site, r.I32()); return;
                case "UInt32": case "unsigned int": case "Type*": run.V.Value(site, r.U32()); return;
                case "float": run.V.Value(site, r.F32()); return;
                case "SInt64": case "long long": run.V.Value(site, r.I64()); return;
                case "UInt64": case "unsigned long long": case "FileSize": run.V.Value(site, r.U64()); return;
                case "double": run.V.Value(site, r.F64()); return;
                default:
                    r.Skip(size);
                    run.V.Skipped(site, size);
                    return;
            }
        }
    }
}
