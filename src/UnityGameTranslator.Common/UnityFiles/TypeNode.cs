namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// One field of a Unity type tree, as the engine writes it: a list in depth-first order, each
    /// node a level deeper than the field it belongs to. A file's own tree (kept in development
    /// builds) and the generated one (<see cref="FontLayouts"/>) read the same way.
    /// </summary>
    public readonly struct TypeNode
    {
        public TypeNode(int depth, string type, string name, int byteSize, byte typeFlags, uint metaFlags)
        {
            Depth = depth;
            Type = type;
            Name = name;
            ByteSize = byteSize;
            TypeFlags = typeFlags;
            MetaFlags = metaFlags;
        }

        public int Depth { get; }
        public string Type { get; }
        public string Name { get; }
        /// <summary>Bytes the value takes; -1 when it varies (an array, a string, a structure holding one).</summary>
        public int ByteSize { get; }
        public byte TypeFlags { get; }
        public uint MetaFlags { get; }

        /// <summary>An array: its two children are its size and its element.</summary>
        public bool IsArray => (TypeFlags & 1) != 0;

        /// <summary>The reader moves to a multiple of four after this value.</summary>
        public bool AlignsAfter => (MetaFlags & 0x4000) != 0;

        public override string ToString() => new string(' ', Depth * 2) + Type + " " + Name;
    }
}
