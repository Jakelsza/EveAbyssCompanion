using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EveAbyssCompanion
{
    /// <summary>
    /// Loads npc_dataset.json (the user-supplied dataset format) and converts it into the app's NpcEntry list.
    /// The dataset is "best-effort"; we normalize missing fields and derive Threat/KillPriority heuristically.
    /// </summary>
    public static class NpcDatasetImporter
    {
        public static bool TryLoadFromFile(string path, out List<NpcEntry> entries, out string error)
        {
            entries = new List<NpcEntry>();
            error = string.Empty;

            try
            {
                if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                {
                    error = "File not found: " + path;
                    return false;
                }

                var json = File.ReadAllText(path);
                var model = JsonSerializer.Deserialize<NpcDatasetRoot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (model?.Factions == null || model.Factions.Count == 0)
                {
                    error = "Dataset has no factions.";
                    return false;
                }

                var list = new List<NpcEntry>();

                foreach (var factionKvp in model.Factions)
                {
                    var factionName = factionKvp.Key ?? "Unknown";
                    var faction = factionKvp.Value;

                    if (faction?.Npcs == null) continue;

                    foreach (var npc in faction.Npcs)
                    {
                        if (npc == null) continue;

                        var entry = new NpcEntry
                        {
                            Name = npc.Name ?? "Unknown",
                            TypeId = npc.TypeId,
                            Family = factionName,
                            Class = npc.Class ?? "Unknown",
                            Ewar = npc.Ewar ?? string.Empty,
                            DamageDealt = npc.DamageDealt ?? string.Empty,
                            WeakTo = npc.WeakTo ?? string.Empty,
                            Behaviors = npc.Behaviors ?? string.Empty,
                            Threat = npc.Threat ?? string.Empty,
                            KillPriority = npc.KillPriority ?? string.Empty,
                            OverlayTags = npc.OverlayTags ?? new List<string>(),
                            IsBoss = npc.IsBoss
                        };

                        NormalizeEntry(entry);
                        list.Add(entry);
                    }
                }

                // De-dup by TypeId if present, otherwise by Name.
                entries = list
                    .GroupBy(e => e.TypeId > 0 ? e.TypeId.ToString() : e.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.First())
                    .OrderBy(e => e.Family)
                    .ThenBy(e => e.Name)
                    .ToList();

                error = $"NPC dataset imported: {entries.Count} entries ({Path.GetFileName(path)}).";
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static void NormalizeEntry(NpcEntry entry)
        {
            // Basic cleanup
            entry.Name = (entry.Name ?? string.Empty).Trim();
            entry.Family = (entry.Family ?? string.Empty).Trim();
            entry.Class = (entry.Class ?? string.Empty).Trim();
            entry.Ewar = (entry.Ewar ?? string.Empty).Trim();
            entry.DamageDealt = (entry.DamageDealt ?? string.Empty).Trim();
            entry.WeakTo = (entry.WeakTo ?? string.Empty).Trim();
            entry.Behaviors = (entry.Behaviors ?? string.Empty).Trim();
            entry.Threat = (entry.Threat ?? string.Empty).Trim();
            entry.KillPriority = (entry.KillPriority ?? string.Empty).Trim();

            // Heuristic threat + kill order (only if missing)
            if (string.IsNullOrWhiteSpace(entry.Threat) || string.IsNullOrWhiteSpace(entry.KillPriority))
            {
                DeriveThreatAndKillPriority(entry);
            }
        }

        private static void DeriveThreatAndKillPriority(NpcEntry e)
        {
            var text = (e.Name + " " + e.Ewar + " " + e.Behaviors).ToLowerInvariant();

            // Role flags
            bool scram = ContainsAny(text, "scram", "warp scram", "warp scramble");
            bool point = ContainsAny(text, "warp disrupt", "disruptor", "point");
            bool neut = ContainsAny(text, "neut", "neutraliz");
            bool web = ContainsAny(text, "web");
            bool rr = ContainsAny(text, "remote", "logi", "repair");
            bool damp = ContainsAny(text, "damp");
            bool disrupt = ContainsAny(text, "disrupt", "tracking", "missile");
            bool paint = ContainsAny(text, "paint");

            // Boss / DPS checks (best-effort)
            bool boss = ContainsAny(text, "overmind", "deepwatcher", "leshak", "thunderchild", "marshal");
            bool trig = (e.Family ?? "").ToLowerInvariant().Contains("trig");
            bool ramp = trig && ContainsAny(text, "ramp", "disintegrator");

            int score = 0;
            if (scram) score += 100;
            if (neut) score += 90;
            if (rr) score += 75;
            if (web) score += 60;
            if (damp || disrupt) score += 45;
            if (paint) score += 25;
            if (boss) score += 40;
            if (ramp) score += 20;
            if (point) score += 20;

            // Class modifier
            var cls = (e.Class ?? "").ToLowerInvariant();
            if (cls.Contains("battleship")) score += 25;
            else if (cls.Contains("battlecruiser")) score += 18;
            else if (cls.Contains("cruiser")) score += 12;
            else if (cls.Contains("destroyer")) score += 8;

            string threat;
            if (score >= 140) threat = "Extreme";
            else if (score >= 100) threat = "High";
            else if (score >= 60) threat = "Medium";
            else threat = "Low";

            // Kill priority hint
            var reasons = new List<string>();
            if (scram) reasons.Add("Scram");
            else if (point) reasons.Add("Point");
            if (neut) reasons.Add("Neut");
            if (rr) reasons.Add("Logi/RR");
            if (web) reasons.Add("Web");
            if (damp) reasons.Add("Damp");
            if (disrupt) reasons.Add("Disrupt");
            if (paint) reasons.Add("Paint");
            if (boss) reasons.Add("Boss/DPS check");

            string kill;
            if (reasons.Count == 0)
            {
                kill = threat == "Low" ? "Last" : "After tackle/ewar";
            }
            else
            {
                // Order the reasons in an actual kill-priority order
                var ordered = OrderReasonsForKill(reasons);
                kill = string.Join(" > ", ordered);
            }

            if (string.IsNullOrWhiteSpace(e.Threat)) e.Threat = threat;
            if (string.IsNullOrWhiteSpace(e.KillPriority)) e.KillPriority = kill;
        }

        private static List<string> OrderReasonsForKill(List<string> reasons)
        {
            int Rank(string r)
            {
                switch (r)
                {
                    case "Scram": return 1;
                    case "Point": return 2;
                    case "Neut": return 3;
                    case "Logi/RR": return 4;
                    case "Web": return 5;
                    case "Disrupt": return 6;
                    case "Damp": return 7;
                    case "Paint": return 8;
                    case "Boss/DPS check": return 9;
                    default: return 99;
                }
            }

            return reasons
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(Rank)
                .ToList();
        }

        private static bool ContainsAny(string haystack, params string[] needles)
        {
            foreach (var n in needles)
            {
                if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        // JSON model (only the parts we use)
        private sealed class NpcDatasetRoot
        {
            public Dictionary<string, NpcDatasetFaction>? Factions { get; set; }
        }

        private sealed class NpcDatasetFaction
        {
            public List<NpcDatasetNpc>? Npcs { get; set; }
        }

        private sealed class NpcDatasetNpc
        {
            public string? Name { get; set; }

            [JsonPropertyName("type_id")]
            public int TypeId { get; set; }

            public string? Class { get; set; }
            public string? Ewar { get; set; }

            [JsonPropertyName("damage_dealt")]
            public string? DamageDealt { get; set; }

            [JsonPropertyName("weak_to")]
            public string? WeakTo { get; set; }

            public string? Behaviors { get; set; }

            // Optional fields (may be absent)
            public string? Threat { get; set; }

            [JsonPropertyName("kill_priority")]
            public string? KillPriority { get; set; }

            [JsonPropertyName("overlay_tags")]
            public List<string>? OverlayTags { get; set; }

            [JsonPropertyName("is_boss")]
            public bool IsBoss { get; set; }
        }
    }
}
