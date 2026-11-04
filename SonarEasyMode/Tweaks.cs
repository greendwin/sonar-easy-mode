using System;
using System.Collections.Generic;
using System.Reflection;
using Il2CppInterop.Runtime.InteropTypes;
using Indy;

namespace SonarEasyMode;

public enum ConfigAsset
{
    Leveling,
    Game,
    OpenWorld,
}

public enum TweakMode
{
    /// <summary>field = original * knob</summary>
    Multiply,
    /// <summary>field = knob (when knob &gt;= 0)</summary>
    Override,
}

public sealed class Tweak
{
    public readonly ConfigAsset Asset;
    public readonly string Field;
    public readonly Knob Knob;
    public readonly TweakMode Mode;

    public Tweak(ConfigAsset asset, string field, Knob knob, TweakMode mode = TweakMode.Multiply)
    {
        Asset = asset;
        Field = field;
        Knob = knob;
        Mode = mode;
    }
}

/// <summary>Applies / restores field values on the game's config ScriptableObjects.</summary>
public static class Tweaks
{
    public static readonly Tweak[] All =
    {
        // Survival (Indy.LevelingConfig)
        new(ConfigAsset.Leveling, "hungerDurationSeconds", Settings.SurvivalDuration),
        new(ConfigAsset.Leveling, "staminaDurationSeconds", Settings.SurvivalDuration),
        new(ConfigAsset.Leveling, "staminaReductionRunningOrHeavyWork", Settings.WorkStaminaDrain),
        new(ConfigAsset.Leveling, "addStaminaSleepingSpeed", Settings.StaminaRecovery),
        new(ConfigAsset.Leveling, "addStaminaPerCoffe", Settings.StaminaRecovery),

        // Consumables
        new(ConfigAsset.Leveling, "engineFuelDurationSeconds", Settings.ConsumableDuration),
        new(ConfigAsset.Leveling, "co2FilterDurationSeconds", Settings.ConsumableDuration),
        new(ConfigAsset.Leveling, "snorkelTankDuration", Settings.ConsumableDuration),
        new(ConfigAsset.Leveling, "refuelAmount", Settings.RefuelSpeed),

        // Random faults
        new(ConfigAsset.Leveling, "riskOfFailureBaseLeak", Settings.FaultRisk),
        new(ConfigAsset.Leveling, "riskOfFailureBaseFireAndFuse", Settings.FaultRisk),
        new(ConfigAsset.Leveling, "riskOfFireByMedTemperature", Settings.FaultRisk),
        new(ConfigAsset.Leveling, "riskOfFuseBreakByMedTemperature", Settings.FaultRisk),
        new(ConfigAsset.Leveling, "riskOfFireByMedFuses", Settings.FaultRisk),
        new(ConfigAsset.Leveling, "riskOfLeakByDepthMed", Settings.FaultRisk),

        // Faults caused by hits
        new(ConfigAsset.Leveling, "damageFireProbability", Settings.HitFaultChance),
        new(ConfigAsset.Leveling, "damageFuseProbability", Settings.HitFaultChance),
        new(ConfigAsset.Leveling, "damageLeakProbability", Settings.HitFaultChance),
        new(ConfigAsset.Leveling, "damageEngineStopProbabilityHigh", Settings.HitFaultChance),
        new(ConfigAsset.Leveling, "damageEngineStopProbabilityMed", Settings.HitFaultChance),
        new(ConfigAsset.Leveling, "damageEngineStopProbabilityLow", Settings.HitFaultChance),

        // Caps
        new(ConfigAsset.Leveling, "nFiresMaxPerGame", Settings.MaxFaultsPerType, TweakMode.Override),
        new(ConfigAsset.Leveling, "nLeaksMaxPerGame", Settings.MaxFaultsPerType, TweakMode.Override),
        new(ConfigAsset.Leveling, "nFuseBreaksMaxPerGame", Settings.MaxFaultsPerType, TweakMode.Override),
        new(ConfigAsset.Leveling, "amountToFailuresToExplode", Settings.FailuresToExplode, TweakMode.Override),

        // Player damage
        new(ConfigAsset.Leveling, "damageFireMin", Settings.PlayerDamage),
        new(ConfigAsset.Leveling, "damageFireMax", Settings.PlayerDamage),
        new(ConfigAsset.Leveling, "damageExplosionMin", Settings.PlayerDamage),
        new(ConfigAsset.Leveling, "damageExplosionMax", Settings.PlayerDamage),

        // Fire / water / repair
        new(ConfigAsset.Leveling, "propagationProbability", Settings.FireSpread),
        new(ConfigAsset.Leveling, "waterLeakAdd", Settings.FloodSpeed),
        new(ConfigAsset.Leveling, "waterAspirationRemove", Settings.PumpSpeed),
        new(ConfigAsset.Leveling, "hullLeakFixAmount", Settings.RepairSpeed),
        new(ConfigAsset.Leveling, "pipeLeakFixAmount", Settings.RepairSpeed),
        new(ConfigAsset.Leveling, "extinFixAmount", Settings.RepairSpeed),

        // Movement (Indy.GameConfig)
        new(ConfigAsset.Game, "walkSpeed", Settings.MoveSpeed),
        new(ConfigAsset.Game, "runSpeed", Settings.MoveSpeed),
        new(ConfigAsset.Game, "crouchSpeed", Settings.MoveSpeed),
        new(ConfigAsset.Game, "tiredInjuredWalkSpeed", Settings.MoveSpeed),
        new(ConfigAsset.Game, "tiredInjuredWalkSpeedCrouch", Settings.MoveSpeed),

        // Open world (Indy.OpenWorldLevelingConfig)
        new(ConfigAsset.OpenWorld, "fuelEveryDistanceUnit", Settings.OpenWorldConsumption),
        new(ConfigAsset.OpenWorld, "filterEveryDistanceUnit", Settings.OpenWorldConsumption),
        new(ConfigAsset.OpenWorld, "sandwitchEveryDistanceUnit", Settings.OpenWorldConsumption),
    };

