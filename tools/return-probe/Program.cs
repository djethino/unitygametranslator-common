// The public static methods of the library that take no argument, one string or two, called on one fixed list of texts (a text
// per script, per case, per kind of space, per kind of tag), their results written one per line. Run it on two versions of the
// library and diff the two outputs: what differs is a RETURN value that changed — not only a verdict. tools/diff-returns.ps1.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Text;

var inputs = new string[]
{
    null, "", " ", "a", "A", "Hello World", "  padded  ", "\u00a0nbsp\u00a0", "\u3000wide\u3000", "Cafe\u0301", "Caf\u00e9", "CAF\u00c9", "caf\u00e9",
    "<br>", "<BR>", "<b>x</b>", "<color=#FF0000>red</color>", "<#ff00ff>", "[!v*0]", "[!t*1]x[!t*2]", "{[!v*0]}", "Score: 7/10", "7 / 10th", "8,5", "\u0663",
    "fr", "FR", "French", "french", "Fran\u00e7ais", "fran\u00e7ais", "T\u00fcrk\u00e7e", "T\u00dcRK\u00c7E", "zh-CN", "zh_cn", "fr_FR.UTF-8", "pt-BR;q=0.9", "auto", "AUTO",
    "Steam", "steam", "IGDB", "catalogue", "Catalogue", "rawg", "Complete", "manager", "Manager", "Shift+F10", "ctrl + alt + F10", "Hollow Knight", "hollow-knight",
    "\u0130stanbul", "Istanbul", "\u0131sparta", "Isparta", "stra\u00dfe", "STRASSE", "\u0391\u03a3", "\u03c3\u03c2", "\U00010400", "\U00010428", "\u00c9clair", "\u00e9clair",
    "font.ttf", "FONT.TTF", "Font.OTF", "x.ugtpack", "X.UGTPACK", "CON", "con", "nul.txt", "a/b", "ABCDEF0123456789", "abcdef0123456789", "1.2.3", "v1.2.3-beta.1", "2019.4.28f1", "2022.3.10f1c1",
    "Translation: Bonjour", "TRANSLATION: Bonjour", "<think>x</think>Bonjour", "Bonjour\n\nNote: x", "```json\nx\n```", "**bold**", "[X]", "{Y}", "http://localhost:11434", "LOCALHOST", "172.20.0.1", "172.x.1.1",
};
var asm = typeof(UnityGameTranslator.Common.TranslationFormat).Assembly;
var sb = new StringBuilder();
string Show(object o)
{
    if (o == null) return "<null>";
    if (o is string s) return System.Text.Json.JsonSerializer.Serialize(s, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
    if (o is IEnumerable e && !(o is string)) return "[" + string.Join(", ", e.Cast<object>().Select(Show)) + "]";
    return o.ToString();
}
foreach (var type in asm.GetTypes().Where(t => t.IsPublic && t.IsAbstract && t.IsSealed).OrderBy(t => t.FullName))
{
    foreach (var m in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly).OrderBy(m => m.ToString()))
    {
        var ps = m.GetParameters();
        if (m.IsGenericMethod || ps.Length > 2 || ps.Any(p => p.ParameterType != typeof(string))) continue;
        if (ps.Length == 0) { Run(type, m, new object[0]); continue; }
        if (ps.Length == 1) foreach (var a in inputs) Run(type, m, new object[] { a });
        else foreach (var a in inputs) foreach (var b in inputs.Where((x, i) => i % 3 == 0)) Run(type, m, new object[] { a, b });
    }
}
void Run(Type t, MethodInfo m, object[] args)
{
    string result;
    try { result = Show(m.Invoke(null, args)); }
    catch (TargetInvocationException ex) { result = "THROWS " + ex.InnerException.GetType().Name; }
    sb.Append(t.Name).Append('.').Append(m.Name).Append('(').Append(string.Join(", ", args.Select(Show))).Append(") = ").Append(result).Append('\n');
}
Console.Out.Write(sb.ToString());
