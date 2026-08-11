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
        public static bool IsOnYourOwnNetwork(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) return false;

            try
            {
                var uri = new Uri(baseUrl.IndexOf("://", StringComparison.Ordinal) >= 0
                    ? baseUrl
                    : "http://" + baseUrl);

                string host = uri.Host.Trim('[', ']').ToLowerInvariant();

                if (host == "localhost" || host == "127.0.0.1" || host == "::1"
                    || host.EndsWith(".local", StringComparison.Ordinal))
                {
                    return true;
                }

                // The private ranges, for a server running on another machine at home — a common
                // setup, and one where unloading the model matters just as much.
                if (host.StartsWith("10.", StringComparison.Ordinal)
                    || host.StartsWith("192.168.", StringComparison.Ordinal))
                {
                    return true;
                }

                if (host.StartsWith("172.", StringComparison.Ordinal))
                {
                    string[] parts = host.Split('.');
                    int second;
                    if (parts.Length > 1 && int.TryParse(parts[1], out second))
                        return second >= 16 && second <= 31;
                }

                return false;
            }
            catch
            {
                // An address we cannot even parse is certainly not one we should treat as ours.
                return false;
            }
        }
    }
}
