using System;
using System.Linq;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// Which requests free or keep a model, per server — the routes are written out here from each
    /// server's documentation rather than read back from the table, so a changed route has to be
    /// changed here on purpose.
    /// </summary>
    internal static class ModelMemoryChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            var release = ModelMemory.Release("http://127.0.0.1:11434", "qwen3:8b");
            check(release.Select(r => r.Url).SequenceEqual(new[]
                  {
                      "http://127.0.0.1:11434/api/generate",
                      "http://127.0.0.1:11434/api/v1/models/unload",
                      "http://127.0.0.1:11434/models/unload",
                  }),
                "a release is tried on Ollama, then LM Studio, then llama.cpp, at the server's root",
                "the mod speaks one route to all of them and cannot tell which one answers");

            var ollama = release[0];
            check(ollama.Fields.Any(f => f.Key == "keep_alive" && Equals(f.Value, 0)) && ollama.Fields.Any(f => f.Key == "model" && (string)f.Value == "qwen3:8b"),
                "Ollama is released with keep_alive 0 and the model's name",
                "any other keep_alive keeps it on the graphics card");
            check(release[1].Fields.Any(f => f.Key == "instance_id" && (string)f.Value == "qwen3:8b"),
                "LM Studio is released by instance_id",
                "its unload route names the instance, not the model");

            check(ModelMemory.Release("http://127.0.0.1:1234/v1/chat/completions", "m")[0].Url == "http://127.0.0.1:1234/api/generate",
                "an address pasted with its /v1 path is released at the root",
                "the native routes are not under the OpenAI-compatible surface");

            check(ModelMemory.Release("https://api.openai.com/v1", "gpt-4o").Count == 0 && ModelMemory.KeepLoaded("https://api.openai.com/v1", "gpt-4o") == null,
                "a server elsewhere on the internet is never asked",
                "unknown routes fired at a third party would be traffic sent for nothing");

            check(ModelMemory.Release("http://127.0.0.1:11434", "").Count == 0,
                "no model named, nothing asked",
                "an unload without a name is a request no server can act on");

            var keep = ModelMemory.KeepLoaded("http://127.0.0.1:11434", "qwen3:8b");
            check(keep != null && keep.Url.EndsWith("/api/generate") && keep.Fields.Any(f => f.Key == "keep_alive" && Equals(f.Value, -1)),
                "keeping loaded is Ollama's keep_alive -1 on its native route",
                "only Ollama unloads an idle model by itself, and its /v1 route ignores keep_alive");
        }
    }
}
