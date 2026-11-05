using System;
using System.Collections.Generic;
using UnityEngine;

namespace SonarEasyMode;

/// <summary>
/// Scales Ship.speedMultiplier of allied (civilian) and enemy ships, the same knob the game's debug menu
/// uses (DebugMenu.SetCivilShipSpeedMultiplier / SetEnemyShipSpeedMultiplier). Runs from the driver tick:
/// polling instead of a spawn hook also catches ships whose multiplier the game changes later.
/// </summary>
public static class ShipSpeeds
{
    // ship pointer -> (value the game set, value we wrote)
    private static readonly Dictionary<IntPtr, (float Original, float Applied)> Ships = new();
    private static readonly HashSet<IntPtr> Seen = new();

    public static void Apply(bool active)
    {
        Patches.Guard("ShipSpeeds", () => ApplyAll(active));
    }

    private static void ApplyAll(bool active)
    {
        Seen.Clear();
        foreach (var ship in UnityEngine.Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
        {
            if (ship == null) continue;
            var type = ship.GetEntityType();
            Knob knob = DebugMenu.IsCivilShip(type) ? Settings.AllyShipSpeed
                : DebugMenu.IsEnemyShip(type) ? Settings.EnemyShipSpeed
                : null;
            if (knob == null) continue;

            var ptr = ship.Pointer;
            Seen.Add(ptr);
            float current = ship.speedMultiplier;
            // New ship, or the game set its own value since our last write: that value is the new baseline.
            if (!Ships.TryGetValue(ptr, out var entry) || !Mathf.Approximately(current, entry.Applied))
                entry = (current, current);

            float target = entry.Original * (active ? Settings.Value(knob) : 1f);
            if (!Mathf.Approximately(current, target))
            {
                ship.SetSpeedMultiplier(target);
                Patches.ReportOnce("shipspeed:" + type,
                    $"{knob.Key}: {type} speedMultiplier {entry.Original:0.###} -> {target:0.###} " +
                    $"(mission {ship.CivilianData?.speedMultiplier.ToString("0.###") ?? "-"}, " +
                    $"mine slowdown {ship.reduceSpeedIfMinesAhead}/{ship.reduceSpeedForMines:0.###})");
            }
            Ships[ptr] = (entry.Original, ship.speedMultiplier);
        }

        if (Ships.Count == Seen.Count) return;
        var gone = new List<IntPtr>();
        foreach (var ptr in Ships.Keys)
            if (!Seen.Contains(ptr)) gone.Add(ptr);
        foreach (var ptr in gone) Ships.Remove(ptr);
    }
}
