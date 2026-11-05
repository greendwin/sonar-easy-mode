using System;
using System.Collections.Generic;
using HarmonyLib;
using Indy;
using UnityEngine;

namespace SonarEasyMode;

/// <summary>Harmony patches for behaviour that is not a plain config value. All are no-ops unless easy mode is active.</summary>
public static class Patches
{
    private static readonly HashSet<string> Reported = new();

    public static void Apply(string harmonyId)
    {
        var harmony = new Harmony(harmonyId);
        // Patch class by class so one missing method (game update) does not disable the rest.
        foreach (var type in new[] { typeof(NoiseRadiusPatch), typeof(DamageShipPatch), typeof(EnemyTorpedoPatch),
                     typeof(FanTogglePatch), typeof(FansTogglePatch) })
        {
            try
            {
                harmony.CreateClassProcessor(type).Patch();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Patch {type.Name} failed (game update?): {e.Message}");
            }
        }
    }

    /// <summary>Logs the first hit of each patch, so it is visible in the log that a hook works.</summary>
    internal static void ReportOnce(string key, string message)
    {
        if (Reported.Add(key) || Settings.VerboseLog.Value)
            Plugin.Log.LogInfo(message);
    }

    internal static void Guard(string name, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            if (Reported.Add("error:" + name))
                Plugin.Log.LogError($"{name} failed: {e}");
        }
    }
}

[HarmonyPatch(typeof(Engine), nameof(Engine.GetNoiseRadius))]
internal static class NoiseRadiusPatch
{
    private static void Postfix(ref float __result)
    {
        if (!EasyModeDriver.Active) return;
        float k = Settings.Value(Settings.NoiseRadius);
        if (k == 1f) return;
        Patches.ReportOnce("noise", $"NoiseRadius: {__result:0.##} -> {__result * k:0.##}");
        __result *= k;
    }
}

[HarmonyPatch(typeof(Ship), nameof(Ship.DamageShip))]
internal static class DamageShipPatch
{
    private static void Prefix(Ship __instance, ref float damage)
    {
        if (!EasyModeDriver.Active) return;
        float k = Settings.Value(Settings.DamageToEnemies);
        if (k == 1f) return;
        float before = damage;
        bool enemy = false;
        Patches.Guard("DamageShipPatch", () => enemy = __instance.IsEnemy());
        if (!enemy) return;
        damage *= k;
        Patches.ReportOnce("damage", $"DamageToEnemies: {__instance.GetEntityType()} takes {before:0.###} -> {damage:0.###} " +
                                     $"(life {__instance.GetShipLife():0.###}/{__instance.GetShipMaxLife():0.###})");
    }
}

/// <summary>
/// Torpedoes share one WeaponConfig per type between player and enemies, so an enemy torpedo gets
/// a scaled clone of its config at launch instead of the shared asset being edited.
/// </summary>
[HarmonyPatch(typeof(Torpedo), nameof(Torpedo.LaunchWeapon))]
internal static class EnemyTorpedoPatch
{
    // original config pointer -> (settings version, clone)
    private static readonly Dictionary<IntPtr, (int Version, WeaponConfig Clone)> Clones = new();

    private static void Prefix(Torpedo __instance)
    {
        if (!EasyModeDriver.Active) return;
        Patches.Guard("EnemyTorpedoPatch", () => Swap(__instance));
    }

    private static void Swap(Torpedo torpedo)
    {
        float speed = Settings.Value(Settings.EnemyTorpedoSpeed);
        float turn = Settings.Value(Settings.EnemyTorpedoTurnRate);
        if (speed == 1f && turn == 1f) return;

        var ship = torpedo.launchingShip;
        bool enemy = ship != null ? ship.IsEnemy() : !torpedo.GetWeaponIsFriend();
        if (!enemy) return;

        var original = torpedo.weaponConfig;
        if (original == null) return;
        if (IsClone(original)) return;

        if (!Clones.TryGetValue(original.Pointer, out var entry) || entry.Version != Settings.Version || entry.Clone == null)
        {
            var clone = UnityEngine.Object.Instantiate(original);
            clone.name = original.name + " (SonarEasyMode)";
            clone.hideFlags = HideFlags.DontUnloadUnusedAsset;
            clone.maxSpeed = original.maxSpeed * speed;
            clone.rotationSpeed = original.rotationSpeed * turn;
            entry = (Settings.Version, clone);
            Clones[original.Pointer] = entry;
        }

        torpedo.weaponConfig = entry.Clone;
        Patches.ReportOnce("torpedo:" + original.name,
            $"EnemyTorpedo: {original.name} speed {original.maxSpeed:0.##} -> {entry.Clone.maxSpeed:0.##}, " +
            $"turn {original.rotationSpeed:0.##} -> {entry.Clone.rotationSpeed:0.##}");
    }

