# AbyssCompanionApp – Project Log / Roadmap

## Core rules
- Zip workflow: you send a zip, I return a fixed zip.
- Minimal changes: only change what we're working on.
- Version bumps: increment version in csproj on every zip.

## Current version: 1.0.0.63

## Version history

### v1.0.0.63 (2026-06-03) — Claude
- **Overlay Setup button reverted to bring main window forward** — single screen players need this to reach the main app without Alt+Tab. Now calls WindowState=Normal + Show + Activate + Focus for reliable foreground on all setups.

### v1.0.0.62 (2026-06-03) — Claude
- **Overlay toggle button** — "Overlay ▶" opens the overlay, becomes "Overlay ■". Click again hides it. State fully preserved on hide/show — tier, weather, loot values, detected NPCs all intact. Architecture already supported this natively.

### v1.0.0.61 (2026-06-03) — Claude
- **Launch Overlay button added to standard mode** — Settings column now has "Launch Overlay" button visible at all times. No longer need cockpit mode just to bring up the overlay.
- **Overlay Setup button fixed** — Now opens the Re-run Setup wizard (as the label implies) instead of silently focusing the main window with no visible effect.
- **Dataset: Gazedimmer Tessella typo fixed** — was "Gazedinmmer" (double m) in dataset.

### v1.0.0.60 (2026-06-03) — Claude
- **Multi-tag overlay chips** — `overlay_tag` (single string) replaced with `overlay_tags` (array). Chip now renders each tag separately with its own colour. "Elite Lucifer Cynabal — First — Neut Web" where Neut is yellow and Web is orange.
- **6 NPCs now have multiple tags:** Elite Lucifer Cynabal [Neut, Web], Lucifer Cynabal [Neut, Web], Lucifer Echo [Scram, Web], Devoted Knight [Neut, Scram], Lucid Firewatcher [Neut, Logi], Lucid Warden [Web, Logi]
- **Karybdis Tyrannos kill_priority fixed** — changed from "Mid" to "First". Spawns already kiting at 80km+, drones on it immediately or it escapes range. No EWAR, dies fast if caught early. Confirmed from real run experience.
- **Lucifer group behaviors updated** — All Lucifer NPCs now have accurate behaviors. Elite Lucifer Cynabal and Lucifer Cynabal explicitly warn: actively targets and destroys drones.
- **NpcEntry: OverlayTag → OverlayTags** (List<string>)
- **NpcDatasetImporter: overlay_tag → overlay_tags** (List<string> with JsonPropertyName)

### v1.0.0.59 (2026-06-03) — Claude
- **Loot sync root cause fixed** — When overlay first opens, it now receives whatever Before/After values are already in the main window (EnsureOverlay pushes values on creation). Player types Before in main app → starts run → overlay opens with Before already populated → types After in overlay → syncs back to main window → ReadLootMillions calculates correctly. Single source of truth working as planned.
- **Settings buttons fixed** — Left-aligned with proper padding. No more full-width stretch.
- **Before field highlighted** — Blue label and border makes it unmissable pre-run.
- **R1/R2/R3 current room flash** — Active room button flashes blue every second. Completed rooms stay green.
- **Overlay color legend** — Small legend strip below NPC chips: Yellow=Neut, Red=Scram, Orange=Web, Purple=Damp/Dis, Green=Logi, Grey=Paint, Gold=Boss.

### v1.0.0.58 (2026-06-03) — Claude
- **Controls area full rework** — StandardRunControls rebuilt from 4-column to 3-column layout. Left=Status (timer+rooms+R1✓R2✓R3✓), Centre=Workflow (Tier→Weather→Before→Start|Reset|End→After+delta→Submit), Right=Settings. Everything you click is in one column in workflow order.
- **Loot fields moved to workflow column** — Before and After loot now sit between tier/weather and the run controls, exactly where the workflow demands them.
- **Submit moved next to After loot** — Submit & End button now lives directly below After loot + live delta. No hunting.
- **Live loot delta** — `MainLootDeltaText` shows `= +7.4 M` in real time as you type Before and After values.
- **Session loot state** — `_sessionLootBefore` and `_sessionLootAfter` string fields replace direct TextBox reads. Single source of truth. Both overlay and main window write to same state. Cleared on submit.
- **Removed:** `EnableNpcDatasetCheckBox`, `NpcDatasetPathText`, `RefreshAllButton`, `RefreshAllButton_Click`, `EnableNpcDatasetCheckBox_Changed`. Dataset always loads — toggle was redundant.

