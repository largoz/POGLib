using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using POGLib.Core;
using POGLib.Cosmetics.Model;
using UnityEngine;

namespace POGLib.Cosmetics
{
    /// <summary>
    /// Makes the object the game copies when a goblin puts the part on. On the head: one renderer on the object itself (the game
    /// colours that one). On the body: one skinned renderer in a child, with bones that carry the goblin's names (the game binds
    /// them by name, keeps track of the first renderer only, and throws the rest of the copy away).
    /// </summary>
    internal static class CosmeticBuilder
    {
        // The file's axes (right-handed, Y up, facing +Z, metres) to the goblin's mesh space (left-handed, Z up, facing -Y).
        private static Vector3 Turn(float x, float y, float z) => new Vector3(-x, -z, y);

        /// <param name="hidden">objects of the model the wearer does not see in first person</param>
        /// <param name="firstPerson">the mesh without them; null when nothing is hidden, or nothing is left</param>
        public static GameObject Build(string fullId, GlbModel m, GoblinRig rig, Transform holder, Renderer like, ICollection<string> hidden, out Mesh firstPerson)
        {
            firstPerson = null;
            var shader = Shader.Find(GameNames.Shader.HdrpLit);
            if (shader == null) return null;   // too early: asked again at the next goblin
            bool onBody = CosmeticRules.OnBody(m);
            int n = m.VertexCount;
            var verts = new Il2CppStructArray<Vector3>(n);
            var normals = new Il2CppStructArray<Vector3>(n);
            var uv = new Il2CppStructArray<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                var v = Turn(m.Positions[i * 3], m.Positions[i * 3 + 1], m.Positions[i * 3 + 2]) * GameNames.Goblin.MeshUnit;
                var d = Turn(m.Normals[i * 3], m.Normals[i * 3 + 1], m.Normals[i * 3 + 2]);
                verts[i] = onBody ? v : rig.HeadFromMesh.MultiplyPoint3x4(v);
                normals[i] = onBody ? d : rig.HeadFromMesh.MultiplyVector(d).normalized;
                uv[i] = new Vector2(m.Uv[i * 2], 1f - m.Uv[i * 2 + 1]);
            }
            var mesh = new Mesh { name = fullId };
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            if (n > ushort.MaxValue) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uv;
            if (m.HasNormals) mesh.normals = normals;
            mesh.subMeshCount = m.Parts.Count;
            for (int p = 0; p < m.Parts.Count; p++)
            {
                mesh.SetTriangles(Turned(m.Parts[p].Triangles), p);
            }
            if (!m.HasNormals) mesh.RecalculateNormals();
            var materials = new Il2CppReferenceArray<Material>(m.Parts.Select(p => Paint(shader, fullId, p)).ToArray());

            var go = new GameObject(fullId);
            go.transform.SetParent(holder, false);
            Renderer renderer;
            if (onBody)
            {
                var weights = new Il2CppStructArray<BoneWeight>(n);
                for (int i = 0; i < n; i++)
                {
                    int Bone(int k) => m.Weights[i * 4 + k] > 0f ? rig.Index[m.JointNames[m.Joints[i * 4 + k]]] : 0;
                    weights[i] = new BoneWeight
                    {
                        boneIndex0 = Bone(0), weight0 = m.Weights[i * 4], boneIndex1 = Bone(1), weight1 = m.Weights[i * 4 + 1],
                        boneIndex2 = Bone(2), weight2 = m.Weights[i * 4 + 2], boneIndex3 = Bone(3), weight3 = m.Weights[i * 4 + 3],
                    };
                }
                mesh.boneWeights = weights;
                mesh.bindposes = rig.BindPoses;
                var bones = new Il2CppReferenceArray<Transform>(rig.Bones.Count);
                for (int i = 0; i < rig.Bones.Count; i++)
                {
                    var b = new GameObject(rig.Bones[i]);
                    b.transform.SetParent(go.transform, false);
                    bones[i] = b.transform;
                }
                var part = new GameObject(fullId);
                part.transform.SetParent(go.transform, false);
                var skinned = part.AddComponent<SkinnedMeshRenderer>();
                skinned.sharedMesh = mesh;
                skinned.bones = bones;
                skinned.rootBone = bones[rig.Root];
                skinned.updateWhenOffscreen = true;   // its bounds follow the bones: ours are not measured for every animation
                renderer = skinned;
            }
            else
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                renderer = go.AddComponent<MeshRenderer>();
            }
            mesh.RecalculateBounds();
            if (hidden.Count > 0)
            {
                var seen = m.Parts.Select(p => CosmeticRules.Seen(p, m, hidden)).ToList();
                if (seen.Any(s => s.Count > 0))
                {
                    // The same vertices, weights and order of materials (the first one keeps the wearer's colour), fewer triangles.
                    firstPerson = UnityEngine.Object.Instantiate(mesh);
                    firstPerson.name = fullId + " (first person)";
                    firstPerson.hideFlags = HideFlags.DontUnloadUnusedAsset;
                    for (int p = 0; p < seen.Count; p++) firstPerson.SetTriangles(Turned(seen[p]), p);
                }
            }
            renderer.sharedMaterials = materials;
            if (like != null)
            {
                // The game's own parts are on one layer, and the wardrobe's camera draws only that one.
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = like.gameObject.layer;
                renderer.renderingLayerMask = like.renderingLayerMask;
                renderer.shadowCastingMode = like.shadowCastingMode;
            }
            return go;
        }

        private static Il2CppStructArray<int> Turned(List<int> t)
        {
            var tris = new Il2CppStructArray<int>(t.Count);
            for (int i = 0; i < t.Count; i += 3) { tris[i] = t[i]; tris[i + 1] = t[i + 2]; tris[i + 2] = t[i + 1]; }   // the axes' hands differ
            return tris;
        }

        private static Material Paint(Shader shader, string fullId, GlbPart p)
        {
            var m = new Material(shader) { name = $"MOD_{fullId}_{p.MaterialName}" };
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;
            m.SetColor(GameNames.Shader.BaseColor, new Color(p.BaseColor[0], p.BaseColor[1], p.BaseColor[2], 1f).gamma);   // linear in the file, sRGB in the property
            m.SetFloat("_Metallic", Mathf.Clamp01(p.Metallic));
            m.SetFloat("_Smoothness", 1f - Mathf.Clamp01(p.Roughness));
            if (p.Emissive.Any(v => v > 0f)) m.SetColor(GameNames.Shader.EmissiveColor, new Color(p.Emissive[0], p.Emissive[1], p.Emissive[2]));
            if (p.BaseColorImage != null)
            {
                var picture = AssetLoader.LoadTexture(p.BaseColorImage);
                if (picture != null) m.SetTexture(GameNames.Shader.BaseColorMap, picture);
                else MelonLogger.Warning($"[POGLib.Cosmetics] {fullId}: the picture of {p.MaterialName} could not be read.");
            }
            if (p.DoubleSided)
            {
                m.SetFloat("_DoubleSidedEnable", 1f);
                m.SetFloat("_CullMode", 0f);
                m.SetFloat("_CullModeForward", 0f);
                m.SetVector("_DoubleSidedConstants", new Vector4(-1f, -1f, -1f, 0f));   // the back's normal is turned over
                m.EnableKeyword("_DOUBLESIDED_ON");
            }
            return m;
        }
    }
}
