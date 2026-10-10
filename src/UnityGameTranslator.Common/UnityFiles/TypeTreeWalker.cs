using System;
using System.Collections.Generic;
using System.Text;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// Reads an object's values along a type tree. Only the fields asked for are kept — a string,
    /// a byte array (an array of one-byte values: a font file), a list (a vector of strings…) —
    /// the others are read past, which is the only way to reach the next field in Unity's format.
    /// </summary>
    public sealed class TypeTreeWalker
    {
        private readonly UnityFileReader _r;
        private readonly IReadOnlyList<TypeNode> _nodes;
        private readonly List<int>[] _children;

        public TypeTreeWalker(UnityFileReader reader, IReadOnlyList<TypeNode> nodes)
        {
            if (nodes.Count == 0) throw new UnityFileFormatException("an empty type tree");
            _r = reader;
            _nodes = nodes;
            _children = new List<int>[nodes.Count];
            var stack = new List<int>();
            for (int i = 0; i < nodes.Count; i++)
            {
                _children[i] = new List<int>();
                while (stack.Count > 0 && nodes[stack[stack.Count - 1]].Depth >= nodes[i].Depth) stack.RemoveAt(stack.Count - 1);
                if (stack.Count > 0)
                {
                    if (nodes[i].Depth != nodes[stack[stack.Count - 1]].Depth + 1)
                        throw new UnityFileFormatException($"type tree node {i} jumps from depth {nodes[stack[stack.Count - 1]].Depth} to {nodes[i].Depth}");
                    _children[stack[stack.Count - 1]].Add(i);
                }
                else if (i != 0) throw new UnityFileFormatException("a type tree with two roots");
                stack.Add(i);
            }
        }

        /// <summary>
        /// The object's top-level fields named in <paramref name="wanted"/>, read from the reader's
        /// position; reading stops after the last of them.
        /// </summary>
        public Dictionary<string, object?> Fields(ICollection<string> wanted) => Fields(wanted, System.Array.Empty<string>());

        /// <summary>
        /// As <see cref="Fields(ICollection{string})"/>, and the fields named in <paramref name="measured"/>
        /// — arrays of one-byte values (a font file) — read past and given as their length (an int):
        /// what an index needs without holding every font of a game in memory.
        /// </summary>
        public Dictionary<string, object?> Fields(ICollection<string> wanted, ICollection<string> measured)
        {
            var found = new Dictionary<string, object?>();
            int expected = wanted.Count + measured.Count;
            foreach (int child in _children[0])
            {
                if (found.Count == expected) break;
                string name = _nodes[child].Name;
                if (measured.Contains(name))
                {
                    _skippedBytes = -1;
                    Value(child, false);
                    found[name] = _skippedBytes;
                    continue;
                }
                bool keep = wanted.Contains(name);
                var value = Value(child, keep);
                if (keep) found[name] = value;
            }
            return found;
        }

        // The length of the last array of one-byte values read past (Fields' measured fields).
        private int _skippedBytes = -1;

        private object? Value(int index, bool keep)
        {
            var node = _nodes[index];
            var kids = _children[index];
            object? result = null;
            if (node.IsArray) result = Array(index, keep);
            else if (kids.Count == 0) result = Primitive(node, keep);
            else if (node.Type == "string")
            {
                var bytes = Value(kids[0], keep) as byte[];
                if (keep) result = bytes == null ? "" : Encoding.UTF8.GetString(bytes);
            }
            else if (node.Type == "TypelessData") result = Array(index, keep);
            else if (kids.Count == 1 && _nodes[kids[0]].IsArray) result = Value(kids[0], keep);   // vector, map, set…
            else foreach (int kid in kids) Value(kid, false);
            if (node.AlignsAfter) _r.Align();
            return result;
        }

        private object? Array(int index, bool keep)
        {
            var kids = _children[index];
            if (kids.Count != 2) throw new UnityFileFormatException($"array '{_nodes[index].Name}' has {kids.Count} children");
            var element = _nodes[kids[1]];
            bool leaf = _children[kids[1]].Count == 0;
            int size = leaf ? PrimitiveSize(element) : 0;
            int count = _r.Count(Math.Max(1, size));
            if (leaf && size == 1)
            {
                if (keep) return _r.Bytes(count);
                _r.Skip(count);
                _skippedBytes = count;
                return null;
            }
            if (leaf && size > 0 && !keep && !element.AlignsAfter)
            {
                _r.Skip((long)count * size);
                return null;
            }
            var list = keep ? new List<object?>(count) : null;
            for (int i = 0; i < count; i++)
            {
                var v = Value(kids[1], keep);
                list?.Add(v);
            }
            return list;
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

        private object? Primitive(TypeNode node, bool keep)
        {
            int size = PrimitiveSize(node);
            if (size <= 0) throw new UnityFileFormatException($"field '{node.Name}' of type '{node.Type}' has no size this reader knows");
            if (!keep) { _r.Skip(size); return null; }
            switch (node.Type)
            {
                case "bool": return _r.Bool();
                case "char": case "UInt8": return _r.U8();
                case "SInt8": return (sbyte)_r.U8();
                case "SInt16": case "short": return _r.I16();
                case "UInt16": case "unsigned short": return _r.U16();
                case "int": case "SInt32": return _r.I32();
                case "UInt32": case "unsigned int": case "Type*": return _r.U32();
                case "float": return _r.F32();
                case "SInt64": case "long long": return _r.I64();
                case "UInt64": case "unsigned long long": case "FileSize": return _r.U64();
                case "double": return _r.F64();
                default: return _r.Bytes(size);
            }
        }
    }
}
