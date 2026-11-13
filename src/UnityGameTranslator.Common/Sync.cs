using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// One translated line, reduced to what identifies its content.
    ///
    /// ⚠ **It carries what the FILE holds, not a tidied version of it**, and that distinction was
    /// found by measurement rather than by reading. The website hashes the file as written —
    /// `array_intersect_key($value, ['v','t'])` keeps whatever is there and nothing else — so a
    /// null value stays null, an absent tag stays absent, and a pre-tag entry stays a bare string.
    /// Normalising any of those produces a different document and therefore a different hash, for
    /// a file nobody touched.
    ///
    /// ⚠ The capture-order index "i" is the one thing deliberately dropped. It is presentation
    /// metadata — the web editors sort by it — and the website drops it for the same reason.
    /// </summary>
    public struct TranslationLine
    {
        /// <summary>The translation. Null is a real value and is written as JSON null.</summary>
        public string Value;

        /// <summary>Where it came from, or null when the entry has no "t" at all.</summary>
        public string Tag;

        /// <summary>
        /// True when the file holds `"key":"value"` — the format from before tags existed.
        ///
        /// ⚠ The mod turns these into {v,t} in its cache, so it hashes them as objects; the site
        /// hashes them as it finds them. A caller reading a FILE must say so here, or an old
        /// published translation will never match the hash the server issued for it.
        /// </summary>
        public bool BareString;

        // ── The fields of the format 2 (spec/translation-file/schema.json, $defs/lineV2) ─────────
        //
        // ⚠ ADDED BY ADDITION: a line that holds none of them is exactly what it was, and so is its
        // hash (ContentHash.Of). Null or empty means « the file holds no such field » — the hash
        // never writes a field that is not there, whatever the format the file claims.
        //
        // Who sets them is NOT this type's business: a reader of the format 1 never fills them, and
        // a writer that does must have checked LineWrites first.

        /// <summary>« This text names a thing of the game », set by a person or the machine. Written only when true.</summary>
        public bool Term;

        /// <summary>Written forms of this entry, id → text. An id is given once and never reused; "0" is the entry's own v and never listed.</summary>
        public IReadOnlyList<KeyValuePair<string, string>>? Forms;

        /// <summary>What each template takes from this entry, template id → form (or per class).</summary>
        public IReadOnlyList<KeyValuePair<string, LineUse>>? Use;

        /// <summary>The class this entry gives in each template it governs, template id → class name.</summary>
        public IReadOnlyList<KeyValuePair<string, string>>? Class;

        /// <summary>For a template line: how it was made — accepted, refused, line, repeated-name.</summary>
        public string? Oracle;

        /// <summary>The links of the sentence; the list position is the N of its <c>[!g*N]</c>.</summary>
        public IReadOnlyList<LineLink>? Links;

        /// <summary>Keys of entries a person said this sentence does NOT name.</summary>
        public IReadOnlyList<string>? Unlinked;

        /// <summary>An entry of the current form. Tag null means the file has no "t".</summary>
        public TranslationLine(string value, string tag)
        {
            // Not field by field: this struct grows by addition, and a field forgotten here would
            // be an error in one compiler and silence in another.
            this = default;
            Value = value;
            Tag = tag;
            BareString = false;
        }

        /// <summary>Does this line hold any field of the format 2?</summary>
        public bool HasFormat2Fields =>
            Term || !IsEmpty(Forms) || !IsEmpty(Use) || !IsEmpty(Class) || !string.IsNullOrEmpty(Oracle)
            || !IsEmpty(Links) || !IsEmpty(Unlinked);

        private static bool IsEmpty<T>(IReadOnlyList<T>? list) => list == null || list.Count == 0;

        /// <summary>An entry from before tags existed: the value stands alone.</summary>
        public static TranslationLine Bare(string value) =>
            new TranslationLine { Value = value, Tag = null, BareString = true };
    }

    /// <summary>
    /// The identity of a translation's content, computed the same way everywhere.
    ///
    /// ⚠ **THE WEBSITE IS THE REFERENCE**, as it is for <see cref="Quality"/>. This is a port of
    /// Translation::computeHash — the value it produces is what the API returns as file_hash, what
    /// the mod stores as _source.hash, and what every "is there an update" decision compares. A
    /// difference of one byte does not degrade anything: it makes every file look permanently out
    /// of sync, in both directions, for everybody.
    ///
    /// ⚠ **This library takes no JSON dependency, so the serialiser is here.** That is not a
    /// workaround, it is the safer side of the trade: the shape being serialised is tiny and fixed,
    /// while two general-purpose JSON writers agreeing on escaping — across PHP, Newtonsoft and
    /// System.Text.Json — is exactly the kind of assumption that holds until it does not. The rules
    /// below are pinned by frozen vectors in the check project.
    ///
    /// The rules, each of which the reference implementation needs:
    ///  · only the translated lines and _uuid; every metadata key (<see cref="TranslationFileKeys"/>)
    ///    is excluded;
    ///  · each line becomes {"v":…,"t":…}, in that order — and, only when the line holds them, the
    ///    fields of the format 2 AFTER those two, in the fixed order <see cref="WriteFormat2"/> lists
    ///    (a file that holds none of them keeps exactly the hash it always had);
    ///  · keys sorted ORDINALLY — byte order, which is what PHP's ksort does and what
    ///    StringComparer.Ordinal does. Culture-aware sorting puts "a" before "A" and would produce
    ///    a different document on a machine and the server;
    ///  · no whitespace anywhere;
    ///  · non-ASCII stays literal (PHP: JSON_UNESCAPED_UNICODE) and "/" stays literal
    ///    (JSON_UNESCAPED_SLASHES). Escaping either is valid JSON and the wrong bytes;
    ///  · SHA-256, hex, lowercase.
    ///
    /// Verified against a real 690-line published translation: the hash computed from the file on
    /// disk reproduces the file_hash the server issued for it.
    /// </summary>
    public static class ContentHash
    {
        /// <summary>The key the lineage identifier is written under. Part of the hashed content.</summary>
        public const string UuidKey = "_uuid";

        /// <summary>
        /// The content hash of a translation.
        /// </summary>
        /// <param name="lines">
        /// The translated lines only — <see cref="TranslationFileKeys.IsLine"/> decides, on a raw file.
        /// A metadata name met here is dropped anyway.
        /// </param>
        /// <param name="uuid">The lineage identifier, hashed alongside the lines.</param>
        public static string Of(IEnumerable<KeyValuePair<string, TranslationLine>> lines, string uuid)
        {
            var sorted = new SortedDictionary<string, TranslationLine>(StringComparer.Ordinal);

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line.Key == null || TranslationFileKeys.IsMetadataKey(line.Key)) continue;
                    sorted[line.Key] = line.Value;
                }
            }

            // The uuid takes part, and sorts among the lines rather than leading them: "_" sits
            // between the upper-case and lower-case letters in byte order, so it lands in the
            // middle of an ordinary file. Anything that appended it would produce another document.
            //
            // Bare, because it is a plain string in the file and in the mod's cache alike.
            sorted[UuidKey] = TranslationLine.Bare(uuid ?? string.Empty);

            var json = new StringBuilder();
            json.Append('{');

            bool first = true;
            foreach (var entry in sorted)
            {
                if (!first) json.Append(',');
                first = false;

                Write(json, entry.Key);
                json.Append(':');

                // Pre-tag format: the value stands alone, exactly as the file holds it.
                if (entry.Value.BareString)
                {
                    Write(json, entry.Value.Value);
                    continue;
                }

                // ⚠ Only the fields that are there. An absent tag is NOT written as "A": the
                // website keeps whatever the file has, so inventing a default here would make an
                // entry written without one hash differently on the two sides.
                //
                // ⚠ v first, then t. The website preserves the file's own order, and every writer
                // we have emits them this way; a file hand-edited into the other order would hash
                // differently there than here, and that is a limit worth knowing rather than a
                // case worth carrying a field for.
                json.Append("{\"v\":");
                Write(json, entry.Value.Value);

                if (entry.Value.Tag != null)
                {
                    json.Append(",\"t\":");
                    Write(json, entry.Value.Tag);
                }

                WriteFormat2(json, entry.Value);

                json.Append('}');
            }

            json.Append('}');

            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(json.ToString()));

                var hex = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++) hex.Append(hash[i].ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>
        /// The fields of the format 2, written AFTER "v" and "t", each only when the line holds it
        /// and it is not empty. A line with none of them writes nothing here: its document is the
        /// one it always was.
        ///
        /// 🔴 **The order is the contract, and the site copies it byte for byte**
        /// (Translation::computeHash). Fixed, never the file's own:
        ///
        ///   1. "term":true — only when true.
        ///   2. "forms":{id:text,…} — an object, ids sorted ORDINALLY (so "1","10","2": byte order,
        ///      never numeric — a PHP ksort on integer-looking keys sorts numerically and is wrong).
        ///   3. "use":{templateId: "form" | {class:"form",…},…} — ids sorted ordinally, and so are
        ///      the classes of an inner object. A value holding nothing is written null.
        ///   4. "class":{templateId:"className",…} — ids sorted ordinally.
        ///   5. "oracle":"…" — only when not empty.
        ///   6. "links":[{…},…] — an ARRAY in the line's own order (the position is the N of
        ///      [!g*N]); each link writes to, form, case, by, was in that order, only the ones held.
        ///   7. "unlinked":[{"to":…},…] — an array sorted ORDINALLY by key (a set: two writers that
        ///      list the same keys differently must not hash differently).
        ///
        /// ⚠ **An "object" stays an object even when its ids are numbers.** {"1":"a","2":"b"} is
        /// written as such; a language that decodes it into a list and writes it back as [..] has
        /// a different document.
        /// </summary>
        private static void WriteFormat2(StringBuilder json, TranslationLine line)
        {
            if (line.Term) json.Append(",\"term\":true");

            if (line.Forms != null && line.Forms.Count > 0)
            {
                json.Append(",\"forms\":");
                WriteMap(json, line.Forms);
            }

            if (line.Use != null && line.Use.Count > 0)
            {
                json.Append(",\"use\":{");
                bool first = true;
                foreach (var use in Sorted(line.Use))
                {
                    if (!first) json.Append(',');
                    first = false;
                    Write(json, use.Key);
                    json.Append(':');
                    if (use.Value.ByClass != null && use.Value.ByClass.Count > 0) WriteMap(json, use.Value.ByClass);
                    else Write(json, use.Value.Form);
                }
                json.Append('}');
            }

            if (line.Class != null && line.Class.Count > 0)
            {
                json.Append(",\"class\":");
                WriteMap(json, line.Class);
            }

            if (!string.IsNullOrEmpty(line.Oracle))
            {
                json.Append(",\"oracle\":");
                Write(json, line.Oracle);
            }

            if (line.Links != null && line.Links.Count > 0)
            {
                json.Append(",\"links\":[");
                for (int i = 0; i < line.Links.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    LineLink link = line.Links[i];
                    json.Append('{');
                    bool first = true;
                    WriteMember(json, ref first, "to", link.To);
                    WriteMember(json, ref first, "form", link.Form);
                    WriteMember(json, ref first, "case", link.Case);
                    WriteMember(json, ref first, "by", link.By);
                    WriteMember(json, ref first, "was", link.Was);
                    json.Append('}');
                }
                json.Append(']');
            }

            if (line.Unlinked != null && line.Unlinked.Count > 0)
            {
                var keys = new List<string>(line.Unlinked);
                keys.Sort(StringComparer.Ordinal);
                json.Append(",\"unlinked\":[");
                for (int i = 0; i < keys.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    json.Append("{\"to\":");
                    Write(json, keys[i]);
                    json.Append('}');
                }
                json.Append(']');
            }
        }

        /// <summary>One member of an object, written only when the file holds it (null = absent).</summary>
        private static void WriteMember(StringBuilder json, ref bool first, string name, string? value)
        {
            if (value == null) return;
            if (!first) json.Append(',');
            first = false;
            json.Append('"').Append(name).Append("\":");
            Write(json, value);
        }

        /// <summary>An object of strings, keys sorted ordinally.</summary>
        private static void WriteMap(StringBuilder json, IReadOnlyList<KeyValuePair<string, string>> map)
        {
            json.Append('{');
            bool first = true;
            foreach (var pair in Sorted(map))
            {
                if (!first) json.Append(',');
                first = false;
                Write(json, pair.Key);
                json.Append(':');
                Write(json, pair.Value);
            }
            json.Append('}');
        }

        /// <summary>A copy in byte order of the keys — the order PHP's ksort (SORT_STRING) gives, not a culture's.</summary>
        private static List<KeyValuePair<string, T>> Sorted<T>(IReadOnlyList<KeyValuePair<string, T>> map)
        {
            var copy = new List<KeyValuePair<string, T>>(map);
            // List.Sort is not stable, but two equal keys cannot be told apart in a JSON object anyway.
            copy.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return copy;
        }

        /// <summary>
        /// One JSON string, escaped exactly as the reference implementations escape it.
        ///
        /// ⚠ Only the five short forms and \u00xx for the remaining control characters. Anything
        /// above 0x1F is written as itself — including the solidus, which JSON allows to be escaped
        /// and which PHP escapes unless told not to. Both spellings parse; only one hashes right.
        /// </summary>
        private static void Write(StringBuilder json, string? value)
        {
            // ⚠ Null is the JSON literal, NOT an empty string. The website keeps a null "v" as
            // null; writing "" instead produced a different document for a file nobody had
            // touched, which is the whole class of bug this type exists to avoid.
            if (value == null)
            {
                json.Append("null");
                return;
            }

            json.Append('"');

            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];

                    switch (c)
                    {
                        case '"': json.Append("\\\""); break;
                        case '\\': json.Append("\\\\"); break;
                        case '\b': json.Append("\\b"); break;
                        case '\f': json.Append("\\f"); break;
                        case '\n': json.Append("\\n"); break;
                        case '\r': json.Append("\\r"); break;
                        case '\t': json.Append("\\t"); break;
                        default:
                            if (c < ' ') json.Append("\\u").Append(((int)c).ToString("x4"));
                            else json.Append(c);
                            break;
                    }
                }
            }

            json.Append('"');
        }
    }

    /// <summary>What ought to happen between a local translation and the one on the server.</summary>
    public enum SyncDirection
    {
        /// <summary>The two agree. Nothing to do, and nothing to say.</summary>
        InSync,

        /// <summary>The server has moved and nothing here has. Taking it costs nothing.</summary>
        Download,

        /// <summary>Work here that the server does not have, and the server has not moved.</summary>
        Upload,

        /// <summary>Both moved. Only the mod can settle this, line by line.</summary>
        Merge,
    }

    /// <summary>
    /// Where a translation stands against the one published, decided from four facts and nothing
    /// else.
    ///
    /// ⚠ **Shared so that a game and the manager cannot disagree about the same file.** The mod
    /// decided this from its own live state, which meant nothing outside a running game could say
    /// whether an update was waiting — somebody had to launch a game to find out that its
    /// translation had moved on, which is precisely the wrong moment to be told.
    ///
    /// Every input is readable from a file on disk plus one already-cached API answer, so the
    /// manager reaches the same verdict without the game ever being opened, and without a single
    /// extra request: the community search it already runs carries file_hash.
    /// </summary>
    public static class Sync
    {
        /// <summary>
        /// The verdict in the words a player reads. Two or three of them, the same on a badge, in
        /// a game list and on a card.
        ///
        /// 🔴 **Here because they were typed twice.** The badge strip wrote them and the manager's
        /// game list wrote them again — with a comment above it saying "one verdict, one
        /// vocabulary", which is how one can tell it was known and still not mechanical. The
        /// VERDICT was already shared; only the words were not, so a change to one of them would
        /// have reached a badge and left a game row saying the old thing.
        ///
        /// 🔴 **Plain international English, because there is no other.** These read "In step",
        /// "Behind", "Ahead" and "Diverged" until 2026-08-14, when somebody pointed out that
        /// nobody outside a version-control habit says any of them. The mod and the manager ship
        /// no translations: what is written here is what a Polish, Brazilian or Korean player
        /// reads, so the words are the ones every program has used for thirty years.
        ///
        /// ⚠ <see cref="SyncDirection.InSync"/> has a word too, and it is not silence: a game list
        /// says "Up to date" where a badge strip may choose to show nothing. Which chips a screen
        /// shows is a rendering decision; what a shown one SAYS is not.
        /// </summary>
        public static string Name(SyncDirection direction)
        {
            switch (direction)
            {
                case SyncDirection.Download: return "Update available";
                case SyncDirection.Upload: return "Unpublished changes";
                case SyncDirection.Merge: return "Conflict";
                default: return "Up to date";
            }
        }

        /// <summary>
        /// What follows from it, in one sentence — for a tooltip, a card, a status line.
        ///
        /// ⚠ Each says what is AT RISK, because that is the question behind the word: taking an
        /// update costs nothing when nothing of yours is unpublished, and it is not the same act
        /// when both sides have moved.
        /// </summary>
        public static string Explain(SyncDirection direction)
        {
            switch (direction)
            {
                case SyncDirection.Download:
                    return "The published version has moved on. Nothing of yours is at risk — you "
                         + "have no unpublished changes here.";

                case SyncDirection.Upload:
                    return "You have changes here that the published version does not have.";

                case SyncDirection.Merge:
                    return "Both this file and the published one have moved. Settling that is done "
                         + "line by line.";

                default:
                    return "This file and the published version hold the same content.";
            }
        }

        /// <summary>
        /// </summary>
        /// <param name="localContent">
        /// The hash of what is on disk right now, from <see cref="ContentHash.Of"/>.
        /// </param>
        /// <param name="serverContent">The file_hash the server reports, or null when unknown.</param>
        /// <param name="lastSynced">
        /// The server hash this file was last in step with — the mod's _source.hash. Null on a file
        /// that has never been synced, which is a real and common case: a translation somebody
        /// started themselves has no ancestor at all.
        /// </param>
        /// <param name="hasLocalChanges">
        /// Whether anything here has been changed since that last sync. Counted by the mod, which
        /// is the only side that watches lines being written.
        /// </param>
        /// <summary>
        /// The verdict on a whole translation file: its lines (<see cref="Decide"/>), then its
        /// settings — fonts, rules, images, exclusions — which no content hash covers, so identical
        /// lines with settings changed here is something to upload.
        ///
        /// 🔴 **The one composition every product asks** (2026-10-22). The mod reached it through
        /// <see cref="Standings"/>, the Manager called <see cref="Decide"/> on its own and said
        /// « Up to date » beside a game whose corner notice read « settings changed — not uploaded
        /// yet ». Asking for the file's verdict means asking this.
        /// </summary>
        /// <param name="linesChanged">Lines changed since the last sync (the mod's count).</param>
        /// <param name="settingsChanged">The file's settings changed since the last sync (<c>_metadata_dirty</c>).</param>
        public static SyncDirection DecideFile(string localContent, string serverContent, string lastSynced,
                                               bool linesChanged, bool settingsChanged)
        {
            var lines = Decide(localContent, serverContent, lastSynced, linesChanged || settingsChanged);
            return lines == SyncDirection.InSync && settingsChanged ? SyncDirection.Upload : lines;
        }

        public static SyncDirection Decide(string localContent, string serverContent,
                                           string lastSynced, bool hasLocalChanges)
        {
            // Nothing published to compare against: whatever is here is all there is.
            if (string.IsNullOrEmpty(serverContent)) return SyncDirection.InSync;

            // ⚠ Asked FIRST, and it settles the ordinary case on its own: identical content is in
            // sync whatever the counters say. Without it, a file that had been edited back to what
            // the server holds would be offered for upload forever.
            if (string.Equals(localContent, serverContent, StringComparison.OrdinalIgnoreCase))
                return SyncDirection.InSync;

            // Did the server move since we last agreed with it?
            //
            // ⚠ With no last-synced hash we cannot tell, and the safe answer is not "no". A file
            // with local work and no ancestor is exactly a translation somebody built themselves
            // and never published — treating that as "only the server moved" would offer to
            // overwrite it. So an unknown ancestor plus local work is read as a conflict.
            bool serverMoved = string.IsNullOrEmpty(lastSynced)
                ? hasLocalChanges
                : !string.Equals(serverContent, lastSynced, StringComparison.OrdinalIgnoreCase);

            if (hasLocalChanges && serverMoved) return SyncDirection.Merge;
            if (hasLocalChanges) return SyncDirection.Upload;

            return SyncDirection.Download;
        }
    }
}
