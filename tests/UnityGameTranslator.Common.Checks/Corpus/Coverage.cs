using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityGameTranslator.Common.Checks.Corpus
{
    /// <summary>
    /// Every public method of a rule the corpus covers must have at least one case.
    ///
    /// 🔴 This is what keeps the holes from re-forming. The inventory of 2026-09-07 found one
    /// class with no case at all and twenty-six public rules without one — each a place where a
    /// port could answer differently and nothing would say so. A rule enters the corpus with its
    /// classes named, and from then on a method added without a case fails here.
    ///
    /// ⚠ Only rules present in the manifest are judged; the others still live in their
    /// *Checks.cs files and move over one at a time. Judged: public static methods declared on
    /// the named classes, property accessors excluded.
    /// </summary>
    internal static class Coverage
    {
        public static void Run(Action<bool, string, string> check, List<CorpusRunner.LoadedRule> rules)
        {
            Console.WriteLine();
            Console.WriteLine("Corpus coverage");
            Console.WriteLine("---------------");

            var assembly = typeof(Versions).Assembly;

            foreach (var rule in rules)
            {
                check(rule.Classes.Length > 0, $"{rule.Name} names the classes it covers",
                    "without them nothing can be counted");

                // The methods this rule's operations exercise, with a case each.
                var exercised = new HashSet<string>(StringComparer.Ordinal);
                foreach (var op in Operations.All)
                {
                    if (op.Rule != rule.Name) continue;
                    if (!rule.CasesPerOp.TryGetValue(op.Name, out int cases) || cases == 0) continue;
                    foreach (var method in op.Methods) exercised.Add(op.Class.Name + "." + method);
                }

                foreach (var className in rule.Classes)
                {
                    var type = assembly.GetType("UnityGameTranslator.Common." + className);
                    check(type != null, $"{rule.Name}: {className} exists in the library",
                        "a class renamed away from its rule file would escape the count");
                    if (type == null) continue;

                    var missing = new List<string>();
                    foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (method.IsSpecialName) continue;
                        if (!exercised.Contains(className + "." + method.Name)) missing.Add(method.Name);
                    }

                    check(missing.Count == 0,
                        $"{rule.Name}: every public method of {className} has a case",
                        missing.Count == 0 ? "a rule without a case is a rule a port may get wrong"
                                           : "no case for: " + string.Join(", ", missing));
                }

                // And the other way round: an operation in the table with no case in the file.
                foreach (var op in Operations.All)
                {
                    if (op.Rule != rule.Name) continue;
                    bool hasCase = rule.CasesPerOp.TryGetValue(op.Name, out int n) && n > 0;
                    check(hasCase, $"{rule.Name}: operation '{op.Name}' has a case",
                        "an operation the executor knows and the corpus never asks is decoration");
                }
            }
        }
    }
}
