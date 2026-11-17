# SONAR Easy Mode

Makes solo runs of SONAR more forgiving. Everything is configurable. It never touches game files: values are changed in memory only.

## What it does (default settings)

- Crew: hunger lasts 3× longer and stamina 2.5× longer, you move a bit faster, and you take half damage from fire and explosions.
- Consumables: fuel lasts 2.5× longer and CO2 filters 2× longer.
- Faults: random leaks, fires and fuse breaks are rare. Fire spreads half as fast, floods are slower and pumps are twice as fast.
- Combat: enemy torpedoes turn more slowly, so they're easier to evade, and enemy and escorted ships move slower.
- Enemy fire: enemies take a bit longer to detect you, wait 5 seconds before their first shot, fire half as often and must turn toward you before they can fire.
- Auto tagging: while the sonar is on, ships inside its radius get tagged with their correct type, one every few seconds. Your own tags are kept.
- Panel remote: on the diagnostics panel, press the tab that is already open to toggle a silent alarm (STEALTH), switch both engines between 50% and 100% (ENGINES) or turn all fans off or on (TEMPERATURE). Host only.
- Fan linking: toggling one fan also toggles the others on the same floor, or on the whole sub.
- Tube autoload: loading a tube by hand with Shift held also moves a torpedo of the same type from the rack into the next free tube in the same torpedo room.

## Multiplayer friendly

By default it only applies while you're the only player. When someone joins, it switches back to vanilla values.

## Leaderboards

While the mod is enabled, nothing is uploaded to the leaderboards. Set Enabled = false to play ranked.

## Install

1. Install BepInEx 6 Bleeding Edge, IL2CPP x64, build be.791 or newer, and launch the game once.
2. Extract the zip into the game folder, next to SONAR.exe.
3. Optional: tweak BepInEx\config\sonar.easymode.cfg. It is created on first launch with the mod; edit it while the game is closed. Most settings are multipliers where 1 means vanilla.
