using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SonarEasyMode;

/// <summary>
/// Loading a tube by hand also moves one torpedo of the same type from the rack into the next free tube.
/// Nothing is created: the extra torpedo is an existing rack item. The game fills the tube a moment after
/// the load call, so the item comes off the rack at once and goes back only if the tube is still not loaded
/// after <see cref="ConfirmSeconds"/>. No rack torpedo of that type or no free tube = nothing happens.
/// </summary>
internal static class TubeAutoLoad
{
    // TorpedoTubes.LoadItem may call LoadItemByIndex (or the other way round): act once, after the outermost call.
    // Decremented in a finalizer so an exception in the game's load cannot leave it stuck.
    internal static int Depth;
    private static bool Busy;

    private const float ConfirmSeconds = 5f;

    private sealed class PendingLoad
    {
        public TorpedoTubes Tubes;
        public int Tube;
        public ItemsType Type;
        public Item Item;
        public SingleSpot Spot;
        public float Deadline;
    }

    private static readonly List<PendingLoad> Pending = new();

    internal static bool Enabled => EasyModeDriver.Active && Settings.TubeAutoLoad.Value && !Busy;

    internal static void AfterLoad(TorpedoTubes tubes, int loadedIndex, ItemsType type)
    {
        Busy = true;
        try
        {
            LoadNext(tubes, loadedIndex, type);
        }
        finally
        {
            Busy = false;
        }
    }

    private static void LoadNext(TorpedoTubes tubes, int loadedIndex, ItemsType type)
    {
        int tube = FindFreeTube(tubes, loadedIndex);
        if (tube < 0)
        {
            Patches.ReportOnce("autoload:notube", "TubeAutoLoad: no free tube, skipped");
            return;
        }

        var spot = FindRackSpot(tubes, type);
        if (spot == null)
        {
            Patches.ReportOnce("autoload:norack:" + type, $"TubeAutoLoad: no {type} on the rack, skipped");
            return;
        }

        var item = spot.item;
        int itemId = item.GetItemLocalID();

        // Take it off the rack first; abort before loading anything if the spot did not let go.
        spot.ReleaseItemSilently();
        if (spot.IsFilled())
        {
            Plugin.Log.LogWarning($"TubeAutoLoad: rack spot kept its {type}, skipped");
            return;
        }
        item.SetInSingleSpot(null);
        item.DisableContextCollider();
        item.SetState(false);

        tubes.LoadItem(TorpedoTubes.TubeIndexToLoadContext(tube), type, itemId);
        Pending.Add(new PendingLoad
        {
            Tubes = tubes, Tube = tube, Type = type, Item = item, Spot = spot,
            Deadline = Time.unscaledTime + ConfirmSeconds,
        });
        Patches.ReportOnce("autoload:sent", $"TubeAutoLoad: {type} #{itemId} taken off the rack for tube {tube} " +
                                           $"(hand-loaded tube {loadedIndex})");
        Confirm();
    }

    /// <summary>Settles pending loads: done once the tube holds the torpedo, rolled back after the deadline.</summary>
    internal static void Confirm()
    {
        if (Pending.Count == 0) return;
        Patches.Guard("TubeAutoLoad.Confirm", () =>
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                var p = Pending[i];
                if (p.Tubes == null || p.Item == null || p.Spot == null)
                {
                    Pending.RemoveAt(i); // scene unloaded: the mission restores its own state
                    continue;
                }
                if (p.Tubes.IsLoaded(p.Tube) && p.Tubes.GetWeaponLoaded(p.Tube) == p.Type)
                {
                    Patches.ReportOnce("autoload:ok", $"TubeAutoLoad: tube {p.Tube} loaded with {p.Type} from the rack");
                    Pending.RemoveAt(i);
                    continue;
                }
                if (Time.unscaledTime < p.Deadline) continue;

                // The tube never took it: back on the rack, so no torpedo is lost.
                Pending.RemoveAt(i);
                p.Item.SetState(true);
                p.Item.EnableContextCollider();
                if (!p.Spot.IsFilled()) p.Spot.PutItem(p.Item, false);
                Plugin.Log.LogWarning($"TubeAutoLoad: tube {p.Tube} did not take {p.Type} within {ConfirmSeconds:0} s; " +
                                      $"returned it to the rack (on rack: {p.Spot.IsFilled() && p.Spot.item == p.Item})");
            }
        });
    }

    private static bool IsPending(int tube)
    {
        foreach (var p in Pending)
            if (p.Tube == tube) return true;
        return false;
    }

    /// <summary>First empty, unlocked, idle tube after the one just loaded (wrapping around).</summary>
    private static int FindFreeTube(TorpedoTubes tubes, int loadedIndex)
    {
        // Tube indices may be 0- or 1-based; the game's own mapping of the first load context tells which.
        int first = TorpedoTubes.UseContextToTubeIndex(UseContextType.LoadTorpedo1);
        int count = TorpedoTubes.TubeCount;
        for (int step = 1; step < count; step++)
        {
            int i = first + ((loadedIndex - first + step) % count + count) % count;
            if (!TorpedoTubes.IsTubeAvailable(i) || tubes.IsLoaded(i) || IsPending(i)) continue;
            var t = tubes.GetTube(i);
            if (t == null || t.IsTubeAnimationPlaying()) continue;
            return i;
        }
        return -1;
    }

    /// <summary>A rack spot of the same submarine holding a torpedo of the given type.</summary>
    private static SingleSpot FindRackSpot(TorpedoTubes tubes, ItemsType type)
    {
        var net = tubes.gameNetCode;
        foreach (var spot in Object.FindObjectsByType<SingleSpot>(FindObjectsSortMode.None))
        {
            if (spot == null || !spot.IsFilled()) continue;
            var item = spot.item;
            if (item == null || item.GetItemType() != type) continue;
            if (net != null && spot.gameNetCode != null && spot.gameNetCode.Pointer != net.Pointer) continue;
            return spot;
        }
        return null;
    }
}

/// <summary>The load path used when a player pushes a torpedo into a tube (tube given as a load context).</summary>
[HarmonyPatch(typeof(TorpedoTubes), nameof(TorpedoTubes.LoadItem))]
internal static class TubeLoadPatch
{
    private static void Prefix() => TubeAutoLoad.Depth++;

    private static void Finalizer() => TubeAutoLoad.Depth--;

    private static void Postfix(TorpedoTubes __instance, UseContextType useContext, ItemsType item)
    {
        if (TubeAutoLoad.Depth > 1 || !TubeAutoLoad.Enabled) return;
        Patches.Guard("TubeLoadPatch", () =>
            TubeAutoLoad.AfterLoad(__instance, TorpedoTubes.UseContextToTubeIndex(useContext), item));
    }
}

/// <summary>Same as <see cref="TubeLoadPatch"/> for loads addressed by tube index.</summary>
[HarmonyPatch(typeof(TorpedoTubes), nameof(TorpedoTubes.LoadItemByIndex))]
internal static class TubeLoadByIndexPatch
{
    private static void Prefix() => TubeAutoLoad.Depth++;

    private static void Finalizer() => TubeAutoLoad.Depth--;

    private static void Postfix(TorpedoTubes __instance, int tubeIndex, ItemsType item)
    {
        if (TubeAutoLoad.Depth > 1 || !TubeAutoLoad.Enabled) return;
        Patches.Guard("TubeLoadByIndexPatch", () => TubeAutoLoad.AfterLoad(__instance, tubeIndex, item));
    }
}
