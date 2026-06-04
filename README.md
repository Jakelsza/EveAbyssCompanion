# EVE Abyss Companion

A free companion app for tracking Abyssal Deadspace runs in EVE Online.

**v1.0.0 Beta** — Built by JakkelsZA

> **No memory reading. No game hooks. No ToS risk.** The app reads only your EVE Gamelog files — the same files EVE writes itself — and displays information in a separate window on top of EVE.

---

## Contents

1. [What is EVE Abyss Companion?](#1-what-is-eve-abyss-companion)
2. [Requirements](#2-requirements)
3. [Installation](#3-installation)
4. [First-Time Setup](#4-first-time-setup)
5. [Standard Mode vs Cockpit Mode](#5-standard-mode-vs-cockpit-mode)
6. [The Main Window](#6-the-main-window)
7. [Before Your Run](#7-before-your-run)
8. [The Overlay](#8-the-overlay)
9. [During Your Run](#9-during-your-run)
10. [After Your Run — Loot Tracking](#10-after-your-run--loot-tracking)
11. [NPC Library](#11-npc-library)
12. [History Tab](#12-history-tab)
13. [Stats Tab](#13-stats-tab)
14. [Settings Reference](#14-settings-reference)
15. [Tips from the Developer](#15-tips-from-the-developer)

---

## 1. What is EVE Abyss Companion?

EVE Abyss Companion is a free desktop app that sits alongside EVE Online while you run Abyssal Deadspace sites. It tracks your run timer, automatically detects NPCs from your combat log, shows kill priority and EWAR information on a transparent overlay, and records your loot and run splits for long-term stats.

*Built for capsuleers, by a capsuleer.*

---

## 2. Requirements

| Requirement | Details |
|---|---|
| **Operating System** | Windows 10 or Windows 11 |
| **.NET Runtime** | .NET 8.0 Desktop Runtime (free from microsoft.com) |
| **EVE Online** | Any active account. Combat logging must be enabled. |
| **Screens** | Works on single screen (Cockpit Mode) or dual screen (Standard Mode) |

**Enable combat logging in EVE:** ESC → Accessories → Log Settings → enable *Log Combat*.

---

## 3. Installation

1. **Download the latest release** from the [GitHub releases page](https://github.com/Jakelsza/EveAbyssCompanion/releases/latest).
2. **Extract the zip** into a fresh folder. Keep the folder named *EveAbyssCompanion*.
3. **Run EveAbyssCompanion.exe** directly from the extracted folder. No installer needed.
4. The Setup Wizard will open automatically on first launch.

> **Always extract into a fresh folder.** Do not overwrite an old version in-place. Extract to a new folder, then delete the old one.

---

## 4. First-Time Setup

### Step 1 of 3 — Choose Your Mode

| Mode | When to use |
|---|---|
| **Cockpit Mode** | EVE takes your whole screen. Overlay is your run control centre. Main window = between-runs dashboard. |
| **Standard Mode** | EVE on one screen, main window on the other. Full controls in both places. |

### Step 2 of 3 — Combat Log Folder

Point the app at your EVE Gamelogs folder. The default path is *Documents\EVE\logs\Gamelogs*. If you use OneDrive or a custom EVE install, click **Browse** to find it.

**Finding Gamelogs:** In EVE go to **Help → Show log folder** then navigate into *logs\Gamelogs*.

### Step 3 of 3 — Ready

Click **Let's Go!**

---

## 5. Standard Mode vs Cockpit Mode

### Standard Mode — Two Screens

Full workflow controls in the centre column of the main window.

### Cockpit Mode — Single Screen

Run controls move to the overlay. Main window shows NPC Library, History, Stats, About. Click **Setup** on the overlay to bring the main window forward at any time.

---

## 6. The Main Window

| Zone | What it does |
|---|---|
| **Left — Timer & Rooms** | 20-minute countdown, room status, R1✓ R2✓ R3✓ buttons. Active room flashes. |
| **Centre — Workflow** | Tier, weather, Before/After loot, Start/Reset/End, Submit. In order of use top to bottom. |
| **Right — Settings** | Mode checkboxes, Overlay toggle, Re-run Setup, last run summary. |

---

## 7. Before Your Run

1. **Select Tier** matching your filament (T0–T6).
2. **Select Weather** (Electrical, Exotic, Firestorm, Gamma, or Dark).
3. **Enter Before loot.** Open EVE inventory. Read the *Est. price* at the bottom. Type the value in millions into the blue **Before** field (e.g. *1950* for 1.95B).
4. **Click Start.** Timer begins. Overlay opens automatically.

> **Enter Before BEFORE activating the filament.** You cannot check inventory once inside. Missing Before = loot shows n/a for that run.

---

## 8. The Overlay

The overlay floats above EVE without stealing keyboard or mouse focus. Click overlay buttons and fly normally at the same time.

| Area | What it shows |
|---|---|
| **Detected NPCs** | Auto-detected NPCs as colour-coded chips |
| **Colour legend** | Quick colour reference, always visible |
| **Timer** | 20-minute countdown |
| **Status** | Current room and tier/weather |
| **Room buttons** | R1✓ R2✓ R3✓ — click when a room is cleared |
| **Drone button** | Flag when drones take armor damage |
| **Loot tracking** | Before / After fields with live delta |
| **Actions** | Start, Submit, End, Setup |

### NPC Chip Colours

| Colour | Meaning |
|---|---|
| 🟡 Yellow | Neut |
| 🔴 Red | Scram |
| 🟠 Orange | Web |
| 🟣 Purple | Damp / Disrupt |
| 🟢 Green | Logi |
| ⚫ Grey | Paint |
| Gold border | Boss |

Some NPCs carry multiple EWAR types — e.g. *Elite Lucifer Cynabal* shows both Neut and Web on the same chip.

> **Lucifer rooms (Angel Cartel):** Elite Lucifer Cynabal and Lucifer Cynabal actively target and destroy drones. Pull drones immediately.

> **Karybdis Tyrannos (Drifter boss):** Spawns 80km+ away already kiting. Target it the moment you land — every second it lives wastes time and gate distance.

---

## 9. During Your Run

1. **Auto-detection starts immediately.** NPCs appear sorted First → Early → Mid → Last.
2. **Click R1✓ when Room 1 is cleared.** NPC list resets. R2✓ starts flashing.
3. **Drone damage:** Click *Drone took armor* if drones take hits. Click *Repaired* after fixing.
4. **Repeat for R2✓ and R3✓.**

---

## 10. After Your Run — Loot Tracking

1. **Loot the cache** after Room 3.
2. **Open EVE inventory.** Read *Est. price* at the bottom. Type the value in millions into the **After** field on the overlay.
3. **Live delta appears** — e.g. *+17.25M*.
4. **Click Submit & End** to save the run to History.

> **Two-screen tip:** Type Before in the main window before the run. The overlay receives it automatically when it opens. Type After in the overlay after looting.

---

## 11. NPC Library

128 Abyssal NPCs with full detail on each entry.

| Field | What it tells you |
|---|---|
| **Kill Priority** | First / Early / Mid / Last |
| **Threat** | Very High / High / Medium / Low |
| **EWAR** | Electronic warfare type(s) |
| **Damage** | Damage types and NPC weaknesses |
| **Behaviors** | Tactical notes from real runs |

| Priority | Meaning |
|---|---|
| **First** | Kill immediately — Neuts, Scrammers, Logis, time-critical bosses |
| **Early** | Kill after First — Webs, secondary EWAR |
| **Mid** | Standard DPS targets |
| **Last** | No direct threat |

---

## 12. History Tab

| Column | What it shows |
|---|---|
| **Time** | Date and time submitted |
| **Tier / Weather** | Filament tier and weather type |
| **Rooms** | Rooms completed (3/3 = full clear) |
| **Elapsed / Remain** | Time used and time left on clock |
| **Splits** | Time per room (R1 / R2 / R3) |
| **Loot (M)** | Net ISK from loot in millions |

---

## 13. Stats Tab

| Stat | How it's calculated |
|---|---|
| **Best run** | Run with most time remaining — most efficient clear, not fastest clock |
| **ISK/hour** | Total loot ÷ total time including 60-second re-entry wait between runs |

---

## 14. Settings Reference

| Setting | What it does |
|---|---|
| **Cockpit mode** | Hides run controls on main window. Use overlay as primary control. |
| **Always keep overlay on** | Overlay stays visible between runs. |
| **Open on 2nd monitor** | Overlay opens on secondary display. |
| **Auto-detect NPCs from log** | Reads EVE Gamelogs to auto-identify NPCs during runs. |
| **Overlay ▶ / ■** | Toggle overlay open/closed. State preserved on hide. |
| **Re-run Setup** | Re-open Setup Wizard to change mode or log folder. |
| **Setup (on overlay)** | Brings main window to front. Single-screen players use this instead of Alt+Tab. |

---

## 15. Tips from the Developer

- Always enter **Before loot** before activating the filament.
- In **Lucifer (Angel) rooms** pull drones immediately — both the Elite and regular Cynabal destroy them.
- **Karybdis Tyrannos** spawns already kiting at 80km+. Target it the moment you land.
- A **gold border chip** = boss. Primary objective for that room.
- Room **splits in History** show which room type takes you longest — identify where to improve.
- The **best run stat** rewards efficient clears. Finishing with 12 minutes left beats squeaking through with 30 seconds.

---

## Support the Project

The app is free and always will be. If it saves you ISK or time and you want to say thanks, any support is genuinely appreciated.

- **Ko-fi:** [ko-fi.com/jakkelsza](https://ko-fi.com/jakkelsza)
- **PayPal:** [paypal.me/JakkelsZA](https://paypal.me/JakkelsZA)
- **Bitcoin:** 35U3rbr7XWAsi55KqUJDpRKoKB8PistGZv

---

*EVE Online and all associated assets are property of Fenris Creations (formerly CCP Games). This app is not affiliated with or endorsed by Fenris Creations.*
