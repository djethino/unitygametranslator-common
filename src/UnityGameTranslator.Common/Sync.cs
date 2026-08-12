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

        /// <summary>An entry of the current form. Tag null means the file has no "t".</summary>
        public TranslationLine(string value, string tag)
        {
            Value = value;
            Tag = tag;
            BareString = false;
        }

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
    ///  · only the translated lines and _uuid; every other underscore key is metadata and excluded;
    ///  · each line becomes {"v":…,"t":…}, in that order, with no other field;
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
        /// The translated lines only. Metadata keys must not be here — the caller knows its own
        /// storage, and filtering by "starts with an underscore" twice would be a rule to keep in
        /// step twice. <see cref="IsMetadataKey"/> exists for callers reading a raw file.
        /// </param>
        /// <param name="uuid">The lineage identifier, hashed alongside the lines.</param>
        public static string Of(IEnumerable<KeyValuePair<string, TranslationLine>> lines, string uuid)
        {
            var sorted = new SortedDictionary<string, TranslationLine>(StringComparer.Ordinal);

            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line.Key == null || IsMetadataKey(line.Key)) continue;
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
        /// Whether a key is metadata rather than a translated line. The mod's convention, and the
        /// website's: everything the tools write about a file starts with an underscore.
        /// </summary>
        public static bool IsMetadataKey(string key) =>
            !string.IsNullOrEmpty(key) && key[0] == '_';

        /// <summary>
        /// One JSON string, escaped exactly as the reference implementations escape it.
        ///
        /// ⚠ Only the five short forms and \u00xx for the remaining control characters. Anything
        /// above 0x1F is written as itself — including the solidus, which JSON allows to be escaped
        /// and which PHP escapes unless told not to. Both spellings parse; only one hashes right.
        /// </summary>
        private static void Write(StringBuilder json, string value)
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
