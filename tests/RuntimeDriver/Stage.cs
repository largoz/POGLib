// Where goblins are put to be looked at: the great plate before the king and queen (the user's choice, 2026-11-25: roomy and level; by the
// starter shop it is dark and crowded), under two lights of the scene's own kind. Every shot of clothes uses it.
using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppEntities;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class Stage
    {
        internal static bool Found { get; private set; }
        internal static Vector3 Spot { get; private set; }
        /// <summary>From the spot to where a camera has most room.</summary>
        internal static Vector3 CamDir { get; private set; } = Vector3.forward;
        internal static float Radius { get; private set; } = 1.15f;
        static bool _looked, _lit;
        /// <summary>A third light behind the stage, for pictures of the back too.</summary>
        internal static bool BackLight;

        /// <summary>Finds the plate and lights it, once; false when the scene has none.</summary>
        internal static bool Ready(Action<string> log)
        {
            if (!_looked) { _looked = true; Found = OnPlate(log); if (!Found) log("stage: no Plate_01 in this scene"); }
            if (Found && !_lit) { _lit = true; Light(log); }
            return Found;
        }

        /// <summary>The player onto the stage, facing the camera's way (for what they see of themselves).</summary>
        internal static void BringMe()
        {
            if (!Found) return;
            foreach (var p in UnityEngine.Object.FindObjectsOfType<EntityPlayer>())
                if (p != null && p.IsOwner) p.Teleport(Spot - CamDir * 2.2f + Vector3.up * 0.3f, Quaternion.LookRotation(CamDir));
        }

        static float Clear(Vector3 from, Vector3 dir)
        {
            float room = 7f;
            foreach (var h in Physics.SphereCastAll(from, 0.25f, dir, 7f, ~0, QueryTriggerInteraction.Ignore))
                if (h.transform != null && h.distance > 0.02f) room = Math.Min(room, h.distance);
            return room;
        }

        static bool OnPlate(Action<string> log)
        {
            foreach (var r in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (r == null || !r.name.StartsWith("Plate_01")) continue;
                var b = r.bounds;
                foreach (var h in Physics.RaycastAll(new Vector3(b.center.x, b.max.y + 4f, b.center.z), Vector3.down, 9f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
                {
                    if (h.normal.y < 0.9f || h.point.y > b.max.y + 0.6f) continue;
                    Spot = h.point;
                    Radius = Mathf.Clamp(Math.Min(b.extents.x, b.extents.z) * 0.5f, 0.9f, 1.8f);
                    float most = 0f;
                    for (int k = 0; k < 24; k++)
                    {
                        var d = Quaternion.Euler(0f, k * 15f, 0f) * Vector3.forward;
                        float c = Clear(Spot + Vector3.up * 1.1f, d);
                        if (c > most) { most = c; CamDir = d; }
                    }
                    log($"stage: {r.name} at {b.center:F1}, size {b.size:F1}; spot {Spot:F1}, room {most:0.0} m");
                    return true;
                }
            }
            return false;
        }

        // The renderer needs a light's extra data, so one of the scene is copied.
        static void Light(Action<string> log)
        {
            Light like = null;
            foreach (var l in UnityEngine.Object.FindObjectsOfType<Light>()) if (l != null && l.enabled && l.type == LightType.Point && l.GetComponents<Component>().Length <= 4) { like = l; break; }
            if (like == null) { log("stage: no light to copy"); return; }
            var lights = new List<(float side, float up, float power, float ahead)> { (0.9f, 2.1f, 1f, 2f), (-1.4f, 1.7f, 0.7f, 2f) };
            if (BackLight) lights.Add((0.4f, 2.0f, 0.9f, -2.2f));
            foreach (var (side, up, power, ahead) in lights)
            {
                var go = UnityEngine.Object.Instantiate(like.gameObject);
                go.name = "StageLight";
                go.transform.position = Spot + CamDir * ahead + Vector3.Cross(Vector3.up, CamDir) * side * 1.8f + Vector3.up * up;
                var l = go.GetComponent<Light>();
                l.color = new Color(1f, 0.95f, 0.88f); l.range = 14f; l.intensity = like.intensity * 2.4f * power; l.enabled = true;
            }
            log($"stage: lit like {like.name}");
        }
    }
}
