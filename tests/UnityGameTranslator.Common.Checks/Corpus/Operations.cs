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

            // ── edge_give ─────────────────────────────────────────────────────
            // 🔴 **One operation, and it replays a gesture.** Everything else in this corpus asks a
            // question and compares the answer; this rule is a physical one, whose statement is a
            // LIMIT — "an uneven wheel must not saw the edge up and down". There is no exact number
            // to freeze: it is floating point, so a port in another language is right and writes
            // different decimals. So a case describes a wheel, and the measurements come back.
            new Operation("edge_give", "replay", typeof(EdgeGive), nameof(EdgeGive.Advance),
                e => ReplayWheel(e)),

            // ── dropdown_fit ──────────────────────────────────────────────────
            // ⚠ The cases are chosen so every answer lands on a whole number: the share of a
            // window is a multiplication by 0.45, and a port in another language will not write
            // the same decimals for it. What the boundary cases assert is `needs_search`, which is
            // a yes or a no and travels intact.
            new Operation("dropdown_fit", "height", typeof(DropdownFit), nameof(DropdownFit.Height),
                e => DropdownFit.Height(Int(e, "count"), NullableDouble(e, "row_height") ?? 0,
                                        NullableDouble(e, "available") ?? 0)),
            new Operation("dropdown_fit", "needs_search", typeof(DropdownFit), nameof(DropdownFit.NeedsSearch),
                e => DropdownFit.NeedsSearch(Int(e, "count"), NullableDouble(e, "row_height") ?? 0,
                                             NullableDouble(e, "available") ?? 0)),
            new Operation("dropdown_fit", "overflows", typeof(DropdownFit), nameof(DropdownFit.Overflows),
                e => DropdownFit.Overflows(Int(e, "count"), NullableDouble(e, "row_height") ?? 0,
                                           NullableDouble(e, "list_height") ?? 0)),

            // ── list_room ─────────────────────────────────────────────────────
            // ⚠ Two operations read the same inputs because the answer is a pair — what a list
            // holds and what it may be squeezed to — and the corpus compares one value at a time.
            new Operation("list_room", "whole", typeof(ListRooms), nameof(ListRooms.For),
                e => ListRooms.For(Int(e, "rows"), NullableDouble(e, "row_space") ?? 0,
                                   NullableDouble(e, "chrome") ?? 0).Whole),
            new Operation("list_room", "least", typeof(ListRooms), nameof(ListRooms.For),
                e => ListRooms.For(Int(e, "rows"), NullableDouble(e, "row_space") ?? 0,
                                   NullableDouble(e, "chrome") ?? 0).Least),
            new Operation("list_room", "least_measured", typeof(ListRooms), nameof(ListRooms.Of),
                e => ListRooms.Of(NullableDouble(e, "whole") ?? 0, Int(e, "rows"),
                                  NullableDouble(e, "row_space") ?? 0).Least),
            new Operation("list_room", "least_surface", typeof(ListRooms), nameof(ListRooms.LeastSurface),
                e => ListRooms.LeastSurface(RoomsOf(e), NullableDouble(e, "around") ?? 0)),

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

            // ── placeholders ──────────────────────────────────────────────────
            // The frozen sequences are derived from the source inside the operation: a case that
            // handed them in would be restating the implementation it is meant to hold.
            new Operation("placeholders", "frozen_sequences", typeof(Placeholders), nameof(Placeholders.FrozenSequences),
                e => Placeholders.FrozenSequences(Str(e, "source") ?? "")),
            new Operation("placeholders", "accepts", typeof(Placeholders), nameof(Placeholders.Accepts),
                e => Gate(Placeholders.Accepts(Str(e, "source") ?? "", Str(e, "translation") ?? "",
                                               Placeholders.FrozenSequences(Str(e, "source") ?? ""), out var errors), errors)),
            new Operation("placeholders", "accepts_edit", typeof(Placeholders), nameof(Placeholders.AcceptsEdit),
                e => Gate(Placeholders.AcceptsEdit(Str(e, "source") ?? "", Str(e, "edited") ?? "",
                                                   Placeholders.FrozenSequences(Str(e, "source") ?? ""), out var errors), errors)),
            new Operation("placeholders", "repair_trailing_breaks", typeof(Placeholders), nameof(Placeholders.RepairTrailingBreaks),
                e => Placeholders.RepairTrailingBreaks(Str(e, "source") ?? "", Str(e, "translation") ?? "")),
            new Operation("placeholders", "correction", typeof(Placeholders), nameof(Placeholders.Correction),
                e => Placeholders.Correction(Strings(e, "errors"), Strings(e, "frozen"))),
            new Operation("placeholders", "mandatory_sequences", typeof(Placeholders), nameof(Placeholders.MandatorySequences),
                e => Placeholders.MandatorySequences(Strings(e, "frozen"))),
            new Operation("placeholders", "tokens", typeof(Placeholders), nameof(Placeholders.Tokens),
                e => Placeholders.Tokens(Str(e, "text") ?? "")),
            new Operation("placeholders", "invented", typeof(Placeholders), nameof(Placeholders.Invented),
                e => Placeholders.Invented(Str(e, "source") ?? "", Str(e, "translation"))),
            new Operation("placeholders", "tally", typeof(Placeholders), nameof(Placeholders.Tally),
                e => Placeholders.Tally(Str(e, "text") ?? "")),
            new Operation("placeholders", "max_attempts", typeof(Placeholders), nameof(Placeholders.MaxAttempts),
                e => Placeholders.MaxAttempts),

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

        /// <summary>A gate's verdict as the corpus writes it: the answer and the lines it would send.</summary>
        private static Dictionary<string, object?> Gate(bool accepted, List<string> errors) =>
            new Dictionary<string, object?> { ["accepted"] = accepted, ["errors"] = errors };

        /// <summary>
        /// Turns a wheel over an edge and reports what the gesture looked like.
        ///
        /// The case says how many frames pass between notches (`gaps`, walked in order and
        /// repeated), how long a frame is (`frame`, or a list under `frames` for an uneven clock),
        /// and how many notches to ignore before measuring (`warmup` — the opening climb is not a
        /// state). `settle` then lets go and counts the frames home.
        ///
        /// What comes back is what the rule is stated in:
        ///   worst_fall  the largest run of backwards travel WHILE the wheel is still turning —
        ///               the sawtooth, which is what a tremble is
        ///   band        highest minus lowest in the steady turn — a held edge is narrow
        ///   jolt        the largest change in per-frame step — what the eye reads as a judder
        ///   crossings   times the edge changed sign on the way home — a critically damped return
        ///               has none
        ///   frames_home how long the return took, once the wheel stopped
        /// </summary>
        /// <summary>
        /// The lists on one surface, described by their row counts — every one of them measured
        /// with the same row height and chrome, because they are drawn by the same product.
        /// </summary>
        private static List<ListRoom> RoomsOf(JsonElement e)
        {
            var rowSpace = NullableDouble(e, "row_space") ?? 0;
            var chrome = NullableDouble(e, "chrome") ?? 0;

            var rooms = new List<ListRoom>();
            if (!e.TryGetProperty("rows", out var rows) || rows.ValueKind != JsonValueKind.Array)
                return rooms;

            foreach (var item in rows.EnumerateArray())
                rooms.Add(ListRooms.For(item.GetInt32(), rowSpace, chrome));

            return rooms;
        }

        private static Dictionary<string, object?> ReplayWheel(JsonElement e)
        {
            var gaps = new List<int>();
            if (e.TryGetProperty("gaps", out var g) && g.ValueKind == JsonValueKind.Array)
                foreach (var item in g.EnumerateArray()) gaps.Add(item.GetInt32());
            if (gaps.Count == 0) gaps.Add(3);

            var frames = new List<double>();
            if (e.TryGetProperty("frames", out var f) && f.ValueKind == JsonValueKind.Array)
                foreach (var item in f.EnumerateArray()) frames.Add(item.GetDouble());
            if (frames.Count == 0) frames.Add(NullableDouble(e, "frame") ?? (1.0 / 60));

            int notches = Int(e, "notches", 40);
            int warmup = Int(e, "warmup", 12);

            var give = new EdgeGive();

            double worstFall = 0, falling = 0, previous = 0, previousStep = 0;
            double low = double.MaxValue, high = double.MinValue, jolt = 0;
            bool first = true;
            int tick = 0;

            for (int notch = 0; notch < notches; notch++)
            {
                give.Push(1);

                for (int f2 = 0; f2 < gaps[notch % gaps.Count]; f2++)
                {
                    give.Advance(frames[tick++ % frames.Count]);

                    double step = give.Offset - previous;

                    if (!first)
                    {
                        // A run of falls counts once and at full size: one long slide is one fall.
                        if (step < 0) falling += -step;
                        else { worstFall = Math.Max(worstFall, falling); falling = 0; }

                        if (notch >= warmup)
                        {
                            jolt = Math.Max(jolt, Math.Abs(step - previousStep));
                            low = Math.Min(low, give.Offset);
                            high = Math.Max(high, give.Offset);
                        }
                    }

                    previousStep = step;
                    previous = give.Offset;
                    first = false;
                }
            }

            worstFall = Math.Max(worstFall, falling);

            long framesHome = 0;
            long crossings = 0;

            if (Bool(e, "settle", true))
            {
                int sign = Math.Sign(give.Offset);
                while (give.Advance(frames[tick++ % frames.Count]))
                {
                    int now = Math.Sign(give.Offset);
                    if (now != 0 && now != sign) crossings++;
                    sign = now;
                    if (++framesHome > 600) break;
                }
            }

            return new Dictionary<string, object?>
            {
                ["worst_fall"] = Math.Round(worstFall, 3),
                ["band"] = high >= low ? Math.Round(high - low, 3) : 0d,
                ["jolt"] = Math.Round(jolt, 3),
                ["crossings"] = crossings,
                ["frames_home"] = framesHome,
                ["at_rest"] = give.AtRest,
            };
        }

        public static Operation? Find(string rule, string op) =>
            ById.TryGetValue(rule + "/" + op, out var found) ? found : null;
    }
}
