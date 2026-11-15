using System.Linq;
using Indy;
using UnityEngine;

namespace SonarEasyMode;

/// <summary>
/// While the sonar is on, tags the closest untagged ship inside the sonar display radius, one ship per
/// AutoTagInterval seconds. Tags go through SubmarineOutside.AddTag with the correct type
/// (DebugMenu.GetCorrectTagType), like the debug menu's TagAllActiveShips. Unlike the sonar station's AddTag,
/// there is no sound and no Stats.CheckAddTagToShip. Tags the player set by hand are never changed.
/// </summary>
public static class AutoTag
{
    private static SubmarineOutside _outside;
    private static float _next;

    /// <summary>Called from the driver tick (1 s); Time.time stops while the game is paused.</summary>
    public static void Tick(bool active)
    {
        float interval = Settings.AutoTagInterval.Value;
        if (!active || interval <= 0f || Time.time < _next) return;
        Patches.Guard("AutoTag", () =>
        {
            if (TagClosest()) _next = Time.time + interval;
        });
    }

    private static bool TagClosest()
    {
        if (_outside == null)
            _outside = Object.FindObjectsByType<SubmarineOutside>(FindObjectsSortMode.None).FirstOrDefault(s => s != null);
        var player = _outside?.playerSubmarine;
        var engine = Engine.Instance;
        if (player == null || engine == null || !_outside.IsSonarOn()) return false;

        // Same radius the sonar screen shows, sonar upgrades included.
        float range = engine.GetSonarDisplayRange();
        Vector3 origin = player.GetPosition();
        Ship best = null;
        TagType bestTag = TagType.None;
        float bestDistance = range;
        foreach (var ship in Object.FindObjectsByType<Ship>(FindObjectsSortMode.None))
        {
            if (ship == null || ship.Pointer == player.Pointer) continue;
            if (ship.GetShipState() != EntityState.Active || ship.GetTagType() != TagType.None) continue;
            var tag = DebugMenu.GetCorrectTagType(ship.GetEntityType());
            if (tag == TagType.None) continue;
            Vector3 delta = ship.GetPosition() - origin;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            if (distance > bestDistance) continue;
            best = ship;
            bestTag = tag;
            bestDistance = distance;
        }
        if (best == null) return false;

        _outside.AddTag(best.GetId(), bestTag);
        Patches.ReportOnce("autotag", "AutoTag: first ship tagged");
        if (Settings.VerboseLog.Value)
            Plugin.Log.LogInfo($"AutoTag: {best.GetEntityType()} #{best.GetId()} as {bestTag} at {bestDistance:0} / {range:0}");
        return true;
    }
}
