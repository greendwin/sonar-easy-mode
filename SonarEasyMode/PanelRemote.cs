using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Indy;
using UnityEngine;

namespace SonarEasyMode;

/// <summary>
/// Diagnostics panel remote: pressing the tab that is already open runs an action (silent alarm, engine power,
/// all fans), and the tab title shows the resulting state, e.g. "ENGINES - 120%".
/// </summary>
internal static class PanelRemote
{
    // Tab title suffixes are cached per tab: (title the game set, title we wrote).
    private static readonly Dictionary<IntPtr, (string Base, string Written)> Titles = new();
    private static FanController[] _fans;
    private static float _fansAt;

    /// <summary>Silent alarm toggled from the STEALTH tab; kept for the whole session.</summary>
    internal static bool Silent;

    internal static bool Enabled => EasyModeDriver.Active && Settings.PanelRemote.Value;

    internal static bool SilentActive => Silent && Enabled;

    /// <summary>Second press on the open tab. Host only: engine and fan state are host-authoritative.</summary>
    internal static void Press(SubmarineInside sub, DiagnosticsStationType tab)
    {
        if (!IsHost()) return;
        switch (tab)
        {
            case DiagnosticsStationType.Diagnostics_Stealth:
                Silent = !Silent;
                Plugin.Log.LogInfo($"PanelRemote: silent alarm {(Silent ? "on" : "off")}");
                break;
            case DiagnosticsStationType.Diagnostics_Engines:
                CycleEngines(sub);
                break;
            case DiagnosticsStationType.Diagnostics_Temperature:
                ToggleFans();
                break;
        }
    }

    // Vanilla lever order is 50 / 100 / 120; the remote goes 100 -> 50 -> 120 -> 100.
    private static void CycleEngines(SubmarineInside sub)
    {
        var next = sub.Engine1Power switch
        {
            EnginePower.Normal => EnginePower.SilentRunning,
            EnginePower.SilentRunning => EnginePower.ForcingEngines,
            _ => EnginePower.Normal,
        };
        // StationEngine.SetEnginePower only writes these fields; the levers are moved to match below.
        sub.Engine1Power = next;
        sub.Engine2Power = next;
        foreach (var station in UnityEngine.Object.FindObjectsByType<StationEngine>(FindObjectsSortMode.None))
            if (station != null) station.SyncWithAutoritativeState();
        Plugin.Log.LogInfo($"PanelRemote: engines {PowerLabel(next)}");
    }

    /// <summary>Any fan on: all off. All off: all on. Partly-on fans are not toggled one by one.</summary>
    private static void ToggleFans()
    {
        _fans = null;
        var fans = UsableFans();
        bool on = !fans.Any(f => f.IsOn());
        int changed = 0;
        FanLink.Busy = true;
        try
        {
            foreach (var fan in fans)
            {
                if (fan.IsOn() == on) continue;
                fan.SetStatus(on, true);
                changed++;
            }
        }
        finally
        {
            FanLink.Busy = false;
        }
        Plugin.Log.LogInfo($"PanelRemote: {changed} fan(s) switched {(on ? "on" : "off")}");
    }

    internal static void UpdateTitle(SubmarineOSDiagnostics_TabBase tab)
    {
        var label = tab.titleLabel;
        if (label == null) return;
        string text = label.text;
        Titles.TryGetValue(tab.Pointer, out var cached);
        // Anything other than our own last write is a fresh title from the game (e.g. a language change).
        string baseTitle = cached.Written != null && text == cached.Written ? cached.Base : text;
        string suffix = Enabled ? Suffix(tab) : null;
        string want = suffix == null ? baseTitle : $"{baseTitle} - {suffix}";
        if (want != text) label.text = want;
        Titles[tab.Pointer] = (baseTitle, want);
    }

    private static string Suffix(SubmarineOSDiagnostics_TabBase tab)
    {
        var sub = tab.submarineInside;
        switch (tab.diagnosticsTabType)
        {
            case DiagnosticsTabType.Diagnostics_Stealth:
                return Silent ? "SILENT" : null;
            case DiagnosticsTabType.Diagnostics_Engines:
                if (sub == null) return null;
                string e1 = sub.Engine1On ? PowerLabel(sub.Engine1Power) : "OFF";
                string e2 = sub.Engine2On ? PowerLabel(sub.Engine2Power) : "OFF";
                return e1 == e2 ? e1 : $"{e1} / {e2}";
            case DiagnosticsTabType.Diagnostics_Temperature:
                var fans = UsableFans();
                if (fans.Length == 0) return null;
                int on = fans.Count(f => f.IsOn());
                return on == 0 ? "FANS OFF" : on == fans.Length ? "FANS ON" : $"FANS {on}/{fans.Length}";
            default:
                return null;
        }
    }

