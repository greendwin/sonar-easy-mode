# SONAR Easy Mode

A BepInEx 6 (IL2CPP) plugin that makes solo runs of **SONAR** more forgiving. It changes values in memory only, never game files. By default it applies only while you are the only player, and restores vanilla values when someone joins.

While `Enabled = true`, no times or PvP scores are uploaded to the leaderboards, even in sessions where vanilla values apply. Set `Enabled = false` to play ranked.

## Settings

Config file: `<GameDir>\BepInEx\config\sonar.easymode.cfg`. It is created on first launch with the plugin. Edit it while the game is closed.

**[General]**

| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch. While on, nothing is uploaded to the leaderboards |
| `OnlyWhenSolo` | `true` | Apply only while you're the only player in the session |
| `VerboseLog` | `false` | Log every changed value |

**[Features]**

| Setting | Default | Meaning |
|---|---|---|
| `FanLink` | `ToggleFloor` | What toggling a fan does to the others. `Off`: vanilla. `ToggleAll`: every fan follows, so one switch gives silent running. `ToggleFloor`: only fans on the same floor follow, so silent running needs one switch on each floor |

**[Tweaks]**: multipliers, where `1` = vanilla. For the two absolute values, `-1` = vanilla.

| Knob | Effect | Default |
|---|---|---|
| **Crew** | | |
| `HungerDuration` | Hunger bar lasts N× longer | 3 |
| `StaminaDuration` | Stamina bar lasts N× longer | 2.5 |
| `WorkStaminaDrain` | Extra stamina drain from running / heavy work | 0.6 |
| `StaminaRecovery` | Stamina from sleep and coffee | 1.5 |
| `MoveSpeed` | Walk / run / crouch speed | 1.15 |
| `PlayerDamage` | Damage taken from fire and explosions | 0.5 |
| **Consumables** | | |
| `FuelDuration` | Engine fuel lasts N× longer | 2.5 |
| `Co2FilterDuration` | CO2 filter lasts N× longer | 2 |
| `SnorkelDuration` | Snorkel tank lasts N× longer | 1 |
| `RefuelSpeed` | Refueling speed | 1 |
| **Faults** | | |
| `FaultRisk` | Chance of random leaks / fires / fuse breaks | 0.1 |
| `HitFaultChance` | Chance that a hit causes a fault or engine stop | 0.5 |
| `MaxFaultsPerType` | Max fires / leaks / fuse breaks per mission (absolute, vanilla 2) | 2 |
| `FailuresToExplode` | Failures before the sub is lost (absolute, vanilla 20) | 25 |
| `FireSpread` | Fire propagation chance | 0.5 |
| `FloodSpeed` | Water ingress from leaks | 0.6 |
| `PumpSpeed` | Pump water removal | 2 |
| `RepairSpeed` | Welding / pipe repair / extinguishing speed | 1 |
| **Stealth and combat** | | |
| `NoiseRadius` | Distance at which enemies hear you | 1 |
| `DepthNoise` | Game's per-depth noise factors (×1.25 / 1.0 / 0.75 at 500 / 900 / 1500 m). Probably stacks with `NoiseRadius` | 1 |
| `DamageToEnemies` | Damage enemy vessels take (see below) | 1 |
| `EnemyTorpedoSpeed` | Speed of enemy torpedoes; yours are unaffected | 1 |
| `EnemyTorpedoTurnRate` | Turn rate of enemy torpedoes; lower is easier to evade | 0.7 |
| `AllyShipSpeed` | Speed of civilian / allied ships, e.g. the ship you escort | 0.8 |
| `EnemyShipSpeed` | Speed of enemy vessels | 0.8 |
| `MineSlowdownRange` | Range at which an escorted ship slows down for mines ahead (vanilla 200). 0 = never slows down, but then it may sail into mines you haven't cleared | 1 |
| **Open World** *(experimental)* | | |
| `OpenWorldFuelUse`, `OpenWorldFilterUse`, `OpenWorldFoodUse` | Fuel / filter / food use per map distance unit | 1 |

**Hits to sink.** Torpedo 1 does 0.66 damage. Enemy life is 1.0 for a submarine or frigate, 1.2 for a destroyer and 2.0 for a carrier.

