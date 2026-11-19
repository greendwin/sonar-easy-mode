using System;
using System.Collections.Generic;
using HarmonyLib;
using Indy;

namespace SonarEasyMode;

/// <summary>
/// Holding one bilge pump also runs the pumps of every other flooded room on the same floor.
/// A pump runs while its <c>PumpWater_*_pressed</c> flag is set (written from the pump station's held button). Each host step
/// clears the flag of any pump whose station nobody claims, then calls RemoveWaterAspiration for the flagged rooms.
/// The prefix sets the flags of the linked rooms, and while that step runs their stations report as claimed, so the game
/// keeps them and pumps, lights, sounds and syncs those rooms itself. Pumping is free (no noise, fuel or power) and water
/// is clamped at 0, so only flooded rooms are linked to keep dry pumps quiet; a room is released once it is dry.
/// Rooms without power are skipped (and released when they lose it), as the pump station itself requires power.
/// </summary>
internal static class PumpLink
{
    // Zones the host step pumps from a pressed flag; StairsUp has a flag but no RemoveWaterAspiration call there, so it is left out.
    private static readonly ZoneType[] Zones =
    {
        ZoneType.Bridge, ZoneType.UpperCorridor, ZoneType.OxygenFiltration, ZoneType.StairsDown, ZoneType.Infirmary,
        ZoneType.CrewQuarters, ZoneType.CommonRoom, ZoneType.Galley, ZoneType.LowerCorridor, ZoneType.RecoveryBay,
        ZoneType.EngineRoom, ZoneType.TorpedoPlatform, ZoneType.TorpedoRoom1, ZoneType.TorpedoRoom2,
    };

    /// <summary>Pump stations this mod keeps running; reported as claimed during the host step.</summary>
    private static readonly HashSet<StationType> Linked = new();

    internal static bool InStep;

    internal static bool Enabled => EasyModeDriver.Active && Settings.PumpLink.Value;

    internal static bool IsLinked(StationType station) => InStep && Linked.Contains(station);

    internal static void BeforeStep(SubmarineInside sub)
    {
        if (!Enabled)
        {
            Release(sub);
            return;
        }

        var claims = sub.game?.stationClaimManager;
        var topology = sub.subTopology ?? Engine.Instance?.subTopology;
        if (topology == null)
        {
            Patches.ReportOnce("pumps:notopology", "PumpLink: no submarine topology, does nothing");
            return;
        }

        // Sources: pumps a player is holding. Without a claim manager the game does not clear flags either,
        // so any pressed pump this mod did not set counts.
        var sources = new List<ZoneType>();
        foreach (var zone in Zones)
        {
            var station = UtilsProgression.GetPumpOutStationTypeForZoneType(zone);
            if (!sub.IsAbsorbing(zone)) continue;
            bool held = claims != null ? claims.IsStationClaimed(station) : !Linked.Contains(station);
            if (held) sources.Add(zone);
        }

        int added = 0;
        foreach (var zone in Zones)
        {
            var station = UtilsProgression.GetPumpOutStationTypeForZoneType(zone);
            if (sources.Contains(zone))
            {
                Linked.Remove(station);
                continue;
            }

            // Same power rule as StationPump: a pump works unless its room's energy is Off (Half still pumps).
            bool link = sub.HaveWaterInZone(zone) && sub.GetEnergyStatusForZone(zone) != EnergyStatus.Off
                        && sources.Exists(s => topology.IsInSameFloor(s, zone));
            if (link)
            {
                if (Linked.Add(station)) added++;
                SetPressed(sub, zone, true);
            }
            else if (Linked.Remove(station))
            {
                SetPressed(sub, zone, false);
            }
        }

        if (added > 0)
            Patches.ReportOnce("pumps", $"PumpLink: {string.Join(", ", sources)} also pumps {Linked.Count} flooded room(s) on the same floor");
    }

    /// <summary>Stops every pump this mod started (feature off, easy mode inactive).</summary>
    private static void Release(SubmarineInside sub)
    {
        if (Linked.Count == 0) return;
        foreach (var zone in Zones)
            if (Linked.Contains(UtilsProgression.GetPumpOutStationTypeForZoneType(zone)))
                SetPressed(sub, zone, false);
        Linked.Clear();
    }

    private static void SetPressed(SubmarineInside sub, ZoneType zone, bool on)
    {
        switch (zone)
        {
            case ZoneType.Bridge: sub.PumpWater_Bridge_pressed = on; break;
            case ZoneType.UpperCorridor: sub.PumpWater_UpperCorridor_pressed = on; break;
            case ZoneType.OxygenFiltration: sub.PumpWater_OxygenFiltration_pressed = on; break;
            case ZoneType.StairsDown: sub.PumpWater_StairsDown_pressed = on; break;
            case ZoneType.Infirmary: sub.PumpWater_Infirmary_pressed = on; break;
            case ZoneType.CrewQuarters: sub.PumpWater_CrewQuarters_pressed = on; break;
            case ZoneType.CommonRoom: sub.PumpWater_CommonRoom_pressed = on; break;
            case ZoneType.Galley: sub.PumpWater_Galley_pressed = on; break;
            case ZoneType.LowerCorridor: sub.PumpWater_LowerCorridor_pressed = on; break;
            case ZoneType.RecoveryBay: sub.PumpWater_RecoveryBay_pressed = on; break;
            case ZoneType.EngineRoom: sub.PumpWater_EngineRoom_pressed = on; break;
            case ZoneType.TorpedoPlatform: sub.PumpWater_TorpedoPlatform_pressed = on; break;
            case ZoneType.TorpedoRoom1: sub.PumpWater_TorpedoRoom1_pressed = on; break;
            case ZoneType.TorpedoRoom2: sub.PumpWater_TorpedoRoom2_pressed = on; break;
        }
    }
}

/// <summary>Host only: AdvanceHostSimulationStep runs on the host, so clients never link pumps.</summary>
[HarmonyPatch(typeof(SubmarineInside), nameof(SubmarineInside.AdvanceHostSimulationStep))]
internal static class PumpLinkStepPatch
{
    private static void Prefix(SubmarineInside __instance)
    {
        Patches.Guard("PumpLinkStepPatch", () => PumpLink.BeforeStep(__instance));
        PumpLink.InStep = true;
    }

    private static void Finalizer() => PumpLink.InStep = false;
}

/// <summary>Linked pump stations count as claimed during the host step, so the game does not clear their flags.</summary>
[HarmonyPatch(typeof(StationClaimManager), nameof(StationClaimManager.IsStationClaimed))]
internal static class PumpLinkClaimPatch
{
    private static void Postfix(StationType stationType, ref bool __result)
    {
        if (!__result && PumpLink.IsLinked(stationType)) __result = true;
    }
}
