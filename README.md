# SONAR Easy Mode

A BepInEx 6 (IL2CPP) plugin for **SONAR** that makes solo runs manageable. Hunger, stamina, fuel and CO2 last longer, there are fewer random faults, repairs are faster and you take less damage. By default it is active only while you are the only player, and vanilla values come back as soon as someone joins.

The mod changes values in memory only. It never modifies game files.

## Repository layout

```
Makefile                  build / deploy / package helper
local.mk.example          template for local.mk (your game path; local.mk is git-ignored)
SonarEasyMode/            plugin source (C#, net6.0)
  SonarEasyMode.csproj    project; references the game's BepInEx install
  Plugin.cs               entry point + EasyModeDriver (applies/restores values)
  Settings.cs             config file binding, presets, knobs
  Tweaks.cs               list of game fields touched by each knob
research/                 reverse-engineering notes (NOTES.md), decoded data, config dumps, scripts
```

---

## 1. Install BepInEx into the game

The plugin needs **BepInEx 6 Bleeding Edge, IL2CPP, win-x64**, build **be.791 or newer**. Older builds can't read this game's metadata (Unity 6000.3, metadata v39).

1. Download `BepInEx-Unity.IL2CPP-win-x64-6.0.0-be.<N>+<hash>.zip` from <https://builds.bepinex.dev/projects/bepinex_be>.
2. Open the game folder: Steam → SONAR → *Manage* → *Browse local files*, for example `<SteamLibrary>\steamapps\common\SONAR`.
3. Extract the zip **into the game folder**, so that the following sit next to `SONAR.exe`:
   ```
   SONAR.exe
   winhttp.dll
   doorstop_config.ini
   .doorstop_version
   BepInEx\
   dotnet\
   ```
4. **Launch the game once** and wait at the main menu. On this first run BepInEx generates the interop assemblies (`BepInEx\interop\*.dll`), which takes about 1–3 minutes. The game window may look frozen during this time.
5. Check `BepInEx\LogOutput.log`. It should contain:
   ```
   [Info   :   BepInEx] Running under Unity 6000.3.19f1
   [Message:   BepInEx] Chainloader startup complete
   ```
   and `BepInEx\interop\` should contain `Assembly-CSharp.dll` and about 128 other DLLs.

> The interop folder is regenerated automatically when the game updates. This also happens on the first launch after a patch.

## 2. Build the plugin

### Prerequisites
- **.NET SDK 6.0 or newer.** 8.0 is recommended; the project targets `net6.0`, which newer SDKs build fine.
  - Normal install: <https://dotnet.microsoft.com/download>
  - Or a per-user install without admin rights:
    ```powershell
    Invoke-WebRequest https://dot.net/v1/dotnet-install.ps1 -OutFile dotnet-install.ps1
    .\dotnet-install.ps1 -Channel 8.0 -InstallDir "$env:USERPROFILE\.dotnet" -NoPath
    $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"; $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    ```
- **BepInEx installed in the game, with interop generated** (step 1). The project compiles against
  `<GameDir>\BepInEx\core\*.dll` and `<GameDir>\BepInEx\interop\*.dll`. Nothing is downloaded from NuGet.

### Local settings (`local.mk`)
The repository contains no machine-specific paths. Set your game location once in a git-ignored `local.mk`:

```
cp local.mk.example local.mk      # or: copy local.mk.example local.mk
```

```make
# local.mk
# folder containing SONAR.exe (keep comments on their own lines: make keeps trailing spaces)
GAME_DIR = C:/Program Files (x86)/Steam/steamapps/common/SONAR
# optional, .NET 6+ SDK
# DOTNET = C:/Program Files/dotnet/dotnet.exe
```

Variables passed on the command line (`make GAME_DIR=...`) override `local.mk`. If `GAME_DIR` isn't set anywhere, make stops with a hint.

### Build with make (recommended)
The root `Makefile` needs GNU make (for example `choco install make`). If `DOTNET` isn't set, it uses the per-user SDK in `%USERPROFILE%\.dotnet` when that exists, and otherwise `dotnet` from PATH.

```
make                      # build Release + copy DLL into the game (= make deploy)
make build                # build only
make deploy               # build + copy to <GAME_DIR>\BepInEx\plugins\SonarEasyMode
make package              # build + dist\SonarEasyMode-<version>.zip for players
make clean                # remove bin/obj/dist

make GAME_DIR="E:/Games/Steam/steamapps/common/SONAR"   # one-off override of local.mk
make CONFIG=Debug                                       # debug build
```

