using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>One reason a line may not be written, as a machine code and in words.</summary>
    public struct LineRefusal
    {
        /// <summary>One of the <c>LineWrites.*</c> codes — stable, the corpus holds them.</summary>
        public string Code;

        /// <summary>The reason in plain English, naming the line (and the marker, entry or list) at fault.</summary>
        public string Words;
    }

    /// <summary>
    /// What a writer owes a file before a line of it is written (draft §6): the guarantees that
    /// keep every reader from meeting a marker with nothing behind it.
    ///
    /// 🔴 **The rule is checked where the line is written, not where it is read.** A reader never
    /// shows a hole (<see cref="LinkReading"/>), but it cannot tell a marker whose link was lost
    /// from a text that happens to contain one — so the writer, the site's door and the editors
    /// ask the SAME question, and a line that fails it is refused with the reason in words.
    ///
    /// The first reason found is the answer, in this order — the order is part of the contract
    /// because a port that answered another one first would say something else about the same
    /// line:
    ///
    ///  1. <see cref="FormatTooOld"/> — the file says it is format 1 and the line holds a field of the format 2
    ///     (a format 1 file is asked nothing else: its text is the game's);
    ///  2. <see cref="CarriageReturn"/> — every text of the format 2 (forms, a link's <c>was</c> and <c>to</c>, a refused
    ///     entry) is LF only, like <c>v</c> and the keys: nobody normalises them before hashing, so
    ///     a carriage return (CR) would make two hashes for one text;
    ///     <see cref="MarkerWithoutLink"/> / <see cref="LinkWithoutMarker"/> — <c>[!g*N]</c> and <c>links[N]</c> must pair;
    ///  3. <see cref="LinkEntryMissing"/> / <see cref="LinkFormMissing"/> — the entry exists, and so does the form it names;
    ///     <see cref="EntryHasLinkMarker"/> — a term, or the entry a link points at, shows no <c>[!g*N]</c> itself
    ///     (a term citing a term would put a marker on the screen: no recursion is read, none is allowed);
    ///  4. <see cref="SlotListMissing"/> / <see cref="SlotOpenMissing"/> — every slot names a list the file holds or an open slot the key holds;
    ///  5. <see cref="UseFormMissing"/> — what a term says a template takes names a form of its own;
    ///  6. <see cref="MachineChangedPersonLine"/> / <see cref="MachineWrotePersonLink"/> / <see cref="MachineWroteUnlinked"/> — a machine writing (the person's decisions are theirs).
    ///
    /// ⚠ **« Written » is not « read »:** a file already on disk that breaks one of these is still
    /// read (the reader shows <c>was</c>); only a NEW write is refused.
    /// </summary>
    public static class LineWrites
    {
        public const string FormatTooOld = "format-2-field-in-format-1";
        public const string CarriageReturn = "carriage-return-in-text";
        public const string MarkerWithoutLink = "marker-without-link";
        public const string LinkWithoutMarker = "link-without-marker";
        public const string LinkEntryMissing = "link-entry-missing";
        public const string LinkFormMissing = "link-form-missing";
        public const string EntryHasLinkMarker = "entry-has-link-marker";
        public const string TemplateIdCollision = "template-id-collision";
        public const string TokenInvalid = "token-invalid";
        public const string TokensNotInOrder = "tokens-not-in-order";
        public const string SlotListMissing = "slot-list-missing";
        public const string SlotOpenMissing = "slot-open-missing";
        public const string UseFormMissing = "use-form-missing";
        public const string MachineChangedPersonLine = "machine-changed-person-line";
        public const string MachineWrotePersonLink = "machine-wrote-person-link";
        public const string MachineWroteUnlinked = "machine-wrote-unlinked";

        /// <summary>
        /// Why this line may not be written, or null when it may.
        /// </summary>
        /// <param name="key">The line's key — the game's text, where a template holds its slots.</param>
        /// <param name="line">The line to write.</param>
        /// <param name="fileFormat">The file's format (<see cref="TranslationFormat.OfFile"/>).</param>
        /// <param name="entryOf">The line of an entry by its key, as the file will hold it; null when there is none.</param>
        /// <param name="hasList">Does the file's <c>_lists</c> hold a list of that name?</param>
        /// <param name="byMachine">The writer is the machine — a person's decisions are then protected.</param>
        /// <param name="previous">The line as it was, when it exists — what a machine may not change on a person's line.</param>
        public static LineRefusal? Refusal(string key, TranslationLine line, int fileFormat,
                                           Func<string, TranslationLine?> entryOf, Func<string, bool> hasList,
                                           bool byMachine, TranslationLine? previous)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (entryOf == null) throw new ArgumentNullException(nameof(entryOf));
            if (hasList == null) throw new ArgumentNullException(nameof(hasList));

            if (fileFormat < 2 && line.HasFormat2Fields)
                return Say(FormatTooOld,
                    $"The line \"{key}\" holds fields of the format 2, and this file is written in the format {fileFormat}.");

            // ⚠ Everything below is the format 2's. A line of a format 1 file holds the game's text
            // as it always did — a « [!g*0] » or a « [!L*x] » written there is text, not a marker.
            if (fileFormat < 2) return null;

            string? carriage = CarriageReturnIn(line);
            if (carriage != null)
                return Say(CarriageReturn,
                    $"The line \"{key}\" holds a carriage return in {carriage}: the texts of the format 2 use LF alone.");

            var links = line.Links ?? (IReadOnlyList<LineLink>)new LineLink[0];
            var markers = LineMarkers.LinkNumbers(line.Value);

            foreach (int n in markers)
                if (n >= links.Count)
                    return Say(MarkerWithoutLink,
                        $"The line \"{key}\" shows the link marker [!g*{n}] but holds no link number {n}.");

            for (int i = 0; i < links.Count; i++)
                if (!markers.Contains(i))
                    return Say(LinkWithoutMarker,
                        $"The line \"{key}\" holds link number {i} but its text has no [!g*{i}] to show it.");

            for (int i = 0; i < links.Count; i++)
            {
                LineLink link = links[i];
                TranslationLine? entry = string.IsNullOrEmpty(link.To) ? (TranslationLine?)null : entryOf(link.To!);

                if (!entry.HasValue)
                    return Say(LinkEntryMissing,
                        $"Link number {i} of the line \"{key}\" points at \"{link.To}\", which is not a line of this file.");

                if (HasLinkMarker(entry.GetValueOrDefault()))
                    return Say(EntryHasLinkMarker,
                        $"Link number {i} of the line \"{key}\" points at \"{link.To}\", whose own text shows a link marker: a term does not cite another term.");

                if (!string.IsNullOrEmpty(link.Form) && link.Form != LinkReading.OwnForm
                    && !HasForm(entry.GetValueOrDefault(), link.Form!))
                    return Say(LinkFormMissing,
                        $"Link number {i} of the line \"{key}\" asks for form {link.Form} of \"{link.To}\", which has no such form.");
            }

            if (line.Term && HasLinkMarker(line))
                return Say(EntryHasLinkMarker,
                    $"The line \"{key}\" is a term and its own text shows a link marker: a term does not cite another term.");

            // The key holds the slots of a template; its translation repeats them. Both are checked.
            var openInKey = LineMarkers.OpenSlots(key);
            foreach (string text in new[] { key, line.Value ?? string.Empty })
            {
                foreach (var slot in LineMarkers.ClosedSlots(text))
                {
                    if (!hasList(slot.List))
                        return Say(SlotListMissing,
                            $"The line \"{key}\" has a slot for the list \"{slot.List}\", which this file does not hold.");

                    if (slot.Agrees != null)
                    {
                        int open = LineMarkers.OpenSlotRef(slot.Agrees);
                        bool known = hasList(slot.Agrees) || (open >= 0 && openInKey.Contains(open));
                        if (!known)
                            return Say(SlotListMissing,
                                $"The slot of \"{slot.List}\" in the line \"{key}\" agrees with \"{slot.Agrees}\", which is neither a list of this file nor an open slot of the key.");
                    }
                }
            }

            foreach (int open in LineMarkers.OpenSlots(line.Value))
                if (!openInKey.Contains(open))
                    return Say(SlotOpenMissing,
                        $"The line \"{key}\" shows the open slot [!T*{open}], which its key does not hold.");

            if (line.Use != null)
                foreach (var use in line.Use)
                {
                    if (use.Value.Form != null && !FormExists(line, use.Value.Form))
                        return Say(UseFormMissing,
                            $"The line \"{key}\" says template {use.Key} takes form {use.Value.Form}, which this line does not hold.");

                    if (use.Value.ByClass != null)
                        foreach (var byClass in use.Value.ByClass)
                            if (!FormExists(line, byClass.Value))
                                return Say(UseFormMissing,
                                    $"The line \"{key}\" says template {use.Key} takes form {byClass.Value} for class {byClass.Key}, which this line does not hold.");
                }

            if (byMachine)
            {
                if (previous.HasValue)
                {
                    TranslationLine was = previous.GetValueOrDefault();
                    bool person = was.Tag == "H" || was.Tag == "V";

                    if (person && (!string.Equals(line.Value, was.Value, StringComparison.Ordinal)
                                   || !SameMap(line.Forms, was.Forms) || !SameMap(line.Class, was.Class)))
                        return Say(MachineChangedPersonLine,
                            $"The line \"{key}\" was written or validated by a person: the machine does not change its text, forms or classes.");
                }

                foreach (var link in links)
                    if (link.By == LinkReading.ByPerson && !HeldBefore(previous, link))
                        return Say(MachineWrotePersonLink,
                            $"The line \"{key}\" has a link set by a person that it did not have: only a person writes one.");

                if (line.Unlinked != null && line.Unlinked.Count > 0
                    && !SameSet(line.Unlinked, previous.HasValue ? previous.GetValueOrDefault().Unlinked : null))
                    return Say(MachineWroteUnlinked,
                        $"The line \"{key}\" says some words are not terms: only a person says that.");
            }

            return null;
        }

        private static LineRefusal Say(string code, string words) => new LineRefusal { Code = code, Words = words };

        /// <summary>Which text of the line holds a « \r », or null.</summary>
        private static string? CarriageReturnIn(TranslationLine line)
        {
            if (line.Forms != null)
                foreach (var form in line.Forms)
                    if (form.Value != null && form.Value.IndexOf('\r') >= 0) return $"its form {form.Key}";
            if (line.Links != null)
                for (int i = 0; i < line.Links.Count; i++)
                {
                    var link = line.Links[i];
                    if (link.Was != null && link.Was.IndexOf('\r') >= 0) return $"the `was` of link {i}";
                    if (link.Own != null && link.Own.IndexOf('\r') >= 0) return $"the own writing of link {i}";
                    if (link.To != null && link.To.IndexOf('\r') >= 0) return $"the target of link {i}";
                }
            if (line.Unlinked != null)
                foreach (var to in line.Unlinked)
                    if (to != null && to.IndexOf('\r') >= 0) return "a refused entry";
            return null;
        }

        /// <summary>The line's own text, or one of its written forms, shows a <c>[!g*N]</c>.</summary>
        private static bool HasLinkMarker(TranslationLine line)
        {
            if (LineMarkers.LinkNumbers(line.Value).Count > 0) return true;
            if (line.Forms != null)
                foreach (var form in line.Forms)
                    if (LineMarkers.LinkNumbers(form.Value).Count > 0) return true;
            return false;
        }

        private static bool HasForm(TranslationLine entry, string form)
        {
            if (entry.Forms == null) return false;
            foreach (var f in entry.Forms)
                if (string.Equals(f.Key, form, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool FormExists(TranslationLine line, string form) =>
            form == LinkReading.OwnForm || HasForm(line, form);

        private static bool HeldBefore(TranslationLine? previous, LineLink link)
        {
            if (!previous.HasValue || previous.GetValueOrDefault().Links == null) return false;
            foreach (var old in previous.GetValueOrDefault().Links!)
                if (old.To == link.To && old.Form == link.Form && old.Case == link.Case
                    && old.By == link.By && old.Was == link.Was && old.Own == link.Own) return true;
            return false;
        }

        private static bool SameMap(IReadOnlyList<KeyValuePair<string, string>>? a, IReadOnlyList<KeyValuePair<string, string>>? b)
        {
            int na = a?.Count ?? 0, nb = b?.Count ?? 0;
            if (na != nb) return false;
            if (na == 0) return true;
            foreach (var pair in a!)
            {
                bool found = false;
                foreach (var other in b!)
                    if (other.Key == pair.Key && other.Value == pair.Value) { found = true; break; }
                if (!found) return false;
            }
            return true;
        }

        private static bool SameSet(IReadOnlyList<string> a, IReadOnlyList<string>? b)
        {
            if (b == null || b.Count == 0) return false;
            foreach (var x in a) if (!ContainsText(b, x)) return false;
            foreach (var x in b) if (!ContainsText(a, x)) return false;
            return true;
        }

        private static bool ContainsText(IReadOnlyList<string> list, string text)
        {
            foreach (var item in list) if (string.Equals(item, text, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
