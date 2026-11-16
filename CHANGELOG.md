# Changelog

All notable changes to SONAR Easy Mode are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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

[1.0.0]: https://github.com/greendwin/sonar-easy-mode/releases/tag/v1.0.0
