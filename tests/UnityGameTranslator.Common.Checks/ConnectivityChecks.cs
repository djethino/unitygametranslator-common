using System;
using System.Net;
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

            // 🔴 What the mod's OWN client throws (Net/Http/WebRequestTransport): an HttpRequestException around the runtime's WebException,
            // whose status says what happened. A server that does not answer was "Unknown": the player saw nothing.
            Is(check, Web(WebExceptionStatus.Timeout), ConnectionProblem.NoAnswer, "the server did not answer: the request's own deadline");
            Is(check, new AggregateException("One or more errors occurred.", Web(WebExceptionStatus.Timeout)), ConnectionProblem.NoAnswer,
               "found under the task's wrapper");
            Is(check, new System.Reflection.TargetInvocationException(Web(WebExceptionStatus.Timeout)), ConnectionProblem.NoAnswer,
               "found under a call made by reflection");
            Is(check, new AggregateException(new System.Reflection.TargetInvocationException(Web(WebExceptionStatus.Timeout))),
               ConnectionProblem.NoAnswer, "found under both");
            Is(check, Web(WebExceptionStatus.RequestCanceled), ConnectionProblem.NoAnswer, "a request aborted before an answer came");
            Is(check, Web(WebExceptionStatus.NameResolutionFailure), ConnectionProblem.AddressNotFound, "the name did not resolve");
            Is(check, Web(WebExceptionStatus.ProxyNameResolutionFailure), ConnectionProblem.AddressNotFound, "the proxy's name did not resolve");
            Is(check, Web(WebExceptionStatus.SecureChannelFailure), ConnectionProblem.SecureHandshake, "the secure channel could not be set up");
            Is(check, Web(WebExceptionStatus.TrustFailure), ConnectionProblem.SecureHandshake, "the certificate is not trusted");
            Is(check, Web(WebExceptionStatus.ConnectFailure), ConnectionProblem.Refused, "could not connect, nothing underneath says why");
            Is(check, Web(WebExceptionStatus.ConnectFailure, new SocketException((int)SocketError.AccessDenied)), ConnectionProblem.BlockedLocally,
               "a status that only says THAT the connection failed gives way to the socket error that says why");
            Is(check, Web(WebExceptionStatus.ConnectFailure, new SocketException((int)SocketError.HostUnreachable)), ConnectionProblem.NoNetwork,
               "the same, for the network");
            Is(check, Web(WebExceptionStatus.ReceiveFailure), ConnectionProblem.Interrupted, "cut while the answer was coming");
            Is(check, Web(WebExceptionStatus.SendFailure), ConnectionProblem.Interrupted, "cut while the request was going");
            Is(check, Web(WebExceptionStatus.ConnectionClosed), ConnectionProblem.Interrupted, "the server closed the connection");
            Is(check, Web(WebExceptionStatus.ReceiveFailure, new SocketException((int)SocketError.TimedOut)), ConnectionProblem.NoAnswer,
               "the socket error underneath still has the last word");
            Is(check, Web(WebExceptionStatus.ProtocolError), ConnectionProblem.Unknown, "the server answered, with an error status: not a connection problem");
            Is(check, Web(WebExceptionStatus.UnknownError), ConnectionProblem.Unknown, "a failure the runtime could not name stays unnamed");

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

        /// <summary>A WebException as the mod's client hands it over: wrapped, as the transport wraps it.</summary>
        private static Exception Web(WebExceptionStatus status, Exception? inner = null) =>
            new HttpRequestException("An error occurred while sending the request", new WebException("web " + status, inner, status, null));

        private static void Is(Action<bool, string, string> check, Exception? error,
                               ConnectionProblem expected, string why)
        {
            var actual = Connectivity.Classify(error);
            check(actual == expected, $"Classify -> {actual}", why);
        }
    }
}