### v1.0.0.57 (2026-06-03) — Claude
- **Drone flash skip when already flagged** — `FlashDroneReminder` now only fires on room transition if `_dronesNeedRepair` is false. If player already clicked "Drone took armor", no redundant flash.
- **Drone warning text cut-off fixed** — `DroneReminderText` moved to its own line below the buttons in a vertical StackPanel. No longer clipped by button row. TextWrapping added.

### v1.0.0.56 (2026-06-02) — Claude
- **Colour-coded NPC chips** — overlay chips now colour the kill priority text by ewar type: Yellow=Neut, Red=Scram, Orange=Web, Purple=Damp/Disrupt, Grey=Paint, Green=Logi, White=no ewar. `OverlayTagToColourConverter` added.
- **Boss gold border** — 9 boss NPCs get Diablo-style gold chip border: Blastgrip Tessera, Karybdis Tyrannos, Lucid Deepwatcher, Renewing Leshak, Renewing Rodiva, Arrester Marshal Disparu Troop, Thunderchild Disparu Troop, Elite Lucifer Cynabal, Devoted Knight. `BossBorderConverter` added.
- **Dataset: overlay_tag added** — all 126 NPCs have one-word ewar tag for colour mapping.
- **Dataset: is_boss added** — boolean flag on 9 boss NPCs.
- **Dataset: ewar field cleaned** — short consistent phrases across all entries (was inconsistent mix of long/short).
- **PriorityDisplay simplified** — now shows kill_priority only; colour handles ewar communication visually.
- **NpcEntry: OverlayTag + IsBoss properties** added.
- **NpcDatasetImporter: overlay_tag + is_boss** mapped with JsonPropertyName.

### v1.0.0.55 (2026-06-02) — Claude
- **Critical: kill_priority JSON mapping fixed** — `NpcDatasetNpc` model was missing `[JsonPropertyName("kill_priority")]` so the snake_case JSON field never mapped to the C# property. All kill_priority values were silently null → heuristic fired on every NPC → "Disrupt", "Logi/RR", "Paint" etc. shown instead of dataset values. Fixed with `[JsonPropertyName]` on all snake_case fields: `kill_priority`, `type_id`, `damage_dealt`, `weak_to`.
- **Loot merged from both sources** — `ReadLootMillions` now reads Before from main window OR overlay (whichever has a value), and After from main window OR overlay independently. Before in main + After in overlay now calculates correctly.
- **PriorityDisplay "None" fix** — ewar values of literal string "None" now treated as empty, so chips show "First" not "First — None". All "None" ewar entries in dataset also cleared to empty string.
- **Scylla Tyrannos ewar shortened** — "Varies: Nullwarp=Scram | Nullcharge=Neut | Entanglement=Web" → "Varies: Scram/Neut/Web" for compact chip display.
- **Overlay GetInvStart/GetInvEnd** — new public accessors added to support merged loot reading.

### v1.0.0.54 (2026-06-02) — Claude
- **_seen HashSet bug fixed:** `StartCombatLogMonitorIfEnabled` now calls `_combatLogMonitor.Start(ignoreExistingLines: true)` when monitor is already running, resetting `_seen` so NPCs from previous runs don't block detection in new runs (fixes Harrowing Vedmak and any other NPC that appeared in prior run not detecting)
- **Loot fields two-way sync:** Typing in main window Before/After fields now pushes to overlay in real time; typing in overlay Before/After fields now pushes back to main window. `InvChanged` event on overlay, `SyncInvStart`/`SyncInvEnd` public methods added. Loop-safe via `_syncingInv` flag.
- **NPC overlay priority display improved:** Overlay NPC chips now show `KillPriority — Ewar` (e.g. "First — Neut", "Early — Web") via new `PriorityDisplay` computed property on `NpcEntry`. Falls back to just `KillPriority` if no ewar.

### v1.0.0.53 (2026-06-02) — Claude
- **NPC dataset rebuilt:** 119 → 126 NPCs
  - `kill_priority` and `threat` fields added to all 126 entries
  - Added 7 missing NPCs: Karybdis Tyrannos, Scylla Tyrannos (confirmed combat log names for all Drifter BS/cruisers via live gamelog analysis), Starving Damavik, Ruptured Drifter Battleship, Ruptured Shackling Leshak, Marker Enforcer Disparu Troop, Tyrannos Typhon
  - Drifter ship-type entries marked library-only (combat log shows Karybdis/Scylla, not ship-type names)
