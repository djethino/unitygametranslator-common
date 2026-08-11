using System;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Where a request really goes, from whatever somebody pasted.
    ///
    /// Every case below is a provider that exists. There is no single URL shape to assume, and the
    /// naive baseUrl + "/v1/models" that stood here first asked the wrong address for all but one
    /// of them — silently, since a wrong URL just answers 404 and looks like a server that is down.
    /// </summary>
    internal static class EndpointsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            // A bare host: the usual /v1 prefix is assumed.
            check(Endpoints.Chat("http://localhost:11434") == "http://localhost:11434/v1/chat/completions",
                "a bare host gets the usual prefix", "Ollama, and anyone who pasted only their address");
            check(Endpoints.Chat("http://localhost:11434/") == "http://localhost:11434/v1/chat/completions",
                "a trailing slash changes nothing", "people paste what their browser shows");

            // The chat URL pasted as documented — the one thing we ask of anybody.
            check(Endpoints.Chat("https://api.openai.com/v1/chat/completions")
                  == "https://api.openai.com/v1/chat/completions",
                "a chat URL is left alone", "it is already what we want");
            check(Endpoints.Models("https://api.openai.com/v1/chat/completions")
                  == "https://api.openai.com/v1/models",
                "and the listing endpoint is derived from it", "swapping the tail, not appending to it");

            // ⚠ The provider without /v1. Appending one would have asked a URL that does not exist.
            check(Endpoints.Models("https://api.deepseek.com/chat/completions")
                  == "https://api.deepseek.com/models",
                "a provider with no /v1 keeps having none", "we never learn its scheme, we reuse it");

            // ⚠ An unusual prefix, kept whole.
            check(Endpoints.Models("https://generativelanguage.googleapis.com/v1beta/openai/chat/completions")
                  == "https://generativelanguage.googleapis.com/v1beta/openai/models",
                "an unusual prefix survives", "cutting back to /v1 would have lost /v1beta/openai");

            // A /v1 in the middle, and one at the end.
            check(Endpoints.Chat("https://api.groq.com/openai/v1") == "https://api.groq.com/openai/v1/chat/completions",
                "an address ending in /v1 is appended to", "the prefix before it is not ours to touch");
            check(Endpoints.Models("https://api.groq.com/openai/v1/something")
                  == "https://api.groq.com/openai/v1/models",
                "a /v1 in the middle is cut back to", "whatever came after it was not what we wanted");

            // The root, for the native routes a compatible surface does not carry.
            check(Endpoints.RootOf("http://host:11434/v1/chat/completions") == "http://host:11434",
                "the root is found under a chat URL", "unloading a model is not an OpenAI route");
            check(Endpoints.RootOf("http://host:11434/v1") == "http://host:11434", "and under a /v1", "same place");
            check(Endpoints.RootOf("http://host:11434/") == "http://host:11434", "and is already itself", "nothing to strip");

            // ⚠ Whose machine is it. Wrong in one direction costs a few minutes of memory; wrong in
            // the other sends somebody else's service a request they never asked for.
            check(Endpoints.IsOnYourOwnNetwork("http://localhost:11434"), "localhost is ours", "obviously");
            check(Endpoints.IsOnYourOwnNetwork("http://192.168.1.20:11434"), "and a box on the home network",
                "a common setup, where unloading matters just as much");
            check(Endpoints.IsOnYourOwnNetwork("http://172.16.0.5:11434"), "including the middle private range",
                "172.16 to 172.31, and only those");
            check(!Endpoints.IsOnYourOwnNetwork("http://172.15.0.5:11434")
                  && !Endpoints.IsOnYourOwnNetwork("http://172.32.0.5:11434"),
                "but not the addresses either side of it", "172.15 and 172.32 are somebody else's");
            check(!Endpoints.IsOnYourOwnNetwork("https://api.openai.com"), "a provider is not",
                "we do not send it housekeeping");
            check(!Endpoints.IsOnYourOwnNetwork("not a url at all") && !Endpoints.IsOnYourOwnNetwork(""),
                "and anything unreadable counts as remote", "the safe way round");
        }
    }
}
