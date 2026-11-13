using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Common
{
    /// <summary>One closed slot of a template: <c>[!L*name]</c>, <c>[!L*name*]</c>, <c>[!L*name=other]</c>.</summary>
    public struct ClosedSlot
    {
        /// <summary>The list this slot reads — an entry of it fills the slot.</summary>
        public string List;

        /// <summary>The name ended with « * »: one or more entries of the list, glued.</summary>
        public bool Repeated;

        /// <summary>
        /// What the slot agrees with, or null: another list (the entry of that list standing in the
        /// same template) or an open slot (<c>T0</c>, see <see cref="LineMarkers.OpenSlotRef"/>).
        /// </summary>
        public string? Agrees;
    }

    /// <summary>
    /// The markers the format 2 adds to the text of a line, and nothing else about them: what they
    /// look like. What they MEAN is <see cref="LinkReading"/> (the link) and <see cref="LineWrites"/>
    /// (what a writer owes them).
    ///
    /// | marker | where | what it is |
    /// |---|---|---|
    /// | <c>[!g*N]</c> | the translation <c>v</c> | the text of the link <c>links[N]</c> of the same line |
    /// | <c>[!L*name]</c>, <c>[!L*name*]</c>, <c>[!L*name=other]</c> | a template's key and <c>v</c> | an entry of the list <c>name</c>; several glued; agreeing with another slot |
    /// | <c>[!T*N]</c> | a template's key and <c>v</c> | an open slot: any text, looked up on its own |
    ///
    /// 🔴 **The grammar is written once, in spec/translation-file/schema.json (<c>x-markers</c>),
    /// and held here by the checks** — the website and the web editors read the same three
    /// patterns. A marker is never recognised by a looser rule than this: « [!note] » in a game's
    /// text is the game's (the lesson of <see cref="Placeholders"/>, which used to protect
    /// anything from « [! » to the next « ] »).
    ///
    /// ⚠ N is a canonical decimal number of at most nine digits — no sign, no leading zero — so
    /// « [!g*01] » is not a marker and never points at <c>links[1]</c>. A list name holds none of
    /// « ] [ : = * »: the first and last end it, the others are the grammar's own.
    ///
    /// ⚠ <see cref="Placeholders"/> is NOT widened to freeze these: that rule serves the model's
    /// answer, and a translation holds a link marker its source never had. Whether an edit may
    /// keep or add one is the writer's rule (<see cref="LineWrites"/>), decided separately.
    /// </summary>
    public static class LineMarkers
    {
        private const string Number = "(0|[1-9][0-9]{0,8})";
        private const string Name = "([^\\]\\[:=*]+)";

        /// <summary>The pattern of a link marker, as spec/translation-file/schema.json writes it.</summary>
        public const string LinkPattern = "\\[!g\\*" + Number + "\\]";

        /// <summary>The pattern of a closed slot.</summary>
        public const string ClosedSlotPattern = "\\[!L\\*" + Name + "(\\*)?(?:=" + Name + ")?\\]";

        /// <summary>The pattern of an open slot.</summary>
        public const string OpenSlotPattern = "\\[!T\\*" + Number + "\\]";

        private static readonly Regex Link = new Regex(LinkPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Closed = new Regex(ClosedSlotPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex Open = new Regex(OpenSlotPattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex OpenRef = new Regex("^T" + Number + "$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        /// <summary>The N of every <c>[!g*N]</c> of the text, in reading order, a repeated one repeated.</summary>
        public static List<int> LinkNumbers(string? text)
        {
            var numbers = new List<int>();
            if (string.IsNullOrEmpty(text)) return numbers;
            foreach (Match m in Link.Matches(text!)) numbers.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            return numbers;
        }

        /// <summary>Every closed slot of the text, in reading order.</summary>
        public static List<ClosedSlot> ClosedSlots(string? text)
        {
            var slots = new List<ClosedSlot>();
            if (string.IsNullOrEmpty(text)) return slots;
            foreach (Match m in Closed.Matches(text!))
                slots.Add(new ClosedSlot
                {
                    List = m.Groups[1].Value,
                    Repeated = m.Groups[2].Success,
                    Agrees = m.Groups[3].Success ? m.Groups[3].Value : null,
                });
            return slots;
        }

        /// <summary>The N of every <c>[!T*N]</c> of the text, in reading order.</summary>
        public static List<int> OpenSlots(string? text)
        {
            var numbers = new List<int>();
            if (string.IsNullOrEmpty(text)) return numbers;
            foreach (Match m in Open.Matches(text!)) numbers.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));
            return numbers;
        }

        /// <summary>
        /// The N of an open-slot reference — what <see cref="ClosedSlot.Agrees"/> holds when a slot
        /// agrees with an open slot (<c>T0</c> → 0) — or -1 when the text names a list.
        ///
        /// ⚠ A list called « T0 » cannot be told from the open slot 0 by this grammar alone: the
        /// writer owes a list name that is not « T » and a number (<see cref="LineWrites"/>).
        /// </summary>
        public static int OpenSlotRef(string? agrees)
        {
            if (agrees == null) return -1;
            var m = OpenRef.Match(agrees);
            return m.Success ? int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
        }
    }
}
