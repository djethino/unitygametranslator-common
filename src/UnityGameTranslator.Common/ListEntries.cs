using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The entries of a game's list as a slot reads them, and the two writes that change them (spec/translation-file/lists.md § seen):
    /// <c>values</c> is what the game imposes (replaced whole by the extraction), <c>seen</c> what was found in the lines or while playing
    /// (never removed by an extraction, merged by union). Pure, with no JSON library: the caller reads the two arrays and hands them in.
    /// </summary>
    public static class ListEntries
    {
        /// <summary>
        /// What a slot reads: <paramref name="values"/> in their order, then each entry of <paramref name="seen"/> not already there. An empty
        /// or absent entry is none; an entry twice is one. Either list absent is empty.
        /// </summary>
        public static List<string> Of(IReadOnlyList<string?>? values, IReadOnlyList<string?>? seen)
        {
            var entries = new List<string>();
            var held = new HashSet<string>(StringComparer.Ordinal);
            void Take(IReadOnlyList<string?>? list)
            {
                if (list == null) return;
                foreach (var entry in list)
                    if (!string.IsNullOrEmpty(entry) && held.Add(entry!)) entries.Add(entry!);
            }
            Take(values);
            Take(seen);
            return entries;
        }

        /// <summary>
        /// The <c>seen</c> a writer writes after adding <paramref name="found"/> (the optimisation, the mod): the old ones in their order, then the
        /// new ones in the order found — each once, none that <paramref name="values"/> holds, no empty entry.
        /// </summary>
        public static List<string> Seen(IReadOnlyList<string?>? values, IReadOnlyList<string?>? seen, IReadOnlyList<string?>? found)
        {
            var imposed = new HashSet<string>(StringComparer.Ordinal);
            if (values != null) foreach (var v in values) if (!string.IsNullOrEmpty(v)) imposed.Add(v!);
            var result = new List<string>();
            var held = new HashSet<string>(StringComparer.Ordinal);
            void Take(IReadOnlyList<string?>? list)
            {
                if (list == null) return;
                foreach (var entry in list)
                    if (!string.IsNullOrEmpty(entry) && !imposed.Contains(entry!) && held.Add(entry!)) result.Add(entry!);
            }
            Take(seen);
            Take(found);
            return result;
        }

        /// <summary>
        /// Two files' <c>seen</c> of the same list, merged (two machines, a download over local work): the union, <paramref name="mine"/>'s order
        /// first — the same as <see cref="Seen"/> with the other side's as what was found.
        /// </summary>
        public static List<string> Merged(IReadOnlyList<string?>? values, IReadOnlyList<string?>? mine, IReadOnlyList<string?>? theirs)
            => Seen(values, mine, theirs);
    }
}
