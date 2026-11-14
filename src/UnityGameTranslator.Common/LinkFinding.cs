using System;
using System.Collections.Generic;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The terms of a file, indexed ONCE for finding them in many sentences (immutable, built by <see cref="Build"/>):
    /// a trie over the terms' keys (what a SOURCE cites) and one over their written forms (what a TEXT writes), the
    /// first letter of each in both cases (<see cref="Casing"/>). A sentence costs the length of its text, not the
    /// number of terms.
    /// </summary>
    public sealed class TermIndex
    {
        internal sealed class Node
        {
            public Dictionary<char, Node>? Next;
            public List<Hit>? Hits;
        }

        internal struct Hit
        {
            public int Term;
            public string? Form;
            public string? Case;
        }

        internal readonly string[] Keys;
        internal readonly Node KeyRoot = new Node();
        internal readonly Node WritingRoot = new Node();
        internal readonly Dictionary<string, int> TermOfKey;

        /// <summary>How many terms.</summary>
        public int Count => Keys.Length;

        private TermIndex(List<KeyValuePair<string, TranslationLine>> terms, bool withWritings = true)
        {
            // Terms in the ORDINAL order of their keys: an identifier means the same whatever order the file lists them in.
            terms.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            Keys = new string[terms.Count];
            TermOfKey = new Dictionary<string, int>(terms.Count, StringComparer.Ordinal);

            for (int t = 0; t < terms.Count; t++)
            {
                Keys[t] = terms[t].Key;
                TermOfKey[terms[t].Key] = t;
                AddText(KeyRoot, terms[t].Key, t, null);

                if (!withWritings) continue;

                var line = terms[t].Value;
                var seen = new List<string>();
                var writings = new List<KeyValuePair<string?, string>>();
                writings.Add(new KeyValuePair<string?, string>(null, line.Value));
                if (line.Forms != null)
                {
                    var sorted = new List<KeyValuePair<string, string>>(line.Forms);
                    sorted.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
                    foreach (var f in sorted) writings.Add(new KeyValuePair<string?, string>(f.Key, f.Value));
                }

                foreach (var w in writings)
                {
                    if (string.IsNullOrEmpty(w.Value) || w.Value.IndexOf("[!", StringComparison.Ordinal) >= 0 || seen.Contains(w.Value)) continue;
                    seen.Add(w.Value);
                    AddText(WritingRoot, w.Value, t, w.Key);
                }
            }
        }

        /// <summary>
        /// The index of the terms of a file: its lines that carry the term mark, whatever the other lines hold. A key
        /// that is a template, or a term with no words, is no term.
        /// </summary>
        public static TermIndex Build(IEnumerable<KeyValuePair<string, TranslationLine>> lines)
        {
            if (lines == null) throw new ArgumentNullException(nameof(lines));
            var terms = new List<KeyValuePair<string, TranslationLine>>();
            foreach (var line in lines)
                if (line.Value.Term && !string.IsNullOrEmpty(line.Key) && !string.IsNullOrEmpty(line.Value.Value)
                    && LineMarkers.ClosedSlots(line.Key).Count == 0)
                    terms.Add(line);
            return new TermIndex(terms);
        }

        /// <summary>
        /// An index of KEYS only — what a source cites, with nothing to find in a text (<see cref="LinkFinding.Find"/> over it
        /// finds nothing). For asking « which sentences cite this word » of a word that is not a term yet, or of terms whose
        /// writings do not matter (<see cref="LinkFinding.Cites"/>). A key that is empty or a template is no key.
        /// </summary>
        public static TermIndex OfKeys(IEnumerable<string> keys)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            var terms = new List<KeyValuePair<string, TranslationLine>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string key in keys)
                if (!string.IsNullOrEmpty(key) && LineMarkers.ClosedSlots(key).Count == 0 && seen.Add(key))
                    terms.Add(new KeyValuePair<string, TranslationLine>(key, new TranslationLine { Term = true }));
            return new TermIndex(terms, withWritings: false);
        }

        private static void AddText(Node root, string text, int term, string? form)
        {
            string up = Casing.UpperFirst(text)!, low = Casing.LowerFirst(text)!;
            Insert(root, text, new Hit { Term = term, Form = form });
            if (!string.Equals(up, text, StringComparison.Ordinal)) Insert(root, up, new Hit { Term = term, Form = form, Case = LinkReading.UpperFirst });
            if (!string.Equals(low, text, StringComparison.Ordinal)) Insert(root, low, new Hit { Term = term, Form = form, Case = LinkReading.LowerFirst });
        }

        private static void Insert(Node root, string text, Hit hit)
        {
            Node node = root;
            foreach (char c in text)
            {
                if (node.Next == null) node.Next = new Dictionary<char, Node>();
                if (!node.Next.TryGetValue(c, out Node? next)) node.Next[c] = next = new Node();
                node = next;
            }
            if (node.Hits == null) node.Hits = new List<Hit>();
            node.Hits.Add(hit);
        }
    }

    /// <summary>What was found in a text.</summary>
    public struct FoundLinks
    {
        /// <summary>The text with a marker where a link was found: the very same string when none was.</summary>
        public string V;

        /// <summary>The links, in reading order: those a person set that were found again, and the machine's (<c>by: A</c>).</summary>
        public IReadOnlyList<LineLink> Links;

        /// <summary>The links a person set (<c>by: H</c>) whose exact words are not found ONCE: to be shown BEFORE she accepts the text (accepting it drops them).</summary>
        public IReadOnlyList<LineLink> NotFound;

        /// <summary>A code, when the text cannot be read as clear text (it holds a link marker).</summary>
        public string? Problem;
    }

    /// <summary>
    /// FINDING the links in a text nobody typed — a model's reply, a retranslation accepted, a text from a cold
    /// translation (spec/translation-file/finding.md). A link is known, never guessed (R4): a term is a candidate for a
    /// sentence only if the SOURCE of the sentence cites it; its known writings are then looked for in the text; ONE
    /// occurrence is a link, none or several is no link, and nothing is ever rewritten.
    /// </summary>
    public static class LinkFinding
    {
        private static readonly LineLink[] NoLinks = new LineLink[0];

        private struct Place
        {
            public int Start, End, Term;
            public string? Form, Case;
        }

        /// <param name="index">The terms of the file, built once.</param>
        /// <param name="text">The text in clear (the model's reply, the retranslation…).</param>
        /// <param name="source">The sentence's source: its key.</param>
        /// <param name="before">The line as it was, when there is one: its links set by a person, its refused terms.</param>
        public static FoundLinks Find(TermIndex index, string text, string source, TranslationLine? before)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (text == null) throw new ArgumentNullException(nameof(text));
            source = source ?? string.Empty;

            bool hadLinks = before.HasValue && before.Value.Links != null && before.Value.Links.Count > 0;
            if (index.Count == 0 && !hadLinks) return Plain(text);
            if (text.Length == 0) return Plain(text);
            if (text.IndexOf("[!g*", StringComparison.Ordinal) >= 0 && LineMarkers.LinkNumbers(text).Count > 0)
                return new FoundLinks { V = text, Links = NoLinks, NotFound = NoLinks, Problem = "text-has-link-marker" };
            // A term's own line, and a template, never carry a link.
            if (index.TermOfKey.ContainsKey(source) || source.IndexOf("[!L*", StringComparison.Ordinal) >= 0) return Plain(text);

            return FindCore(index, text, source, before);
        }

        // Apart from Find on purpose: the plain paths above must not allocate.
        private static FoundLinks FindCore(TermIndex index, string text, string source, TranslationLine? before)
        {
            var claimed = new List<int[]>();                          // [start, end) already taken in the text
            var accepted = new List<KeyValuePair<int, LineLink>>();   // start → link
            var notFound = new List<LineLink>();
            var decided = new HashSet<int>();                         // terms a person already linked in this sentence

            // 1. What a person decided stays, when its words are found once.
            if (before.HasValue && before.Value.Links != null)
                KeepPeopleLinks(index, text, before.Value.Links, claimed, accepted, notFound, decided);

            // 2. The terms the SOURCE cites: the longest first, never overlapping, none a person refused.
            var cited = new HashSet<int>();
            foreach (var place in CitedPlaces(index, source, before.HasValue ? before.Value.Unlinked : null)) cited.Add(place.Term);
            cited.RemoveWhere(t => decided.Contains(t));

            // 3. Where their writings are in the text.
            if (cited.Count > 0)
            {
                var places = new List<Place>();
                for (int i = 0; i < text.Length; i++)
                {
                    if (!WordGlue.EdgeBefore(text, i)) continue;
                    var node = index.WritingRoot;
                    for (int k = i; k < text.Length; k++)
                    {
                        if (node.Next == null || !node.Next.TryGetValue(text[k], out node!)) break;
                        if (node.Hits == null || !WordGlue.EdgeAfter(text, k + 1)) continue;
                        foreach (var hit in node.Hits)
                            if (cited.Contains(hit.Term))
                                places.Add(new Place { Start = i, End = k + 1, Term = hit.Term, Form = hit.Form, Case = hit.Case });
                    }
                }

                // The longest first, then the key, the form, the place: the same whatever order the file lists its terms in.
                places.Sort((a, b) =>
                {
                    int c = (b.End - b.Start).CompareTo(a.End - a.Start);
                    if (c != 0) return c;
                    c = a.Term.CompareTo(b.Term);                       // terms are numbered in the ordinal order of their keys
                    if (c != 0) return c;
                    // A writing found AS IT IS beats the case of another one landing on the same words: what a person wrote as a
                    // form is the form (LinkEditing.Known reads exact first, WrittenIn too).
                    c = (a.Case == null ? 0 : 1).CompareTo(b.Case == null ? 0 : 1);
                    if (c != 0) return c;
                    c = string.CompareOrdinal(a.Form ?? string.Empty, b.Form ?? string.Empty);
                    return c != 0 ? c : a.Start.CompareTo(b.Start);
                });

                var perTerm = new SortedDictionary<int, List<Place>>();
                foreach (var place in places)
                {
                    if (Overlaps(claimed, place.Start, place.End)) continue;
                    claimed.Add(new[] { place.Start, place.End });
                    if (!perTerm.TryGetValue(place.Term, out var list)) perTerm[place.Term] = list = new List<Place>();
                    list.Add(place);
                }

                // One occurrence is a link; several, or none, is none.
                foreach (var pair in perTerm)
                {
                    if (pair.Value.Count != 1) continue;
                    var p = pair.Value[0];
                    accepted.Add(new KeyValuePair<int, LineLink>(p.Start, new LineLink
                    {
                        To = index.Keys[p.Term],
                        Form = p.Form,
                        Case = p.Case,
                        By = LinkReading.ByMachine,
                        Was = text.Substring(p.Start, p.End - p.Start),
                    }));
                }
            }

            if (accepted.Count == 0) return new FoundLinks { V = text, Links = NoLinks, NotFound = notFound };

            accepted.Sort((a, b) => a.Key.CompareTo(b.Key));
            var v = new StringBuilder(text.Length + 8 * accepted.Count);
            var links = new List<LineLink>(accepted.Count);
            int at = 0;
            foreach (var found in accepted)
            {
                v.Append(text, at, found.Key - at).Append("[!g*").Append(links.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(']');
                at = found.Key + WordsOf(found.Value).Length;
                links.Add(found.Value);
            }
            v.Append(text, at, text.Length - at);
            return new FoundLinks { V = v.ToString(), Links = links, NotFound = notFound };
        }

        /// <summary>The words a link stands for in a text: its own writing, else the words it was set on.</summary>
        private static string WordsOf(LineLink link) => !string.IsNullOrEmpty(link.Own) ? link.Own! : link.Was ?? string.Empty;

        private static void KeepPeopleLinks(TermIndex index, string text, IReadOnlyList<LineLink> before, List<int[]> claimed,
                                            List<KeyValuePair<int, LineLink>> accepted, List<LineLink> notFound, HashSet<int> decided)
        {
            // Group the person's links by their words: as many places as links, or none of them.
            var byWords = new Dictionary<string, List<LineLink>>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var link in before)
            {
                if (link.By != LinkReading.ByPerson) continue;
                string words = WordsOf(link);
                if (words.Length == 0) { notFound.Add(link); continue; }
                if (!byWords.TryGetValue(words, out var list)) { byWords[words] = list = new List<LineLink>(); order.Add(words); }
                list.Add(link);
            }

            foreach (string words in order)
            {
                var places = new List<int>();
                for (int at = text.IndexOf(words, StringComparison.Ordinal); at >= 0;
                     at = at + words.Length <= text.Length ? text.IndexOf(words, at + words.Length, StringComparison.Ordinal) : -1)
                    if (WordGlue.EdgeBefore(text, at) && WordGlue.EdgeAfter(text, at + words.Length) && !Overlaps(claimed, at, at + words.Length))
                        places.Add(at);

                var links = byWords[words];
                if (places.Count != links.Count) { notFound.AddRange(links); continue; }
                for (int i = 0; i < links.Count; i++)
                {
                    claimed.Add(new[] { places[i], places[i] + words.Length });
                    accepted.Add(new KeyValuePair<int, LineLink>(places[i], links[i]));
                    if (index.TermOfKey.TryGetValue(links[i].To ?? string.Empty, out int term)) decided.Add(term);
                }
            }
        }

        /// <summary>One place where a source cites a term.</summary>
        public struct CitedTerm
        {
            /// <summary>The key of the term cited.</summary>
            public string Key;

            /// <summary>Where, in the source: UTF-16 units, <c>[Start, End)</c>.</summary>
            public int Start, End;
        }

        /// <summary>
        /// WHERE a source cites the terms of an index — the « candidate » half of <see cref="Find"/> (spec/translation-file/finding.md §3,
        /// §8): the keys found in the source at word edges, the first letter in either case, the longest first, never overlapping,
        /// a term refused by <paramref name="refused"/> (the line's <c>unlinked</c>) never proposed — its place still taken. In
        /// reading order; a term cited twice is two places. A term's own line and a template cite nothing.
        /// </summary>
        public static IReadOnlyList<CitedTerm> Cites(TermIndex index, string source, IReadOnlyList<string>? refused)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            var none = new List<CitedTerm>();
            if (string.IsNullOrEmpty(source) || index.Count == 0) return none;
            if (index.TermOfKey.ContainsKey(source) || source.IndexOf("[!L*", StringComparison.Ordinal) >= 0) return none;

            var places = CitedPlaces(index, source, refused);
            places.Sort((a, b) => a.Start.CompareTo(b.Start));
            foreach (var place in places) none.Add(new CitedTerm { Key = index.Keys[place.Term], Start = place.Start, End = place.End });
            return none;
        }

        /// <summary>One writing of a term: the words, and the form they are written by (<c>null</c> = the term's own word).</summary>
        public struct LinkWriting
        {
            public string Words;
            public string? Form;
        }

        /// <summary>One place a writing is found at.</summary>
        public struct WrittenPlace
        {
            /// <summary>Which writing, by its position in the list given.</summary>
            public int Writing;

            /// <summary>Where, in the text: UTF-16 units, <c>[Start, End)</c>.</summary>
            public int Start, End;

            /// <summary>The case of the first letter applied (<see cref="LinkReading.UpperFirst"/>, <see cref="LinkReading.LowerFirst"/>); null when the text writes the words as they are.</summary>
            public string? Case;
        }

        /// <summary>
        /// WHERE ONE TERM is written in a text — the second half of <see cref="Find"/>, for one term and with every place kept
        /// (spec/translation-file/finding.md §9). Each writing is looked for at word edges, its first letter in either case; the
        /// places are taken the longest first, then in the order of the writings given, never overlapping each other nor a
        /// <paramref name="claimed"/> span (a passage already linked) — and ALL of them are returned, in reading order: one
        /// place is what <see cref="Find"/> would link, several are for the person to choose, none is « not found ».
        ///
        /// ⚠ The text is the text SHOWN (<c>EditState.Text</c>: no marker), and the claimed spans are the passages of that same
        /// text. List the term's own word first, then its forms in the ordinal order of their ids, as <see cref="Find"/> numbers
        /// them: at an equal length the first writing given has the place. A writing with no words, with a marker in it, or with
        /// the same words as an earlier one has no place of its own. A text that holds a link marker answers nothing.
        /// </summary>
        public static IReadOnlyList<WrittenPlace> WrittenIn(string text, IReadOnlyList<LinkWriting> writings, IReadOnlyList<KeyValuePair<int, int>>? claimed)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (writings == null) throw new ArgumentNullException(nameof(writings));
            var result = new List<WrittenPlace>();
            if (text.Length == 0 || writings.Count == 0) return result;
            if (text.IndexOf("[!g*", StringComparison.Ordinal) >= 0 && LineMarkers.LinkNumbers(text).Count > 0) return result;

            var found = new List<WrittenPlace>();
            var seen = new List<string>();
            for (int w = 0; w < writings.Count; w++)
            {
                string words = writings[w].Words;
                if (string.IsNullOrEmpty(words) || words.IndexOf("[!", StringComparison.Ordinal) >= 0 || seen.Contains(words)) continue;
                seen.Add(words);

                string up = Casing.UpperFirst(words)!, low = Casing.LowerFirst(words)!;
                Look(text, words, null, w, found);
                if (!string.Equals(up, words, StringComparison.Ordinal)) Look(text, up, LinkReading.UpperFirst, w, found);
                if (!string.Equals(low, words, StringComparison.Ordinal)) Look(text, low, LinkReading.LowerFirst, w, found);
            }

            found.Sort((a, b) =>
            {
                int c = (b.End - b.Start).CompareTo(a.End - a.Start);
                if (c != 0) return c;
                // as written first: a writing the text holds as it is beats another writing's capital landing on the same words
                c = (a.Case == null ? 0 : 1).CompareTo(b.Case == null ? 0 : 1);
                if (c != 0) return c;
                c = a.Writing.CompareTo(b.Writing);
                return c != 0 ? c : a.Start.CompareTo(b.Start);
            });

            var taken = new List<int[]>();
            if (claimed != null) foreach (var span in claimed) taken.Add(new[] { span.Key, span.Value });
            foreach (var place in found)
            {
                if (Overlaps(taken, place.Start, place.End)) continue;
                taken.Add(new[] { place.Start, place.End });
                result.Add(place);
            }
            result.Sort((a, b) => a.Start.CompareTo(b.Start));
            return result;
        }

        private static void Look(string text, string variant, string? casing, int writing, List<WrittenPlace> into)
        {
            for (int at = text.IndexOf(variant, StringComparison.Ordinal); at >= 0;
                 at = at + 1 <= text.Length ? text.IndexOf(variant, at + 1, StringComparison.Ordinal) : -1)
            {
                int end = at + variant.Length;
                if (WordGlue.EdgeBefore(text, at) && WordGlue.EdgeAfter(text, end))
                    into.Add(new WrittenPlace { Writing = writing, Start = at, End = end, Case = casing });
            }
        }

        /// <summary>The places a source cites terms at: occurrences of their keys at word edges, the longest first and never overlapping.</summary>
        private static List<Place> CitedPlaces(TermIndex index, string source, IReadOnlyList<string>? refused)
        {
            var found = new List<Place>();
            for (int i = 0; i < source.Length; i++)
            {
                if (!WordGlue.EdgeBefore(source, i)) continue;
                var node = index.KeyRoot;
                for (int k = i; k < source.Length; k++)
                {
                    if (node.Next == null || !node.Next.TryGetValue(source[k], out node!)) break;
                    if (node.Hits == null || !WordGlue.EdgeAfter(source, k + 1)) continue;
                    foreach (var hit in node.Hits) found.Add(new Place { Start = i, End = k + 1, Term = hit.Term });
                }
            }

            found.Sort((a, b) =>
            {
                int c = (b.End - b.Start).CompareTo(a.End - a.Start);
                if (c != 0) return c;
                c = a.Term.CompareTo(b.Term);
                return c != 0 ? c : a.Start.CompareTo(b.Start);
            });

            // Kept in place: the list is the caller's already, and this runs once per sentence.
            int kept = 0;
            var taken = new List<int[]>();
            for (int i = 0; i < found.Count; i++)
            {
                var place = found[i];
                if (Overlaps(taken, place.Start, place.End)) continue;
                taken.Add(new[] { place.Start, place.End });
                if (refused != null && Contains(refused, index.Keys[place.Term])) continue;
                found[kept++] = place;
            }
            found.RemoveRange(kept, found.Count - kept);
            return found;
        }

        private static bool Contains(IReadOnlyList<string> list, string key)
        {
            foreach (string x in list) if (string.Equals(x, key, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool Overlaps(List<int[]> taken, int start, int end)
        {
            foreach (var t in taken) if (start < t[1] && end > t[0]) return true;
            return false;
        }

        private static FoundLinks Plain(string text) => new FoundLinks { V = text, Links = NoLinks, NotFound = NoLinks };
    }
}
