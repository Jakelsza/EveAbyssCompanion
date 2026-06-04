using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EveAbyssCompanion
{
    public sealed class AppConfig
    {
        // Bump this whenever we change default behaviors and need a migration path.
        public int ConfigVersion { get; set; } = 2;

        // NPC dataset import
        public bool EnableNpcDatasetImport { get; set; } = true;

        // If set, overrides the default ("npc_dataset.json" next to the EXE)
        public string NpcDatasetPath { get; set; } = "npc_dataset.json";

        // Cockpit mode — overlay is primary control, main window is dashboard only
        [JsonPropertyName("CockpitMode")]
        public bool CockpitMode { get; set; } = false;

        // First-time setup completed flag
        [JsonPropertyName("SetupComplete")]
        public bool SetupComplete { get; set; } = false;

        // Log reading
        // NOTE: MainWindow expects these names: EnableCombatLogMonitor + CombatLogFolder.
        // We keep the old names as compatibility aliases.

        // New (preferred) names (serialized)
        [JsonPropertyName("EnableCombatLogMonitor")]
        public bool EnableCombatLogMonitor { get; set; } = true;

        [JsonPropertyName("CombatLogFolder")]
        public string CombatLogFolder { get; set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "EVE", "logs", "Gamelogs");

        // Window placement (MainWindow)
        [JsonPropertyName("StartOnSecondMonitor")]
        public bool StartOnSecondMonitor { get; set; } = false;

        [JsonPropertyName("RememberWindowPlacement")]
        public bool RememberWindowPlacement { get; set; } = true;

        [JsonPropertyName("WindowLeft")]
        public double WindowLeft { get; set; } = double.NaN;

        [JsonPropertyName("WindowTop")]
        public double WindowTop { get; set; } = double.NaN;

        [JsonPropertyName("WindowWidth")]
        public double WindowWidth { get; set; } = double.NaN;

        [JsonPropertyName("WindowHeight")]
        public double WindowHeight { get; set; } = double.NaN;

        [JsonPropertyName("WindowMaximized")]
        public bool WindowMaximized { get; set; } = false;

        // Backward-compatible aliases (not serialized)
        [JsonIgnore]
        public bool EnableLogReader
        {
            get => EnableCombatLogMonitor;
            set => EnableCombatLogMonitor = value;
        }

        [JsonIgnore]
        public string EveLogFolder
        {
            get => CombatLogFolder;
            set => CombatLogFolder = value;
        }

        public static string GetConfigFilePath()
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EveAbyssCompanion");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "config.json");
        }

        public static AppConfig Load()
        {
            var path = GetConfigFilePath();
            if (!File.Exists(path))
            {
                // First run: default to using the bundled npc_dataset.json if present.
                var fresh = new AppConfig();
                TryAutoEnableNpcDataset(fresh, fresh.ConfigVersion);
                Save(fresh);
                return fresh;
            }

            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();

                // Migration: older configs (no ConfigVersion) will deserialize as 0.
                if (cfg.ConfigVersion < 2)
                {
                    // Only auto-enable if the dataset file exists next to the EXE.
                    TryAutoEnableNpcDataset(cfg, cfg.ConfigVersion);
                    cfg.ConfigVersion = 2;
                    Save(cfg);
                }

                return cfg;
            }
            catch
            {
                var fallback = new AppConfig();
                TryAutoEnableNpcDataset(fallback, fallback.ConfigVersion);
                Save(fallback);
                return fallback;
            }
        }

        public static void Save(AppConfig cfg)
        {
            var path = GetConfigFilePath();
            var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }

        // Convenience instance wrapper (MainWindow calls _config.Save())
        public void Save() => Save(this);

        private static void TryAutoEnableNpcDataset(AppConfig cfg, int previousVersion)
        {
            // We only auto-enable on first run / migration, so users can still turn it off afterwards.
            // Resolve dataset path relative to EXE.
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var candidate = cfg.NpcDatasetPath;
                if (!Path.IsPathRooted(candidate))
                    candidate = Path.Combine(baseDir, candidate);

                if (File.Exists(candidate))
                    cfg.EnableNpcDatasetImport = true;
            }
            catch
            {
                // ignore
            }
        }
    }
}
