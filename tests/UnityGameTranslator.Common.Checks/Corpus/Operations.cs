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
            new Operation("sync", "name", typeof(Sync), nameof(Sync.Name),
                e => Sync.Name(EnumRequired<SyncDirection>(e, "direction"))),
            new Operation("sync", "explain", typeof(Sync), nameof(Sync.Explain),
                e => Sync.Explain(EnumRequired<SyncDirection>(e, "direction"))),

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

            // ── badges ────────────────────────────────────────────────────────
            //
            // ⚠ Sixteen arguments, nine of them optional: named keys are not a nicety here. An
            // absent key is the C# default, which is what a caller that does not know passes.
            new Operation("badges", "for", typeof(Badges), nameof(Badges.For),
                e => Badges.For(
                    EnumRequired<Publication>(e, "publication"),
                    NullableBool(e, "is_main"),
                    NullableInt(e, "branches_waiting"),
                    Bool(e, "main_missing"),
                    EnumOf<SyncDirection>(e, "sync"),
                    EnumOf<ReviewStage>(e, "stage"),
                    NullableDouble(e, "completeness"),
                    Int(e, "votes"),
                    Int(e, "downloads"),
                    NullableBool(e, "finished"),
                    NullableBool(e, "accepts_contributions"),
                    NullableInt(e, "lines_available"),
                    OriginOf(e, "origin"),
                    Str(e, "main_owner"),
                    Bool(e, "main_abandoned"),
                    Bool(e, "branch_frozen"))),

            // ── mod_ui_migration ──────────────────────────────────────────────
            new Operation("mod_ui_migration", "decide", typeof(ModUiMigration), nameof(ModUiMigration.Decide),
                e => ModUiMigration.Decide(Bool(e, "in_ancestor"), Bool(e, "already_held"), Bool(e, "is_empty"),
                                           Str(e, "line_language")!, Str(e, "interface_language")!)),
            new Operation("mod_ui_migration", "still_counts_as_published",
                typeof(ModUiMigration), nameof(ModUiMigration.StillCountsAsPublished),
                e => ModUiMigration.StillCountsAsPublished(Str(e, "ancestor_tag")!, Bool(e, "present_locally"))),

            new Operation("merge", "is_by_hand", typeof(Merge), nameof(Merge.IsByHand),
                e => Merge.IsByHand(Str(e, "tag")!, Str(e, "value")!)),

            // ── answers ───────────────────────────────────────────────────────
            new Operation("answers", "read", typeof(Answers), nameof(Answers.Read),
                e => Answers.Read(Str(e, "answer"))),
            new Operation("answers", "read_rating", typeof(Answers), nameof(Answers.ReadRating),
                e => Answers.ReadRating(Str(e, "answer"))),
            new Operation("answers", "clean", typeof(Answers), nameof(Answers.Clean),
                e => Answers.Clean(Str(e, "answer")!)),
            new Operation("answers", "store", typeof(Answers), nameof(Answers.Store),
                e => Answers.Store(Bool(e, "from_own_ui"), EnumRequired<AnswerKind>(e, "kind"))),
            new Operation("answers", "capture", typeof(Answers), nameof(Answers.Capture),
                e => Answers.Capture(Bool(e, "from_own_ui"))),
            new Operation("answers", "tag_of", typeof(Answers), nameof(Answers.TagOf),
                e => Answers.TagOf(EnumRequired<Filing>(e, "filing"))),
            new Operation("answers", "stores_the_source", typeof(Answers), nameof(Answers.StoresTheSource),
                e => Answers.StoresTheSource(EnumRequired<Filing>(e, "filing"))),

            // ── settings ──────────────────────────────────────────────────────
            new Operation("settings", "all", typeof(SettingsSections), nameof(SettingsSections.All),
                e => SettingsSections.All),
            new Operation("settings", "json_key", typeof(SettingsSections), nameof(SettingsSections.JsonKey),
                e => SettingsSections.JsonKey(Str(e, "section")!)),
            new Operation("settings", "section_of", typeof(SettingsSections), nameof(SettingsSections.SectionOf),
                e => SettingsSections.SectionOf(Str(e, "json_key")!)),
            new Operation("settings", "name", typeof(SettingsSections), nameof(SettingsSections.Name),
                e => SettingsSections.Name(Str(e, "section")!)),
            new Operation("settings", "description", typeof(SettingsSections), nameof(SettingsSections.Description),
                e => SettingsSections.Description(Str(e, "section")!)),
            new Operation("settings", "classify", typeof(SettingsSections), nameof(SettingsSections.Classify),
                e => SettingsSections.Classify(Bool(e, "ours_matches_theirs"), Bool(e, "has_ancestor"),
                                               Bool(e, "ours_matches_ancestor"), Bool(e, "theirs_matches_ancestor"))),
            new Operation("settings", "needs_decision", typeof(SettingsSections), nameof(SettingsSections.NeedsDecision),
                e => SettingsSections.NeedsDecision(EnumRequired<SettingsSectionState>(e, "state"))),
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
