using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace SonarEasyMode;

public enum Preset
{
    Off,
    Easy,
    VeryEasy,
    Custom,
}

/// <summary>One tunable group: a multiplier (or absolute override) with per-preset values.</summary>
public sealed class Knob
{
    public readonly string Key;
    public readonly float Easy;
    public readonly float VeryEasy;
    public readonly float Neutral;
    public ConfigEntry<float> Custom;

    public Knob(string key, float neutral, float easy, float veryEasy)
    {
        Key = key;
        Neutral = neutral;
        Easy = easy;
        VeryEasy = veryEasy;
    }
}

public static class Settings
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> OnlyWhenSolo;
    public static ConfigEntry<Preset> ActivePreset;
    public static ConfigEntry<bool> VerboseLog;

    // Multipliers: 1 = vanilla. "Duration" knobs: higher = lasts longer. "Chance/speed" knobs: as named.
    public static readonly Knob SurvivalDuration = new("SurvivalDuration", 1f, 2f, 3f);
    public static readonly Knob WorkStaminaDrain = new("WorkStaminaDrain", 1f, 0.6f, 0.4f);
    public static readonly Knob StaminaRecovery = new("StaminaRecovery", 1f, 1.5f, 2f);
    public static readonly Knob ConsumableDuration = new("ConsumableDuration", 1f, 2f, 3f);
    public static readonly Knob RefuelSpeed = new("RefuelSpeed", 1f, 1.5f, 2f);
    public static readonly Knob FaultRisk = new("FaultRisk", 1f, 0.5f, 0.25f);
    public static readonly Knob HitFaultChance = new("HitFaultChance", 1f, 0.6f, 0.35f);
    public static readonly Knob MaxFaultsPerType = new("MaxFaultsPerType", -1f, -1f, 1f);
    public static readonly Knob FailuresToExplode = new("FailuresToExplode", -1f, 25f, 35f);
    public static readonly Knob PlayerDamage = new("PlayerDamage", 1f, 0.6f, 0.4f);
    public static readonly Knob FireSpread = new("FireSpread", 1f, 0.6f, 0.4f);
    public static readonly Knob FloodSpeed = new("FloodSpeed", 1f, 0.6f, 0.4f);
    public static readonly Knob PumpSpeed = new("PumpSpeed", 1f, 1.5f, 2f);
    public static readonly Knob RepairSpeed = new("RepairSpeed", 1f, 1.5f, 2f);
    public static readonly Knob MoveSpeed = new("MoveSpeed", 1f, 1f, 1.15f);
    public static readonly Knob OpenWorldConsumption = new("OpenWorldConsumption", 1f, 0.6f, 0.4f);

    /// <summary>Bumped whenever any setting changes so the driver re-applies.</summary>
    public static int Version { get; private set; }

    public static void Bind(ConfigFile cfg)
    {
        Enabled = cfg.Bind("General", "Enabled", true, "Master switch.");
        OnlyWhenSolo = cfg.Bind("General", "OnlyWhenSolo", true,
            "Apply only while you are the only player in the session (vanilla values are restored when others join).");
        ActivePreset = cfg.Bind("General", "Preset", Preset.Easy,
            "Off = vanilla, Easy / VeryEasy = built-in values, Custom = use the [Custom] section below.");
        VerboseLog = cfg.Bind("General", "VerboseLog", false, "Log every field change.");

        BindKnob(cfg, SurvivalDuration, "Hunger and stamina bars last N times longer (hungerDurationSeconds, staminaDurationSeconds).");
        BindKnob(cfg, WorkStaminaDrain, "Extra stamina drain while running or doing heavy work (multiplier).");
        BindKnob(cfg, StaminaRecovery, "Stamina restored by sleeping and coffee (multiplier).");
        BindKnob(cfg, ConsumableDuration, "Engine fuel tank, CO2 filter and snorkel tank last N times longer.");
        BindKnob(cfg, RefuelSpeed, "Refueling speed (multiplier).");
        BindKnob(cfg, FaultRisk, "Random fault risk: leaks, fires, fuse breaks (multiplier).");
        BindKnob(cfg, HitFaultChance, "Chance that a hit causes fire/leak/fuse/engine stop (multiplier).");
        BindKnob(cfg, MaxFaultsPerType, "Max fires / leaks / fuse breaks per mission (absolute; -1 = keep vanilla 2).");
        BindKnob(cfg, FailuresToExplode, "Failures before the sub is lost (absolute; -1 = keep vanilla 20).");
        BindKnob(cfg, PlayerDamage, "Damage taken from fire and explosions (multiplier).");
        BindKnob(cfg, FireSpread, "Fire propagation probability (multiplier).");
        BindKnob(cfg, FloodSpeed, "Water ingress speed from leaks (multiplier).");
        BindKnob(cfg, PumpSpeed, "Pump water removal speed (multiplier).");
        BindKnob(cfg, RepairSpeed, "Welding, pipe repair and extinguishing speed (multiplier).");
        BindKnob(cfg, MoveSpeed, "Walk / run / crouch speed (multiplier).");
        BindKnob(cfg, OpenWorldConsumption, "EXPERIMENTAL: open-world fuel/filter/food use per distance unit (multiplier).");

        cfg.SettingChanged += OnSettingChanged;
    }

    private static void OnSettingChanged(object sender, SettingChangedEventArgs e) => Version++;

    private static void BindKnob(ConfigFile cfg, Knob knob, string description)
    {
        knob.Custom = cfg.Bind("Custom", knob.Key, knob.Easy,
            FormattableString.Invariant($"{description} Vanilla: {knob.Neutral}. Easy: {knob.Easy}. VeryEasy: {knob.VeryEasy}."));
    }

    public static float Value(Knob knob) => ActivePreset.Value switch
    {
        Preset.Easy => knob.Easy,
        Preset.VeryEasy => knob.VeryEasy,
        Preset.Custom => knob.Custom.Value,
        _ => knob.Neutral,
    };
}
