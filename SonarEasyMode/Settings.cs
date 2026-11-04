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
    public static readonly Knob HungerDuration = new("HungerDuration", 1f, 2f, 3f);
    public static readonly Knob StaminaDuration = new("StaminaDuration", 1f, 2f, 3f);
    public static readonly Knob WorkStaminaDrain = new("WorkStaminaDrain", 1f, 0.6f, 0.4f);
    public static readonly Knob StaminaRecovery = new("StaminaRecovery", 1f, 1.5f, 2f);
    public static readonly Knob FuelDuration = new("FuelDuration", 1f, 2f, 3f);
    public static readonly Knob Co2FilterDuration = new("Co2FilterDuration", 1f, 2f, 3f);
    public static readonly Knob SnorkelDuration = new("SnorkelDuration", 1f, 2f, 3f);
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
    public static readonly Knob OpenWorldFuelUse = new("OpenWorldFuelUse", 1f, 0.6f, 0.4f);
    public static readonly Knob OpenWorldFilterUse = new("OpenWorldFilterUse", 1f, 0.6f, 0.4f);
    public static readonly Knob OpenWorldFoodUse = new("OpenWorldFoodUse", 1f, 0.6f, 0.4f);

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

        BindKnob(cfg, HungerDuration, "Hunger bar lasts N times longer (hungerDurationSeconds).");
        BindKnob(cfg, StaminaDuration, "Stamina bar lasts N times longer (staminaDurationSeconds).");
        BindKnob(cfg, WorkStaminaDrain, "Extra stamina drain while running or doing heavy work (multiplier).");
        BindKnob(cfg, StaminaRecovery, "Stamina restored by sleeping and coffee (multiplier).");
        BindKnob(cfg, FuelDuration, "Engine fuel tank lasts N times longer (engineFuelDurationSeconds).");
        BindKnob(cfg, Co2FilterDuration, "CO2 filter lasts N times longer (co2FilterDurationSeconds).");
        BindKnob(cfg, SnorkelDuration, "Snorkel tank lasts N times longer (snorkelTankDuration).");
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
        BindKnob(cfg, OpenWorldFuelUse, "EXPERIMENTAL, Open World mode only, probably no in-game effect. Scales fuelEveryDistanceUnit: the fraction of a fuel can used per map distance unit (= timeToMoveDistanceUnit / durationFuelSeconds). The only code found reading it is the developers' balancing simulator (OpenWorldLevelingSimulator), not the submarine. Lower = less fuel per distance (multiplier).");
        BindKnob(cfg, OpenWorldFilterUse, "EXPERIMENTAL, Open World mode only, probably no in-game effect. Scales filterEveryDistanceUnit: the fraction of a CO2 filter used per map distance unit (= timeToMoveDistanceUnit / durationFilterSeconds). Same caveat as OpenWorldFuelUse (multiplier).");
        BindKnob(cfg, OpenWorldFoodUse, "EXPERIMENTAL, Open World mode only, probably no in-game effect. Scales sandwitchEveryDistanceUnit: the sandwiches eaten per map distance unit (= timeToMoveDistanceUnit / durationSandwitchSeconds). Same caveat as OpenWorldFuelUse (multiplier).");

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
