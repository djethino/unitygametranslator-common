using System;
using System.Linq;
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
            // ⚠ One height at a time, named by `of`: the corpus compares one value per case, and a
            // case that asserts a whole list is a case nobody can read when it goes red.
            new Operation("list_room", "share", typeof(ListRooms), nameof(ListRooms.Share),
                e => ListRooms.Share(RoomsOf(e), NullableDouble(e, "available") ?? 0)[Int(e, "of")]),

            // ── sync ──────────────────────────────────────────────────────────
            new Operation("sync", "content_hash", typeof(ContentHash), nameof(ContentHash.Of),
                e => ContentHash.Of(Lines(e, "lines"), Str(e, "uuid")!)),
            new Operation("sync", "decide", typeof(Sync), nameof(Sync.Decide),
                e => Sync.Decide(Str(e, "local_content")!, Str(e, "server_content")!,
                                 Str(e, "last_synced")!, Bool(e, "has_local_changes"))),
            new Operation("sync", "decide_file", typeof(Sync), nameof(Sync.DecideFile),
                e => Sync.DecideFile(Str(e, "local_content")!, Str(e, "server_content")!,
                                     Str(e, "last_synced")!, Bool(e, "lines_changed"), Bool(e, "settings_changed"))),
            new Operation("sync", "name", typeof(Sync), nameof(Sync.Name),
                e => Sync.Name(EnumRequired<SyncDirection>(e, "direction"))),
            new Operation("sync", "explain", typeof(Sync), nameof(Sync.Explain),
                e => Sync.Explain(EnumRequired<SyncDirection>(e, "direction"))),

            // ── translation_file_keys ─────────────────────────────────────────
            new Operation("translation_file_keys", "is_metadata_key", typeof(TranslationFileKeys), nameof(TranslationFileKeys.IsMetadataKey),
                e => TranslationFileKeys.IsMetadataKey(Str(e, "key")!)),
            new Operation("translation_file_keys", "is_line", typeof(TranslationFileKeys), nameof(TranslationFileKeys.IsLine),
                e => TranslationFileKeys.IsLine(Str(e, "key")!, Bool(e, "line_shaped"))),

            // ── standing ──────────────────────────────────────────────────────
            new Operation("standing", "from", typeof(Standings), nameof(Standings.From),
                e => Standings.From(LocalFactsOf(e), ServerFactsOf(e), AccountFactsOf(e))),
            new Operation("standing", "leads_the_lineage", typeof(Standings), nameof(Standings.LeadsTheLineage),
                e => Standings.LeadsTheLineage(StandingOf(e))),
            new Operation("standing", "on_a_branch", typeof(Standings), nameof(Standings.OnABranch),
                e => Standings.OnABranch(StandingOf(e))),
            new Operation("standing", "may_write_locally", typeof(Standings), nameof(Standings.MayWriteLocally),
                e => Standings.MayWriteLocally(EnumRequired<AccountStanding>(e, "account"))),
            new Operation("standing", "may_write_to_server", typeof(Standings), nameof(Standings.MayWriteToServer),
                e => Standings.MayWriteToServer(EnumRequired<AccountStanding>(e, "account"))),
            new Operation("standing", "explain_refusal", typeof(Standings), nameof(Standings.ExplainRefusal),
                e => Standings.ExplainRefusal(EnumRequired<AccountStanding>(e, "account"), Bool(e, "to_server"))),

            // ── uploads ───────────────────────────────────────────────────────
            new Operation("uploads", "act_of", typeof(Uploads), nameof(Uploads.ActOf),
                e => Uploads.ActOf(EnumRequired<Publication>(e, "publication"), Bool(e, "on_a_branch"),
                                   NullableBool(e, "accepts_branches"), NullableBool(e, "main_missing"),
                                   NullableBool(e, "main_abandoned"), NullableBool(e, "branch_frozen"))),
            new Operation("uploads", "verb", typeof(Uploads), nameof(Uploads.Verb),
                e => Uploads.Verb(EnumRequired<UploadAct>(e, "act"))),
            new Operation("uploads", "wall", typeof(Uploads), nameof(Uploads.Wall),
                e => Uploads.Wall(EnumRequired<Publication>(e, "publication"), Bool(e, "on_a_branch"), Str(e, "owner"),
                                  NullableBool(e, "accepts_branches"), NullableBool(e, "main_missing"),
                                  NullableBool(e, "main_abandoned"), NullableBool(e, "branch_frozen"))),
            new Operation("uploads", "wall_of", typeof(Uploads), nameof(Uploads.WallOf),
                e => Uploads.WallOf(EnumRequired<Publication>(e, "publication"), Bool(e, "on_a_branch"), Str(e, "owner"),
                                    NullableBool(e, "accepts_branches"), NullableBool(e, "main_missing"),
                                    NullableBool(e, "main_abandoned"), NullableBool(e, "branch_frozen"))),
            new Operation("uploads", "game_wall", typeof(Uploads), nameof(Uploads.GameWall),
                e => Uploads.GameWall(EnumRequired<UploadAct>(e, "act"), Bool(e, "on_a_branch"),
                                      GameChoiceOf(e, "confirmed_game"), LineageGameOf(e, "game"))),
            new Operation("uploads", "decided_in_the_game", typeof(Uploads), nameof(Uploads.DecidedInTheGame),
                e => Uploads.DecidedInTheGame(EnumRequired<UploadAct>(e, "act"))),
            new Operation("uploads", "closed_reason", typeof(Uploads), nameof(Uploads.ClosedReason),
                e => Uploads.ClosedReason(EnumRequired<UploadAct>(e, "act"), Int(e, "lines"), Bool(e, "untouched_copy"),
                                          Bool(e, "online", fallback: true), Bool(e, "signed_in", fallback: true),
                                          Bool(e, "in_sync"), Bool(e, "server_moved"))),
            // The button composes the standing from the same facts it is then judged on, so a case
            // states the facts once and the two rules are held together.
            new Operation("uploads", "button", typeof(Uploads), nameof(Uploads.Button),
                e => Uploads.Button(Standings.From(LocalFactsOf(e), ServerFactsOf(e), AccountFactsOf(e)),
                                    LocalFactsOf(e), ServerFactsOf(e), AccountFactsOf(e))),

            // ── notices ───────────────────────────────────────────────────────
            new Operation("notices", "sync", typeof(Notices), nameof(Notices.Sync),
                e => Notices.Sync(SyncWorkOf(e), Standings.From(LocalFactsOf(e), ServerFactsOf(e), AccountFactsOf(e)),
                                  LocalFactsOf(e), ServerFactsOf(e), AccountFactsOf(e))),

            // ── status_card ───────────────────────────────────────────────────
            new Operation("status_card", "secondary", typeof(StatusCards), nameof(StatusCards.Secondary),
                e => StatusCards.Secondary(Standings.From(LocalFactsOf(e), ServerFactsOf(e), AccountFactsOf(e)),
                                           ServerFactsOf(e), Int(e, "local_changes"))),
            new Operation("status_card", "notice", typeof(StatusCards), nameof(StatusCards.Notice),
                e => StatusCards.Notice(Standings.From(LocalFactsOf(e), ServerFactsOf(e), AccountFactsOf(e)),
                                        ServerFactsOf(e), Bool(e, "ignoring_dismissed"), Bool(e, "capture_only"))),

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
            new Operation("merge", "decide_formats", typeof(Merge), nameof(Merge.Decide),
                e => Merge.Decide(Line(e, "local"), Line(e, "remote"), Line(e, "ancestor"),
                                  Int(e, "local_format"), Int(e, "remote_format"), Int(e, "ancestor_format"))),
            new Operation("merge", "same_formats", typeof(Merge), nameof(Merge.Same),
                e => Merge.Same(LineRequired(e, "a"), Int(e, "a_format"), LineRequired(e, "b"), Int(e, "b_format"))),
            new Operation("merge", "same_as_ancestor", typeof(Merge), nameof(Merge.SameAsAncestor),
                e => Merge.SameAsAncestor(LineRequired(e, "line"), Int(e, "line_format"), LineRequired(e, "ancestor"), Int(e, "ancestor_format"))),

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
                    Bool(e, "branch_frozen"),
                    Bool(e, "capture_only"))),
            new Operation("badges", "in_listing", typeof(Badges), nameof(Badges.InListing),
                e => Badges.InListing(Bool(e, "installed"), NullableBool(e, "yours_is_main"))),
            new Operation("badges", "format", typeof(Badges), nameof(Badges.Format),
                e => new List<Badge> { Badges.Format(Int(e, "file_format"), NullableInt(e, "reads_up_to")) }),

            // ── translation_format ────────────────────────────────────────────
            new Operation("translation_format", "announced", typeof(TranslationFormat), nameof(TranslationFormat.Announced),
                e => TranslationFormat.Announced(Str(e, "value"))),
            new Operation("translation_format", "of_file", typeof(TranslationFormat), nameof(TranslationFormat.OfFile),
                e => TranslationFormat.OfFile(NullableInt(e, "engine_version"))),
            new Operation("translation_format", "of_content", typeof(TranslationFormat), nameof(TranslationFormat.OfContent),
                e => TranslationFormat.OfContent(Bool(e, "holds_game_lists"), ValuesOf(Lines(e, "lines")))),
            new Operation("translation_format", "reads", typeof(TranslationFormat), nameof(TranslationFormat.Reads),
                e => TranslationFormat.Reads(Int(e, "reads_up_to"), Int(e, "file_format"))),

            // ── line_markers ──────────────────────────────────────────────────
            new Operation("line_markers", "link_numbers", typeof(LineMarkers), nameof(LineMarkers.LinkNumbers),
                e => LineMarkers.LinkNumbers(Str(e, "text"))),
            new Operation("line_markers", "closed_slots", typeof(LineMarkers), nameof(LineMarkers.ClosedSlots),
                e => LineMarkers.ClosedSlots(Str(e, "text"))),
            new Operation("line_markers", "open_slots", typeof(LineMarkers), nameof(LineMarkers.OpenSlots),
                e => LineMarkers.OpenSlots(Str(e, "text"))),
            new Operation("line_markers", "open_slot_ref", typeof(LineMarkers), nameof(LineMarkers.OpenSlotRef),
                e => LineMarkers.OpenSlotRef(Str(e, "agrees"))),

            // ── link_reading ──────────────────────────────────────────────────
            // `casing: first-letter` hands in the socle's own Casing (spec/translation-file/casing.md).
            new Operation("link_reading", "read", typeof(LinkReading), nameof(LinkReading.Read),
                e =>
                {
                    bool casing = Str(e, "casing") == "first-letter";
                    return LinkReading.Read(LinkRequired(e, "link"), LineLookup(e, "entries"),
                        casing ? Casing.UpperFirst! : (Func<string, string>?)null,
                        casing ? Casing.LowerFirst! : (Func<string, string>?)null);
                }),
            new Operation("link_reading", "needs_review", typeof(LinkReading), nameof(LinkReading.NeedsReview),
                e => LinkReading.NeedsReview(LinkRequired(e, "link"), new LinkRead
                {
                    Text = Str(e, "read_text") ?? "",
                    Source = EnumRequired<LinkSource>(e, "source"),
                })),

            // ── casing ────────────────────────────────────────────────────────
            new Operation("casing", "upper_first", typeof(Casing), nameof(Casing.UpperFirst),
                e => Casing.UpperFirst(Str(e, "text"))),
            new Operation("casing", "lower_first", typeof(Casing), nameof(Casing.LowerFirst),
                e => Casing.LowerFirst(Str(e, "text"))),
            new Operation("casing", "lower", typeof(Casing), nameof(Casing.Lower),
                e => Casing.Lower(Str(e, "text"))),

            // ── word_glue / link_finding ──────────────────────────────────────
            new Operation("link_finding", "written_in", typeof(LinkFinding), nameof(LinkFinding.WrittenIn),
                e =>
                {
                    var writings = new List<LinkFinding.LinkWriting>();
                    foreach (var item in e.GetProperty("writings").EnumerateArray())
                        writings.Add(new LinkFinding.LinkWriting
                        {
                            Words = item.GetProperty("words").GetString()!,
                            Form = item.TryGetProperty("form", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null,
                        });
                    List<KeyValuePair<int, int>>? claimed = null;
                    if (e.TryGetProperty("claimed", out var c) && c.ValueKind == JsonValueKind.Array)
                    {
                        claimed = new List<KeyValuePair<int, int>>();
                        foreach (var span in c.EnumerateArray()) claimed.Add(new KeyValuePair<int, int>(span[0].GetInt32(), span[1].GetInt32()));
                    }
                    var places = new List<Dictionary<string, object?>>();
                    foreach (var p in LinkFinding.WrittenIn(Str(e, "text") ?? "", writings, claimed))
                        places.Add(new Dictionary<string, object?> { ["writing"] = p.Writing, ["start"] = p.Start, ["end"] = p.End, ["case"] = p.Case });
                    return places;
                }),
            new Operation("link_finding", "writings_of", typeof(LinkFinding), nameof(LinkFinding.WritingsOf),
                e =>
                {
                    var list = new List<Dictionary<string, object?>>();
                    foreach (var x in LinkFinding.WritingsOf(LineRequired(e, "line"))) list.Add(new Dictionary<string, object?> { ["words"] = x.Words, ["form"] = x.Form });
                    return list;
                }),
            new Operation("link_finding", "written_among", typeof(LinkFinding), nameof(LinkFinding.WrittenAmong),
                e =>
                {
                    var others = new List<LinkFinding.OtherTerm>();
                    foreach (var o in e.GetProperty("others").EnumerateArray())
                        others.Add(new LinkFinding.OtherTerm { Key = o.GetProperty("key").GetString()!, Writings = WritingsOf(o.GetProperty("writings")) });
                    List<KeyValuePair<int, int>>? claimed = null;
                    if (e.TryGetProperty("claimed", out var c) && c.ValueKind == JsonValueKind.Array)
                    {
                        claimed = new List<KeyValuePair<int, int>>();
                        foreach (var span in c.EnumerateArray()) claimed.Add(new KeyValuePair<int, int>(span[0].GetInt32(), span[1].GetInt32()));
                    }
                    var places = new List<Dictionary<string, object?>>();
                    foreach (var p in LinkFinding.WrittenAmong(Str(e, "text") ?? "", Str(e, "term_key") ?? "", WritingsOf(e.GetProperty("writings")), others, claimed))
                        places.Add(new Dictionary<string, object?> { ["writing"] = p.Writing, ["start"] = p.Start, ["end"] = p.End, ["case"] = p.Case });
                    return places;
                }),
            new Operation("link_finding", "cited_whole", typeof(LinkFinding), nameof(LinkFinding.CitedWhole),
                e =>
                {
                    var keys = new List<string>();
                    foreach (var k in e.GetProperty("terms").EnumerateArray()) keys.Add(k.GetString()!);
                    List<string>? refused = null;
                    if (e.TryGetProperty("unlinked", out var u) && u.ValueKind == JsonValueKind.Array)
                    {
                        refused = new List<string>();
                        foreach (var k in u.EnumerateArray()) refused.Add(k.GetString()!);
                    }
                    return LinkFinding.CitedWhole(TermIndex.OfKeys(keys), Str(e, "words") ?? "", refused);
                }),
            new Operation("link_finding", "cites", typeof(LinkFinding), nameof(LinkFinding.Cites),
                e =>
                {
                    var keys = new List<string>();
                    foreach (var k in e.GetProperty("terms").EnumerateArray()) keys.Add(k.GetString()!);
                    List<string>? refused = null;
                    if (e.TryGetProperty("unlinked", out var u) && u.ValueKind == JsonValueKind.Array)
                    {
                        refused = new List<string>();
                        foreach (var k in u.EnumerateArray()) refused.Add(k.GetString()!);
                    }
                    var places = new List<Dictionary<string, object?>>();
                    foreach (var c in LinkFinding.Cites(TermIndex.OfKeys(keys), Str(e, "source") ?? "", refused))
                        places.Add(new Dictionary<string, object?> { ["term"] = c.Key, ["start"] = c.Start, ["end"] = c.End });
                    return places;
                }),
            new Operation("word_glue", "is_glued", typeof(WordGlue), nameof(WordGlue.IsGlued),
                e => WordGlue.IsGlued(Int(e, "code_point"))),
            new Operation("word_glue", "edge_before", typeof(WordGlue), nameof(WordGlue.EdgeBefore),
                e => WordGlue.EdgeBefore(Str(e, "text") ?? "", Int(e, "index"))),
            new Operation("word_glue", "edge_after", typeof(WordGlue), nameof(WordGlue.EdgeAfter),
                e => WordGlue.EdgeAfter(Str(e, "text") ?? "", Int(e, "index"))),
            new Operation("link_finding", "find", typeof(LinkFinding), nameof(LinkFinding.Find),
                e => LinkFinding.Find(TermIndex.Build(Lines(e, "terms")), Str(e, "text") ?? "", Str(e, "source") ?? "", Line(e, "before"))),

            // ── game_tokens ────────────────────────────────────────────────────────
            new Operation("game_tokens", "is_valid", typeof(GameTokenList), nameof(GameTokenList.IsValid),
                e => GameTokenList.IsValid(Str(e, "token"))),
            new Operation("game_tokens", "read", typeof(GameTokenList), nameof(GameTokenList.Read),
                e => GameTokenList.Read(TokenArray(e))),
            new Operation("game_tokens", "canonical", typeof(GameTokenList), nameof(GameTokenList.Canonical),
                e => GameTokenList.Canonical(TokenArray(e)!.Select(t => t ?? "").ToList())),
            new Operation("game_tokens", "refusal", typeof(GameTokenList), nameof(GameTokenList.Refusal),
                e => Refusal(GameTokenList.Refusal(Str(e, "token")))),
            new Operation("game_tokens", "refusal_of_list", typeof(GameTokenList), nameof(GameTokenList.Refusal),
                e => Refusal(GameTokenList.Refusal(TokenArray(e)!.Select(t => t ?? "").ToList()))),

            // ── line_edit ──────────────────────────────────────────────────────────
            new Operation("line_edit", "run", typeof(LineEditing), LineEditJson.Methods, LineEditJson.Run),
            new Operation("line_edit", "term_choices", typeof(LineEditing), nameof(LineEditing.TermChoices), LineEditJson.Choices),
            new Operation("line_edit", "tag_after_person", typeof(LineEditing), nameof(LineEditing.TagAfterPerson),
                e => LineEditing.TagAfterPerson(Str(e, "stored"))),

            // ── negotiation ────────────────────────────────────────────────────────
            new Operation("negotiation", "run", typeof(Negotiation), new[] { nameof(Negotiation.ForgetIfChanged), nameof(Negotiation.Concede) },
                e =>
                {
                    var negotiation = new Negotiation();
                    negotiation.ForgetIfChanged("http://server|model");
                    var states = new List<object?>();
                    foreach (var step in e.GetProperty("steps").EnumerateArray())
                    {
                        var map = new Dictionary<string, object?>();
                        if (step.TryGetProperty("refusal", out var refusal))
                            map["concession"] = negotiation.Concede(refusal.GetString(), Bool(step, "structured"), out _).ToString();
                        else if (step.TryGetProperty("forget", out var forget))
                            negotiation.ForgetIfChanged(forget.GetString()!);
                        map["token_field"] = negotiation.TokenField;
                        map["send_temperature"] = negotiation.SendTemperature;
                        map["send_seed"] = negotiation.SendSeed;
                        map["reasoning_effort"] = negotiation.ReasoningEffort;
                        states.Add(map);
                    }
                    return states;
                }),
            new Operation("negotiation", "is_about_our_request", typeof(Negotiation), nameof(Negotiation.IsAboutOurRequest),
                e => Negotiation.IsAboutOurRequest(Int(e, "status"))),

            // ── template_uses ──────────────────────────────────────────────────────
            new Operation("template_uses", "key", typeof(TemplateUses), nameof(TemplateUses.Key),
                e => TemplateUses.Key(Str(e, "template_id")!, Str(e, "list")!)),
            new Operation("template_uses", "template_of", typeof(TemplateUses), nameof(TemplateUses.TemplateOf),
                e =>
                {
                    string template = TemplateUses.TemplateOf(Str(e, "key")!, out string? list);
                    return new Dictionary<string, object?> { ["template"] = template, ["list"] = list };
                }),
            new Operation("template_uses", "for", typeof(TemplateUses), nameof(TemplateUses.For),
                e =>
                {
                    var use = TemplateUses.For(LineRequired(e, "line").Use, Str(e, "template_id")!, Str(e, "list")!);
                    if (!use.HasValue) return null;
                    if (use.Value.ByClass != null && use.Value.ByClass.Count > 0)
                    {
                        var map = new Dictionary<string, object?>();
                        foreach (var p in use.Value.ByClass) map[p.Key] = p.Value;
                        return map;
                    }
                    return use.Value.Form;
                }),

            // ── list_join ──────────────────────────────────────────────────────────
            new Operation("list_join", "join", typeof(ListJoin), nameof(ListJoin.Join),
                e => ListJoin.Join(EntryArray(e), Str(e, "between"), Str(e, "join_case"))),
            new Operation("list_join", "is_case", typeof(ListJoin), nameof(ListJoin.IsCase),
                e => ListJoin.IsCase(Str(e, "join_case"))),

            // ── game_names ────────────────────────────────────────────────────────
            new Operation("game_names", "same", typeof(GameNames), nameof(GameNames.Flat),
                e => { var x = GameNames.Flat(Str(e, "a")); return x.Length > 0 && x == GameNames.Flat(Str(e, "b")); }),
            new Operation("game_names", "which", typeof(GameNames), nameof(GameNames.Which),
                e =>
                {
                    var candidates = new List<string>();
                    foreach (var item in e.GetProperty("candidates").EnumerateArray()) candidates.Add(item.GetString()!);
                    var match = GameNames.Which(candidates, Str(e, "asked")!);
                    return new Dictionary<string, object?> { ["chosen"] = new List<int>(match.Chosen), ["ambiguous"] = match.Ambiguous };
                }),
            new Operation("game_names", "flat", typeof(GameNames), nameof(GameNames.Flat),
                e => GameNames.Flat(Str(e, "name"))),

            // ── names ────────────────────────────────────────────────────────
            new Operation("names", "fold", typeof(Names), nameof(Names.Fold),
                e => Names.Fold(Str(e, "text") ?? "")),
            new Operation("names", "fold_code_point", typeof(Names), nameof(Names.Fold),
                e => Names.Fold(Int(e, "code_point"))),
            new Operation("names", "equals", typeof(Names), nameof(Names.Equals),
                e => Names.Equals(Str(e, "a"), Str(e, "b"))),
            new Operation("names", "index_of", typeof(Names), nameof(Names.IndexOf),
                e => Names.IndexOf(Str(e, "text"), Str(e, "part") ?? "")),
            new Operation("names", "compare", typeof(Names), nameof(Names.Compare),
                e => Math.Sign(Names.Compare(Str(e, "a"), Str(e, "b")))),

            // ── unicode_categories ────────────────────────────────────────────
            new Operation("unicode_categories", "of", typeof(UnicodeCategories), nameof(UnicodeCategories.Of),
                e => UnicodeCategories.Of(Int(e, "code_point"))),
            new Operation("unicode_categories", "of_in_text", typeof(UnicodeCategories), nameof(UnicodeCategories.Of),
                e => UnicodeCategories.Of(Str(e, "text") ?? "", Int(e, "index"))),
            new Operation("unicode_categories", "is_letter", typeof(UnicodeCategories), nameof(UnicodeCategories.IsLetter),
                e => UnicodeCategories.IsLetter(Int(e, "code_point"))),
            new Operation("unicode_categories", "is_mark", typeof(UnicodeCategories), nameof(UnicodeCategories.IsMark),
                e => UnicodeCategories.IsMark(Int(e, "code_point"))),
            new Operation("unicode_categories", "is_letter_or_digit", typeof(UnicodeCategories), nameof(UnicodeCategories.IsLetterOrDigit),
                e => UnicodeCategories.IsLetterOrDigit(Int(e, "code_point"))),
            new Operation("unicode_categories", "is_number", typeof(UnicodeCategories), nameof(UnicodeCategories.IsNumber),
                e => UnicodeCategories.IsNumber(Int(e, "code_point"))),

            // ── key_shape / key_chars ─────────────────────────────────────────
            new Operation("key_shape", "of", typeof(KeyShape), nameof(KeyShape.Of),
                e =>
                {
                    string? key = KeyShape.Of(Str(e, "text"), Bool(e, "normalize_numbers"), out var numbers);
                    return new Dictionary<string, object?> { ["key"] = key, ["numbers"] = JsonValues.Canon(numbers) };
                }),
            new Operation("key_shape", "stored", typeof(KeyShape), nameof(KeyShape.Stored),
                e => KeyShape.Stored(Str(e, "text"))),
            new Operation("key_shape", "restore", typeof(KeyShape), nameof(KeyShape.Restore),
                e => KeyShape.Restore(Str(e, "text"), Strings(e, "numbers"))),
            new Operation("key_shape", "restore_by_slot", typeof(KeyShape), nameof(KeyShape.Restore),
                e =>
                {
                    var bySlot = new Dictionary<int, string>();
                    foreach (var p in e.GetProperty("numbers").EnumerateObject()) bySlot[int.Parse(p.Name)] = p.Value.GetString()!;
                    return KeyShape.Restore(Str(e, "text"), bySlot);
                }),
            new Operation("key_shape", "unbrace", typeof(KeyShape), nameof(KeyShape.Unbrace),
                e => KeyShape.Unbrace(Str(e, "key")!)),
            new Operation("key_shape", "rebrace", typeof(KeyShape), nameof(KeyShape.Rebrace),
                e => KeyShape.Rebrace(Str(e, "translation")!, Str(e, "braced_key")!)),
            new Operation("key_chars", "is_digit", typeof(KeyChars), nameof(KeyChars.IsDigit),
                e => KeyChars.IsDigit((char)Int(e, "code_unit"))),
            new Operation("key_chars", "is_space", typeof(KeyChars), nameof(KeyChars.IsSpace),
                e => KeyChars.IsSpace((char)Int(e, "code_unit"))),
            new Operation("key_chars", "trim", typeof(KeyChars), nameof(KeyChars.Trim),
                e => KeyChars.Trim(Str(e, "text"))),

            // ── line_endings ──────────────────────────────────────────────────
            new Operation("line_endings", "to_lf", typeof(LineEndings), nameof(LineEndings.ToLf),
                e => LineEndings.ToLf(Str(e, "text"))),
            new Operation("line_endings", "of_line", typeof(LineEndings), nameof(LineEndings.Of),
                e => FullLine(LineEndings.Of(LineRequired(e, "line")))),

            // ── selection_action ────────────────────────────────────────────────────────
            new Operation("selection_action", "for", typeof(SelectionAction), nameof(SelectionAction.For),
                e =>
                {
                    var keys = new List<string>();
                    foreach (var k in e.GetProperty("terms").EnumerateArray()) keys.Add(k.GetString()!);
                    var choice = SelectionAction.For(Str(e, "words"), LineLookup(e, "file"), Int(e, "file_format", 2), keys.Count == 0 ? null : TermIndex.OfKeys(keys));
                    if (!choice.HasValue) return null;
                    var result = new Dictionary<string, object?> { ["action"] = choice.Value.Action, ["key"] = choice.Value.Key };
                    if (choice.Value.Existing.HasValue) result["existing"] = TermEditJson.Present(choice.Value.Existing.Value);
                    return result;
                }),

            // ── term_edit ─────────────────────────────────────────────────────
            new Operation("term_edit", "open", typeof(TermEdit), nameof(TermEdit.Open),
                e => { var d = TermEdit.Open(Str(e, "key") ?? "", Line(e, "line"), Int(e, "file_format", 2)); return d.HasValue ? TermEditJson.Json(d.Value) : null; }),
            new Operation("term_edit", "open_new", typeof(TermEdit), nameof(TermEdit.OpenNew),
                e => TermEditJson.Json(TermEdit.OpenNew(Str(e, "key"), Str(e, "selected"), Line(e, "existing")))),
            new Operation("term_edit", "rebase", typeof(TermEdit), nameof(TermEdit.Rebase),
                e => TermEditJson.Json(TermEdit.Rebase(TermEditJson.DraftOf(e, "state"), LineRequired(e, "line")))),
            new Operation("term_edit", "set_key", typeof(TermEdit), nameof(TermEdit.SetKey),
                e => TermEditJson.Json(TermEdit.SetKey(TermEditJson.DraftOf(e, "state"), Str(e, "text"), Line(e, "existing")))),
            new Operation("term_edit", "set_word", typeof(TermEdit), nameof(TermEdit.SetWord),
                e => TermEditJson.Json(TermEdit.SetWord(TermEditJson.DraftOf(e, "state"), Str(e, "text")))),
            new Operation("term_edit", "set_form", typeof(TermEdit), nameof(TermEdit.SetForm),
                e => TermEditJson.Json(TermEdit.SetForm(TermEditJson.DraftOf(e, "state"), Str(e, "id") ?? "", Str(e, "text")))),
            new Operation("term_edit", "add_form", typeof(TermEdit), nameof(TermEdit.AddForm),
                e => TermEditJson.Json(TermEdit.AddForm(TermEditJson.DraftOf(e, "state")))),
            new Operation("term_edit", "remove_form", typeof(TermEdit), nameof(TermEdit.RemoveForm),
                e => TermEditJson.Json(TermEdit.RemoveForm(TermEditJson.DraftOf(e, "state"), Str(e, "id") ?? ""))),
            new Operation("term_edit", "blockers", typeof(TermEdit), nameof(TermEdit.Blockers),
                e =>
                {
                    var b = TermEdit.Blockers(TermEditJson.DraftOf(e, "state"), Str(e, "id") ?? "", Lines(e, "file"));
                    return new Dictionary<string, object?> { ["sentences"] = new List<string>(b.Sentences), ["template"] = b.Template };
                }),
            new Operation("term_edit", "changes", typeof(TermEdit), nameof(TermEdit.Changes),
                e => TermEditJson.Json(TermEdit.Changes(TermEditJson.DraftOf(e, "state")))),
            new Operation("term_edit", "count", typeof(TermEdit), nameof(TermEdit.Count),
                e => TermEdit.Count(TermEditJson.DraftOf(e, "state"))),
            new Operation("term_edit", "problem", typeof(TermEdit), nameof(TermEdit.Problem),
                e => TermEdit.Problem(TermEditJson.DraftOf(e, "state"), LineLookup(e, "lookup"))),
            new Operation("term_edit", "after", typeof(TermEdit), nameof(TermEdit.After),
                e => TermEditJson.Present(TermEdit.After(TermEditJson.DraftOf(e, "state"), LineRequired(e, "original")))),
            new Operation("term_edit", "effect", typeof(TermEdit), nameof(TermEdit.Effect),
                e =>
                {
                    var effect = TermEdit.Effect(TermEditJson.DraftOf(e, "state"), LineRequired(e, "original"), Lines(e, "file"), TermEditJson.Lists(e));
                    var uses = new List<Dictionary<string, object?>>();
                    foreach (var u in effect.Uses)
                        uses.Add(new Dictionary<string, object?> { ["key"] = u.Key, ["kind"] = u.Kind.ToString(), ["text_changes"] = u.TextChanges, ["needs_review_after"] = u.NeedsReviewAfter });
                    return new Dictionary<string, object?> { ["texts"] = effect.Texts, ["review"] = effect.Review, ["uses"] = uses, ["forms_to_redo"] = new List<string>(effect.FormsToRedo) };
                }),
            new Operation("term_edit", "write", typeof(TermEdit), nameof(TermEdit.Write),
                e => { var w = TermEdit.Write(TermEditJson.DraftOf(e, "state"), LineRequired(e, "original")); return w.HasValue ? TermEditJson.Json(w.Value) : null; }),

            // ── term_changes ──────────────────────────────────────────────────
            new Operation("term_changes", "impact", typeof(TermChanges), nameof(TermChanges.Impact),
                e =>
                {
                    var lists = new List<KeyValuePair<string, IReadOnlyList<string>>>();
                    if (e.TryGetProperty("lists", out var l) && l.ValueKind == JsonValueKind.Object)
                        foreach (var p in l.EnumerateObject())
                            lists.Add(new KeyValuePair<string, IReadOnlyList<string>>(p.Name, Strings(l, p.Name)));
                    return TermChanges.Impact(Str(e, "entry_key")!, Lines(e, "lines"), lists, Line(e, "after"));
                }),

            // ── text_diff / passage_border ────────────────────────────────────
            new Operation("text_diff", "hunk", typeof(TextDiff), nameof(TextDiff.Hunk),
                e => TextDiff.Hunk(Str(e, "old") ?? "", Str(e, "new") ?? "")),
            new Operation("passage_border", "touches", typeof(PassageBorder), nameof(PassageBorder.Touches),
                e => PassageBorder.Touches(Int(e, "hunk_start"), Int(e, "hunk_end"), Int(e, "passage_start"), Int(e, "passage_end"), Bool(e, "open"))),
            new Operation("passage_border", "straddles", typeof(PassageBorder), nameof(PassageBorder.Straddles),
                e => PassageBorder.Straddles(Int(e, "hunk_start"), Int(e, "hunk_end"), Int(e, "passage_start"), Int(e, "passage_end"), Bool(e, "open"))),
            new Operation("passage_border", "stays_open", typeof(PassageBorder), nameof(PassageBorder.StaysOpen),
                e => PassageBorder.StaysOpen(Int(e, "hunk_start"), Int(e, "hunk_end"), Int(e, "passage_start"), Int(e, "passage_end"), Bool(e, "open"))),
            new Operation("passage_border", "absorbed_at", typeof(PassageBorder), nameof(PassageBorder.AbsorbedAt),
                e => PassageBorder.AbsorbedAt(Str(e, "text") ?? "", Int(e, "start"), Int(e, "end"), Str(e, "writing") ?? "")),

            // ── link_editing ──────────────────────────────────────────────────
            new Operation("link_editing", "show", typeof(LinkEditing), nameof(LinkEditing.Show),
                e => LinkEditing.Show(LineRequired(e, "line"), Int(e, "file_format", 2), LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "view", typeof(LinkEditing), nameof(LinkEditing.View),
                e => LinkEditing.View(StateOf(e, "state"), LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "step", typeof(LinkEditing), nameof(LinkEditing.Step),
                e =>
                {
                    var state = StateOf(e, "state");
                    foreach (var text in Strings(e, "steps")) state = LinkEditing.Step(state, text);
                    return state;
                }),
            new Operation("link_editing", "settle", typeof(LinkEditing), nameof(LinkEditing.Settle),
                e => LinkEditing.Settle(StateOf(e, "state"), LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "next_question", typeof(LinkEditing), nameof(LinkEditing.NextQuestion),
                e => LinkEditing.NextQuestion(StateOf(e, "state"), LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "answer", typeof(LinkEditing), nameof(LinkEditing.Answer),
                e => LinkEditing.Answer(StateOf(e, "state"), QuestionOf(e, "question"), EnumRequired<EditAnswer>(e, "answer"), LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "unlink", typeof(LinkEditing), nameof(LinkEditing.Unlink),
                e => LinkEditing.Unlink(StateOf(e, "state"), Int(e, "passage"), LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "link_to", typeof(LinkEditing), nameof(LinkEditing.LinkTo),
                e => LinkEditing.LinkTo(StateOf(e, "state"), Int(e, "start"), Int(e, "length"), Str(e, "entry")!, Str(e, "form"),
                                        LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "mark_as_read", typeof(LinkEditing), nameof(LinkEditing.MarkAsRead),
                e => LinkEditing.MarkAsRead(StateOf(e, "state"), LineLookup(e, "entries"), Up(e), Low(e))),
            new Operation("link_editing", "resume", typeof(LinkEditing), nameof(LinkEditing.Resume),
                e => LinkEditing.Resume(StateOf(e, "original"), StateOf(e, "current"), LineLookup(e, "entries"), Up(e), Low(e))),

            // ── template_ids ──────────────────────────────────────────────────
            new Operation("template_ids", "of", typeof(TemplateIds), nameof(TemplateIds.Of),
                e => TemplateIds.Of(Str(e, "key")!)),
            new Operation("template_ids", "collision", typeof(TemplateIds), nameof(TemplateIds.Collision),
                e => TemplateIds.Collision(Strings(e, "keys"))?.Code),

            // ── line_writes ───────────────────────────────────────────────────
            new Operation("line_writes", "refusal", typeof(LineWrites), nameof(LineWrites.Refusal),
                e =>
                {
                    var lists = new HashSet<string>(Strings(e, "lists"), StringComparer.Ordinal);
                    return LineWrites.Refusal(Str(e, "key")!, LineRequired(e, "line"), Int(e, "file_format"),
                        LineLookup(e, "entries"), lists.Contains, Bool(e, "by_machine"), Line(e, "previous"))?.Code;
                }),

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
            new Operation("placeholders", "edit_faults", typeof(Placeholders), nameof(Placeholders.EditFaults),
                e => Placeholders.EditFaults(Str(e, "source"), Str(e, "edited")).Select(f =>
                {
                    var fact = new Dictionary<string, object?> { ["kind"] = f.Kind.ToString().ToLowerInvariant() };
                    if (f.Kind == PlaceholderFaultKind.Sequence) fact["sequence"] = f.Sequence;
                    else
                    {
                        fact["token"] = f.Token;
                        if (f.Kind == PlaceholderFaultKind.Count) { fact["found"] = f.Found; fact["expected"] = f.Expected; }
                    }
                    return (object?)fact;
                }).ToList()),
            new Operation("placeholders", "model_sentence", typeof(Placeholders), nameof(Placeholders.ModelSentence),
                e =>
                {
                    var f = e.GetProperty("fault");
                    string kind = Str(f, "kind")!;
                    return Placeholders.ModelSentence(new PlaceholderFault
                    {
                        Kind = kind == "sequence" ? PlaceholderFaultKind.Sequence : kind == "count" ? PlaceholderFaultKind.Count : PlaceholderFaultKind.Invented,
                        Sequence = Str(f, "sequence"),
                        Token = Str(f, "token"),
                        Found = f.TryGetProperty("found", out var found) ? found.GetInt32() : 0,
                        Expected = f.TryGetProperty("expected", out var expected) ? expected.GetInt32() : 0,
                    });
                }),
            new Operation("placeholders", "repair_trailing_breaks", typeof(Placeholders), nameof(Placeholders.RepairTrailingBreaks),
                e => Placeholders.RepairTrailingBreaks(Str(e, "source") ?? "", Str(e, "translation") ?? "")),
            new Operation("placeholders", "correction", typeof(Placeholders), nameof(Placeholders.Correction),
                e => Placeholders.Correction(Strings(e, "errors"), Strings(e, "frozen"))),
            new Operation("placeholders", "mandatory_sequences", typeof(Placeholders), nameof(Placeholders.MandatorySequences),
                e => Placeholders.MandatorySequences(Strings(e, "frozen"))),
            new Operation("placeholders", "labels", typeof(Placeholders), nameof(Placeholders.Labels),
                e => Placeholders.Labels(Str(e, "text") ?? "")),
            new Operation("placeholders", "labels_of", typeof(Placeholders), nameof(Placeholders.LabelsOf),
                e => Placeholders.LabelsOf(Str(e, "text") ?? "").Select(l => (object?)new Dictionary<string, object?>
                    { ["start"] = l.Start, ["length"] = l.Length, ["text"] = l.Text, ["inside"] = l.Inside }).ToList()),
            new Operation("placeholders", "tokens", typeof(Placeholders), nameof(Placeholders.Tokens),
                e => Placeholders.Tokens(Str(e, "text") ?? "")),
            new Operation("placeholders", "invented", typeof(Placeholders), nameof(Placeholders.Invented),
                e => Placeholders.Invented(Str(e, "source") ?? "", Str(e, "translation"))),
            new Operation("placeholders", "markup_kept_by_edit", typeof(Markup), nameof(Markup.KeptByEdit),
                e => Markup.KeptByEdit(Str(e, "source") ?? "", Str(e, "edited") ?? "")),
            new Operation("placeholders", "tally", typeof(Placeholders), nameof(Placeholders.Tally),
                e => Placeholders.Tally(Str(e, "text") ?? "")),
            new Operation("placeholders", "length_at", typeof(Placeholders), nameof(Placeholders.LengthAt),
                e => Placeholders.LengthAt(Str(e, "text") ?? "", Int(e, "index"))),
            new Operation("placeholders", "max_attempts", typeof(Placeholders), nameof(Placeholders.MaxAttempts),
                e => Placeholders.MaxAttempts),

            // ── answers ───────────────────────────────────────────────────────
            new Operation("answers", "read", typeof(Answers), nameof(Answers.Read),
                e => Answers.Read(Str(e, "answer"))),
            new Operation("answers", "read_rating", typeof(Answers), nameof(Answers.ReadRating),
                e => Answers.ReadRating(Str(e, "answer"))),
            new Operation("answers", "clean", typeof(Answers), nameof(Answers.Clean),
                e => Answers.Clean(Str(e, "answer")!, Str(e, "source"))),
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
        private static List<LinkFinding.LinkWriting> WritingsOf(JsonElement array)
        {
            var writings = new List<LinkFinding.LinkWriting>();
            foreach (var item in array.EnumerateArray())
                writings.Add(new LinkFinding.LinkWriting { Words = item.GetProperty("words").GetString()!, Form = item.TryGetProperty("form", out var f) && f.ValueKind == JsonValueKind.String ? f.GetString() : null });
            return writings;
        }

        private static List<string?> EntryArray(JsonElement e)
        {
            var list = new List<string?>();
            if (e.TryGetProperty("entries", out var array) && array.ValueKind == JsonValueKind.Array)
                foreach (var item in array.EnumerateArray()) list.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
            return list;
        }

        private static List<string?>? TokenArray(JsonElement e)
        {
            if (!e.TryGetProperty("tokens", out var array) || array.ValueKind != JsonValueKind.Array) return null;
            var list = new List<string?>();
            foreach (var item in array.EnumerateArray()) list.Add(item.ValueKind == JsonValueKind.String ? item.GetString() : null);
            return list;
        }

        private static Dictionary<string, object?>? Refusal(LineRefusal? refusal) =>
            refusal.HasValue ? new Dictionary<string, object?> { ["code"] = refusal.Value.Code } : null;

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
        /// <summary>The three fact sheets a standing is composed from, each key the snake_case of its field.</summary>
        private static LocalFacts LocalFactsOf(JsonElement e) => new LocalFacts
        {
            Lines = Int(e, "lines"),
            LocalChanges = Int(e, "local_changes"),
            MetadataDirty = Bool(e, "metadata_dirty"),
            LastSyncedHash = Str(e, "last_synced"),
            ContentHash = Str(e, "content_hash"),
            ForkStillTheCopy = Bool(e, "fork_still_the_copy"),
            ConfirmedGame = GameChoiceOf(e, "confirmed_game"),
        };

        /// <summary>`{source, id, name}` — the game confirmed on this machine — or null when absent.</summary>
        private static GameChoice? GameChoiceOf(JsonElement e, string key) =>
            e.TryGetProperty(key, out var g) && g.ValueKind == JsonValueKind.Object
                ? new GameChoice(Str(g, "source") ?? "", Str(g, "id") ?? "", Str(g, "name") ?? "")
                : null;

        /// <summary>`{id, name, steam_id?, igdb_id?, rawg_id?}` — a lineage's game — or null when absent.</summary>
        private static LineageGame? LineageGameOf(JsonElement e, string key) =>
            e.TryGetProperty(key, out var g) && g.ValueKind == JsonValueKind.Object
                ? new LineageGame(Int(g, "id"), Str(g, "name") ?? "", Str(g, "steam_id"),
                                  NullableInt(g, "igdb_id"), NullableInt(g, "rawg_id"))
                : null;

        private static ServerFacts ServerFactsOf(JsonElement e) => new ServerFacts
        {
            Checked = Bool(e, "checked"),
            Exists = Bool(e, "exists"),
            IsOwner = Bool(e, "is_owner"),
            Role = EnumOf<LineageRole>(e, "role") ?? LineageRole.None,
            SiteId = NullableInt(e, "site_id"),
            Hash = Str(e, "server_hash"),
            Uploader = Str(e, "uploader"),
            MainUsername = Str(e, "main_username"),
            BranchesCount = Int(e, "branches_count"),
            BranchesWithWork = NullableInt(e, "branches_with_work"),
            LinesAvailable = NullableInt(e, "lines_available"),
            AcceptsBranches = NullableBool(e, "accepts_branches"),
            MainMissing = NullableBool(e, "main_missing"),
            MainAbandoned = NullableBool(e, "main_abandoned"),
            BranchFrozen = NullableBool(e, "branch_frozen"),
            MainIgnoring = NullableBool(e, "main_ignoring"),
            Status = Str(e, "status"),
            LinesChanged = NullableInt(e, "lines_changed"),
            LinesChangedHere = NullableInt(e, "lines_changed_here"),
            Game = LineageGameOf(e, "game"),
            GameSwitchPending = NullableBool(e, "game_switch_pending"),
        };

        private static AccountFacts AccountFactsOf(JsonElement e) => new AccountFacts
        {
            SignedIn = Bool(e, "signed_in"),
            Online = Bool(e, "online", fallback: true),
        };

        /// <summary>What is waiting, as the product that watches the translation reports it.</summary>
        private static SyncWork SyncWorkOf(JsonElement e) => new SyncWork
        {
            WaitingForAccount = Bool(e, "waiting_for_account"),
            HasLocalChanges = Bool(e, "has_local_changes"),
            HasMetadataChanges = Bool(e, "has_metadata_changes"),
            HasServerUpdate = Bool(e, "has_server_update"),
            NeedsMerge = Bool(e, "needs_merge"),
            HasMainUpdate = Bool(e, "has_main_update"),
            BranchesPendingReview = Int(e, "branches_pending_review"),
            NeverPublished = Bool(e, "never_published"),
        };

        /// <summary>A standing stated directly, for the questions asked of one.</summary>
        private static Standing StandingOf(JsonElement e) => new Standing
        {
            Publication = EnumRequired<Publication>(e, "publication"),
            Role = EnumOf<LineageRole>(e, "role") ?? LineageRole.None,
            Account = EnumOf<AccountStanding>(e, "account") ?? AccountStanding.Anonymous,
        };

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

        private static Func<string, string>? Up(JsonElement e) => Str(e, "casing") == "first-letter" ? Casing.UpperFirst! : (Func<string, string>?)null;

        private static Func<string, string>? Low(JsonElement e) => Str(e, "casing") == "first-letter" ? Casing.LowerFirst! : (Func<string, string>?)null;

        private static System.Collections.Generic.IEnumerable<TranslationLine> ValuesOf(System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, TranslationLine>> lines)
        {
            foreach (var l in lines) yield return l.Value;
        }

        public static Operation? Find(string rule, string op) =>
            ById.TryGetValue(rule + "/" + op, out var found) ? found : null;
    }
}
