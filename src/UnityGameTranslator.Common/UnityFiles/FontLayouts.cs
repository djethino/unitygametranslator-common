using System.Collections.Generic;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// The shape of Unity's Font object (class 128) for a Unity version — what a game's data file
    /// does not say when its type trees were stripped (every release build). The tables are
    /// generated (FontLayouts.Tables.g.cs, common/tools/generate-font-layouts.py) from AssetRipper's
    /// type tree dumps; this part chooses among them.
    /// </summary>
    public static partial class FontLayouts
    {
        /// <summary>Unity's class id of Font.</summary>
        public const int FontClassId = 128;

        /// <summary>
        /// The Font layout of <paramref name="versionKey"/> (<see cref="UnityVersions.Key"/>): the last
        /// one introduced at or before it. Null when the version is unknown (0) or older than any
        /// Font. <paramref name="newerThanTable"/>: the version is newer than every version the source
        /// knew — the closest layout below is given, and may be wrong if Unity changed Font since.
        /// </summary>
        public static IReadOnlyList<TypeNode>? For(ulong versionKey, out bool newerThanTable)
        {
            newerThanTable = versionKey > NewestKnown;
            if (versionKey == 0 || versionKey < Since[0]) return null;
            int at = 0;
            for (int i = 0; i < Since.Length && Since[i] <= versionKey; i++) at = i;
            int layout = LayoutAt[at];
            return layout < 0 ? null : Layouts[layout];
        }

        private static Dictionary<uint, string>? _commonByOffset;

        /// <summary>
        /// The common string at <paramref name="offset"/> in Unity's built-in buffer (an embedded type
        /// tree writes such a name as the offset with its top bit set); null when the table has none
        /// there — a newer engine's string.
        /// </summary>
        public static string? CommonString(uint offset)
        {
            var map = _commonByOffset;
            if (map == null)
            {
                map = new Dictionary<uint, string>();
                uint at = 0;
                foreach (var s in CommonStrings)
                {
                    map[at] = s;
                    at += (uint)System.Text.Encoding.UTF8.GetByteCount(s) + 1;
                }
                _commonByOffset = map;
            }
            return map.TryGetValue(offset, out var found) ? found : null;
        }
    }
}
