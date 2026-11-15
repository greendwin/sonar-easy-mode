using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SonarEasyMode;

/// <summary>
/// Loading a tube by hand also moves one torpedo of the same type from the rack into the next free tube.
/// Nothing is created: the extra torpedo is an existing rack item. The game fills the tube a moment after
/// the load call, so the item comes off the rack at once and goes back only if the tube is still not loaded
/// after <see cref="ConfirmSeconds"/>. Tube and rack must both be in the hand-loaded tube's room: tubes #1-4 are
/// torpedo room 1, #5-8 room 2 (the scene-culling zone at a tube is unreliable: it put #5 in room 1 and #8 nowhere,
/// so it is only used for racks). No rack torpedo of that type there or no free tube there = nothing happens.
/// </summary>
internal static class TubeAutoLoad
{
    // TorpedoTubes.LoadItem may call LoadItemByIndex (or the other way round): act once, after the outermost call.
    // Decremented in a finalizer so an exception in the game's load cannot leave it stuck.
    internal static int Depth;
    private static bool Busy;

    private const float ConfirmSeconds = 5f;
    private const int TubesPerRoom = 4;

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
        // Everything stays in the hand-loaded tube's room: its tubes and its rack only.
        var room = TubeRoom(loadedIndex);
        if (room == ZoneType.None)
        {
            Patches.ReportOnce("autoload:noroom", $"TubeAutoLoad: tube {loadedIndex} is not a known tube, skipped");
            return;
        }

        int tube = FindFreeTube(tubes, loadedIndex, room);
        if (tube < 0) return;

        var culling = Object.FindFirstObjectByType<SceneCulling>();
        var spot = FindRackSpot(tubes, type, culling, room);
        if (spot == null) return;

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
                                           $"(hand-loaded tube {loadedIndex}, {room})");
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

    private static ZoneType ZoneAt(SceneCulling culling, Component c) =>
        culling == null || c == null ? ZoneType.None : culling.GetZoneType(c.transform.position);

    /// <summary>Torpedo room of a tube by its number (1-based): #1-4 room 1, #5-8 room 2.</summary>
    private static ZoneType TubeRoom(int index) => index switch
    {
        >= 1 and <= TubesPerRoom => ZoneType.TorpedoRoom1,
        > TubesPerRoom and <= 2 * TubesPerRoom => ZoneType.TorpedoRoom2,
        _ => ZoneType.None,
    };

    /// <summary>First empty, unlocked, idle tube of the room after the one just loaded, wrapping within the room.</summary>
    private static int FindFreeTube(TorpedoTubes tubes, int loadedIndex, ZoneType room)
    {
        int first = (loadedIndex - 1) / TubesPerRoom * TubesPerRoom + 1;
        var skipped = new List<string>();
        for (int step = 1; step < TubesPerRoom; step++)
        {
            int i = first + (loadedIndex - first + step) % TubesPerRoom;
            string why = null;
            if (!TorpedoTubes.IsTubeAvailable(i)) why = "locked";
            else if (tubes.IsLoaded(i) || IsPending(i)) why = "loaded";
            else if (tubes.GetTube(i) is not { } t) why = "missing";
            else if (t.IsTubeAnimationPlaying()) why = "door moving";
            if (why == null) return i;
            if (why != "loaded") skipped.Add($"#{i} {why}");
        }
        Patches.ReportOnce("autoload:notube:" + string.Join(",", skipped),
            $"TubeAutoLoad: no free tube in {room}, skipped" + (skipped.Count > 0 ? $" ({string.Join(", ", skipped)})" : ""));
        return -1;
    }

    /// <summary>A rack spot of the same submarine and room holding a torpedo of the given type.</summary>
    private static SingleSpot FindRackSpot(TorpedoTubes tubes, ItemsType type, SceneCulling culling, ZoneType room)
    {
        var net = tubes.gameNetCode;
        var elsewhere = new HashSet<ZoneType>();
        foreach (var spot in Object.FindObjectsByType<SingleSpot>(FindObjectsSortMode.None))
        {
            if (spot == null || !spot.IsFilled()) continue;
            var item = spot.item;
            if (item == null || item.GetItemType() != type) continue;
            if (net != null && spot.gameNetCode != null && spot.gameNetCode.Pointer != net.Pointer) continue;
            var zone = ZoneAt(culling, spot);
            if (zone == room) return spot;
            elsewhere.Add(zone);
        }
        Patches.ReportOnce("autoload:norack:" + type + ":" + room, $"TubeAutoLoad: no {type} on the {room} rack, skipped" +
                                                                 (elsewhere.Count > 0 ? $" (others in: {string.Join(", ", elsewhere)})" : ""));
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