    // Same labels as the game's ENGINE_POWER_0..2 texts.
    private static string PowerLabel(EnginePower power) => power switch
    {
        EnginePower.SilentRunning => "50%",
        EnginePower.ForcingEngines => "120%",
        _ => "100%",
    };

    /// <summary>Installed fans (extra fans stay upgradeBlocked until bought); the scene lookup is cached for 2 s.</summary>
    private static FanController[] UsableFans()
    {
        if (_fans == null || _fans.Any(f => f == null) || Time.unscaledTime - _fansAt > 2f)
        {
            _fans = UnityEngine.Object.FindObjectsByType<FanController>(FindObjectsSortMode.None)
                .Where(f => f != null && !f.upgradeBlocked).ToArray();
            _fansAt = Time.unscaledTime;
        }
        return _fans;
    }

    private static bool IsHost()
    {
        var mp = Engine.Instance?.multiplayer;
        return mp == null || mp.IsOfflineSession || mp.IsLocalPlayerHost();
    }
}

/// <summary>
/// A tab button press calls SetDiagnosticsStationType once (InstrumentButton.OnClickDown); the field still holds
/// the old tab here, so an equal value means the open tab was pressed again.
/// </summary>
[HarmonyPatch(typeof(SubmarineInside), nameof(SubmarineInside.SetDiagnosticsStationType))]
internal static class DiagnosticsTabPressPatch
{
    private static void Prefix(SubmarineInside __instance, DiagnosticsStationType diagnosticsStationType)
    {
        if (!PanelRemote.Enabled || __instance.diagnosticsStationType != diagnosticsStationType) return;
        Patches.Guard("DiagnosticsTabPressPatch", () => PanelRemote.Press(__instance, diagnosticsStationType));
    }
}

/// <summary>
/// The host step computes noise_made_alarm = diagnosticsAlarmOn ? 1 : 0 (weight 0.1). With the silent alarm on,
/// the flag is cleared for that step only, so lights and panel still see the alarm.
/// </summary>
[HarmonyPatch(typeof(SubmarineInside), nameof(SubmarineInside.AdvanceHostSimulationStep))]
internal static class SilentAlarmNoisePatch
{
    private static void Prefix(SubmarineInside __instance, out bool __state)
    {
        __state = PanelRemote.SilentActive && __instance.diagnosticsAlarmOn;
        if (!__state) return;
        __instance.diagnosticsAlarmOn = false;
        Patches.ReportOnce("silentalarm", "PanelRemote: alarm noise suppressed");
    }

    private static void Postfix(SubmarineInside __instance, bool __state)
    {
        if (__state) __instance.diagnosticsAlarmOn = true;
    }
}

/// <summary>
/// Speaker.UnityUpdate fades the alarm loop from its current volume toward a target each frame. The game's own
/// (unscaled) volume is put back before its update and scaled after it, so the fade stays vanilla and only what
/// is heard is quieter.
/// </summary>
[HarmonyPatch(typeof(Speaker), nameof(Speaker.UnityUpdate))]
internal static class SilentAlarmSoundPatch
{
    private static readonly Dictionary<IntPtr, float> GameVolume = new();

    private static void Prefix(Speaker __instance)
    {
        if (!GameVolume.TryGetValue(__instance.Pointer, out float volume)) return;
        var source = __instance.audioSourceAlarm;
        if (source != null) source.volume = volume;
        if (!PanelRemote.SilentActive) GameVolume.Remove(__instance.Pointer);
    }

    private static void Postfix(Speaker __instance)
    {
        if (!PanelRemote.SilentActive) return;
        var source = __instance.audioSourceAlarm;
        if (source == null) return;
        GameVolume[__instance.Pointer] = source.volume;
        source.volume *= Mathf.Clamp01(Settings.SilentAlarmVolume.Value);
    }
}

[HarmonyPatch(typeof(SubmarineOSDiagnostics_TabBase), nameof(SubmarineOSDiagnostics_TabBase.UnityUpdate))]
internal static class DiagnosticsTabTitlePatch
{
    private static void Postfix(SubmarineOSDiagnostics_TabBase __instance, bool isTabVisible)
    {
        if (!isTabVisible) return;
        Patches.Guard("DiagnosticsTabTitlePatch", () => PanelRemote.UpdateTitle(__instance));
    }
}
