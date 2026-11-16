# SONAR Easy Mode

Makes solo runs of SONAR more forgiving. Everything is configurable. It never touches game files: values are changed in memory only.

## What it does (default settings)

- Crew: hunger lasts 3× longer and stamina 2.5× longer, you move a bit faster, and you take half damage from fire and explosions.
- Consumables: fuel lasts 2.5× longer and CO2 filters 2× longer.
- Faults: random leaks, fires and fuse breaks are rare. Fire spreads half as fast, floods are slower and pumps are twice as fast.
- Combat: enemy torpedoes turn more slowly, so they're easier to evade, and enemy and escorted ships move slower.
- Fan linking: toggling one fan also toggles the others on the same floor, or on the whole sub.
- Tube autoload: loading a tube by hand also moves a torpedo of the same type from the rack into the next free tube.
Multiplayer friendly
By default it only applies while you're the only player. When someone joins, it switches back to vanilla values.
Leaderboards
While the mod is enabled, nothing is uploaded to the leaderboards. Set Enabled = false to play ranked.

## Install

1. Install BepInEx 6 Bleeding Edge, IL2CPP x64, build be.791 or newer, and launch the game once.
2. Extract the zip into the game folder, next to SONAR.exe.
3. Optional: tweak BepInEx\config\sonar.easymode.cfg. It is cr Jump to bottom (ctrl+End) ↓ ery setting is a multiplier where 1 means vanilla.