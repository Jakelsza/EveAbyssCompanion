# EVE Abyss Companion — Development Journal
# Private — for development continuity only
# Author: JakkelsZA (Leon) + Claude
# Started: 2026-05-23

================================================================
## WHO IS LEON (JakkelsZA)
================================================================

- Red Seal Section 13 Electrician, underground Westonaria SA
- ADHD (hyperactive), color blind (red-brown problem)
- EVE player since 2009, character AgN DieBaas (Omega, 68M SP)
- Alt: eva silver (Alpha, Orca pilot, full mining skills)
- Wife Leonette (gamer), sons DeWinter (16, chess captain) and Leon Jr (13, ADHD)
- Lives Randfontein, commutes via Bolt
- Coding background: Turbo Pascal, Python, C++, C#, VB, PLC ladder logic
- Built working Runes of Magic bot previously
- Goal: transition out of electrical trade into coding career

================================================================
## THE APP — WHAT IT IS
================================================================

EVE Abyss Companion — free WPF C# desktop app for tracking
Abyssal Deadspace runs. Built for both new and veteran players.

**Core philosophy:**
- No memory reading. No game hooks. No hotkeys injected into EVE.
- Works from combat log files EVE writes to disk (legal, EULA safe)
- Everything optional — can be used as pure timer only
- NPC library incomplete — noted in manual, grows over time

**Target users:**
- Single screen players — cockpit mode, overlay is everything
- Dual screen players — standard mode, full controls in main window
- New players — NPC library helps them learn what they're fighting
- Veterans — ISK/hour tracking, run history, stats

================================================================
## ARCHITECTURE DECISIONS
================================================================

**Why WPF C# not web app:**
Leon knows C#. WPF gives proper Windows overlay support.
Web app would need Electron which adds complexity.

**Why two windows (MainWindow + OverlayWindow):**
Overlay needs to sit on top of EVE independently.
MainWindow is the dashboard — never needs to be on top.

**Why NOT two separate exe files:**
Considered splitting into two apps communicating via text files.
Decided against it — WS_EX_NOACTIVATE fix solved the focus problem
without the complexity of IPC. Current single-app approach works.

**Why text file communication was considered:**
- Main app writes state JSON → overlay reads it
- Overlay writes button presses JSON → main app reads it
- Completely independent processes
- Filed as future architecture option if needed

**Overlay focus fix (v1.0.0.41):**
Problem: clicking overlay brought main window to front.
Fix 1 attempt: WS_EX_NOACTIVATE — blocked all focus including textboxes.
Fix 2 (v1.0.0.43): WM_MOUSEACTIVATE interceptor — blocks activation
for button clicks but allows it for TextBox clicks.
Result: EVE keeps focus on button clicks, loot textboxes still typeable.

**Owner removal (v1.0.0.41):**
Problem: minimizing main window minimized overlay too.
Fix: removed _overlay.Owner = this
Result: overlay is completely independent of main window.

**WS_EX_TOOLWINDOW kept:**
Overlay doesn't appear in Alt+Tab — cleaner experience.

================================================================
## FEATURE DECISIONS — WHY EACH EXISTS
================================================================

**Cockpit Mode:**
Leon realized single-screen players can't reach main window during runs.
EVE takes the whole screen. Overlay must be self-sufficient.
Cockpit mode hides top controls, shows Launch Overlay button.
Overlay has EVERYTHING a single-screen player needs during a run.

**Setup Wizard (3 steps):**
Step 1: Mode selection (cockpit vs standard)
Step 2: EVE log folder path
Step 3: Ready confirmation
Reason: Without wizard, players don't know to point app at Gamelogs folder.
Common mistake found in testing: folder pointed at Chatlogs not Gamelogs.

**Re-run Setup button:**
Leon asked "what if my screen dies and I need to reconfigure?"
Added to cockpit bar (always visible) and options panel.

**Smart NPC Inference (no API required):**
Originally planned AI enrichment via Anthropic API.
Leon correctly pointed out: players shouldn't be forced to pay for API keys.
Built pattern matching instead — recognises Triglavian prefixes
(Starving=Neut/First, Anchoring=Tackle, Blinding=Damp, etc),
all hull types, Drifters, Sleepers, EDENCOM, Rogue Drones, Angels, Sansha.
AI enrichment kept as optional feature for truly unknown NPCs.
Result: works for 95% of abyss content with zero API cost.

