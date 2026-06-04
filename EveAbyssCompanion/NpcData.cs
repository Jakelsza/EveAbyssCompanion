using System.Collections.Generic;

namespace EveAbyssCompanion
{
    public static class NpcData
    {
        public static string LastLoadStatus { get; private set; } = "";

        public static List<NpcEntry> Build(AppConfig config)
        {
            // Default dataset always available.
            var fallback = BuildDefault();

            if (config == null)
            {
                LastLoadStatus = "NPC dataset: using built-in defaults (no config loaded).";
                return fallback;
            }

            if (!config.EnableNpcDatasetImport)
            {
                LastLoadStatus = "NPC dataset: built-in defaults (import disabled).";
                return fallback;
            }

            if (string.IsNullOrWhiteSpace(config.NpcDatasetPath) || !System.IO.File.Exists(config.NpcDatasetPath))
            {
                LastLoadStatus = $"NPC dataset: built-in defaults (dataset not found: {config.NpcDatasetPath}).";
                return fallback;
            }

            var import = NpcDatasetImporter.TryLoadFromFile(config.NpcDatasetPath, out var imported, out var status);
            LastLoadStatus = status;

            if (!import || imported == null || imported.Count == 0)
                return fallback;

            // Merge: keep any built-in entries that aren't present in the dataset.
            var byName = imported.ToDictionary(n => n.Name, n => n, System.StringComparer.OrdinalIgnoreCase);
            foreach (var f in fallback)
            {
                if (!byName.ContainsKey(f.Name))
                    imported.Add(f);
            }

            return imported;
        }

        public static List<NpcEntry> BuildDefault()
        {
            return new List<NpcEntry>
            {
                new() {
                    Name="Starving Damavik", Family="Damavik", Class="Frigate",
                    Threat="Very High", KillPriority="First",
                    Notes="Neut pressure. Can break active tanks.",
                    Handle="Kill first. Recall drones if yellow-boxed.",
                    Tags=new List<string>{"Neut","Triglavian"}
                },
                new() {
                    Name="Harrowing Damavik", Family="Damavik", Class="Frigate",
                    Threat="High", KillPriority="Early",
                    Notes="Support/pressure type.",
                    Handle="Clear early. Watch drone aggro.",
                    Tags=new List<string>{"Triglavian"}
                },
                new() {
                    Name="Starving Vedmak", Family="Vedmak", Class="Cruiser",
                    Threat="Very High", KillPriority="First",
                    Notes="Heavy neut pressure.",
                    Handle="Primary if present. Manage cap.",
                    Tags=new List<string>{"Neut","Triglavian"}
                },
                new() {
                    Name="Lucid Upholder", Family="Sleeper", Class="Cruiser",
                    Threat="High", KillPriority="Early",
                    Notes="Sustained pressure cruiser.",
                    Handle="Kill early if it’s stacking pressure.",
                    Tags=new List<string>{"Sleeper"}
                },
                new() {
                    Name="Thunderchild", Family="EDENCOM", Class="Battleship",
                    Threat="Very High", KillPriority="First",
                    Notes="Chain lightning pressure. Punishes drones/positioning.",
                    Handle="Primary. Keep moving; recall drones if targeted.",
                    Tags=new List<string>{"EDENCOM"}
                },
                new() {
                    Name="Karybdis Tyrannos", Family="Drifter", Class="Battleship",
                    Threat="Very High", KillPriority="First",
                    Notes="Major battleship threat.",
                    Handle="Primary. Transversal + manage tank/heat.",
                    Tags=new List<string>{"Drifter","Boss"}
                },
            };
        }
    }
}
