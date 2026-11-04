using System;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using Indy;
using UnityEngine;

namespace SonarEasyMode;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "sonar.easymode";
    public const string Name = "SONAR Easy Mode";
    public const string Version = "0.1.0";

    internal static new ManualLogSource Log;

    public override void Load()
    {
        Log = base.Log;
        Settings.Bind(Config);
        AddComponent<EasyModeDriver>();
        Log.LogInfo($"{Name} {Version} loaded. Preset={Settings.ActivePreset.Value}, OnlyWhenSolo={Settings.OnlyWhenSolo.Value}");
    }
}

/// <summary>
/// Polls the game's Engine singleton and keeps the config assets in the desired state
/// (modded while solo, vanilla otherwise).
/// </summary>
public class EasyModeDriver : MonoBehaviour
{
    private const float PollInterval = 1f;

    private float _nextPoll;
    private IntPtr _lastLevelingPtr;
    private bool? _lastActive;
    private int _lastVersion = -1;
    private int _lastPlayers = -1;

    public EasyModeDriver(IntPtr ptr) : base(ptr) { }

    private void Update()
    {
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + PollInterval;

        try
        {
            Tick();
        }
        catch (Exception e)
        {
            Plugin.Log.LogError($"Tick failed: {e}");
            _nextPoll = Time.unscaledTime + 10f;
        }
    }

    private void Tick()
    {
        var engine = Engine.Instance;
        if (engine == null) return;
        var leveling = engine.levelingConfig;
        if (leveling == null) return;

        int players = CountPlayers(engine);
        bool active = Settings.Enabled.Value
                      && Settings.ActivePreset.Value != Preset.Off
                      && (!Settings.OnlyWhenSolo.Value || players <= 1);

        bool assetChanged = leveling.Pointer != _lastLevelingPtr;
        if (!assetChanged && active == _lastActive && Settings.Version == _lastVersion) return;

        int changed = Tweaks.Apply(engine, active);
        if (assetChanged || active != _lastActive || players != _lastPlayers || changed > 0)
            Plugin.Log.LogInfo($"{(active ? "Easy mode ACTIVE" : "Vanilla values")} " +
                               $"(preset {Settings.ActivePreset.Value}, players {players}, {changed} fields changed)");

        _lastLevelingPtr = leveling.Pointer;
        _lastActive = active;
        _lastVersion = Settings.Version;
        _lastPlayers = players;
    }

    /// <summary>Players in the current session; 1 when there is no multiplayer session.</summary>
    private static int CountPlayers(Engine engine)
    {
        try
        {
            var multiplayer = engine.multiplayer;
            if (multiplayer == null) return 1;
            return Math.Max(1, multiplayer.GetPlayersCount());
        }
        catch
        {
            return 1;
        }
    }
}