    private static bool IsClone(WeaponConfig cfg)
    {
        foreach (var e in Clones.Values)
            if (e.Clone != null && e.Clone.Pointer == cfg.Pointer) return true;
        return false;
    }
}

/// <summary>Toggling one fan sets other fans (all of them, or those on its floor) to the same state.</summary>
internal static class FanLink
{
    internal static bool Busy;

    internal static bool Enabled => EasyModeDriver.Active && Settings.FanLink.Value != FanLinkMode.Off && !Busy;

    internal static void SyncAllTo(bool on, FanController source)
    {
        Busy = true;
        try
        {
            var mode = Settings.FanLink.Value;
            // Floors come from the game's own zone topology (upper / lower floor zone lists).
            var topology = mode == FanLinkMode.ToggleFloor ? source?.submarine?.subTopology ?? Engine.Instance?.subTopology : null;
            if (mode == FanLinkMode.ToggleFloor && (source == null || topology == null))
            {
                Patches.ReportOnce("fans:notopology", "FanLink: no submarine topology, ToggleFloor does nothing");
                return;
            }

            int changed = 0;
            var all = UnityEngine.Object.FindObjectsByType<FanController>(FindObjectsSortMode.None);
            foreach (var fan in all)
            {
                if (fan == null || (source != null && fan.Pointer == source.Pointer)) continue;
                if (fan.upgradeBlocked || fan.IsOn() == on) continue;
                if (topology != null && !topology.IsInSameFloor(source.zoneType, fan.zoneType)) continue;
                fan.SetStatus(on, true);
                changed++;
            }
            Patches.ReportOnce("fans", $"FanLink ({mode}): switched {changed} other fan(s) {(on ? "on" : "off")}");
        }
        finally
        {
            Busy = false;
        }
    }
}

[HarmonyPatch(typeof(FanController), nameof(FanController.ToggleStatus))]
internal static class FanTogglePatch
{
    private static void Postfix(FanController __instance)
    {
        if (!FanLink.Enabled) return;
        Patches.Guard("FanTogglePatch", () => FanLink.SyncAllTo(__instance.IsOn(), __instance));
    }
}

/// <summary>Same as <see cref="FanTogglePatch"/> for toggles that go through the Fans group by index.</summary>
[HarmonyPatch(typeof(Fans), nameof(Fans.ToggleFanStatus))]
internal static class FansTogglePatch
{
    private static void Prefix(Fans __instance, out bool[] __state)
    {
        __state = null;
        if (!FanLink.Enabled) return;
        var states = Snapshot(__instance);
        __state = states;
        // The inner FanController.ToggleStatus (if any) must not sync on its own; this postfix does.
        FanLink.Busy = true;
    }

    private static void Postfix(Fans __instance, bool[] __state)
    {
        if (__state == null) return;
        FanLink.Busy = false;
        Patches.Guard("FansTogglePatch", () =>
        {
            var fans = FanArray(__instance);
            for (int i = 0; i < fans.Length; i++)
            {
                if (fans[i] == null || fans[i].IsOn() == __state[i]) continue;
                FanLink.SyncAllTo(fans[i].IsOn(), fans[i]);
                return;
            }
        });
    }

    private static FanController[] FanArray(Fans f) =>
        new[] { f.Fan1, f.Fan2, f.Fan3, f.Fan4, f.Fan5, f.Fan6 };

    private static bool[] Snapshot(Fans f)
    {
        var fans = FanArray(f);
        var states = new bool[fans.Length];
        for (int i = 0; i < fans.Length; i++)
            states[i] = fans[i] != null && fans[i].IsOn();
        return states;
    }
}
