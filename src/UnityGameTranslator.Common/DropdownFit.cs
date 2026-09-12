using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// How tall a dropdown's list may be, and whether it needs a search field.
    ///
    /// 🔴 **Both are the same question — "how long is this list against the room it has" — and both
    /// were being answered by hand, differently, in every product.** The mod wrote `popupHeight:`
    /// and `showSearch:` at each of its twenty call sites, with three different heights and a
    /// judgement per list; the Manager had a constant and a search field that was always there. So
    /// the same list of four answers got a search box in one product and not in the other, and a
    /// list of a hundred and eighty could be given two hundred pixels by a call site that had not
    /// thought about it.
    ///
    /// ⚠ **A search field over a list that fits is furniture**: it costs a line of screen, a focus
    /// stop and a decision, to filter something already entirely visible. It earns its place exactly
    /// when it becomes the only way through — which is when the list is taller than its room.
    ///
    /// ⚠ What this does NOT decide is where the list opens, how a row is drawn, or what a row is.
    /// That is rendering, and it stays per product.
    /// </summary>
    public static class DropdownFit
    {
        /// <summary>
        /// How much of the available room a list may take.
        ///
        /// ⚠ Not the whole of it: a dropdown that covers its window stops reading as something
        /// standing in front of a screen and starts reading as a new screen. Just under half leaves
        /// the context visible behind it, which is what makes it dismissible in the reader's mind.
        /// </summary>
        public const double Share = 0.45;

        /// <summary>
        /// The fewest rows a list is allowed to show, whatever the room.
        ///
        /// ⚠ A list cut to one or two rows is worse than a tall one: it turns choosing into
        /// scrolling. On a short window the share above can ask for less than this, and this wins.
        /// </summary>
        public const int LeastRows = 4;

        /// <summary>
        /// How tall the rows themselves may be, given <paramref name="count"/> of them at
        /// <paramref name="rowHeight"/> each and <paramref name="available"/> pixels of room.
        ///
        /// ⚠ Never taller than the list actually is: a panel with empty space under the last row
        /// says an entry is missing.
        /// </summary>
        public static double Height(int count, double rowHeight, double available)
        {
            if (rowHeight <= 0) return 0;

            var room = available > 0 ? available * Share : LeastRows * rowHeight;
            var floor = LeastRows * rowHeight;
            var allowed = Math.Max(floor, room);
            var wanted = Math.Max(0, count) * rowHeight;

            return Math.Min(wanted, allowed);
        }

        /// <summary>
        /// Whether this list has to be searchable: it does exactly when it cannot be seen at once.
        /// </summary>
        public static bool NeedsSearch(int count, double rowHeight, double available)
        {
            return Overflows(count, rowHeight, Height(count, rowHeight, available));
        }

        /// <summary>
        /// The same question asked of a list whose height is already settled by something else.
        ///
        /// ⚠ It exists for a caller that cannot let this decide its geometry. The mod's popup opens
        /// downwards from its button and nothing flips it when it would run off the bottom, so its
        /// height is its own business until that is dealt with — but whether the list needs a search
        /// field is the same question there as anywhere, and it is answered here rather than by a
        /// judgement written at each call site.
        /// </summary>
        public static bool Overflows(int count, double rowHeight, double listHeight)
        {
            if (rowHeight <= 0) return false;

            return Math.Max(0, count) * rowHeight > listHeight + 0.5;
        }
    }
}
