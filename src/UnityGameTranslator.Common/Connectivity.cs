using System;
using System.Net.Sockets;

namespace UnityGameTranslator.Common
{
    /// <summary>Why a request never reached the server, in the terms a player can act on.</summary>
    public enum ConnectionProblem
    {
        /// <summary>Not a connection failure — the request got through.</summary>
        None = 0,

        /// <summary>This computer forbade the socket. A firewall or security program.</summary>
        BlockedLocally,

        /// <summary>No network at all on this machine.</summary>
        NoNetwork,

        /// <summary>The name could not be resolved to an address.</summary>
        AddressNotFound,

        /// <summary>Something answered and said no.</summary>
        Refused,

        /// <summary>Nothing answered in time.</summary>
        NoAnswer,

        /// <summary>The encrypted channel could not be agreed.</summary>
        SecureHandshake,

        /// <summary>The connection was cut part-way through.</summary>
        Interrupted,

        /// <summary>A failure we cannot name. The raw message is all there is.</summary>
        Unknown,
    }

    /// <summary>
    /// Turns the exception a failed request throws into something worth reading.
    ///
    /// 🔴 **Why this exists.** A blocked request surfaced as the operating system's own sentence —
    /// "An attempt was made to access a socket in a way forbidden by its access permissions" — which
    /// names the mechanism and not the cause. Somebody reading it has no way to tell a firewall
    /// from a server outage, and the two call for opposite reactions: check this computer, or wait.
    ///
    /// ⚠ **It never replaces the raw error, it stands in front of it.** The caller logs what was
    /// actually thrown and shows this instead; hiding the original would trade one unreadable
    /// failure for one nobody can diagnose at all.
    ///
    /// ⚠ Here in the shared library rather than in either product: the mod and the Manager both
    /// make these calls, and a player told two different things about one failure has been told
    /// nothing. Same reasoning as every other rule in this assembly.
    /// </summary>
    public static class Connectivity
    {
        /// <summary>
        /// What went wrong, read from the exception chain.
        ///
        /// ⚠ Walks INNER exceptions: an HttpRequestException is a wrapper, and the socket error —
        /// the only part that says anything specific — sits underneath it.
        /// </summary>
        public static ConnectionProblem Classify(Exception? error)
        {
            for (var e = error; e != null; e = e.InnerException)
            {
                if (e is SocketException socket)
                    return FromSocket(socket.SocketErrorCode);

                // The name rather than the type: System.Security.Authentication is not somewhere a
                // netstandard2.0 consumer is guaranteed to bind, and a TLS failure is worth naming.
                string name = e.GetType().Name;
                if (name == "AuthenticationException") return ConnectionProblem.SecureHandshake;

                // Our own deadline, or the framework's. Not distinguishable, and not worth
                // distinguishing: both mean nothing came back in time.
                if (e is TimeoutException) return ConnectionProblem.NoAnswer;
                if (name == "TaskCanceledException" || name == "OperationCanceledException")
                    return ConnectionProblem.NoAnswer;
            }

            return error == null ? ConnectionProblem.None : ConnectionProblem.Unknown;
        }

        private static ConnectionProblem FromSocket(SocketError code)
        {
            switch (code)
            {
                // 10013. The one that started this: the request never left the machine.
                case SocketError.AccessDenied:
                    return ConnectionProblem.BlockedLocally;

                case SocketError.NetworkDown:
                case SocketError.NetworkUnreachable:
                case SocketError.HostUnreachable:
                case SocketError.AddressNotAvailable:
                    return ConnectionProblem.NoNetwork;

                case SocketError.HostNotFound:
                case SocketError.TryAgain:
                case SocketError.NoData:
                    return ConnectionProblem.AddressNotFound;

                case SocketError.ConnectionRefused:
                    return ConnectionProblem.Refused;

                case SocketError.TimedOut:
                    return ConnectionProblem.NoAnswer;

                case SocketError.ConnectionReset:
                case SocketError.ConnectionAborted:
                    return ConnectionProblem.Interrupted;

                default:
                    return ConnectionProblem.Unknown;
            }
        }

        /// <summary>
        /// One sentence for the cause, one for what to do about it. Null when we cannot name the
        /// problem — the caller then shows the raw message, which is better than a guess.
        ///
        /// ⚠ Plain international English, like everything the mod and the Manager display: this is
        /// read by players in their third or fourth language, and "socket", "handshake" and "TCP"
        /// mean nothing to them.
        /// </summary>
        public static string? Explain(ConnectionProblem problem)
        {
            switch (problem)
            {
                case ConnectionProblem.BlockedLocally:
                    return "This computer blocked the connection. "
                         + "A firewall or a security program is stopping this game from going online.";

                case ConnectionProblem.NoNetwork:
                    return "This computer is not connected to a network.";

                case ConnectionProblem.AddressNotFound:
                    return "The server's address could not be found. "
                         + "The connection may be down, or a proxy or name server setting may be wrong.";

                case ConnectionProblem.Refused:
                    return "The server refused the connection. "
                         + "It may be down, or something on this computer may be intercepting the request.";

                case ConnectionProblem.NoAnswer:
                    return "The server did not answer in time. "
                         + "It may be busy or down, or a firewall may be dropping the request.";

                case ConnectionProblem.SecureHandshake:
                    return "The secure connection could not be set up. "
                         + "An antivirus or a company proxy inspecting traffic is the usual cause.";

                case ConnectionProblem.Interrupted:
                    return "The connection was cut before the answer arrived.";

                default:
                    return null;
            }
        }

        /// <summary>The sentence for this exception, or null when it cannot be named.</summary>
        public static string? Explain(Exception? error) => Explain(Classify(error));

        /// <summary>
        /// A few words for the cause, to sit INSIDE a larger sentence.
        ///
        /// ⚠ Its own form rather than a truncation of <see cref="Explain"/>: the long one is two
        /// sentences of advice, and screens that embed a reason — "could not check for a newer
        /// version (…)" — need a noun phrase, not a paragraph. Cutting the long one at the first
        /// full stop would give a sentence with a capital letter in the middle of another.
        /// </summary>
        public static string? Summarize(ConnectionProblem problem)
        {
            switch (problem)
            {
                case ConnectionProblem.BlockedLocally:  return "blocked by this computer";
                case ConnectionProblem.NoNetwork:       return "no network connection";
                case ConnectionProblem.AddressNotFound: return "the address could not be found";
                case ConnectionProblem.Refused:         return "the server refused the connection";
                case ConnectionProblem.NoAnswer:        return "no answer in time";
                case ConnectionProblem.SecureHandshake: return "the secure connection failed";
                case ConnectionProblem.Interrupted:     return "the connection was cut";
                default:                                return null;
            }
        }

        /// <summary>A few words for this exception, or its raw message when it cannot be named.</summary>
        public static string Summarize(Exception? error)
        {
            string? said = Summarize(Classify(error));
            if (!string.IsNullOrEmpty(said)) return said!;

            string? raw = error?.Message;
            return string.IsNullOrEmpty(raw) ? "the connection failed" : raw!;
        }

        /// <summary>
        /// What to show: the sentence when we have one, the raw message when we do not.
        ///
        /// ⚠ Never returns an empty string — a failure that displays as nothing reads as a failure
        /// that did not happen.
        /// </summary>
        public static string Describe(Exception? error)
        {
            string? explained = Explain(error);
            if (!string.IsNullOrEmpty(explained)) return explained!;

            string? raw = error?.Message;
            return string.IsNullOrEmpty(raw) ? "The connection failed." : raw!;
        }
    }
}
