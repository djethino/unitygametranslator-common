using System;
using System.Collections.Generic;
using UnityGameTranslator.Common;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Runs the shared rules against the answers they are supposed to give.
    ///
    /// This library exists so that two programs cannot disagree. That guarantee is worth exactly
    /// as much as the rules being right in the first place — and the version comparison shipped
    /// wrong for a long time in both copies, in a way nobody could see: a "-beta" suffix sorted
    /// correctly while a "-beta.1" sorted backwards, and the release process happens to use the
    /// second form. Whoever installed a beta was told, forever, that they were up to date.
    ///
    /// So each rule that moves in here brings its cases with it. Run with `dotnet run` from this
    /// folder; the exit code is what a script should read.
    /// </summary>
    internal static class Program
    {
        private static int _failures;

        private static int Main()
        {
            VersionOrdering();
            VersionTolerance();
            UpdateVerdicts();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("All checks passed.");
                return 0;
            }

            Console.WriteLine($"{_failures} check(s) FAILED.");
            return 1;
        }

        /// <summary>Which of two versions comes first, including the suffix rules.</summary>
        private static void VersionOrdering()
        {
            Section("Version ordering");

            // Numbers are numbers, not text. This is the one everybody knows about.
            Older("0.9.9", "0.9.10", "ten comes after nine");
            Older("0.9.9", "0.9.66", "sixty-six comes after nine");
            Older("0.9.66", "0.10.0", "the middle field outranks the last one");
            Older("0.11.0", "1.0.0", "and the first outranks them both");
            Older("2.0", "10.0", "two digits are not 'smaller' for starting with a one");

            // A missing field is a zero, so these are the same version written two ways.
            Same("1.2", "1.2.0", "a missing field counts as zero");
            Same("1.2.0", "1.2.0.0", "and so does a spare one");

            // The rule that was broken. A suffix means "not there yet", whatever it contains.
            Older("0.11.0-beta.1", "0.11.0", "the final release replaces the beta");
            Older("0.11.0-beta", "0.11.0", "with or without a number in the suffix");
            Older("0.11.0-beta.1", "0.11.0-beta.2", "the next beta");
            Older("0.11.0-alpha.9", "0.11.0-beta.1", "alpha before beta, whatever the numbers say");
            Older("0.11.0-beta.9", "0.11.0-rc.1", "beta before rc, same");
            Older("0.11.0-beta", "0.11.0-beta.1", "saying more means being further along");
            Older("0.11.0-rc.1", "0.11.1-beta.1", "but the version itself is read first");

            // Inside a suffix, a number ranks below a word — the standard says so and it keeps
            // "beta.1" from outranking "beta.final".
            Older("1.0.0-beta.1", "1.0.0-beta.final", "a number ranks below a word");
        }

        /// <summary>What we accept as a version at all. Tags arrive from a website, not from us.</summary>
        private static void VersionTolerance()
        {
            Section("Version tolerance");

            Same("0.11.0", "v0.11.0", "the leading v of a git tag");
            Same("0.11.0", "V0.11.0", "in either case");
            Same("0.11.0", "  v0.11.0  ", "and surrounded by blanks");
            Same("0.11.0", "0.11.0+build7", "build metadata is not part of the version");
            Same("0.11.0-beta.1", "v0.11.0-beta.1+ci", "both at once");

            // Nothing here may throw: an unreadable tag must not stop a program from starting.
            Same("", "", "two empty strings are equal, not an error");
            Older("", "1.0.0", "an absent version is older than any version");
            Older(null, "1.0.0", "including when it is null");
            Older("abc", "0.0.1", "a tag we cannot read counts as zero rather than crashing");
        }

        /// <summary>The question the calling code actually asks: do I offer this update?</summary>
        private static void UpdateVerdicts()
        {
            Section("Update verdicts");

            Offers("0.9.9", "v0.9.10", true, "0.9.10 is newer than 0.9.9");
            Offers("0.9.66", "v0.9.9", false, "not the other way round");
            Offers("0.11.0", "v0.11.0", false, "the same version is not an update");
            Offers("0.11.0", "v0.11.0-beta.1", false, "a beta does not replace a published release");
            Offers("0.11.0-beta.1", "v0.11.0", true, "but the published release replaces the beta");
            Offers("0.11.0-beta.1", "v0.11.0-beta.2", true, "and one beta replaces the previous one");
        }

        private static void Older(string? older, string? newer, string why)
        {
            Check(Versions.Compare(older, newer) < 0, $"{Show(older)} < {Show(newer)}", why);
            Check(Versions.Compare(newer, older) > 0, $"{Show(newer)} > {Show(older)}", "and the reverse holds");
        }

        private static void Same(string? a, string? b, string why)
        {
            Check(Versions.Compare(a, b) == 0, $"{Show(a)} == {Show(b)}", why);
            Check(Versions.Compare(b, a) == 0, $"{Show(b)} == {Show(a)}", "and the reverse holds");
        }

        private static void Offers(string installed, string published, bool expected, string why)
        {
            bool offered = Versions.IsNewer(installed, published);
            Check(offered == expected, $"installed {installed}, published {published} -> offer={offered}", why);
        }

        private static void Check(bool passed, string what, string why)
        {
            if (!passed) _failures++;
            Console.WriteLine($"  {(passed ? "ok  " : "FAIL")}  {what,-52}  {why}");
        }

        private static string Show(string? value) =>
            value == null ? "(null)" : value.Length == 0 ? "(empty)" : value;

        private static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine(new string('-', title.Length));
        }
    }
}
