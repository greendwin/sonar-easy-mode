using HarmonyLib;
using Indy;

namespace SonarEasyMode;

/// <summary>
/// Fills the snorkel air tank by itself while the submarine floats on the surface.
/// The host step fills AirLevelFromAirTank (ushort, 65535 = full) only while the snorkel lever is held:
/// SnorkelAvailable (periscope depth) &amp;&amp; SnorkelLevel &gt; 0 &amp;&amp; OxygenFiltration has power, adding
/// SnorkelLevel * 65535 / timeToLoadSnorkelTime per second. On the surface (OnSurface = IsFloatingOverWater) the tank
/// does not drain, but does not fill either. The postfix adds the full-lever rate on the surface, minus whatever the
/// lever already added this step, under the same power rule. Below the surface (periscope depth included) nothing changes.
/// </summary>
internal static class SnorkelAutoFill
{
    private const float Full = 65535f;

    internal static void AfterStep(SubmarineInside sub, float dt)
    {
        if (!EasyModeDriver.Active || !Settings.SnorkelAutoFill.Value) return;
        // The tutorial teaches the snorkel lever.
        if (!sub.OnSurface || Engine.gameMode == GameMode.Tutorial) return;
        if (sub.AirLevelFromAirTank == ushort.MaxValue) return;
        // A submarine without a snorkel station (open world upgrade) has nothing to fill the tank with.
        if (sub.game?.stationManager?.stationSnorkel == null) return;
        if (sub.GetEnergyStatusForZone(ZoneType.OxygenFiltration) == EnergyStatus.Off) return;

        var leveling = sub.levelingConfig;
        if (leveling == null || leveling.timeToLoadSnorkelTime <= 0f) return;

        // The game already added SnorkelLevel's share if a player holds the lever at periscope depth.
        float lever = sub.SnorkelAvailable ? UnityEngine.Mathf.Clamp01(sub.SnorkelLevel) : 0f;
        int add = (int)(Full / leveling.timeToLoadSnorkelTime * dt * (1f - lever));
        if (add <= 0) return;
        sub.AirLevelFromAirTank = (ushort)System.Math.Min(ushort.MaxValue, sub.AirLevelFromAirTank + add);
        Patches.ReportOnce("snorkel", "SnorkelAutoFill: on the surface, filling the air tank");
    }
}

/// <summary>Host only: AdvanceHostSimulationStep runs on the host, and AirLevelFromAirTank is synced to clients.</summary>
[HarmonyPatch(typeof(SubmarineInside), nameof(SubmarineInside.AdvanceHostSimulationStep))]
internal static class SnorkelAutoFillPatch
{
    private static void Postfix(SubmarineInside __instance, float deltaTime) =>
        Patches.Guard("SnorkelAutoFillPatch", () => SnorkelAutoFill.AfterStep(__instance, deltaTime));
}
