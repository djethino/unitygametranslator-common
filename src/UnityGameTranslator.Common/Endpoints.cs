using System;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// Turning the address somebody pasted into the URL a request actually goes to.
    ///
    /// ⚠ There is no single shape to assume. OpenAI puts /v1 in front, DeepSeek has none, Gemini's
    /// compatible surface sits under /v1beta/openai, Groq under /openai/v1, and Ollama answers at
    /// the root. A naive baseUrl + "/v1/models" works for exactly one of those and quietly asks the
    /// wrong URL for the rest — someone who pasted the full chat URL their provider documents ended
    /// up being tested at ".../v1/chat/completions/v1/models".
    ///
    /// So only one thing is asked of anybody: paste the chat URL your provider documents. The
    /// endpoint that lists models is derived from it, whatever prefix that provider uses, without
    /// anyone having to describe their scheme to us.
    ///
    /// Everything here is string work on an address, with no request sent — which is the point for
    /// <see cref="IsOnYourOwnNetwork"/>: the question is asked precisely in order to decide whether
    /// to send one.
    /// </summary>
    public static class Endpoints
    {
        private const string ChatSuffix = "/chat/completions";

        /// <summary>
        /// How we spell "this machine", everywhere, in both programs.
        ///
        /// ⚠ ONE spelling, and it lives here because of something we watched happen: the mod
        /// shipped "localhost" as its default while somebody had typed "127.0.0.1" into their game,
        /// and the manager's difference report duly offered to "fix" an address that was already
        /// correct. Two spellings of one endpoint are indistinguishable from a real disagreement to
        /// anything comparing them as text — and everything compares them as text.
        ///
        /// ⚠ **127.0.0.1 rather than "localhost", and that is not a style choice.** "localhost" is
        /// a NAME: it goes through resolution, it resolves to ::1 before 127.0.0.1 on Windows, and
        /// every local AI server we know of binds IPv4 by default (Ollama's own OLLAMA_HOST is
        /// 127.0.0.1). So the ordinary path is an IPv6 attempt that has to fail before the real one
        /// is tried. Measured warm, the two are the same to within noise; the first call cost 97 ms
        /// against 19 ms. What the literal removes is not those milliseconds, it is a class of
        /// failure that presents as "the AI server is not responding" with nothing to look at — a
        /// hosts file somebody edited, a DNS suffix search, a firewall dropping ::1 rather than
        /// refusing it.
        ///
        /// The one case it loses is a server bound ONLY to ::1, which is rare and which the person
        /// can type for themselves. That is the whole contract: we pick the most universal default,
        /// they put whatever they want.
        /// </summary>
        public const string LocalHost = "127.0.0.1";

        /// <summary>A local server on one port, spelled the one way. Ollama, LM Studio, any of them.</summary>
        public static string LocalServer(int port) => "http://" + LocalHost + ":" + port;

        /// <summary>Ollama where it installs itself. The default both programs offer.</summary>
        public const string OllamaDefault = "http://" + LocalHost + ":11434";

        /// <summary>Where a translation request goes.</summary>
        public static string Chat(string baseUrl) => Resolve(baseUrl, "chat/completions");

        /// <summary>The endpoint that lists models — what a connection test asks for.</summary>
        public static string Models(string baseUrl) => Resolve(baseUrl, "models");

        /// <summary>
        /// The five rules, in order. Each one exists because a real provider needed it.
        ///
        ///   "http://localhost:11434"                          → .../v1/chat/completions
        ///   "https://api.openai.com/v1/chat/completions"      → unchanged, and .../v1/models to test
        ///   "https://api.deepseek.com/chat/completions"       → unchanged, and .../models (no /v1)
        ///   ".../v1beta/openai/chat/completions"              → .../v1beta/openai/models to test
        ///   "https://api.groq.com/openai/v1"                  → .../openai/v1/chat/completions
        /// </summary>
        public static string Resolve(string baseUrl, string path)
        {
            if (baseUrl == null) return string.Empty;

            string url = baseUrl.TrimEnd('/');
            string wanted = (path ?? string.Empty).TrimStart('/');

            // 1. Already ends with what we want.
            if (url.EndsWith("/" + wanted, StringComparison.Ordinal)
                || url.EndsWith(wanted, StringComparison.Ordinal))
            {
                return url;
            }

            // 2. The chat URL was pasted but another endpoint is wanted — swap the tail. This is
            //    what makes a provider with no /v1, and one with an unusual prefix, both work
            //    without anyone describing their scheme to us.
            if (url.EndsWith(ChatSuffix, StringComparison.Ordinal))
                return url.Substring(0, url.Length - ChatSuffix.Length) + "/" + wanted;

            // 3. A /v1/ appears somewhere: cut back to it.
            int v1 = url.LastIndexOf("/v1/", StringComparison.Ordinal);
            if (v1 >= 0)
                return url.Substring(0, v1 + 3) + "/" + wanted;

            // 4. Ends with /v1: append.
            if (url.EndsWith("/v1", StringComparison.Ordinal))
                return url + "/" + wanted;

            // 5. Otherwise assume the usual /v1 prefix — Ollama, OpenAI, Groq. A provider without
            //    one is expected to have its full chat URL configured, which rule 2 then handles.
            return url + "/v1/" + wanted;
        }

        /// <summary>
        /// The server's own root, with whatever OpenAI-compatible tail was pasted taken off.
        ///
        /// Needed because a local server's native routes — the one that unloads a model, for
        /// instance — sit at the root, not under the compatible surface. "http://host:11434",
        /// ".../v1" and ".../v1/chat/completions" all have to end up at the same place.
        /// </summary>
        public static string RootOf(string baseUrl)
        {
            if (baseUrl == null) return string.Empty;

            string root = baseUrl.TrimEnd('/');

            if (root.EndsWith(ChatSuffix, StringComparison.Ordinal))
                root = root.Substring(0, root.Length - ChatSuffix.Length);

            if (root.EndsWith("/v1", StringComparison.Ordinal))
                root = root.Substring(0, root.Length - 3);

            return root.TrimEnd('/');
        }

        /// <summary>
        /// Whether this address is a server the person runs themselves — this machine, or a box on
        /// their own network.
        ///
        /// ⚠ Judged on the address alone, and an address we do not recognise counts as REMOTE.
        /// That is the safe way round: getting it wrong in one direction leaves a model loaded a
        /// few minutes longer, getting it wrong in the other sends somebody else's service a
        /// request it never asked for.
        /// </summary>
        public static bool IsOnYourOwnNetwork(string baseUrl) => Where(baseUrl) != Locality.Elsewhere;

        /// <summary>
        /// Where an AI server sits, which is the only thing that decides what has to be said about
        /// it.
        ///
        /// Three, not two, because the three carry different consequences and folding any pair of
        /// them together produces a sentence that is wrong for somebody:
        ///  · <see cref="ThisMachine"/> — nothing leaves, nothing is billed. Warning about cost
        ///    here is noise, and worse than noise: free local translation is the thing this project
        ///    exists to offer, and putting a bill notice under it contradicts the offer.
        ///  · <see cref="YourNetwork"/> — nothing is billed either, but the text of the game
        ///    crosses a network. That is a privacy statement, never a cost one.
        ///  · <see cref="Elsewhere"/> — it leaves for somebody else's service, which may well be
        ///    metered. Both things have to be said.
        /// </summary>
        public enum Locality
        {
            /// <summary>Loopback. The server is on the machine playing the game.</summary>
            ThisMachine,

            /// <summary>A private address or a .local name — another box the person owns.</summary>
            YourNetwork,

            /// <summary>Anything else, INCLUDING anything we failed to parse.</summary>
            Elsewhere,
        }

        /// <summary>
        /// Classifies an address without sending anything to it — which is the point, since the
        /// answer is what decides whether sending anything is acceptable.
        ///
        /// ⚠ Unparseable counts as <see cref="Locality.Elsewhere"/>. Every caution this answer
        /// produces is one it is safe to give unnecessarily, and every one it withholds is one
        /// somebody needed.
        /// </summary>
        public static Locality Where(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) return Locality.Elsewhere;

            try
            {
                var uri = new Uri(baseUrl.IndexOf("://", StringComparison.Ordinal) >= 0
                    ? baseUrl
                    : "http://" + baseUrl);

                string host = uri.Host.Trim('[', ']').ToLowerInvariant();

                if (host == "localhost" || host == "::1"
                    || host.StartsWith("127.", StringComparison.Ordinal))
                {
                    return Locality.ThisMachine;
                }

                // A name a household router hands out, and the one Bonjour/mDNS uses.
                if (host.EndsWith(".local", StringComparison.Ordinal)) return Locality.YourNetwork;

                // The private ranges, for a server running on another machine at home — a common
                // setup, and one where unloading the model matters just as much.
                if (host.StartsWith("10.", StringComparison.Ordinal)
                    || host.StartsWith("192.168.", StringComparison.Ordinal))
                {
                    return Locality.YourNetwork;
                }

                if (host.StartsWith("172.", StringComparison.Ordinal))
                {
                    string[] parts = host.Split('.');
                    int second;
                    if (parts.Length > 1 && int.TryParse(parts[1], out second)
                        && second >= 16 && second <= 31)
                    {
                        return Locality.YourNetwork;
                    }
                }

                return Locality.Elsewhere;
            }
            catch
            {
                // An address we cannot even parse is certainly not one we should treat as ours.
                return Locality.Elsewhere;
            }
        }

        /// <summary>
        /// What has to be said about sending a game's text to this address, or null when there is
        /// nothing to say.
        ///
        /// ⚠ Written once, for both programs, because it is a statement about somebody's money and
        /// somebody's data. Two copies would eventually differ, and the version that under-warns
        /// is the one that ends up in front of the person who needed it.
        ///
        /// ⚠ It says what LEAVES and what MIGHT be billed. It never names a price, never estimates
        /// one, and never suggests a provider: we take no part in that arrangement and are in no
        /// position to know what anybody will be charged.
        /// </summary>
        public static string CautionFor(string baseUrl)
        {
            switch (Where(baseUrl))
            {
                case Locality.ThisMachine:
                    return null;

                case Locality.YourNetwork:
                    return "This server is on your network, not on this machine. The text of your "
                         + "game is sent to it as you play. Nothing is billed for that.";

                default:
                    return "This address is not on your machine or your network. The text of your "
                         + "game is sent to it as you play, and a provider bills you directly for "
                         + "what you use — we take no part in that and cannot know what it costs.";
            }
        }
    }
}