| `DamageToEnemies` | Sub | Frigate | Destroyer | Carrier |
|---|---|---|---|---|
| 1 (vanilla) | 2 | 2 | 2 | 4 |
| 1.6 | 1 | 1 | 2 | 2 |
| 2 | 1 | 1 | 1 | 2 |

**Open World knobs.** These probably have no effect. The only code found reading these values is the developers' balancing simulator. Submarine drain in Open World most likely comes from the regular `Fuel/Co2Filter/HungerDuration` knobs. See `research/NOTES.md`.

## Install

1. **BepInEx 6 Bleeding Edge, IL2CPP, win-x64, build be.791 or newer.** Older builds can't read this game's metadata.
   1. Download it from <https://builds.bepinex.dev/projects/bepinex_be>.
   2. Extract it into the game folder, next to `SONAR.exe`.
   3. Launch the game once. The first start takes 1–3 minutes while BepInEx generates `BepInEx\interop\`.
2. **The plugin:** put `SonarEasyMode.dll` into `<GameDir>\BepInEx\plugins\SonarEasyMode\`, or build it with `make` (below).
3. **Check that it loaded.** `BepInEx\LogOutput.log` should contain `Easy mode ACTIVE (players 1, <N> fields changed)`.

**Uninstall:** delete `BepInEx\plugins\SonarEasyMode\`. To disable BepInEx itself, set `enabled = false` in `doorstop_config.ini`.

## Build

Requirements:
- .NET SDK 6+ (8 recommended).
- GNU make (for example `choco install make`).
- BepInEx installed in the game with interop generated (Install step 1). The project compiles against `<GameDir>\BepInEx\core` and `<GameDir>\BepInEx\interop`.

Set your game path once in a git-ignored `local.mk`:

```
copy local.mk.example local.mk
```
```make
# keep comments on their own lines: make keeps trailing spaces
GAME_DIR = C:/Program Files (x86)/Steam/steamapps/common/SONAR
```

```
make            # build and copy the DLL into the game (close the game first)
make build      # build only
make package    # dist\SonarEasyMode-<version>.zip
make clean
```

Without make, pass the path yourself:

```
dotnet build SonarEasyMode -c Release -p:GameDir="X:\...\SONAR"
```

Add `-p:Deploy=false` to skip copying the DLL into the game.

## Developer notes

- **`Plugin.cs`**: `EasyModeDriver` runs once per second.
  - It reads `Indy.Engine.Instance`, counts players, and decides whether the mod is active.
  - It then applies or restores the config values.
- **`Tweaks.cs`**: maps knobs to fields of the game's config ScriptableObjects (`LevelingConfig`, `GameConfig`, `MissionsConfig`, `OpenWorldLevelingConfig`).
  - Original values are saved on first sight and restored when the mod is inactive.
- **`ShipSpeeds.cs`**: once per driver tick, scales `Ship.speedMultiplier` of civilian and enemy ships (`AllyShipSpeed` / `EnemyShipSpeed`), the same multiplier the game's debug menu sets. A value the game sets later becomes the new baseline.
- **`Patches.cs`**: Harmony patches for things that are not plain config values:
  - `Engine.GetNoiseRadius` (NoiseRadius)
  - `Ship.DamageShip` (DamageToEnemies)
  - `Torpedo.LaunchWeapon` (enemy torpedoes get a scaled copy of their weapon config)
  - fan toggles (FanLink)
  - `AppUI.LeaderboardUploadTime`, `AbyLib.Leaderboards.UploadTime` / `UploadPvpScore` (skipped while `Enabled`)

  Each patch logs its first hit, so `LogOutput.log` shows which hooks fire in play.
- **`Settings.cs`**: config binding. To add a knob:
  1. Add a `Knob(key, vanilla, default)`.
  2. Bind it in `Settings.Bind`.
  3. Map it to fields in `Tweaks.cs`, or use it in a patch.
- **Game update breaks a field:** the log shows `Field 'X' not found` or `Patch X failed`.
- **Research:** `research/NOTES.md` holds the reverse-engineering notes. `research/config_dumps/*.json` lists every config field with its default.
