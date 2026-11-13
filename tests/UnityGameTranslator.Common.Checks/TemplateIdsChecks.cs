using System;
using System.Collections.Generic;

namespace UnityGameTranslator.Common.Checks
{
    /// <summary>
    /// The one thing the corpus cannot hold: a REAL collision of two template ids needs two keys with
    /// the same 48 bits. So it is forced here, with an id function that keeps only the length of the key.
    /// </summary>
    internal static class TemplateIdsChecks
    {
        public static void Run(Action<bool, string, string> check)
        {
            Func<string, string> tiny = key => key.Length.ToString();

            var found = TemplateIds.Collision(new[] { "[!L*a] one", "[!L*a] two" }, tiny);
            check(found.HasValue && found.Value.Code == LineWrites.TemplateIdCollision,
                "two templates with one id are refused", "a silent pick would show one template's words for the other");
            check(found.HasValue && found.Value.Words.Contains("[!L*a] one") && found.Value.Words.Contains("[!L*a] two"),
                "and the refusal names both", "a reason that names nobody cannot be fixed");

            var plain = TemplateIds.Collision(new[] { "plain one", "plain two" }, tiny);
            check(!plain.HasValue, "keys that are not templates never collide", "only a key with a closed slot is a template");

            var apart = TemplateIds.Collision(new[] { "[!L*a] one", "[!L*b] twos" }, tiny);
            check(!apart.HasValue, "templates with different ids do not collide", "the check must be able to say no");

            check(TemplateIds.Of("[!L*a]") != TemplateIds.Of("[!L*b]"), "two keys give two ids", "the id is the key's own");
            check(System.Text.RegularExpressions.Regex.IsMatch(TemplateIds.Of("x"), "^t[0-9a-f]{12}$"),
                "an id is `t` and twelve lower-case hex digits", "the schema's templateId pattern");
        }
    }
}
