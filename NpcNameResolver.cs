using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EveAbyssCompanion
{
    /// <summary>
    /// Best-effort normalization + alias mapping for NPC names coming from logs/manual paste.
    /// Keeps changes minimal: we don't change the dataset schema; we just try to map incoming
    /// names onto existing library entries.
    /// </summary>
    public static class NpcNameResolver
    {
        // Small built-in alias map for common variants seen in logs/UI.
        // Key + values are normalized.
        private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // Trig abbreviations / common typos
            { Normalize("Tessella"), Normalize("Tessella") },
            { Normalize("Tessella "), Normalize("Tessella") },

            // Some players shorten / misread these (kept conservative)
            { Normalize("Lucid Upholder"), Normalize("Lucid Upholder") },
            { Normalize("Thunder Child"), Normalize("Thunderchild") },
        };

        public static string ResolveToLibraryName(string rawName, IEnumerable<NpcEntry> library)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

            var rawTrim = rawName.Trim();
            if (rawTrim.Length == 0) return string.Empty;

            // 1) Exact match (fast path)
            var exact = library.FirstOrDefault(n => string.Equals(n.Name, rawTrim, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact.Name;

            // 2) Normalized match
            var norm = Normalize(rawTrim);
            var byNorm = BuildNormalizedIndex(library);
            if (byNorm.TryGetValue(norm, out var hit)) return hit;

            // 3) Alias mapping -> normalized match
            if (Aliases.TryGetValue(norm, out var aliasNorm) && byNorm.TryGetValue(aliasNorm, out var aliasHit))
                return aliasHit;

            // 4) No match: return a cleaned name so Unknown entries stay consistent
            return DenormalizeForDisplay(norm);
        }

        public static string Normalize(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return string.Empty;

            // Replace non-breaking spaces and collapse whitespace.
            var s = name.Replace('\u00A0', ' ').Trim();

            // Strip common bracketed suffixes like " [ABC]" or " (something)".
            s = StripBracketSuffix(s, '[', ']');
            s = StripBracketSuffix(s, '(', ')');

            // Collapse runs of whitespace
            var sb = new StringBuilder(s.Length);
            bool lastWasSpace = false;
            foreach (var ch in s)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace) sb.Append(' ');
                    lastWasSpace = true;
                }
                else
                {
                    sb.Append(ch);
                    lastWasSpace = false;
                }
            }

            return sb.ToString().Trim();
        }

        private static Dictionary<string, string> BuildNormalizedIndex(IEnumerable<NpcEntry> library)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var n in library)
            {
                if (string.IsNullOrWhiteSpace(n?.Name)) continue;
                var k = Normalize(n.Name);
                if (!dict.ContainsKey(k))
                    dict[k] = n.Name;
            }
            return dict;
        }

        private static string StripBracketSuffix(string s, char open, char close)
        {
            // Only strip if it looks like a trailing suffix (space + bracketed).
            int openIdx = s.LastIndexOf(open);
            if (openIdx < 0) return s;
            int closeIdx = s.LastIndexOf(close);
            if (closeIdx < openIdx) return s;

            // Ensure it's at the end or near-end.
            if (closeIdx != s.Length - 1) return s;

            // Require a space before the bracket so we don't destroy real names.
            if (openIdx > 0 && s[openIdx - 1] == ' ')
                return s.Substring(0, openIdx - 1).TrimEnd();

            return s;
        }

        private static string DenormalizeForDisplay(string normalized)
        {
            // Currently, our normalization is display-safe, so we just return it.
            return normalized;
        }
    }
}
