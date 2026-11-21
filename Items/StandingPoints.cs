using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace POGLib.Items
{
    /// <summary>
    /// A shop slot places its item so that <c>InventoryItem.m_standingTransform</c> lands on the slot's item
    /// holder, and throws when that field is null — leaving the slot empty (read in the game's code, see
    /// _research/SHOPS_NATIVE.md Q2). Shells such as Item_Diamond have none, so every mod item gets one.
    /// </summary>
    internal static class StandingPoints
    {
        public const string ChildName = "ModStandingPoint";

        /// <summary>Make sure the item has a standing point inside it; optionally move/rotate it.</summary>
        public static Transform Ensure(InventoryItem item, Vector3? localPos = null, Vector3? localEuler = null)
        {
            if (item == null) return null;
            var t = item.m_standingTransform;
            if (t == null || !t.IsChildOf(item.transform))
            {
                var existing = item.transform.Find(ChildName);
                var go = existing != null ? existing.gameObject : new GameObject(ChildName);
                go.transform.SetParent(item.transform, false);
                go.transform.localPosition = BottomCentre(item);
                go.transform.localRotation = Quaternion.identity;
                item.m_standingTransform = go.transform;
                t = go.transform;
            }
            if (localPos.HasValue) t.localPosition = localPos.Value;
            if (localEuler.HasValue) t.localEulerAngles = localEuler.Value;
            return t;
        }

        /// <summary>
        /// Bottom centre of the item's meshes in its own space. From mesh bounds, not Renderer.bounds, which
        /// is empty on an inactive template.
        /// </summary>
        public static Vector3 BottomCentre(InventoryItem item)
        {
            bool any = false;
            Bounds b = default;
            var root = item.transform;
            foreach (var mf in item.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null || mf.gameObject.name.Contains("Outline")) continue;
                var mb = mf.sharedMesh.bounds;
                var c = mb.center; var e = mb.extents;
                for (int i = 0; i < 8; i++)
                {
                    var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                    var p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            if (!any) MelonLogger.Warning($"[POGLib] {item.name}: no mesh to find a standing point from; using the pivot.");
            return any ? new Vector3(b.center.x, b.min.y, b.center.z) : Vector3.zero;
        }
    }
}
