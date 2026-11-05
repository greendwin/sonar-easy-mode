using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace SonarEasyMode;

/// <summary>One tunable group: a multiplier (or absolute override) with its vanilla and default values.</summary>
public sealed class Knob
{
    public readonly string Key;
    public readonly float Vanilla;
    public readonly float Default;
    public ConfigEntry<float> Entry;

    public Knob(string key, float vanilla, float @default)
    {
        Key = key;
        Vanilla = vanilla;
        Default = @default;
    }
}

/// <summary>What toggling one fan does to the others.</summary>
public enum FanLinkMode
{
    /// <summary>Vanilla: each fan is switched on its own.</summary>
    Off,
    /// <summary>Every fan in the submarine follows the toggled one.</summary>
    ToggleAll,
    /// <summary>Only fans on the same floor as the toggled one follow it.</summary>
    ToggleFloor,
}

public static class Settings
{
    public static ConfigEntry<bool> Enabled;
    public static ConfigEntry<bool> OnlyWhenSolo;
    public static ConfigEntry<bool> VerboseLog;
    public static ConfigEntry<FanLinkMode> FanLink;

    // Defaults are the tuned solo values; 1 (or -1 for absolute knobs) = vanilla. "Duration" knobs: higher = lasts longer. "Chance/speed" knobs: as named.
    public static readonly Knob HungerDuration = new("HungerDuration", 1f, 3f);
    public static readonly Knob StaminaDuration = new("StaminaDuration", 1f, 2.5f);
    public static readonly Knob WorkStaminaDrain = new("WorkStaminaDrain", 1f, 0.6f);
    public static readonly Knob StaminaRecovery = new("StaminaRecovery", 1f, 1.5f);
    public static readonly Knob FuelDuration = new("FuelDuration", 1f, 2.5f);
    public static readonly Knob Co2FilterDuration = new("Co2FilterDuration", 1f, 2f);
    public static readonly Knob SnorkelDuration = new("SnorkelDuration", 1f, 1f);
    public static readonly Knob RefuelSpeed = new("RefuelSpeed", 1f, 1f);
    public static readonly Knob FaultRisk = new("FaultRisk", 1f, 0.1f);
    public static readonly Knob HitFaultChance = new("HitFaultChance", 1f, 0.5f);
    public static readonly Knob MaxFaultsPerType = new("MaxFaultsPerType", -1f, 2f);
    public static readonly Knob FailuresToExplode = new("FailuresToExplode", -1f, 25f);
    public static readonly Knob PlayerDamage = new("PlayerDamage", 1f, 0.5f);
    public static readonly Knob FireSpread = new("FireSpread", 1f, 0.5f);
    public static readonly Knob FloodSpeed = new("FloodSpeed", 1f, 0.6f);
    public static readonly Knob PumpSpeed = new("PumpSpeed", 1f, 2f);
    public static readonly Knob RepairSpeed = new("RepairSpeed", 1f, 1f);
    public static readonly Knob MoveSpeed = new("MoveSpeed", 1f, 1.15f);
    public static readonly Knob NoiseRadius = new("NoiseRadius", 1f, 1f);
    public static readonly Knob DepthNoise = new("DepthNoise", 1f, 1f);
    public static readonly Knob DamageToEnemies = new("DamageToEnemies", 1f, 1f);
    public static readonly Knob EnemyTorpedoSpeed = new("EnemyTorpedoSpeed", 1f, 1f);
    public static readonly Knob EnemyTorpedoTurnRate = new("EnemyTorpedoTurnRate", 1f, 0.7f);
    public static readonly Knob OpenWorldFuelUse = new("OpenWorldFuelUse", 1f, 1f);
    public static readonly Knob OpenWorldFilterUse = new("OpenWorldFilterUse", 1f, 1f);
    public static readonly Knob OpenWorldFoodUse = new("OpenWorldFoodUse", 1f, 1f);

    /// <summary>Bumped whenever any setting changes so the driver re-applies.</summary>
    public static int Version { get; private set; }

    public static void Bind(ConfigFile cfg)
    {
        Enabled = cfg.Bind("General", "Enabled", true, "Master switch.");
        OnlyWhenSolo = cfg.Bind("General", "OnlyWhenSolo", true,
            "Apply only while you are the only player in the session (vanilla values are restored when others join).");
        VerboseLog = cfg.Bind("General", "VerboseLog", false, "Log every field change.");
        FanLink = cfg.Bind("Features", "FanLink", FanLinkMode.ToggleFloor,
            "What toggling a fan does to the other fans. Off = vanilla. ToggleAll = every fan follows (one switch for silent running). " +
            "ToggleFloor = only fans on the same floor follow, so silent running needs one switch per floor. Works while easy mode is active.");

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
        BindKnob(cfg, NoiseRadius, "Radius at which enemies hear your submarine (multiplier on Engine.GetNoiseRadius). Lower = quieter.");
        BindKnob(cfg, DepthNoise, "Multiplier on the per-depth noise factors (MissionsConfig.noiseLow/Medium/HighMultiplier = 1.25 / 1.0 / 0.75 at 500 / 900 / 1500 m). Lower = quieter. Probably stacks with NoiseRadius, so presets leave it at 1.");
        BindKnob(cfg, DamageToEnemies, "Damage enemy vessels take from any source (multiplier). Torpedo 1 does 0.66 against enemy sub / frigate life 1.0, destroyer 1.2, carrier 2.0: vanilla 2/2/2/4 hits, x1.6 = 1/1/2/2, x2 = 1/1/1/2.");
        BindKnob(cfg, EnemyTorpedoSpeed, "Speed of torpedoes launched by enemies (multiplier). Your torpedoes are not affected.");
        BindKnob(cfg, EnemyTorpedoTurnRate, "Turn rate of torpedoes launched by enemies (multiplier); lower = easier to evade. Your torpedoes are not affected.");
        BindKnob(cfg, OpenWorldFuelUse, "EXPERIMENTAL, Open World mode only, probably no in-game effect. Scales fuelEveryDistanceUnit: the fraction of a fuel can used per map distance unit (= timeToMoveDistanceUnit / durationFuelSeconds). The only code found reading it is the developers' balancing simulator (OpenWorldLevelingSimulator), not the submarine. Lower = less fuel per distance (multiplier).");
        BindKnob(cfg, OpenWorldFilterUse, "EXPERIMENTAL, Open World mode only, probably no in-game effect. Scales filterEveryDistanceUnit: the fraction of a CO2 filter used per map distance unit (= timeToMoveDistanceUnit / durationFilterSeconds). Same caveat as OpenWorldFuelUse (multiplier).");
        BindKnob(cfg, OpenWorldFoodUse, "EXPERIMENTAL, Open World mode only, probably no in-game effect. Scales sandwitchEveryDistanceUnit: the sandwiches eaten per map distance unit (= timeToMoveDistanceUnit / durationSandwitchSeconds). Same caveat as OpenWorldFuelUse (multiplier).");

        cfg.SettingChanged += OnSettingChanged;
    }

    private static void OnSettingChanged(object sender, SettingChangedEventArgs e) => Version++;

    private static void BindKnob(ConfigFile cfg, Knob knob, string description)
    {
        knob.Entry = cfg.Bind("Tweaks", knob.Key, knob.Default,
            FormattableString.Invariant($"{description} Vanilla: {knob.Vanilla}."));
    }

    public static float Value(Knob knob) => knob.Entry.Value;
}
