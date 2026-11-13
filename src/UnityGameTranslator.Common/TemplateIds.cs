using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace UnityGameTranslator.Common
{
    /// <summary>
    /// The identifier of a template, DERIVED from its key — nothing to store, nothing that two
    /// writers can disagree about, the same in C#, PHP and JavaScript. A key that changes is
    /// another template anyway.
    ///
    /// The id is « t » then the first 12 hexadecimal digits (lower case) of the SHA-256 of the key
    /// in UTF-8, line breaks already LF. It starts with a letter ON PURPOSE: twelve digits alone
    /// would, one time in about three hundred, be an integer-looking key, and PHP turns those into
    /// numbers (arrays, numeric sort, re-encoded as a list). <c>use</c> and <c>class</c> of an
    /// entry are keyed by it.
    ///
    /// A template is a line whose KEY holds a closed slot (<see cref="LineMarkers.ClosedSlots"/>).
    /// </summary>
    public static class TemplateIds
    {
        /// <summary>The id of the template whose key is this.</summary>
        public static string Of(string key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
                var hex = new StringBuilder(13).Append('t');
                for (int i = 0; i < 6; i++) hex.Append(hash[i].ToString("x2"));
                return hex.ToString();
            }
        }

        /// <summary>
        /// Two different templates of one file with the same id: refused with the reason in words,
        /// never a silent pick. Null when there is none. The keys of a file may be passed whole:
        /// only those that are templates count, and a key listed twice is one template.
        /// </summary>
        /// <param name="keys">The keys of the file's lines.</param>
        /// <param name="idOf">The id function — the library's own, unless a check wants a small id space.</param>
        public static LineRefusal? Collision(IEnumerable<string> keys, Func<string, string>? idOf = null)
        {
            if (keys == null) throw new ArgumentNullException(nameof(keys));
            idOf = idOf ?? Of;
            var seen = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string key in keys)
            {
                if (LineMarkers.ClosedSlots(key).Count == 0) continue;
                string id = idOf(key);
                if (seen.TryGetValue(id, out string? other))
                {
                    if (other == key) continue;
                    return new LineRefusal
                    {
                        Code = LineWrites.TemplateIdCollision,
                        Words = $"The templates \"{other}\" and \"{key}\" have the same identifier {id}: one of them must be written differently.",
                    };
                }
                seen[id] = key;
            }
            return null;
        }
    }
}