- **AI enricher removed:** NpcAiEnricher.cs deleted; all references removed from MainWindow.xaml.cs, MainWindow.xaml, AppConfig.cs (EnableAiNpcEnrichment, AnthropicApiKey removed)
- **Combat log name verification:** Extracted all unique NPC names from 200+ real gamelogs — confirmed Karybdis Tyrannos and Scylla Tyrannos are the actual combat log names, not EVE Ref ship-type names

### v1.0.0.52 (2026-06-01) — Claude
- Before/After loot fields (MainInvStartTextBox, MainInvEndTextBox) added to main window
- Personal path (agnru/OneDrive) removed from CombatLogMonitor.cs (runtime path now)
- About tab complete with Ko-fi, PayPal, Bitcoin, GitHub links
- Disclaimer updated: Fenris Creations (formerly CCP Games)
- Family dropdown dark theme fixed (full ControlTemplate)
- Emoji symbols removed from About tab (replaced with plain text labels)
- Overview manual input block (dead UI) removed

### v1.0.0.51 (2026-05-31) — Claude
- app.ico rebuilt as BMP-format multi-size ICO (fixed crash on launch from PNG-compressed ICO)

### v1.0.0.50 (2026-05-31) — Claude
- Drone armor flag fix: removed _dronesNeedRepair = false from ResetTimer (flag now only clears on explicit Repaired click or new Start)

### v1.0.0.43 (2026-05-27) — Claude
- WM_MOUSEACTIVATE interceptor: blocks overlay activation on button clicks but allows it for TextBox clicks (loot fields typeable, EVE keeps focus)

### v1.0.0.41 (2026-05-27) — Claude
- Owner removed from overlay (_overlay.Owner = this removed): overlay no longer minimises with main window
- WS_EX_TOOLWINDOW kept: overlay stays out of Alt+Tab

### v1.0.0.40 (2026-05-26) — Claude
- Overlay focus fix attempt 1 (WS_EX_NOACTIVATE — blocked all input incl. TextBoxes; superseded by v1.0.0.43)

### v1.0.0.35 (2026-05-25) — Claude
- ISK/hour fix: denominator now includes (totalRuns - 1) × 60s mandatory re-entry wait
- Best run metric corrected: most time REMAINING = most efficient clear (not fastest elapsed)

### v1.0.0.30 (2026-05-24) — Claude
- Auto-clear timer fix: _sessionActive and _pendingSubmit checks added; timer no longer clears detected NPCs during active runs

### v1.0.0.28 (2026-05-23) — Claude
- NPC panel moved to TOP of overlay (single-screen visibility)
- Detail panel LEFT, list RIGHT in NPC Library
- Auto-clear detected NPCs on room done (R1✓/R2✓/R3✓)
- Drone two-button system: separate "Drone took armor" (sets flag) and "Repaired" (clears flag) buttons
- Drone flash reminder on room transition

### v1.0.0.27 (2026-05-23) — Claude
- **First-time setup wizard** — runs on first launch, sets cockpit mode + log folder
- **Cockpit mode** — checkbox in settings; hides top run controls, shows "🚀 Launch Overlay" bar with timer
- **Overlay completely rebuilt:** bigger (420x480), detected NPC panel, drone flash reminder, inventory fields renamed Before/After
- **UpdateOverlayNpcs** — overlay NPC list syncs with combat log detection automatically
- UseWindowsForms removed (replaced with WPF native OpenFolderDialog)

### v1.0.0.26 — Layout polish, merged Refresh/Recalc, Get Key button
### v1.0.0.25 — Smart NPC inference from name (no API)
### v1.0.0.24 — Bug fixes, ISK/hour, AI enricher
### v1.0.0.23 — Detail panel LEFT, list RIGHT
### v1.0.0.22 — ComboBox dark theme
### v1.0.0.21 — Single tab panel
### v1.0.0.20 — Version number, tab styling

## Roadmap

### v1.1 — Next sprint
1. Unknown NPCs tab — auto-logs NPC names from combat log not in dataset; inline form to fill in details, saves to npc_dataset.json
2. Drone show/hide checkbox in setup (config bool UseDrones, default true; hides drone buttons on overlay + main if false)

### v1.2 — Medium term
- Inventory OCR (Windows.Media.Ocr) — auto-read Est. Price from EVE inventory
- Mobile companion app via local WiFi

### Future — Separate branded apps (NOT features in this app)
- Jita Trading App (mobile, camera OCR)
- L4 Mission Companion
- ML.NET run analysis

## Known gotcha
Extract into a FRESH folder. Keep folder named EveAbyssCompanion.

