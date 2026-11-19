# Changelog

All notable changes to SONAR Easy Mode are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-11-20

### Added

- **Pump linking** - `PumpLink` (default `true`): holding a bilge pump button also runs the pumps of every other room on the same floor that has water on the floor. Rooms without power are skipped. Each linked pump stops when its room is dry, loses power, or when you let go. Host only.
- **Full fuel at start** - `FullFuelAtStart` (default `true`): both engines start a mission, and a restart, with a full tank (vanilla starts engine 1 half full). Host only.

### Changed

- Tube autoload now acts only while Shift is held when you load a tube by hand, so loading a single tube no longer pulls a second torpedo from the rack.
- `FaultRisk` default 0.1 -> 0.2: random leaks, fires and fuse breaks are still rare, but a bit more frequent than before.

### Fixed

- Updated for game version 1.1.

## [1.1.0] - 2026-11-16

### Added

- **Panel remote** - `PanelRemote` (default `true`): on the diagnostics panel, pressing the tab that is already open runs an action, and the tab title shows the result. Host only.
  - **STEALTH**: silent alarm on/off. The alarm keeps its red lights, but its sound is turned down and it adds no noise (vanilla adds noise while any panel alarm is on, e.g. overheating with fans off).
  - **ENGINES**: both engines to 50%, or back to 100% if both are already at 50%.
  - **TEMPERATURE**: all fans off if any is on, otherwise all on.
- `SilentAlarmVolume` (default `0.15`): alarm volume while the silent alarm is on, as a fraction of normal. `0` mutes it.
- **Auto tagging** - `AutoTagInterval` (default `4`): while the sonar is on, the closest untagged ship inside the sonar radius is tagged with its correct type, one ship every 4 seconds. `0` = off. Tags you set by hand are kept, and auto tags count toward the tagging score.
- **Enemy fire tweaks**:
  - `EnemyShotInterval` (2): enemy ships wait twice as long between torpedo shots.
  - `EnemyAimCone` (0.33): enemy ships can only fire inside a narrower cone in front of them, so they must turn toward you first.
  - `EnemyFirstShotDelay` (5): seconds an enemy waits before its first torpedo after detecting you (vanilla fires at once). Applies again after each re-detection.
  - `EnemyDetectionTime` (5): seconds you must stay in an enemy's hearing range before it detects you (vanilla 4). Also delays its alarm and the mission's "detected" flag.

### Fixed

- Tube autoload now takes torpedoes only from the rack of the same torpedo room and loads only that room's tubes.

## [1.0.0] - 2026-11-06

First release. All settings live in `BepInEx\config\sonar.easymode.cfg`; values are changed in memory only, never in game files.

### Added

- **General**
  - `Enabled` (default `true`): master switch. While on, no times or PvP scores are uploaded to the leaderboards.
  - `OnlyWhenSolo` (default `true`): apply only while you are the only player; vanilla values come back when someone joins.
  - `VerboseLog` (default `false`): log every changed value.
- **Fan linking** - `FanLink` (default `ToggleFloor`): toggling one fan also toggles the others on the same floor (`ToggleFloor`) or on the whole sub (`ToggleAll`).
- **Tube autoload** - `TubeAutoLoad` (default `true`): loading a tube by hand also moves a torpedo of the same type from the rack into the next free tube. No torpedo is created or lost.
- **Crew tweaks**: `HungerDuration` (3), `StaminaDuration` (2.5), `WorkStaminaDrain` (0.6), `StaminaRecovery` (1.5), `MoveSpeed` (1.15), `PlayerDamage` (0.5).
- **Consumable tweaks**: `FuelDuration` (2.5), `Co2FilterDuration` (2), `SnorkelDuration` (1), `RefuelSpeed` (1).
- **Fault tweaks**: `FaultRisk` (0.1), `HitFaultChance` (0.5), `MaxFaultsPerType` (2), `FailuresToExplode` (25), `FireSpread` (0.5), `FloodSpeed` (0.6), `PumpSpeed` (2), `RepairSpeed` (1).
- **Stealth and combat tweaks**: `NoiseRadius` (1), `DepthNoise` (1), `DamageToEnemies` (1), `EnemyTorpedoSpeed` (1), `EnemyTorpedoTurnRate` (0.7), `AllyShipSpeed` (0.8), `EnemyShipSpeed` (0.8), `MineSlowdownRange` (1).
- **Open World tweaks** (experimental, probably no effect): `OpenWorldFuelUse`, `OpenWorldFilterUse`, `OpenWorldFoodUse` (1).

[1.2.0]: https://github.com/greendwin/sonar-easy-mode/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/greendwin/sonar-easy-mode/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/greendwin/sonar-easy-mode/releases/tag/v1.0.0
