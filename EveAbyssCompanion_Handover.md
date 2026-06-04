# EVE Abyss Companion — Session Handover
## Date: 2026-06-03 | Build: v1.0.0.61 | Status: Pre-GitHub, testing complete

---

## Current Build Status

All core features working and tested across real T3 Fierce Electrical runs:
- NPC detection via combat log — working
- Colour-coded overlay chips with multi-tag ewar — working
- Boss gold borders (Diablo style) — working
- Loot Before/After sync between main app and overlay — working
- Live loot delta on overlay — working
- Stats calculating (ISK/hour, avg loot, best run) — working
- R1/R2/R3 flash for current room — working
- Drone warning on its own line, no cut-off — working

---

## What Was Done This Session (2026-06-03)

### Major changes

**v1.0.0.58 — Controls area full rework**
- StandardRunControls rebuilt: 3 columns (Status | Workflow | Settings)
- Workflow column: Tier → Weather → Before loot → Start/Reset/End → After loot + live delta → Submit
- R1✓ R2✓ R3✓ moved into timer/status block
- Submit button next to After loot — no hunting
- Removed: EnableNpcDatasetCheckBox, NpcDatasetPathText, RefreshAllButton

**v1.0.0.59 — Loot sync root cause fixed + polish**
- EnsureOverlay now pushes main window loot values to overlay on creation
- Player types Before in main app → starts run → overlay opens with Before already populated
- Session loot state (_sessionLootBefore/_sessionLootAfter) as single source of truth
- Before field highlighted blue (label + border) — unmissable
- R1/R2/R3 flash (1-second toggle via _roomFlashState in Timer_Tick)
- Overlay colour legend strip below NPC chips
- Settings buttons left-aligned (no more full-width stretch)

**v1.0.0.60 — Multi-tag overlay chips**
- overlay_tag (string) → overlay_tags (List<string>) on all 126 NPCs
- 6 NPCs now have multiple tags: Elite Lucifer Cynabal [Neut,Web], Lucifer Cynabal [Neut,Web], Lucifer Echo [Scram,Web], Devoted Knight [Neut,Scram], Lucid Firewatcher [Neut,Logi], Lucid Warden [Web,Logi]
- Chip renders each tag separately with individual colour
- Karybdis Tyrannos kill_priority fixed: Mid → First (confirmed from real runs — spawns 80km+ already kiting, send drones immediately)
- Lucifer group behaviors updated — explicitly warns drone users: Elite Lucifer Cynabal and Lucifer Cynabal actively target and destroy drones

**v1.0.0.61 — Overlay usability fixes**
- Launch Overlay button added to settings column (standard mode) — no longer need cockpit mode
- Overlay Setup button now opens Re-run Setup wizard (was silently doing nothing)

### Dataset state (npc_dataset.json)
- 126 NPCs, all with: kill_priority, threat, overlay_tags (array), is_boss, ewar (clean short phrases), behaviors
- 9 bosses (is_boss: true): Blastgrip Tessera, Karybdis Tyrannos, Lucid Deepwatcher, Renewing Leshak, Renewing Rodiva, Arrester Marshal Disparu Troop, Thunderchild Disparu Troop, Elite Lucifer Cynabal, Devoted Knight
- Overlay tag colour map: Neut=Yellow #FFD700, Scram=Red #FF5A5A, Web=Orange #FFA040, Damp/Disrupt=Purple #CC88FF, Logi=Green #4CFFB3, Paint=Grey #999999, Boss=Gold border #FFD700

### Chip colour legend (on overlay)
■ Neut (Yellow) ■ Scram (Red) ■ Web (Orange) ■ Damp/Dis (Purple) ■ Logi (Green) ■ Paint (Grey) □ Boss (Gold border)

---

## Immediate Next Steps

1. Update handover doc and zip — DONE (this file)
2. Push to GitHub via Cowork — Leon on PC, use Cowork desktop tool
3. User guide — screenshots collected Feb 2026, need masking + About tab screenshot missing. LAUNCH REQUIREMENT.

---

## Roadmap

### v1.1 — Next sprint
- Unknown NPC tab — auto-logs unrecognised combat log names, inline form, saves to dataset
- Drone show/hide checkbox in setup (config bool UseDrones)
- Incoming DPS meter — rolling window from combat log damage lines

### v1.2 — Medium term
- Inventory OCR (Windows.Media.Ocr)
- Mobile companion via local WiFi
- Ship class icons — WPF vector shapes in NPC library detail panel (Frigate/Destroyer/Cruiser/BC/BS/Seeker)

### Future — Separate apps
- Jita Trading App, L4 Mission Companion, ML.NET run analysis

---

## Dataset Structure

```json
{
  "name": "Starving Damavik",
  "type_id": 48090,
  "class": "Frigate",
  "kill_priority": "First",
  "threat": "Very High",
  "overlay_tags": ["Neut"],
  "is_boss": false,
  "ewar": "Energy Neutralizer (heavy, 34 GJ/cycle)",
  "damage_dealt": "Thermal/Explosive (69/31)",
  "weak_to": "Explosive",
  "behaviors": "Heavy close-range neut; 2700 m/s; ramping damage.",
  "stats": { ... }
}
```

---

## Architecture Notes

**Combat log detection:**
- CombatLogMonitor tails newest gamelog file every 350ms
- Regex extracts NPC names from HTML "from</font>" and "to</font>" patterns
- _seen HashSet cleared on each Start (fixed v1.0.0.54) — NPCs from previous run no longer block detection
- kill_priority JSON field uses [JsonPropertyName("kill_priority")] — was broken before v1.0.0.55

**Loot sync:**
- _sessionLootBefore and _sessionLootAfter as session state fields
- Main window TextChanged → updates session state + syncs to overlay via SyncInvStart/SyncInvEnd
- Overlay InvChanged event → updates main window TextBoxes → triggers TextChanged → updates session state
- EnsureOverlay pushes existing values to overlay on creation
- ReadLootMillions reads from session state (falls back to overlay GetInvStart/GetInvEnd)

**Overlay chip rendering:**
- OverlayTagToColourConverter maps tag string to SolidColorBrush
- BossBorderConverter maps IsBoss bool to Gold or Transparent border
- NpcEntry.OverlayTags is List<string> — nested ItemsControl renders each tag with its colour
- NpcEntry.PriorityDisplay = KillPriority only (colour handles ewar visually)

---

## File Locations
- EVE Gamelogs: C:\Users\agnru\OneDrive\Documents\EVE\logs\Gamelogs
- Project workspace: C:\My Apps

## Donation Links (verified — use "jakkels" with e, NOT "jakkals")
- Ko-fi: ko-fi.com/jakkelsza
- PayPal: paypal.me/JakkelsZA
- Bitcoin: 35U3rbr7XWAsi55KqUJDpRKoKB8PistGZv
- GitHub: github.com/JakkelsZA

## Known NPC Intel (from real runs)
- Karybdis Tyrannos: spawns 80km+ already kiting, send drones IMMEDIATELY on room entry
- Elite Lucifer Cynabal + Lucifer Cynabal: actively target and destroy drones — pull drones or lose them
- Lucifer room = most dangerous for drone ships (Gila etc.)
- Blastgrip Tessera: extreme DPS + tracking, high HP — boss of Rogue Drone rooms
