using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.UnityFiles
{
    /// <summary>
    /// The shape of some of Unity's classes (TextAsset 49, MonoBehaviour 114, MonoScript 115) for a
    /// Unity version — what a game's data file does not say when its type trees were stripped (every
    /// release build). The tables are generated (ObjectLayouts.Tables.g.cs,
    /// common/tools/generate-object-layouts.py) from the SAME dump as <see cref="FontLayouts"/>; this
    /// part chooses among them. A MonoBehaviour's layout is its header only (m_GameObject, m_Enabled,
    /// m_Script, m_Name): its own fields follow, and come from the game's code, never from here.
    /// </summary>
    public static partial class ObjectLayouts
    {
        public const int TextAssetClassId = 49;
        public const int MonoBehaviourClassId = 114;
        public const int MonoScriptClassId = 115;

        /// <summary>Whether this table holds the class.</summary>
        public static bool Has(int classId) => Array.IndexOf(Classes, classId) >= 0;

        /// <summary>
        /// The layout of <paramref name="classId"/> for <paramref name="versionKey"/>
        /// (<see cref="UnityVersions.Key"/>): the last one introduced at or before it. Null when the
        /// version is unknown (0), older than any this class had, or the class is not one of this
        /// table's (<see cref="Has"/> says). <paramref name="newerThanTable"/>: the version is newer
        /// than every version the source knew — the closest layout below is given, and may be wrong if
        /// Unity changed the class since; a caller says so once.
        /// </summary>
        public static IReadOnlyList<TypeNode>? For(int classId, ulong versionKey, out bool newerThanTable)
        {
            newerThanTable = versionKey > NewestKnown;
            int c = Array.IndexOf(Classes, classId);
            if (c < 0 || versionKey == 0 || versionKey < Since[c][0]) return null;
            int at = 0;
            for (int i = 0; i < Since[c].Length && Since[c][i] <= versionKey; i++) at = i;
            int layout = LayoutAt[c][at];
            return layout < 0 ? null : Layouts[c][layout];
        }
    }
}
