using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// How to ask a local AI server to let go of a model, or to keep it — per server, since none of
    /// this is in the OpenAI-compatible surface every one of them otherwise shares.
    ///
    /// Measured against each server's own documentation (2026-09-25):
    ///
    /// | server | unload | keep loaded |
    /// |---|---|---|
    /// | Ollama | POST /api/generate {model, keep_alive: 0} | same route, keep_alive: -1 — its /v1 route ignores keep_alive |
    /// | LM Studio | POST /api/v1/models/unload {instance_id} | a model loaded by hand never unloads by itself |
    /// | llama.cpp llama-server, router mode | POST /models/unload {model} | evicts the least recently used only when another is asked for |
    /// | vLLM | nothing: one model per process | nothing |
    ///
    /// ⚠ vLLM's /sleep is left out on purpose: it exists only in its developer mode, and a sleeping
    /// server does not wake by itself — the next request, from this game or any other program,
    /// would fail. Freeing memory is a favour; breaking the server for its next user is not one.
    ///
    /// ⚠ No server says what it is on the route the mod speaks, so a release is TRIED, one route
    /// after the other, until one says yes. A route a server does not know answers "not found" at
    /// once, which costs nothing on this machine — and only a server on this machine or network is
    /// ever asked (<see cref="Endpoints.IsOnYourOwnNetwork"/>), never a third party.
    ///
    /// ⚠ No JSON here, like the rest of the socle: a request is an address and its fields, and the
    /// caller writes the body with its own library.
    /// </summary>
    public static class ModelMemory
    {
        /// <summary>One request: where, which server it is meant for (for the log), and the body's fields.</summary>
        public sealed class Request
        {
            public string Server;
            public string Url;
            public IReadOnlyList<KeyValuePair<string, object>> Fields;
        }

        /// <summary>
        /// The requests that release <paramref name="model"/>, in the order to try them: stop at the
        /// first one answered with success. Empty for a server that is not on this machine or
        /// network, or when there is nothing to name.
        /// </summary>
        public static IReadOnlyList<Request> Release(string baseUrl, string model)
        {
            if (!Applies(baseUrl, model)) return Array.Empty<Request>();
            string root = Endpoints.RootOf(baseUrl);
            return new[]
            {
                Make("Ollama", root + "/api/generate", "model", model, "keep_alive", 0),
                Make("LM Studio", root + "/api/v1/models/unload", "instance_id", model),
                Make("llama.cpp", root + "/models/unload", "model", model),
            };
        }

        /// <summary>
        /// The request that keeps <paramref name="model"/> loaded until released, or null where no
        /// server needs one: only Ollama unloads an idle model on its own (after five minutes),
        /// and only its native route takes the instruction. Sent after every answer, since each
        /// OpenAI-compatible request puts Ollama's own delay back on the model.
        /// </summary>
        public static Request KeepLoaded(string baseUrl, string model)
        {
            if (!Applies(baseUrl, model)) return null;
            return Make("Ollama", Endpoints.RootOf(baseUrl) + "/api/generate", "model", model, "keep_alive", -1);
        }

        private static bool Applies(string baseUrl, string model) =>
            !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(model) && Endpoints.IsOnYourOwnNetwork(baseUrl);

        private static Request Make(string server, string url, params object[] pairs)
        {
            var fields = new List<KeyValuePair<string, object>>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                fields.Add(new KeyValuePair<string, object>((string)pairs[i], pairs[i + 1]));
            return new Request { Server = server, Url = url, Fields = fields };
        }
    }
}