    // Original values keyed by (native object pointer, field) so a reloaded asset is handled separately.
    private static readonly Dictionary<(IntPtr, string), object> Originals = new();
    private static readonly Dictionary<(Type, string), PropertyInfo> PropCache = new();

    public static Il2CppObjectBase Resolve(Engine engine, ConfigAsset asset) => asset switch
    {
        ConfigAsset.Leveling => engine.levelingConfig,
        ConfigAsset.Game => engine.gameConfig,
        ConfigAsset.OpenWorld => engine.openWorldLevelingConfig,
        _ => null,
    };

    /// <summary>Restores vanilla values, then applies the current settings if <paramref name="active"/>.</summary>
    public static int Apply(Engine engine, bool active)
    {
        int changed = 0;
        foreach (var t in All)
        {
            var obj = Resolve(engine, t.Asset);
            if (obj == null) continue;

            var prop = GetProp(obj.GetType(), t.Field);
            if (prop == null) continue;

            var key = (obj.Pointer, t.Field);
            if (!Originals.TryGetValue(key, out var original))
            {
                original = prop.GetValue(obj);
                Originals[key] = original;
            }

            object target = active ? Compute(t, original) : original;
            var current = prop.GetValue(obj);
            if (Equals(current, target)) continue;

            prop.SetValue(obj, target);
            changed++;
            if (Settings.VerboseLog.Value)
                Plugin.Log.LogInfo($"{t.Asset}.{t.Field}: {current} -> {target} (vanilla {original})");
        }
        return changed;
    }

    private static object Compute(Tweak t, object original)
    {
        float knob = Settings.Value(t.Knob);
        switch (t.Mode)
        {
            case TweakMode.Override:
                if (knob < 0) return original;
                return original is int ? (object)(int)Math.Round(knob) : knob;
            default:
                return original switch
                {
                    float f => f * knob,
                    int i => (int)Math.Max(1, Math.Round(i * knob)),
                    _ => original,
                };
        }
    }

    private static PropertyInfo GetProp(Type type, string field)
    {
        if (PropCache.TryGetValue((type, field), out var p)) return p;
        p = type.GetProperty(field, BindingFlags.Instance | BindingFlags.Public);
        if (p == null) Plugin.Log.LogWarning($"Field '{field}' not found on {type.FullName} (game update?)");
        PropCache[(type, field)] = p;
        return p;
    }
}