**Drone two-button system:**
Originally one toggle button — clicking turned warning on/off.
Problem found in testing: clicking twice accidentally cleared the warning.
Fix: split into two buttons:
- "🛸 Drone took armor" — always visible, only sets flag ON
- "✔ Repaired" — only appears when flag active, clears when clicked
Flag survives submit/reset — only clears on Repaired click or new run Start.

**Drone flash on room transition:**
When R1✓ or R2✓ clicked — overlay briefly flashes "🛸 Check drone HP!"
Passive, non-blocking. Leon specifically requested NO popups.
"Abyss is brutal enough without popups getting in the way."

**Best run metric:**
Originally showed fastest elapsed time.
Leon corrected: fastest elapsed is NOT best run — most time remaining IS.
A 01:05 elapsed run is bad if you barely made it.
Fixed to show most time remaining = most efficient clear.

**ISK/hour correction:**
Originally divided total loot by total run time.
Leon identified: ~60 second mandatory re-entry wait between runs.
5 runs = 4 minutes of waiting not counted.
Fixed: add (totalRuns - 1) * 60 seconds to denominator.
Result: accurate real-world ISK/hour not inflated number.

**NPC panel at TOP of overlay:**
Originally at bottom of overlay.
Leon requested move to top — single screen player's eyes are already
on the overlay, top placement means immediate visibility mid-run.

**Detail panel LEFT, list RIGHT in NPC Library:**
Originally list on left, detail on right.
Leon's second screen is on the right — detail panel closer to
left means less eye movement from main screen.

**Auto-clear detected NPCs on room done:**
R1✓/R2✓/R3✓ clicks now auto-clear the detected NPC list.
Reason: those NPCs are dead, new room = fresh list needed.
No manual clear required mid-run.

**Loot tracking Before/After:**
Uses EVE's built-in "Est. Price" at bottom of inventory window.
Player types value before entering filament, types again after exiting.
App calculates difference (Δ).
Deliberately manual — EVE blocks inventory reading by external apps.
Instruction manual explains exactly where to find Est. Price.

**Pressure color system:**
Green: room time < 5 minutes
Amber: room time 5-7 minutes  
Red: room time > 7 minutes
Based on per-room time not total run time.
Leon is color blind (red-brown) but confirmed green always shows
during his fast runs. Red = something already went wrong anyway.
No text warning added — if you see red you already know.

**Auto-clear timer fix:**
Original auto-clear timer cleared detected NPCs every 30 seconds.
Problem: was clearing during active runs — NPCs disappeared mid-fight.
Fix: timer now checks _sessionActive and _pendingSubmit before clearing.
Only clears between runs during idle period.

================================================================
## BUGS FOUND IN LIVE TESTING
================================================================

**Bug: NPC list cleared during run**
Cause: auto-clear timer not checking session state
Fix: added _sessionActive check to AutoClearDetectedTimer_Tick

**Bug: Drone button cleared on Submit**
Cause: ResetTimer() called _dronesNeedRepair = false
Fix: removed drone reset from ResetTimer — only clears on explicit action

**Bug: Config pointing to Chatlogs not Gamelogs**
Cause: Leon accidentally browsed to wrong folder in setup wizard
Fix: setup wizard now validates and the label clarifies "Gamelogs"
Note: most common user error expected — mention prominently in forum post

**Bug: Duplicate x:Name controls**
Caused multiple CS0104 compile errors.
Fixed by ensuring single instance of each named control.

**Bug: Missing closing tags in XAML**
Several instances of mismatched tags during refactoring.
Fixed by running python3 XML validation after every edit.

**Bug: Overlay brings main window to front on click**
See Architecture Decisions — WM_MOUSEACTIVATE fix.

**Bug: Loot textboxes not typeable**
Caused by WS_EX_NOACTIVATE blocking all keyboard input.
Fixed in v1.0.0.43 with smart WM_MOUSEACTIVATE interceptor.

**Bug: UseWindowsForms caused CS0104 ambiguous references**
Added UseWindowsForms for FolderBrowserDialog.
Caused WPF/WinForms namespace conflicts on Button, Brush, Panel, Color.
Fix: replaced with WPF native OpenFolderDialog then later OpenFileDialog workaround.
UseWindowsForms removed completely.

================================================================
## WHAT'S OPTIONAL (IMPORTANT FOR NEW PLAYERS)
================================================================