`deploy` refuses to run while `SONAR.exe` is running, because the DLL would be locked. `build` checks first that BepInEx interop exists in `GAME_DIR`.

### Build with dotnet directly
The project has no default game path. `dotnet` doesn't read `local.mk`, so pass `GameDir` explicitly, or set it as an environment variable (MSBuild picks environment variables up as properties). Run these from the repository root:

```powershell
# Build and deploy into the game
dotnet build SonarEasyMode -c Release -p:GameDir="X:\Steam\steamapps\common\SONAR"

# Same, via environment variable
$env:GameDir = "X:\Steam\steamapps\common\SONAR"; dotnet build SonarEasyMode -c Release

# Build only, no copy into the game
dotnet build SonarEasyMode -c Release -p:GameDir="..." -p:Deploy=false
```

- Output: `SonarEasyMode\bin\Release\net6.0\SonarEasyMode.dll`
- Deploy target: `<GameDir>\BepInEx\plugins\SonarEasyMode\SonarEasyMode.dll`
- If `GameDir` is missing or BepInEx interop isn't there, the build fails with a clear error.

> **Close the game before building.** While the game runs, the DLL is locked and the copy step fails. The build itself still succeeds.

## 3. Install the plugin (without building)

If you already have `SonarEasyMode.dll`:

