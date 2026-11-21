using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System.Collections.Generic;
using System;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace POGLib.Core
{
    /// <summary>
    /// Reskin helpers: transplant a mesh child from a vanilla prefab, tint the jewel/mesh via
    /// HDRP material properties, and strip SpeakingStone voice/VFX (a common donor mesh).
    /// This is the no-AssetBundle visual tier.
    /// </summary>
    internal static class ItemVisuals
    {
        /// <summary>
        /// Reskin by REPLACING the mesh on the shell's own renderers, rather than adding a visual of
        /// our own. This matters more than it sounds: a game item ships two visual variants —
        /// <c>PickupItem</c> (lying in the world) and <c>HeldItem</c> (in a character's hands, seen in
        /// third person) — and the game swaps them, hides them in the inventory, and re-enables their
        /// renderers from its own <c>m_renderers</c> list.
        ///
        /// The previous approach parented an extra "ModItemVisual" object to the item root and
        /// disabled everything else. The game promptly re-enabled its own renderers (so a diamond
        /// showed in hand) while our extra object, unknown to any of that logic, floated above it and
        /// stayed visible inside the backpack. Swapping the mesh in place means every one of those
        /// behaviours keeps working, because the objects doing them are still the game's own.
        /// </summary>
        public static void ReplaceMeshes(GameObject target, GameObject visualSource, string childName)
        {
            if (target == null || visualSource == null || string.IsNullOrEmpty(childName)) return;

            // Locate the donor mesh (a MeshFilter under the named child).
            MeshFilter donorFilter = null;
            MeshRenderer donorRenderer = null;
            foreach (var mf in visualSource.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (!mf.gameObject.name.Contains(childName) || mf.gameObject.name.Contains("Outline")) continue;
                donorFilter = mf;
                donorRenderer = mf.GetComponent<MeshRenderer>();
                break;
            }

            if (donorFilter == null)
            {
                var names = new System.Text.StringBuilder();
                foreach (var mf in visualSource.GetComponentsInChildren<MeshFilter>(true))
                    if (mf != null) names.Append("\n    ").Append(mf.gameObject.name);
                MelonLogger.Warning(
                    $"[POGLib] Reskin FAILED: no mesh child matching '{childName}' in " +
                    $"'{visualSource.name}' — the item keeps its shell mesh. Meshes available:{names}");
                return;
            }

            // Swap into EVERY mesh the shell has, so the world view and the in-hand view match.
            int swapped = 0;
            foreach (var mf in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.gameObject.name.Contains("Outline")) continue;   // outline meshes track the shape

                mf.sharedMesh = donorFilter.sharedMesh;
                var r = mf.GetComponent<MeshRenderer>();
                if (r != null && donorRenderer != null) r.sharedMaterials = donorRenderer.sharedMaterials;
                swapped++;
            }

            // The item caches mesh bounds for tooltip placement and the inventory silhouette.
            try { target.GetComponent<InventoryItem>()?.RefreshMeshInfo(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] RefreshMeshInfo failed: {ex.Message}"); }

            MelonLogger.Msg($"[POGLib] Reskin: '{childName}' mesh applied to {swapped} renderer(s) on {target.name}.");
        }


        /// <summary>
        /// Same in-place swap as <see cref="ReplaceMeshes(GameObject,GameObject,string)"/>, but with a
        /// mesh we built ourselves rather than one borrowed from a game prefab — this is what carries a
        /// fully custom model (see <see cref="ObjMesh"/>). Every renderer of the shell gets the mesh and
        /// its materials, so the world view and the in-hand view stay in step and the game's own
        /// show/hide logic keeps working, exactly as in the reskin path.
        /// </summary>
        public static void ReplaceMeshes(GameObject target, Mesh mesh, Il2CppReferenceArray<Material> materials)
        {
            if (target == null || mesh == null) return;

            int swapped = 0;
            foreach (var mf in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null) continue;
                if (mf.gameObject.name.Contains("Outline")) continue;   // outline meshes track the shape

                mf.sharedMesh = mesh;
                var r = mf.GetComponent<MeshRenderer>();
                if (r != null && materials != null && materials.Length > 0) r.sharedMaterials = materials;

                // Our model is authored Y-up in the item's own space; the shell's mesh child is posed for the shell's
                // model (the diamond's lies tilted). Keeping that pose laid the Ember Lantern on its side in a shop
                // slot (2026-11-19, host and client), so the child takes the item root's orientation.
                var before = Quaternion.Inverse(target.transform.rotation) * mf.transform.rotation;
                if (Quaternion.Angle(before, Quaternion.identity) > 0.5f)
                {
                    mf.transform.rotation = target.transform.rotation;
                    MelonLogger.Msg($"[POGLib] Custom mesh '{mesh.name}': '{mf.gameObject.name}' was turned {before.eulerAngles} " +
                                    "from the item; set upright to the item.");
                }
                swapped++;
            }

            try { target.GetComponent<InventoryItem>()?.RefreshMeshInfo(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] RefreshMeshInfo failed: {ex.Message}"); }

            if (swapped == 0)
                MelonLogger.Warning($"[POGLib] Custom mesh '{mesh.name}': no renderer on {target.name} to apply it to.");
            else
                MelonLogger.Msg($"[POGLib] Custom mesh '{mesh.name}' applied to {swapped} renderer(s) on {target.name}.");
        }

        /// <summary>
        /// Give the item a hitbox that matches its model.
        ///
        /// Swapping the mesh alone leaves the donor shell's collider behind: a custom lantern ends up
        /// with a diamond's small box, so it is picked up from the wrong place and bounces off the floor
        /// wrongly. This is the other half of a custom visual, and it is easy to forget precisely because
        /// nothing looks broken until you interact with the thing.
        ///
        /// Two strategies, in order of fidelity:
        ///  • The model ships <c>UCX_*</c> hulls → the shell's solid colliders are replaced with convex
        ///    MeshColliders built from them. Convex is not a preference: a moving Rigidbody can only use
        ///    convex shapes, which is why a concave object needs several hulls rather than one.
        ///  • No hulls → each solid primitive collider is refitted to the visual mesh's bounds. Less
        ///    exact, but it needs nothing from the artist and is never worse than the donor's shape.
        ///
        /// Trigger colliders are left strictly alone: they are the game's interaction volumes (pickup
        /// range and the like), not hitboxes, and resizing them would change how the item is used.
        /// </summary>
        public static void ApplyColliders(GameObject target, Mesh[] hulls, Mesh visualMesh)
        {
            if (target == null) return;

            var all = target.GetComponentsInChildren<Collider>(true);
            if (all == null) return;

            var solid = new List<Collider>();
            int triggers = 0;
            foreach (var c in all)
            {
                if (c == null) continue;
                if (c.isTrigger) { triggers++; continue; }
                solid.Add(c);
            }

            if (hulls != null && hulls.Length > 0)
                ReplaceWithHulls(target, solid, hulls, triggers);
            else if (visualMesh != null)
                RefitToBounds(target, solid, visualMesh, triggers);

            try { target.GetComponent<InventoryItem>()?.RefreshMeshInfo(); }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] RefreshMeshInfo failed: {ex.Message}"); }
        }

        private const string ModCollisionChild = "ModCollision";

        private static void ReplaceWithHulls(GameObject target, List<Collider> solid, Mesh[] hulls, int triggers)
        {
            // Layer and physic material come from the shell's own collider (solids live on "Interact" —
            // measured on the vanilla prefabs — and losing that layer would break pickup raycasts).
            PhysicsMaterial physic = solid.Count > 0 && solid[0] != null ? solid[0].sharedMaterial : null;
            int layer = solid.Count > 0 && solid[0] != null ? solid[0].gameObject.layer : target.layer;

            // Host the hulls on an identity-transform child of the SAME transform that carries the
            // visual mesh. Hull vertices and render vertices come from the same file and the same
            // space, so parenting both to one transform makes alignment structural — hosting on the
            // shell's old collider object instead means trusting that its transform happens to match
            // the mesh child's, which is the donor's business, not ours.
            Transform anchor = FindVisualAnchor(target);

            var existing = anchor.Find(ModCollisionChild);
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

            var hostGo = new GameObject(ModCollisionChild);
            hostGo.transform.SetParent(anchor, false);   // identity local transform
            hostGo.layer = layer;

            foreach (var c in solid)
                if (c != null) UnityEngine.Object.Destroy(c);

            int added = 0;
            foreach (var hull in hulls)
            {
                if (hull == null) continue;
                var mc = hostGo.AddComponent<MeshCollider>();
                mc.convex = true;              // set before the mesh so PhysX never cooks a concave one
                mc.sharedMesh = hull;
                if (physic != null) mc.sharedMaterial = physic;
                added++;
            }

            MelonLogger.Msg($"[POGLib] Colliders: replaced {solid.Count} shell collider(s) with " +
                            $"{added} convex hull(s) under '{anchor.name}/{ModCollisionChild}' " +
                            $"(layer={LayerMask.LayerToName(layer)}, {triggers} trigger(s) left untouched).");
        }

        /// <summary>
        /// The transform whose local space the visual mesh is drawn in — the world-variant (PickupItem)
        /// mesh child when there is one, since that is the variant physics interacts with.
        /// </summary>
        private static Transform FindVisualAnchor(GameObject target)
        {
            MeshFilter first = null;
            foreach (var mf in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf == null || mf.sharedMesh == null) continue;
                if (mf.gameObject.name.Contains("Outline")) continue;
                if (first == null) first = mf;

                for (var t = mf.transform; t != null && t != target.transform; t = t.parent)
                    if (t.name == "PickupItem") return mf.transform;
            }
            return first != null ? first.transform : target.transform;
        }

        private static void RefitToBounds(GameObject target, List<Collider> solid, Mesh visualMesh, int triggers)
        {
            var bounds = visualMesh.bounds;      // model space, which is the item root's space
            int refitted = 0;

            foreach (var c in solid)
            {
                if (c == null) continue;
                var t = c.transform;
                Vector3 centreWorld = target.transform.TransformPoint(bounds.center);
                Vector3 localCentre = t.InverseTransformPoint(centreWorld);
                Vector3 sizeScale = Ratio(target.transform.lossyScale, t.lossyScale);
                Vector3 localSize = Vector3.Scale(bounds.size, sizeScale);

                var box = c.TryCast<BoxCollider>();
                if (box != null)
                {
                    box.center = localCentre;
                    box.size = localSize;
                    refitted++;
                    continue;
                }

                var sphere = c.TryCast<SphereCollider>();
                if (sphere != null)
                {
                    sphere.center = localCentre;
                    // Unity sizes a sphere by the largest extent; the inscribed radius would leave
                    // corners of the model sticking out of its own hitbox.
                    sphere.radius = Mathf.Max(localSize.x, Mathf.Max(localSize.y, localSize.z)) * 0.5f;
                    refitted++;
                    continue;
                }

                var capsule = c.TryCast<CapsuleCollider>();
                if (capsule != null)
                {
                    capsule.center = localCentre;
                    capsule.height = localSize.y;
                    capsule.radius = Mathf.Max(localSize.x, localSize.z) * 0.5f;
                    capsule.direction = 1;
                    refitted++;
                }
                // A MeshCollider already follows a mesh; leave it to whoever set it.
            }

            MelonLogger.Msg($"[POGLib] Colliders: refitted {refitted}/{solid.Count} shell collider(s) to " +
                            $"the model bounds {bounds.size} ({triggers} trigger(s) left untouched). " +
                            "Ship UCX_ hulls in the model for an exact fit.");
        }

        private static Vector3 Ratio(Vector3 a, Vector3 b) => new Vector3(
            Mathf.Approximately(b.x, 0f) ? 1f : a.x / b.x,
            Mathf.Approximately(b.y, 0f) ? 1f : a.y / b.y,
            Mathf.Approximately(b.z, 0f) ? 1f : a.z / b.z);

        /// <summary>
        /// Legacy path: parent a cloned mesh child under the item. Kept for visuals that genuinely need
        /// an extra object (a bundle prefab with its own hierarchy); prefer <see cref="ReplaceMeshes"/>

        /// <summary>
        /// Legacy path: parent a cloned mesh child under the item. Kept for visuals that genuinely need
        /// an extra object (a bundle prefab with its own hierarchy); prefer <see cref="ReplaceMeshes"/>
        /// for a plain reskin, since only that participates in the game's own show/hide logic.
        /// </summary>
        public static void AttachGameMesh(GameObject target, GameObject visualSource, string childName)
        {
            if (target == null || visualSource == null || string.IsNullOrEmpty(childName)) return;

            Transform child = null;
            var transforms = visualSource.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = transforms[i];
                if (t == null) continue;
                if (t.name.Contains(childName) && !t.name.Contains("Outline")) { child = t; break; }
            }
            if (child == null)
            {
                // Silence here meant "the item just looks like its shell" with no clue why — that is
                // how two mod stones shipped as plain diamonds. Name the miss and list the options.
                var names = new System.Text.StringBuilder();
                for (int i = 0; i < transforms.Length && i < 40; i++)
                    if (transforms[i] != null && transforms[i] != visualSource.transform)
                        names.Append("\n    ").Append(transforms[i].name);
                MelonLogger.Warning(
                    $"[POGLib] Reskin FAILED: no child matching '{childName}' in " +
                    $"'{visualSource.name}' — the item will keep its shell mesh. Available children:{names}");
                return;
            }

            var existing = target.transform.Find(GameNames.ModVisualChild);
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

            var visualGo = UnityEngine.Object.Instantiate(child.gameObject, target.transform);
            visualGo.name = GameNames.ModVisualChild;
            visualGo.SetActive(true);

            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.transform.IsChildOf(visualGo.transform)) continue;
                if (r.gameObject.name == GameNames.ModVisualChild) continue;
                r.enabled = false;
            }
        }

        /// <summary>
        /// Instantiate a custom prefab/mesh loaded from an AssetBundle as the item's visual, replacing
        /// the shell's own renderers. The prefab keeps everything Unity itself can serialize —
        /// MeshRenderer, ParticleSystem, Light, TrailRenderer, Animator — so custom visual EFFECTS
        /// come along with the mesh. What it CANNOT carry is custom C# MonoBehaviour scripts: under
        /// IL2CPP those types do not exist in the runtime, so Unity drops them on load (use the Lib's
        /// capabilities / use handlers for behaviour instead).
        /// </summary>
        public static GameObject AttachBundlePrefab(GameObject target, GameObject sourcePrefab,
                                                    Vector3 localOffset, Vector3 localEuler, Vector3 localScale)
        {
            if (target == null || sourcePrefab == null) return null;

            var existing = target.transform.Find(GameNames.ModVisualChild);
            if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);

            var visualGo = UnityEngine.Object.Instantiate(sourcePrefab, target.transform);
            visualGo.name = GameNames.ModVisualChild;
            visualGo.transform.localPosition = localOffset;
            visualGo.transform.localEulerAngles = localEuler;
            visualGo.transform.localScale = localScale;
            visualGo.SetActive(true);

            // Hide the shell's own look so only the custom mesh shows.
            foreach (var r in target.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (r.transform.IsChildOf(visualGo.transform)) continue;
                r.enabled = false;
            }
            return visualGo;
        }

        /// <summary>
        /// Repair materials whose shader did not survive the bundle (Unity substitutes the magenta
        /// error shader). Re-pointing them at the game's own HDRP/Lit keeps every property whose name
        /// matches — base colour, maps, smoothness — because Unity preserves same-named properties
        /// across a shader swap. Returns how many materials were repaired.
        /// </summary>
        public static int RepairBundleShaders(GameObject root)
        {
            if (root == null) return 0;
            var replacement = Shader.Find(GameNames.Shader.HdrpLit);
            if (replacement == null)
            {
                MelonLogger.Warning($"[POGLib] Cannot repair bundle shaders: '{GameNames.Shader.HdrpLit}' not found.");
                return 0;
            }

            int fixedCount = 0;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    bool broken = m.shader == null || m.shader.name.Contains(GameNames.Shader.ErrorShader);
                    if (!broken) continue;
                    m.shader = replacement;
                    fixedCount++;
                }
                r.materials = mats;
            }
            if (fixedCount > 0)
                MelonLogger.Msg($"[POGLib] Repaired {fixedCount} bundle material(s) onto {GameNames.Shader.HdrpLit}. " +
                                "Build the bundle against the game's Unity/HDRP version to avoid this.");
            return fixedCount;
        }

        /// <summary>
        /// Tint the item's mesh renderers via HDRP _BaseColor (fallback _Color). Applies to every mesh
        /// variant — the world one and the in-hand one — so the item does not change colour when
        /// picked up. Outlines and particle renderers are left alone.
        /// </summary>
        public static void ApplyTint(GameObject root, Color tint, string childHint)
        {
            var renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                if (r.gameObject.name.Contains("Outline")) continue;

                var mats = r.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null) continue;
                    var copy = new Material(mats[m]);
                    if (copy.HasProperty(GameNames.Shader.BaseColor)) copy.SetColor(GameNames.Shader.BaseColor, tint);
                    else if (copy.HasProperty(GameNames.Shader.Color)) copy.SetColor(GameNames.Shader.Color, tint);
                    mats[m] = copy;
                }
                r.materials = mats;
            }
        }

        /// <summary>Destroy SpeakingStone/voice components dragged in with a cloned SpeakingStone mesh.</summary>
        public static void StripVoiceComponents(GameObject go)
        {
            var behaviours = go.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null) continue;
                string tn = mb.GetIl2CppType().Name;
                if (tn == GameNames.VoiceComponents.SpeakingStone || tn.Contains(GameNames.VoiceComponents.VoiceProximity))
                    UnityEngine.Object.Destroy(mb);
            }
        }

        /// <summary>Hide SpeakingStone VFX/outline objects carried by the donor mesh.</summary>
        public static void HideSpeakingStoneVfx(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                string n = r.gameObject.name;
                for (int k = 0; k < GameNames.SpeakingStoneVfx.Length; k++)
                {
                    if (n.IndexOf(GameNames.SpeakingStoneVfx[k], StringComparison.OrdinalIgnoreCase) >= 0)
                    { r.gameObject.SetActive(false); break; }
                }
            }
        }
    }
}
