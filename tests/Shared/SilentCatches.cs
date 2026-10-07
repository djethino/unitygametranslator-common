#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace UnityGameTranslator.Checks.Shared
{
    /// <summary>
    /// No catch may swallow a failure without a word — in the mod, in the Manager, in this library.
    /// Every catch either says it (a Log call, <c>Faults.Say</c>) or lets it go on (throw). A
    /// <c>catch { }</c>, one that only returns, continues or breaks, one that quietly hands back a
    /// default — all the same blindness.
    ///
    /// 🔴 **Written once, linked by the three check projects** (the mod's Core.Checks, the
    /// Manager's Core.Checks, Common.Checks). It began in the mod alone on 2026-09-27 and took the
    /// Core from 375 silent catches to none; the Manager, which nothing held, had 234 on 2026-10-07,
    /// one of which announced a backup that had not been written just before deleting what it was
    /// meant to protect. A rule held in one product is a rule the other two break.
    ///
    /// ⚠ **No baseline, no ratchet any more**: every product is at zero, and one silent catch fails
    /// the run. Each one is fixed by READING it: a condition that recognises the case
    /// (<c>Uri.TryCreate</c>, <c>File.Exists</c>, a type check), or <c>Faults.Say</c> at a real boundary.
    ///
    /// ⚠ **A debug line is not a voice for a bare catch** (2026-10-07). The mod's debug log is off by
    /// default, so <c>catch (Exception) { LogDebug(…) }</c> was as blind as an empty one for every
    /// player — 74 of them, some hiding a whole pass that failed. A PRECISE type answered at debug
    /// level stays allowed: that is an expected answer (an overload that does not take the argument,
    /// a route this server lacks), and naming the type is what says it was expected.
    /// </summary>
    internal static class SilentCatches
    {
        // Read with the comments and the string literals taken out, so a comment inside a catch does
        // not hide it and one that merely NAMES the pattern (this file's own documentation) is not
        // counted.
        private static readonly Regex Comments = new Regex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex Strings = new Regex(@"@""(?:[^""]|"""")*""|\$?""(?:[^""\\\n]|\\.)*""", RegexOptions.Compiled);
        private static readonly Regex CatchHead = new Regex(@"\bcatch\b\s*(\([^)]*\))?\s*(when\s*\([^)]*\)\s*)?\{", RegexOptions.Compiled);
        // What makes a catch NOT silent: it says something, or it lets the failure go on. The
        // Manager's Journal.Note is a voice too: a case recognised and written down, once.
        private static readonly Regex Speaks = new Regex(@"\bLog\w*\s*\(|\bFaults\.|\bJournal\.Note\s*\(|\bthrow\b|\bSay\w*\s*\(", RegexOptions.Compiled);
        private static readonly Regex DebugLine = new Regex(@"\bLogDebug\s*\(", RegexOptions.Compiled);
        private static readonly Regex CaughtName = new Regex(@"catch\s*\(\s*[\w.]+\s+(\w+)\s*\)", RegexOptions.Compiled);
        private static readonly Regex CaughtType = new Regex(@"catch\s*\(\s*([\w.]+)", RegexOptions.Compiled);
        // A body that does nothing but name an outcome: `return SseStopReason.Closed;`.
        private static readonly Regex NamesOutcome = new Regex(@"^\{\s*return\s+\w+\.\w+\s*;\s*\}$", RegexOptions.Compiled);

        /// <summary>How many catches in this source neither say anything nor rethrow.</summary>
        internal static int CountSilent(string source) => SilentLines(source).Count;

        /// <summary>
        /// The line of each silent catch. Comments and strings are blanked, never removed, so a
        /// position in the blanked text is a position in the source.
        /// </summary>
        internal static List<int> SilentLines(string source)
        {
            // Strings are kept for one question only — does the catch use its exception, which an
            // interpolated $"…{ex.Message}" does — and blanked for the rest (braces, calls).
            string withStrings = Comments.Replace(source, Blank);
            string code = Strings.Replace(withStrings, Blank);
            var lines = new List<int>();
            foreach (Match m in CatchHead.Matches(code))
            {
                int open = m.Index + m.Length - 1;
                int depth = 0, end = -1;
                for (int i = open; i < code.Length; i++)
                {
                    if (code[i] == '{') depth++;
                    else if (code[i] == '}' && --depth == 0) { end = i; break; }
                }
                if (end < 0) continue;
                string body = code.Substring(open, end - open + 1);
                var type = CaughtType.Match(m.Value);
                bool bare = !type.Success || type.Groups[1].Value == "Exception" || type.Groups[1].Value == "System.Exception";

                if (Speaks.IsMatch(body))
                {
                    // Says it — unless the only thing it says is a debug line, from a catch that
                    // names no case. Then nobody reads it.
                    bool onlyDebug = DebugLine.IsMatch(body) && !Speaks.IsMatch(DebugLine.Replace(body, Blank));
                    if (!(onlyDebug && bare)) continue;
                    lines.Add(LineOf(code, m.Index));
                    continue;
                }
                // Recognised, not swallowed — the two shapes where the catch IS the condition:
                // ① a filter on this code's own cancellation (`when (ct.IsCancellationRequested)`):
                //   the stop it asked for, and any other cancellation goes on past it;
                // ② one precise type turned into a named outcome the caller acts on and reports
                //   (`catch (RegexMatchTimeoutException) { return Outcome.TimedOut; }`). A plain
                //   Exception never qualifies: it names nothing.
                if (m.Groups[2].Success && m.Groups[2].Value.Contains("IsCancellationRequested")) continue;
                if (!bare && NamesOutcome.IsMatch(body)) continue;
                // It carries the exception somewhere (a diagnostic line it returns, a field a
                // report reads): not mute either.
                var named = CaughtName.Match(m.Value);
                if (named.Success && Regex.IsMatch(withStrings.Substring(open, end - open + 1),
                        @"\b" + Regex.Escape(named.Groups[1].Value) + @"\b")) continue;
                lines.Add(LineOf(code, m.Index));
            }
            return lines;
        }

        private static int LineOf(string code, int index) => 1 + code.Take(index).Count(c => c == '\n');

        // Same length, line breaks kept: positions survive the blanking.
        private static string Blank(Match m) => new string(m.Value.Select(c => c == '\n' ? '\n' : ' ').ToArray());

        /// <summary>The counter itself, on the shapes it has to tell apart.</summary>
        internal static void SelfCheck(Action<bool, string, string> check)
        {
            void Counts(string code, int expected, string what)
            {
                int got = CountSilent(code);
                check(got == expected, what, got == expected ? "the counter counts what it names" : $"counted {got}, expected {expected}");
            }
            Counts("try { A(); } catch { }", 1, "an empty catch is silent");
            Counts("try { A(); } catch { return null; }", 1, "one that only returns is silent");
            Counts("try { A(); } catch { list = new List<int>(); }", 1, "one that quietly hands back a default is silent");
            Counts("try { A(); } catch (Exception ex) { Faults.Say(\"here\", ex); }", 0, "one that says it through Faults is not");
            Counts("try { A(); } catch (Exception e) { TranslatorCore.LogWarning(e.Message); return; }", 0, "nor one that logs");
            Counts("try { A(); } catch { throw; }", 0, "nor one that lets it go on");
            Counts("try { return A(); } catch (Exception ex) { return \"unknown (\" + ex.GetType().Name + \")\"; }", 0,
                "nor one that carries the exception into what it returns");
            Counts("try { A(); } catch (Exception ex) { return null; }", 1, "but naming it and dropping it is silent");
            Counts("try { A(); } catch (Exception ex) { note = $\"(error: {ex.Message})\"; }", 0,
                "an interpolated string that carries it counts as carrying it");
            Counts("// catch { }\nvar s = \"catch { }\";", 0, "a catch in a comment or a string is not code");
            Counts("try { A(); } catch { if (x) { y = 1; } }", 1, "nested braces are read to the catch's own end");
            Counts("try { A(); } catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }", 0,
                "a filter on its own cancellation recognises the case");
            Counts("try { A(); } catch (OperationCanceledException) when (other) { return; }", 1, "any other filter does not");
            Counts("try { A(); } catch (RegexMatchTimeoutException) { return Outcome.TimedOut; }", 0,
                "one precise type turned into a named outcome is recognised");
            Counts("try { A(); } catch (Exception) { return Outcome.Failed; }", 1, "but a plain Exception names nothing");
            Counts("try { A(); } catch (IOException) { return null; }", 1, "and a precise type dropped to null is still silent");
            Counts("try { A(); } catch (Exception ex) { TranslatorCore.LogDebug(ex.Message); }", 1,
                "a bare catch that only says it at debug level is silent: debug is off for players");
            Counts("try { A(); } catch { LogDebug(\"refused\"); }", 1, "so is an untyped one");
            Counts("try { A(); } catch (ArgumentException ex) { TranslatorCore.LogDebug(ex.Message); }", 0,
                "a precise type answered at debug level is an expected answer");
            Counts("try { A(); } catch (Exception ex) { LogDebug(\"x\"); Faults.Say(\"here\", ex); }", 0,
                "a debug line beside a real voice is fine");
            Counts("try { A(); } catch (ArgumentException) { Journal.Note(\"process\", \"ended first\"); }", 0,
                "a case the Manager notes in its journal is said");
        }

        /// <summary>
        /// No silent catch in any .cs file under <paramref name="roots"/> (bin/obj excluded). Paths in
        /// the message are relative to <paramref name="relativeTo"/>.
        /// </summary>
        internal static void NoneUnder(Action<bool, string, string> check, string what,
                                       IEnumerable<string> roots, string relativeTo)
        {
            var found = Find(roots, relativeTo, null);
            check(found.Count == 0, what,
                found.Count == 0
                    ? "a failure swallowed without a word is a failure nobody can diagnose"
                    : "🔴 " + string.Join(", ", found.Take(20)) + (found.Count > 20 ? $" … ({found.Count} in all)" : "")
                      + " — recognise the case with a condition, or say it through Faults.Say");
        }

        /// <summary>`silent-list [file]`: where the silent catches are, one per line.</summary>
        internal static int List(IEnumerable<string> roots, string relativeTo, string? only)
        {
            foreach (string where in Find(roots, relativeTo, only)) Console.WriteLine(where);
            return 0;
        }

        private static List<string> Find(IEnumerable<string> roots, string relativeTo, string? only)
        {
            var found = new List<string>();
            foreach (string root in roots)
            {
                foreach (string path in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal))
                {
                    string rel = Path.GetRelativePath(relativeTo, path).Replace('\\', '/');
                    if (rel.Contains("/obj/") || rel.Contains("/bin/")) continue;
                    if (only != null && !rel.EndsWith(only, StringComparison.OrdinalIgnoreCase)) continue;
                    foreach (int line in SilentLines(File.ReadAllText(path))) found.Add($"{rel}:{line}");
                }
            }
            return found;
        }

        /// <summary>
        /// The first folder, walking up from where the checks run, that holds
        /// <paramref name="marker"/> (a relative path to a file or folder) — null when none does.
        /// </summary>
        internal static string? FolderHolding(string marker)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, marker);
                if (File.Exists(candidate) || Directory.Exists(candidate)) return dir.FullName;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
