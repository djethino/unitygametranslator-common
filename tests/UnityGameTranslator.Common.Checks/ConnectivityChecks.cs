using System;
using System.Net.Http;
using System.Net.Sockets;
using UnityGameTranslator.Common;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Which failure a player is told about, from the exception a request actually throws.
    ///
    /// ⚠ The exceptions are built the way the runtime builds them — a wrapper around a
    /// SocketException — because the whole point is that the useful part is NOT the outer one.
    /// A check that classified a bare SocketException would pass while the real thing failed.
    /// </summary>
    internal static class ConnectivityChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // The case that started this: the request never left the machine.
            Is(check, Wrapped(SocketError.AccessDenied), ConnectionProblem.BlockedLocally,
               "a firewall refusing the socket is not a server problem");

            Is(check, Wrapped(SocketError.HostNotFound), ConnectionProblem.AddressNotFound,
               "a name that resolves to nothing");
            Is(check, Wrapped(SocketError.ConnectionRefused), ConnectionProblem.Refused,
               "something answered and said no");
            Is(check, Wrapped(SocketError.TimedOut), ConnectionProblem.NoAnswer,
               "nothing answered in time");
            Is(check, Wrapped(SocketError.NetworkUnreachable), ConnectionProblem.NoNetwork,
               "no network at all");
            Is(check, Wrapped(SocketError.ConnectionReset), ConnectionProblem.Interrupted,
               "cut part-way through");

            // Two levels of wrapper, which is what a real HttpClient failure looks like.
            var deep = new HttpRequestException("one",
                           new HttpRequestException("two", new SocketException((int)SocketError.AccessDenied)));
            Is(check, deep, ConnectionProblem.BlockedLocally, "found however deep it is buried");

            Is(check, new TimeoutException("late"), ConnectionProblem.NoAnswer,
               "our own deadline says the same thing as the socket's");

            Is(check, null, ConnectionProblem.None, "no exception, no problem");
            Is(check, new InvalidOperationException("something else"), ConnectionProblem.Unknown,
               "a failure we cannot name stays unnamed rather than being guessed");

            // Every named problem must actually say something, or the enum is decoration.
            foreach (ConnectionProblem p in Enum.GetValues(typeof(ConnectionProblem)))
            {
                bool named = p != ConnectionProblem.None && p != ConnectionProblem.Unknown;
                string? said = Connectivity.Explain(p);
                check(named == !string.IsNullOrEmpty(said),
                      $"Explain({p}) {(string.IsNullOrEmpty(said) ? "says nothing" : "says something")}",
                      named ? "a named cause owes the reader a sentence"
                            : "an unnamed one must not invent one");
            }

            // 🔴 Describe must never come back empty: a failure that displays as nothing reads as
            // a failure that did not happen.
            check(!string.IsNullOrEmpty(Connectivity.Describe(Wrapped(SocketError.AccessDenied))),
                  "Describe(blocked) is not empty", "the player is told something");
            check(Connectivity.Describe(new InvalidOperationException("raw text")) == "raw text",
                  "Describe(unknown) falls back to the raw message",
                  "better the original than a guess");
            check(!string.IsNullOrEmpty(Connectivity.Describe(new InvalidOperationException(""))),
                  "Describe(empty message) still says something",
                  "an exception with no message is still a failure");

            // The short form goes INSIDE a sentence, so it must not be one itself.
            foreach (ConnectionProblem p in Enum.GetValues(typeof(ConnectionProblem)))
            {
                string? said = Connectivity.Summarize(p);
                if (string.IsNullOrEmpty(said)) continue;

                check(said![0] == char.ToLowerInvariant(said[0]),
                      $"Summarize({p}) starts lower case",
                      "it is embedded mid-sentence, not started with");
                check(!said.EndsWith("."),
                      $"Summarize({p}) has no full stop",
                      "the sentence around it owns the punctuation");
                check(said.IndexOf('.') < 0,
                      $"Summarize({p}) is one phrase",
                      "two sentences inside a parenthesis is what this form exists to avoid");
            }

            check(Connectivity.Summarize(Wrapped(SocketError.AccessDenied)) == "blocked by this computer",
                  "Summarize(blocked) names the machine", "the reader checks their own firewall, not ours");
            check(Connectivity.Summarize(new InvalidOperationException("raw")) == "raw",
                  "Summarize(unknown) falls back to the raw message", "better the original than a guess");

            // 🔴 `.Result` wraps everything in an AggregateException whose message, on the Mono of
            // Unity 2018–2020, is only "One or more errors occurred." — the cause is underneath.
            var aggregate = new AggregateException("One or more errors occurred.",
                                                   Wrapped(SocketError.AccessDenied));
            Is(check, aggregate, ConnectionProblem.BlockedLocally, "found under the task wrapper too");
            check(Connectivity.Cause(aggregate) == "SocketException: " + new SocketException((int)SocketError.AccessDenied).Message,
                  "Cause names what was actually thrown", "the wrapper's own sentence says nothing");
            check(Connectivity.ForLog(aggregate).StartsWith(Connectivity.Explain(ConnectionProblem.BlockedLocally)!)
                  && Connectivity.ForLog(aggregate).Contains("SocketException"),
                  "ForLog gives the meaning, then the cause", "a reader and a maintainer both need their half");
            var unnamed = new AggregateException("One or more errors occurred.", new InvalidOperationException("deep"));
            check(Connectivity.Describe(unnamed) == "deep" && Connectivity.Summarize(unnamed) == "deep"
                  && Connectivity.ForLog(unnamed) == "InvalidOperationException: deep",
                  "an unnamed failure falls back to the innermost message", "not to the wrapper's");

            // The wording is read by players in their fourth language: no mechanism words.
            foreach (ConnectionProblem p in Enum.GetValues(typeof(ConnectionProblem)))
            {
                string said = Connectivity.Explain(p) ?? "";
                foreach (string jargon in new[] { "socket", "TCP", "handshake", "TLS", "SSL", "DNS" })
                {
                    check(said.IndexOf(jargon, StringComparison.OrdinalIgnoreCase) < 0,
                          $"Explain({p}) avoids \"{jargon}\"",
                          "the interface is not translated; the words have to be ordinary ones");
                }
            }
        }

        /// <summary>A socket error the way HttpClient hands it over: wrapped.</summary>
        private static Exception Wrapped(SocketError code) =>
            new HttpRequestException("request failed", new SocketException((int)code));

        private static void Is(Action<bool, string, string> check, Exception? error,
                               ConnectionProblem expected, string why)
        {
            var actual = Connectivity.Classify(error);
            check(actual == expected, $"Classify -> {actual}", why);
        }
    }
}
