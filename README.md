# AutoAnimalTraining

A RimWorld 1.6 mod that automatically routes tamed animals to a designated Training Zone when their training skills degrade, then releases them once training is restored.

## The Problem

When animal training degrades, colonists must chase animals across the entire map to re-train them — wasting time pathfinding to scattered animals instead of doing productive work.

## The Solution

Create an allowed area named `@Training` near your base. The mod automatically:

1. Detects when an animal's training drops below threshold
2. Saves the animal's current area restriction
3. Assigns the animal to the `@Training` zone
4. Waits for a colonist to restore the training
5. Releases the animal back to its original area

No micromanagement needed. Trainers work in one spot, animals come to them.

## Setup

1. Prerequest: [Harmony](https://steamcommunity.com/workshop/filedetails/?id=2009463077)
2. Subscribe AutoAnimalTraining
3. In-game: create an allowed area named `@Training` (Architect > Zone > Manage Areas)
4. Draw the zone somewhere convenient
5. Done — the mod handles the rest

## Settings

Access via **Options > Mod Settings > Auto Animal Training**:

| Setting | Default | Description |
|---------|---------|-------------|
| Training zone name | `@Training` | Area name to match |
| Poll interval | 2000 ticks (~33s) | How often the mod checks animals |
| Restrict training to zone | On | Only train animals inside the zone |
| Per-skill thresholds | Per-skill defaults | When each skill triggers routing (set to -1 to disable) |
| Verbose logging | Off | Detailed logging for debugging |

## Which Animals?

**Supported:** Non-Roamer tamed animals (dogs, wolves, cats, elephants, pigs, goats, cows, etc.)

**Not supported:** Roamer animals (muffalo, boomalope, dromedary) — RimWorld does not allow area restrictions on these animals by design.

## Project Structure

```
AutoAnimalTraining/
├── About/About.xml                          # Mod metadata
├── Source/
│   ├── AutoAnimalTrainingMod.cs             # Mod entry point, Harmony init, settings UI
│   ├── AutoAnimalTrainingSettings.cs        # Per-skill thresholds, configuration
│   ├── MapComponent_AutoTraining.cs         # Core polling loop, routing, save/load
│   └── Patches/
│       ├── Patch_AreaEvents.cs              # Area create/delete/rename detection
│       └── Patch_WorkGiver_Train.cs         # Restrict training to zone
├── 1.6/Assemblies/                          # Compiled DLL (RimWorld 1.6)
└── agent_communicate/                       # Design docs & planning
    ├── AutoAnimalTraining-idea.md           # Phase 1: concept & vanilla flow analysis
    ├── AutoAnimalTraining-planning-flow.md  # Phase 2: technical design & flowcharts
    ├── AutoAnimalTraining-finalize.md       # Phase 3: final architecture & release plan
    ├── AutoAnimalTraining-log.md            # Chronological development log
    ├── milestones.md                        # Milestone tracking (M1–M5)
    └── steam-workshop-draft.md              # Steam Workshop page draft
```

## Building

```bash
dotnet build Source/AnimalTrainingArea.csproj -c Release
```

Output goes to `1.6/Assemblies/AutoAnimalTraining.dll`. Restart RimWorld to reload.

**Target framework:** .NET Framework 4.7.2

## Compatibility

- RimWorld 1.6
- Requires Harmony
- Safe to add mid-save
- No destructive patches — uses postfixes on area management and a single prefix on `WorkGiver_Train`