1. Complete step 1 (BepInEx installed, launched once).
2. Create `<GameDir>\BepInEx\plugins\SonarEasyMode\` and put `SonarEasyMode.dll` in it.
3. Launch the game. `BepInEx\LogOutput.log` should show:
   ```
   [Info   :   BepInEx] Loading [SONAR Easy Mode 0.1.0]
   [Info   :SONAR Easy Mode] SONAR Easy Mode 0.1.0 loaded. Preset=Easy, OnlyWhenSolo=True
   [Info   :SONAR Easy Mode] Easy mode ACTIVE (preset Easy, players 1, 35 fields changed)
   ```

To share the mod, zip it with the folder structure intact (`BepInEx\plugins\SonarEasyMode\SonarEasyMode.dll`). Players then extract the zip into a game folder that already has BepInEx.

## 4. Configure

After the first launch with the plugin, the config file `<GameDir>\BepInEx\config\sonar.easymode.cfg` is created. Edit it while the game is closed; changes apply on the next launch.

`[General]`
| Setting | Default | Meaning |
|---|---|---|
| `Enabled` | `true` | Master switch |
| `OnlyWhenSolo` | `true` | Apply only while you're the only player; vanilla values come back when others join |
| `Preset` | `Easy` | `Off`, `Easy`, `VeryEasy`, or `Custom` (uses the `[Custom]` section) |
| `VerboseLog` | `false` | Log every field change (old → new, plus the vanilla value) |

`[Custom]` knobs (used only when `Preset = Custom`). Multipliers use 1 for vanilla; for absolute values, `-1` keeps the vanilla value.

| Knob | Game fields (`Indy.*Config`) | Easy | VeryEasy |
|---|---|---|---|
| HungerDuration | hungerDurationSeconds | ×2 | ×3 |
| StaminaDuration | staminaDurationSeconds | ×2 | ×3 |
| WorkStaminaDrain | staminaReductionRunningOrHeavyWork | ×0.6 | ×0.4 |
| StaminaRecovery | addStaminaSleepingSpeed, addStaminaPerCoffe | ×1.5 | ×2 |
| FuelDuration | engineFuelDurationSeconds | ×2 | ×3 |
| Co2FilterDuration | co2FilterDurationSeconds | ×2 | ×3 |
| SnorkelDuration | snorkelTankDuration | ×2 | ×3 |
| RefuelSpeed | refuelAmount | ×1.5 | ×2 |
| FaultRisk | riskOf* (leak / fire / fuse) | ×0.5 | ×0.25 |
| HitFaultChance | damage*Probability, damageEngineStop* | ×0.6 | ×0.35 |
| MaxFaultsPerType | nFires / nLeaks / nFuseBreaksMaxPerGame | vanilla (2) | 1 |
| FailuresToExplode | amountToFailuresToExplode | 25 | 35 |
| PlayerDamage | damageFire / damageExplosion Min/Max | ×0.6 | ×0.4 |
| FireSpread | propagationProbability | ×0.6 | ×0.4 |
| FloodSpeed | waterLeakAdd | ×0.6 | ×0.4 |
| PumpSpeed | waterAspirationRemove | ×1.5 | ×2 |
| RepairSpeed | hullLeakFixAmount, pipeLeakFixAmount, extinFixAmount | ×1.5 | ×2 |
| MoveSpeed | walk / run / crouch / tired speeds | ×1 | ×1.15 |
| OpenWorldFuelUse *(experimental, see below)* | fuelEveryDistanceUnit | ×0.6 | ×0.4 |
| OpenWorldFilterUse *(experimental, see below)* | filterEveryDistanceUnit | ×0.6 | ×0.4 |
| OpenWorldFoodUse *(experimental, see below)* | sandwitchEveryDistanceUnit | ×0.6 | ×0.4 |

**About the `OpenWorld*Use` knobs.** They apply only to the *Open World* game mode (free roaming between ports, selling loot and buying supplies). Each field is a balancing figure: how much of one fuel can, CO2 filter or sandwich is used per map "distance unit" of travel. A distance unit takes `timeToMoveDistanceUnit` = 0.2 s, so `fuelEveryDistanceUnit` = 0.2 / `durationFuelSeconds` (225) ≈ 0.00089, and the same for filter (300 s) and sandwich (262.5 s). The only code found reading them is `OpenWorldLevelingSimulator`, a developer bot that simulates a playthrough to tune prices and rewards. The real fuel, CO2 and hunger drain on the submarine in Open World most likely comes from the regular `Fuel/Co2Filter/HungerDuration` knobs. Expect these three knobs to change nothing in play; they stay in until this is confirmed in-game (see `research/NOTES.md`).

## 5. Uninstall

- **Plugin only:** delete `<GameDir>\BepInEx\plugins\SonarEasyMode\`.
- **Disable BepInEx temporarily:** set `enabled = false` in `<GameDir>\doorstop_config.ini`.
- **Remove BepInEx completely:** delete `BepInEx\`, `dotnet\`, `winhttp.dll`, `doorstop_config.ini` and `.doorstop_version` from the game folder. You can also run Steam's *Verify integrity of game files*. That check doesn't remove the BepInEx files, but it restores any changed game files.

## Troubleshooting

| Symptom | Fix |
|---|---|
| No `BepInEx\LogOutput.log` after launch | `winhttp.dll` isn't next to `SONAR.exe`, or `doorstop_config.ini` has `enabled = false`. |
| `Unsupported metadata version` in the log | The BepInEx build is too old. Use be.791 or newer. |
| Build error: `Metadata file '...\BepInEx\interop\Assembly-CSharp.dll' could not be found` | BepInEx isn't installed at `GameDir`, or the game hasn't been launched once. Check `GAME_DIR` in `local.mk`, or pass `-p:GameDir=...`. |
| `GAME_DIR is not set` / `GameDir is not set` | Create `local.mk` from `local.mk.example`, or pass the path explicitly. |
| Build error `MSB3027` / file is locked | The game is running. Close it and rebuild. |
| `Field 'X' not found on Indy.LevelingConfig (game update?)` | The game renamed a field. Update `Tweaks.cs`, and check `research/NOTES.md` for how fields were found. |
| Log says `Vanilla values (... players 2 ...)` | Expected: `OnlyWhenSolo` is on and someone else is in the session. |
| Mod is in multiplayer and others see odd behavior | Leave `OnlyWhenSolo = true`. The game is host-authoritative for many systems. |

## How it works (for developers)

`EasyModeDriver` is a MonoBehaviour added by the plugin. Once per second it:
1. reads `Indy.Engine.Instance` and its `levelingConfig`, `gameConfig` and `openWorldLevelingConfig` ScriptableObjects,
2. counts players through `Engine.multiplayer.GetPlayersCount()`,
3. on first sight of an asset, records the original value of every field listed in `Tweaks.All`,
4. writes `original × knob`, or the override value, while active, and restores the originals otherwise.

The values are applied in the main menu, before any mission starts.

To add a knob:
1. Add a `Knob` in `Settings.cs`, with its vanilla, Easy and VeryEasy values.
2. Bind it in `Settings.Bind`.
3. Add `Tweak` entries in `Tweaks.cs` mapping it to game fields. Field names must match the interop property names; see `research/config_dumps/*.json` for every available field and its default.
