using System;
using System.Linq;

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

            // ⚠ One spelling of "this machine", or a difference report offers to fix an address
            // that is already right — which is exactly what it did.
            check(Endpoints.OllamaDefault == "http://127.0.0.1:11434",
                "the local default is the literal address", "a name resolves, and resolves to ::1 first");
            check(Endpoints.LocalServer(1234) == "http://127.0.0.1:1234",
                "and every other local port is spelled the same way", "one rule, whatever the server");

            // ⚠ An older "localhost" is respelled silently — so every case where it must NOT be is
            // the half that matters. The host, whole, and nothing else.
            check(Endpoints.Canonical("http://localhost:11434") == "http://127.0.0.1:11434",
                "a typed localhost becomes the one spelling", "files written before the rule carry it");
            check(Endpoints.Canonical("http://LocalHost:1234/v1/chat/completions?x=1#f")
                  == "http://127.0.0.1:1234/v1/chat/completions?x=1#f",
                "whatever its case, and the rest is kept as typed", "only the host differs");
            // ⚠ Not an Ollama rule, and NOT a list of accepted ports: the port and the server are
            // never read. Whatever engine somebody runs and whatever port they moved it to, the
            // port comes back exactly as typed — these are arbitrary values, not a whitelist.
            check(new[] { 1, 1234, 11434, 43219, 65535 }.All(port =>
                      Endpoints.Canonical("http://localhost:" + port + "/v1") == "http://127.0.0.1:" + port + "/v1")
                  && Endpoints.Canonical("https://localhost/v1/chat/completions") == "https://127.0.0.1/v1/chat/completions",
                "any local engine, any port it was moved to, any scheme",
                "the host alone changes; nothing about the setup is imposed");
            check(Endpoints.Canonical("localhost:11434") == "127.0.0.1:11434",
                "with no scheme too", "people type what they remember");
            check(Endpoints.Canonical("http://user:secret@localhost:8080/v1") == "http://user:secret@127.0.0.1:8080/v1",
                "credentials in front are kept", "the host is after the last @");
            check(Endpoints.Canonical("https://localhost.com/v1") == "https://localhost.com/v1"
                  && Endpoints.Canonical("http://mylocalhost:11434") == "http://mylocalhost:11434"
                  && Endpoints.Canonical("http://localhost.example.org") == "http://localhost.example.org",
                "a host merely containing the word is another machine", "compared whole, never as a substring");
            check(Endpoints.Canonical("https://api.example.com/localhost/v1?h=localhost")
                  == "https://api.example.com/localhost/v1?h=localhost",
                "a path or query saying localhost is left alone", "it is somebody else's text");
            check(Endpoints.Canonical("http://localhost@api.example.com/v1") == "http://localhost@api.example.com/v1",
                "a user NAMED localhost is not the host", "the host is api.example.com");
            check(Endpoints.Canonical("http://[::1]:11434") == "http://[::1]:11434",
                "the IPv6 loopback typed on purpose is kept", "the one server 127.0.0.1 would not reach");
            check(Endpoints.Canonical("http://127.0.0.1:11434") == "http://127.0.0.1:11434"
                  && Endpoints.Canonical("") == "" && Endpoints.Canonical(null) == null
                  && Endpoints.Canonical("http://") == "http://" && Endpoints.Canonical("/v1") == "/v1",
                "anything else comes back untouched", "no host to rename, nothing to throw");

            // ⚠ Three localities, because each carries a different consequence. Folding any two of
            // them together produces a sentence that is wrong for somebody.
            check(Endpoints.Where("http://127.0.0.1:11434") == Endpoints.Locality.ThisMachine,
                "loopback is this machine", "nothing leaves it");
            check(Endpoints.Where("http://127.0.0.2:11434") == Endpoints.Locality.ThisMachine,
                "and so is the rest of 127", "the whole block is loopback, not just the first address");
            check(Endpoints.Where("http://localhost:11434") == Endpoints.Locality.ThisMachine,
                "the name counts too", "somebody may well have typed it");
            check(Endpoints.Where("http://192.168.1.20:11434") == Endpoints.Locality.YourNetwork,
                "a private address is your network", "yours, but not this machine");
            check(Endpoints.Where("http://nas.local:1234") == Endpoints.Locality.YourNetwork,
                "and so is a .local name", "what a household router and mDNS hand out");
            check(Endpoints.Where("https://api.openai.com/v1") == Endpoints.Locality.Elsewhere,
                "a provider is elsewhere", "somebody else's service, on somebody else's terms");
            check(Endpoints.Where("gibberish") == Endpoints.Locality.Elsewhere,
                "and so is anything we cannot read", "an unnecessary caution costs nothing");

            // ⚠ What is said about each. Silence on the local one is the load-bearing part: free
            // local translation is what this project offers, and a bill notice under it would
            // contradict the offer.
            check(Endpoints.CautionFor("http://127.0.0.1:11434") == null,
                "nothing is said about a server on this machine", "nothing leaves and nothing is billed");
            check(Endpoints.CautionFor("http://192.168.1.20:11434") is string onNetwork
                  && onNetwork.Contains("network") && !onNetwork.Contains("bills"),
                "a server on your network raises privacy, never cost", "nobody bills you for your own box");
            check(Endpoints.CautionFor("https://api.openai.com/v1") is string remote
                  && remote.Contains("bills") && remote.Contains("sent"),
                "a provider raises both", "the text leaves AND the meter runs");
        }
    }
}
