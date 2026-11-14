using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The class for the texts declared ASCII. Held against the runtime on ASCII ONLY (where every runtime agrees, so the
    /// comparison proves the class does what OrdinalIgnoreCase did there), and against the exact opposite outside it: no other
    /// letter folds, whatever the machine's Unicode says.
    /// </summary>
    internal static class AsciiChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            int disagreements = 0;
            for (char a = '\0'; a < 128; a++)
            {
                if (Ascii.Lower(a) != (a >= 'A' && a <= 'Z' ? (char)(a + 32) : a)) disagreements++;
                for (char b = '\0'; b < 128; b++)
                {
                    string x = new string(a, 1), y = new string(b, 1);
                    if (Ascii.EqualsIgnoreCase(x, y) != string.Equals(x, y, StringComparison.OrdinalIgnoreCase)) disagreements++;
                }
            }
            check(disagreements == 0, "on ASCII, the class answers as OrdinalIgnoreCase did (all 16 384 pairs)", disagreements + " pair(s) differ");

            check(!Ascii.EqualsIgnoreCase("é", "É"), "é is not É", "only A-Z fold: a name is not ASCII and goes elsewhere");
            check(!Ascii.EqualsIgnoreCase("ı", "I") && !Ascii.EqualsIgnoreCase("İ", "i"), "the Turkish dotted and dotless i fold with nothing",
                "the same answer under a Turkish culture and any other");
            check(!Ascii.EqualsIgnoreCase("K", "k"), "the Kelvin sign is not k", "a runtime that folds it answers otherwise elsewhere");
            check(!Ascii.EqualsIgnoreCase("ſ", "s"), "the long s is not s", "same");
            check(Ascii.EqualsIgnoreCase(null, null) && !Ascii.EqualsIgnoreCase(null, "") && !Ascii.EqualsIgnoreCase("", null),
                "two nulls are equal, a null is no text", "what string.Equals says");

            string lowAlready = new string('x', 3) + "1.2";   // built at run time: not the same instance as any literal
            check(ReferenceEquals(Ascii.Lower(lowAlready), lowAlready), "a text with no capital is returned as it is", "nothing to allocate");
            check(Ascii.Lower("Hello É") == "hello É", "only the capitals of A-Z are lowered", "É stays");

            check(Ascii.StartsWithIgnoreCase("Shift+F10", "shift+") && !Ascii.StartsWithIgnoreCase("Sh", "shift+") && !Ascii.StartsWithIgnoreCase(null, "a"),
                "a prefix, and none longer than the text", "Hotkeys reads « Shift+ » so");
            check(Ascii.EndsWithIgnoreCase("Font.TTF", ".ttf") && !Ascii.EndsWithIgnoreCase("f", ".ttf") && Ascii.EndsWithIgnoreCase("x", ""),
                "a suffix, and none longer than the text", "extensions");
            check(Ascii.IndexOfIgnoreCase("Unsupported: SEED not allowed", "seed") == 13 && Ascii.IndexOfIgnoreCase("none", "seed") == -1 && Ascii.IndexOfIgnoreCase("abc", "") == 0,
                "a keyword is found in a provider's refusal, wherever it is", "Negotiation");
            check(Ascii.IndexOfIgnoreCase("café seed", "seed") == 5, "found after a non-ASCII letter", "positions are UTF-16 units, as IndexOf's");
        }
    }
}
