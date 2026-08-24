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
            StoredSecrets();
            SyncState();
            MergeDecisions();
            EditingSides();
            HasItBeenPublished();
            WhoMayRate();
            WhereSomebodyStands();
            HowMuchStripFits();
            BrowserSessions();
            LanguageLookup();
            HotkeySpelling();
            QualityMeasures();
            PlaceholderRules();
            PromptWording();
            EndpointAddresses();
            WhyItNeverArrived();
            ProviderNegotiation();
            ProductColours();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("All checks passed.");
                return 0;
            }

            Console.WriteLine($"{_failures} check(s) FAILED.");
            return 1;
        }

        /// <summary>Settling one line between here, there, and what both came from.</summary>
        private static void MergeDecisions()
        {
            Section("Merge");
            MergeChecks.Run(Check);
        }

        /// <summary>Which copy an editor is about to change, and what is reachable from where.</summary>
        private static void EditingSides()
        {
            Section("Edit scope");
            EditScopeChecks.Run(Check);
        }

        /// <summary>Whether a translation has ever left this machine.</summary>
        private static void HasItBeenPublished()
        {
            Section("Publication");
            PublicationChecks.Run(Check);
        }

        /// <summary>Who may rate a translation, and why the arrows are sometimes absent.</summary>
        private static void WhoMayRate()
        {
            Section("Voting");
            VotingChecks.Run(Check);
        }

        /// <summary>The durations and the words of a browser edit session.</summary>
        private static void BrowserSessions()
        {
            Section("Edit sessions");
            EditSessionsChecks.Run(Check);
        }

        /// <summary>Which form of the scope strip fits beside a title.</summary>
        private static void HowMuchStripFits()
        {
            Section("Scope strip");
            ScopeStripChecks.Run(Check);
        }

        /// <summary>The four independent questions a screen answers, and who may write.</summary>
        private static void WhereSomebodyStands()
        {
            Section("Standing");
            StandingChecks.Run(Check);
        }

        /// <summary>The palette, against what the website actually renders.</summary>
        private static void ProductColours()
        {
            Section("Theme");
            ThemeChecks.Run(Check);
        }

        /// <summary>What a provider will accept, learned by being refused.</summary>
        private static void ProviderNegotiation()
        {
            Section("Negotiation");
            NegotiationChecks.Run(Check);
        }

        /// <summary>Why a request never arrived, in words that point at the right culprit.</summary>
        private static void WhyItNeverArrived()
        {
            Section("Connectivity");
            ConnectivityChecks.Run(Check);
        }

        /// <summary>Where a request really goes, from whatever address somebody pasted.</summary>
        private static void EndpointAddresses()
        {
            Section("Endpoints");
            EndpointsChecks.Run(Check);
        }

        /// <summary>What a model is actually told, and how a text is sorted before being asked for.</summary>
        private static void PromptWording()
        {
            Section("Prompts");
            PromptsChecks.Run(Check);
        }

        /// <summary>What a game will accept back from a model, and what it says when it will not.</summary>
        private static void PlaceholderRules()
        {
            Section("Placeholder rules");
            PlaceholdersChecks.Run(Check);
        }

        /// <summary>What a player is told about a file, checked against the website's rules.</summary>
        private static void QualityMeasures()
        {
            Section("Quality measures");
            QualityChecks.Run(Check);
        }

        /// <summary>How a keyboard shortcut is spelled — silent when wrong, hence the cases.</summary>
        private static void HotkeySpelling()
        {
            Section("Hotkeys");
            HotkeysChecks.Run(Check);
        }

        /// <summary>Codes, names, and the two inventories that must not be collapsed.</summary>
        private static void LanguageLookup()
        {
            Section("Languages");
            LanguagesChecks.Run(Check);

            Section("Flags");
            FlagChecks.Run(Check);

            Section("Badges");
            BadgesChecks.Run(Check);

            Section("Origins");
            OriginsChecks.Run(Check);

            Section("Contributions");
            ContributionsChecks.Run(Check);

            Section("People");
            PeopleChecks.Run(Check);

            Section("Backups");
            BackupsChecks.Run(Check);
        }

        /// <summary>The stored-secret format, checked against its own specification.</summary>
        private static void StoredSecrets()
        {
            Section("Stored secrets");
            SecretsChecks.Run(Check);
        }

        /// <summary>
        /// A translation's identity and where it stands against the published one.
        ///
        /// ⚠ Its vectors come from the website's implementation, not from ours — see the note at
        /// the top of SyncChecks. This is the one measure in the library where agreeing with
        /// ourselves proves nothing at all.
        /// </summary>
        private static void SyncState()
        {
            SyncChecks.Run(Check);
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