Everything except Start button and room buttons is optional:
- Tier selection: optional
- Weather selection: optional  
- Loot tracking: optional
- NPC detection: optional (needs log folder setup)
- AI enrichment: optional (needs Anthropic API key)
- Cockpit mode: optional

Many players will use it as a pure timer. That's valid and supported.

================================================================
## NPC LIBRARY STATUS
================================================================

Current: 119 NPCs across 7 factions
Factions covered: Triglavian, Drifters/Seekers, Sleepers, EDENCOM,
Rogue Drones, Angels (Lucifer), Sansha (Devoted)

Known gaps:
- Some Ephialtes variants
- Grip Tessera
- Depths of Abyss additions
- Some newer event NPCs

Strategy: library grows through live testing.
When combat log sees unknown NPC — smart inference fills in what it can.
Auto-detected NPCs tagged so players know info is inferred not confirmed.

================================================================
## PLANNED FEATURES (FUTURE VERSIONS)
================================================================

**Near term:**
- Inventory OCR (Windows.Media.Ocr) — auto-read Est. Price
  without manual typing. Planned but complex — requires OCR
  trained on EVE's font and UI layout.

**Medium term:**
- Mobile companion app (phone as second screen)
  Phone connects to PC app via local WiFi
  Shows timer, NPCs, room status on phone screen
  Fully customizable — user picks which views they want
  Could be PWA (Progressive Web App) — works iOS and Android
  No app store approval needed

**Longer term:**
- Jita trading app — ESI market API, calculates sell vs list after fees
- Jita camera app — phone OCR reads market screen, instant profit calc
- L4 mission info app — mission database and tips
- ML.NET run analysis — predicts best tier/weather for your skills/fit

================================================================
## LEON'S EVE CONTEXT (relevant to app development)
================================================================

**Current ships:**
- Gila (T2-T4 Electrical Abyss, Frarolle system)
- Jakkals (Astero exploration)
- Kasteel (Orca, eva silver alt, full mining boost fit)
- Spoed Vark (Punisher bookmark runner, 8648 m/s)
- Hoer (Catalyst salvager — "whore" in Afrikaans, passes CCP filter 😄)
- Zephyr (2009 anniversary ship, museum piece, Sleepers ignore it)
- Apotheosis (5th anniversary shuttle, never sell)

**Current goals:**
- Golem in ~175 days when all skills maxed (Nov 16, 2026)
- Orbweaver SW-300-I x46 held as market investment
  Bought at ~55M each, target sell 150-200M+
  Event-limited hybrid web+combat drone from Capsuleer Day XXIII
  No new supply entering market
  Check price end of June, sell within 3 months max

**App testing results (T3 Fierce Electrical):**
- 06:45 elapsed, 11:16 remaining
- 53.9M ISK/hour
- All 3 rooms completed
- App tracking correctly

================================================================
## DONATION LINKS (for About popup)
================================================================

Ko-fi: ko-fi.com/jakkelsza
PayPal: paypal.me/JakkelsZA
Bitcoin: 35U3rbr7XWAsi55KqUJDpRKoKB8PistGZv
GitHub: github.com/JakkelsZA

================================================================
## LAUNCH PLAN
================================================================

Target: Saturday 30 May 2026

Still needed before launch:
- About popup built in app with all 4 donation links
- README written for GitHub
- Final zip with About popup
- Upload to GitHub via Cowork
- EVE Forums post (Tools & Services section)
- Reddit r/Eve post
- EVE Discord post

App description for posts:
"EVE Abyss Companion was built for both new and veteran capsuleers
who want to track their Abyssal Deadspace runs properly.
No memory reading. No game hooks. Just a clean companion
that sits alongside your game."

Do NOT mention Leon's personal story (electrician, SA, etc)
App story: "built by a capsuleer for capsuleers"

================================================================
## DEVELOPMENT RULES (keep these always)
================================================================

1. Zip workflow: Leon sends zip, Claude modifies, Claude returns zip
2. Validate ALL XAML with python3 XML parser before packaging
3. Bump version in csproj every zip
4. Minimal changes — only touch what we're working on
5. Personal story stays private — app is anonymous JakkelsZA
6. Test in real abyss runs before claiming something works
7. Leon's feedback from live testing overrides theory always
8. No popups during runs — Leon's hard rule
9. Everything optional — never force the user

================================================================
EOF
