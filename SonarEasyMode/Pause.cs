using HarmonyLib;
using Indy;
using UnityEngine;

namespace SonarEasyMode;

/// <summary>
/// Freezes the game (Time.timeScale = 0) while the Esc menu is open and you are the host and the only player,
/// offline or in an online lobby. The host simulation (SubmarineInside.AdvanceHostSimulationStep, ship and torpedo
/// movement) runs on scaled time, while the network layer (transport, heartbeats, lobby keep-alive) runs on
/// realtimeSinceStartup, so an online lobby stays alive while paused. AppWindowPauseGame.Close already sets
/// timeScale back to 1.
/// </summary>
internal static class Pause
{
    private static AppWindowPauseGame _menu;
    // A peer connected while this menu was open: stay unpaused until the menu is opened again.
    private static bool _peerJoined;

    internal static bool Paused { get; private set; }

    internal static void Opened(AppWindowPauseGame menu)
    {
        _menu = menu;
        _peerJoined = false;
        Tick();
    }

    /// <summary>
    /// A hot joiner gets an immediate full state refresh (Submarine.UnityUpdate forces one host step and send), but
    /// with the host frozen no further state would follow, so the game resumes at once.
    /// </summary>
    internal static void PeerConnected()
    {
        _peerJoined = true;
        Tick();
    }

    /// <summary>
    /// Called every frame from the driver's LateUpdate: TorpedoLaunchSystem.UnityUpdate re-applies its slow motion
    /// each frame, so the 0 is set again after it. On resume that slow motion comes back the same way.
    /// </summary>
    internal static void Tick()
    {
        bool pause = _menu != null && _menu.IsOpened() && !_peerJoined && Allowed();
        if (pause)
        {
            if (!Paused) Plugin.Log.LogInfo("Pause: game paused");
            Paused = true;
            Time.timeScale = 0f;
        }
        else if (Paused)
        {
            Paused = false;
            Time.timeScale = 1f;
            Plugin.Log.LogInfo(_peerJoined ? "Pause: a player joined, game resumed" : "Pause: game resumed");
        }
    }

    private static bool Allowed()
    {
        if (!EasyModeDriver.Active || !Settings.PauseInMenu.Value) return false;
        var mp = Engine.Instance?.multiplayer;
        return mp == null || mp.IsUsingDummyStack || (mp.IsLocalPlayerHost() && mp.GetPlayersCount() <= 1);
    }
}

[HarmonyPatch(typeof(AppWindowPauseGame), nameof(AppWindowPauseGame.OpenCore))]
internal static class PauseMenuOpenPatch
{
    // The open flag may be set by AppWindow after OpenCore returns; the driver's LateUpdate picks it up then.
    private static void Postfix(AppWindowPauseGame __instance)
    {
        Patches.Guard("PauseMenuOpenPatch", () => Pause.Opened(__instance));
    }
}

/// <summary>Raised by the transport's OnPeerConnected event on every machine, for each newly connected peer.</summary>
[HarmonyPatch(typeof(Multiplayer), nameof(Multiplayer.OnPeerConnectedTimeSync))]
internal static class PausePeerConnectedPatch
{
    private static void Prefix()
    {
        if (Pause.Paused) Patches.Guard("PausePeerConnectedPatch", Pause.PeerConnected);
    }
}
