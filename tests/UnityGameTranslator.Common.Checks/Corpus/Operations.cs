using System;
using System.Collections.Generic;
using System.Text.Json;
using static UnityGameTranslator.Common.Checks.Corpus.JsonValues;

namespace UnityGameTranslator.Common.Checks.Corpus
{
    /// <summary>One operation of the corpus, and the C# call behind it.</summary>
    internal sealed class Operation
    {
        public readonly string Rule;
        public readonly string Name;

        /// <summary>The class and the public method(s) this operation exercises — what coverage counts.</summary>
        public readonly Type Class;
        public readonly string[] Methods;

        public readonly Func<JsonElement, object?> Call;

        public Operation(string rule, string name, Type type, string method, Func<JsonElement, object?> call)
            : this(rule, name, type, new[] { method }, call) { }

        public Operation(string rule, string name, Type type, string[] methods, Func<JsonElement, object?> call)
        {
            Rule = rule;
            Name = name;
            Class = type;
            Methods = methods;
            Call = call;
        }

        public string Id => Rule + "/" + Name;
    }

    /// <summary>
    /// The dispatch table: <c>rule/op</c> → the library call. This is the ONLY code in the corpus
    /// executor that knows UnityGameTranslator.Common, and the only thing a port has to write in
    /// its own language — one line per operation. The cases themselves are in corpus/rules/.
    ///
    /// ⚠ Input keys are the snake_case of the C# parameter names, always named, never positional.
    /// A number that stands for an ordering (<c>compare</c>) comes back as its sign.
    /// </summary>
    internal static class Operations
    {
        public static readonly Operation[] All =
        {
            // ── versions ──────────────────────────────────────────────────────
            new Operation("versions", "compare", typeof(Versions), nameof(Versions.Compare),
                e => Math.Sign(Versions.Compare(Str(e, "a"), Str(e, "b")))),
            new Operation("versions", "is_newer", typeof(Versions), nameof(Versions.IsNewer),
                e => Versions.IsNewer(Str(e, "current"), Str(e, "candidate"))),

            // ── sync ──────────────────────────────────────────────────────────
            new Operation("sync", "content_hash", typeof(ContentHash), nameof(ContentHash.Of),
                e => ContentHash.Of(Lines(e, "lines"), Str(e, "uuid")!)),
            new Operation("sync", "is_metadata_key", typeof(ContentHash), nameof(ContentHash.IsMetadataKey),
                e => ContentHash.IsMetadataKey(Str(e, "key")!)),
            new Operation("sync", "decide", typeof(Sync), nameof(Sync.Decide),
                e => Sync.Decide(Str(e, "local_content")!, Str(e, "server_content")!,
                                 Str(e, "last_synced")!, Bool(e, "has_local_changes"))),

            // ── merge ─────────────────────────────────────────────────────────
            new Operation("merge", "priority_of", typeof(Merge), nameof(Merge.PriorityOf),
                e => Merge.PriorityOf(Str(e, "tag")!, Str(e, "value")!)),
            new Operation("merge", "is_game_line", typeof(Merge), nameof(Merge.IsGameLine),
                e => Merge.IsGameLine(Str(e, "tag")!)),
            new Operation("merge", "can_replace", typeof(Merge), nameof(Merge.CanReplace),
                e => Merge.CanReplace(LineRequired(e, "candidate"), Line(e, "existing"))),
            new Operation("merge", "contribution_wins", typeof(Merge), nameof(Merge.ContributionWins),
                e => Merge.ContributionWins(Line(e, "main"), LineRequired(e, "contribution"))),
            new Operation("merge", "decide", typeof(Merge), nameof(Merge.Decide),
                e => Merge.Decide(Line(e, "local"), Line(e, "remote"), Line(e, "ancestor"))),
            new Operation("merge", "same", typeof(Merge), nameof(Merge.Same),
                e => Merge.Same(LineRequired(e, "a"), LineRequired(e, "b"))),
        };

        private static readonly Dictionary<string, Operation> ById = Index();

        private static Dictionary<string, Operation> Index()
        {
            var index = new Dictionary<string, Operation>(StringComparer.Ordinal);
            foreach (var op in All) index[op.Id] = op;
            return index;
        }

        public static Operation? Find(string rule, string op) =>
            ById.TryGetValue(rule + "/" + op, out var found) ? found : null;
    }
}
