using System.Collections.Generic;

namespace EveAbyssCompanion
{
    public class NpcEntry
    {
        public string Name { get; set; } = "";
        public int? TypeId { get; set; }
        public string Family { get; set; } = "";

	        // Alias expected by MainWindow's dataset detail panel.
	        // We keep Family as the primary grouping field, but expose NpcFamily
	        // to avoid touching existing UI logic.
	        public string NpcFamily
	        {
	            get => Family;
	            set => Family = value ?? "";
	        }
        public string Class { get; set; } = "";
        public string Threat { get; set; } = "";
        public string KillPriority { get; set; } = "";
        public string Notes { get; set; } = "";

        // Extra fields (optional) for richer NPC detail.
        public string Ewar { get; set; } = "";
        public string DamageDealt { get; set; } = "";
        public string WeakTo { get; set; } = "";
        public string Behaviors { get; set; } = "";
	        public string StatsNotes { get; set; } = "";

	        // Common numeric stats (nullable because many entries are partial).
	        public double? ShieldHp { get; set; }
	        public double? ArmorHp { get; set; }
	        public double? HullHp { get; set; }
	        public double? Dps { get; set; }
        public string Handle { get; set; } = "";
        public List<string> Tags { get; set; } = new();

        // Overlay colour-coding
        public List<string> OverlayTags { get; set; } = new();
        public bool IsBoss { get; set; } = false;

        // Overlay chip text — kill priority only, colour handles ewar visually
        public string PriorityDisplay =>
            string.IsNullOrWhiteSpace(KillPriority) ? "?" : KillPriority;
    }
}
