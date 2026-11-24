using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using UnityEngine;

namespace POGLib.Core
{
    /// <summary>
    /// Builds a Unity <see cref="Mesh"/> from a Wavefront OBJ file at runtime — a custom model with no
    /// AssetBundle and no Unity editor.
    ///
    /// An AssetBundle is only unavoidable for data Unity's EDITOR authors: rigs and animation clips,
    /// particle systems configured in the inspector, custom shaders. A static prop is none of those —
    /// it is vertices, normals, UVs and triangle indices, all of which can be handed to the engine
    /// directly. Materials are built on the game's own <c>HDRP/Lit</c>, which is already loaded.
    ///
    /// OBJ is the interchange format on purpose: every DCC tool exports it, so a modder can ship a
    /// model without owning the game's exact Unity version (2022.3.62f2) — which is what the bundle
    /// path demands. Textures ride along as loose PNGs (see <see cref="AssetLoader"/>).
    ///
    /// Coordinate handling: OBJ is right-handed and Y-up; Unity is left-handed and Y-up. The loader
    /// negates Z on positions and normals, which converts handedness, and turns the triangles over so
    /// that their outsides stay outside (<see cref="ObjWinding"/>). Export from Blender with Y up / -Z
    /// forward (the exporter's default) and the model arrives upright, unmirrored and correctly facing.
    /// </summary>
    internal static class ObjMesh
    {
        /// <summary>
        /// Objects in the OBJ whose name marks them as collision geometry rather than something to
        /// draw. <c>UCX_</c> is the long-standing convention across engines; the other two are accepted
        /// because people reasonably expect them to work.
        /// </summary>
        private static readonly string[] CollisionPrefixes = { "UCX_", "UBX_", "USP_" };
        private static readonly string[] CollisionWords = { "collision", "collider" };

        /// <summary>A parsed model: one mesh with a submesh per material, in file order.</summary>
        public sealed class Model
        {
            public Mesh Mesh;
            public Il2CppReferenceArray<Material> Materials;
            /// <summary>Material names in submesh order — useful for per-slot effects.</summary>
            public string[] MaterialNames;

            /// <summary>
            /// Convex collision hulls found in the file (objects named <c>UCX_*</c>). Empty when the
            /// model ships none, in which case the item's existing collider is refitted to the mesh
            /// bounds instead — see <c>ItemVisuals.ApplyColliders</c>.
            /// </summary>
            public Mesh[] CollisionMeshes = new Mesh[0];
        }

        private static bool IsCollisionName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var p in CollisionPrefixes)
                if (name.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
            foreach (var w in CollisionWords)
                if (name.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        /// <summary>
        /// Vertex accumulator. Collision hulls need their OWN buffer, not a slice of the render mesh's:
        /// PhysX cooks a convex hull from a mesh's whole vertex array, not just the vertices its
        /// triangles reference, so sharing one buffer would produce a hull wrapping the entire model.
        /// </summary>
        private sealed class Buffer
        {
            internal readonly Dictionary<string, int> Index = new Dictionary<string, int>(StringComparer.Ordinal);
            internal readonly List<Vector3> Pos = new List<Vector3>();
            internal readonly List<Vector3> Nor = new List<Vector3>();
            internal readonly List<Vector2> Uv = new List<Vector2>();
            internal readonly List<int> Tris = new List<int>();
        }

        private static readonly Dictionary<string, Model> Cache = new Dictionary<string, Model>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Description of one MTL entry, mapped onto HDRP/Lit when the material is built.</summary>
        private sealed class MtlEntry
        {
            public Color BaseColor = Color.white;
            public Color Emissive = Color.black;
            public float Metallic;
            public float Smoothness = 0.5f;
            /// <summary>Pr (roughness) is authoritative; Ns is only a fallback for files without it.</summary>
            public bool SawRoughness;
        }

        /// <summary>
        /// Load an OBJ (and its .mtl, if referenced) as a ready-to-use mesh + materials. Results are
        /// cached per path: repeated item builds reuse one mesh instead of leaking a copy each time.
        /// Returns null and logs the reason on failure.
        /// </summary>
        public static Model Load(string objPath)
        {
            if (string.IsNullOrEmpty(objPath)) return null;
            if (Cache.TryGetValue(objPath, out var cached) && cached != null && cached.Mesh != null)
                return cached;

            if (!File.Exists(objPath))
            {
                MelonLogger.Error($"[POGLib] OBJ not found: {objPath}");
                return null;
            }

            try
            {
                var model = Parse(objPath);
                if (model != null) Cache[objPath] = model;
                return model;
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[POGLib] OBJ load failed for {objPath}: {ex}");
                return null;
            }
        }

        private static Model Parse(string objPath)
        {
            var positions = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();

            // Deduplicated output vertices, keyed by the OBJ "v/vt/vn" index triple.
            var vertexIndex = new Dictionary<string, int>(StringComparer.Ordinal);
            var outPos = new List<Vector3>();
            var outNor = new List<Vector3>();
            var outUv = new List<Vector2>();

            var groups = new List<KeyValuePair<string, List<int>>>();   // material name -> triangles
            List<int> current = null;
            string mtlLib = null;
            bool sawNormals = false;

            // Collision objects, each with its own vertex buffer, keyed in file order.
            var hulls = new List<KeyValuePair<string, Buffer>>();
            Buffer collision = null;    // non-null while parsing a UCX_ object

            foreach (var raw in File.ReadLines(objPath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;

                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "o":
                    case "g":
                    {
                        string objName = line.Length > 2 ? line.Substring(2).Trim() : "";
                        if (IsCollisionName(objName))
                        {
                            collision = new Buffer();
                            hulls.Add(new KeyValuePair<string, Buffer>(objName, collision));
                        }
                        else collision = null;
                        break;
                    }
                    case "v":
                        // Negate Z: right-handed OBJ -> left-handed Unity.
                        positions.Add(new Vector3(F(parts, 1), F(parts, 2), -F(parts, 3)));
                        break;
                    case "vn":
                        normals.Add(new Vector3(F(parts, 1), F(parts, 2), -F(parts, 3)));
                        sawNormals = true;
                        break;
                    case "vt":
                        uvs.Add(new Vector2(F(parts, 1), F(parts, 2)));
                        break;
                    case "mtllib":
                        mtlLib = line.Substring(6).Trim();
                        break;
                    case "usemtl":
                        current = GroupFor(groups, line.Substring(6).Trim());
                        break;
                    case "f":
                        if (collision != null)
                        {
                            EmitFace(parts, positions, normals, uvs,
                                     collision.Index, collision.Pos, collision.Nor, collision.Uv, collision.Tris);
                            break;
                        }
                        if (current == null) current = GroupFor(groups, "default");
                        EmitFace(parts, positions, normals, uvs, vertexIndex, outPos, outNor, outUv, current);
                        break;
                }
            }

            if (outPos.Count == 0)
            {
                MelonLogger.Error($"[POGLib] OBJ has no geometry: {objPath}");
                return null;
            }

            // ---- build the Unity mesh -----------------------------------------------------------
            var mesh = new Mesh { name = Path.GetFileNameWithoutExtension(objPath) };
            // A runtime-created asset is owned by nothing in the scene graph; without this flag an
            // UnloadUnusedAssets sweep can take the mesh out from under the item.
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;

            var verts = new Il2CppStructArray<Vector3>(outPos.Count);
            for (int i = 0; i < outPos.Count; i++) verts[i] = outPos[i];
            mesh.vertices = verts;

            if (outUv.Count == outPos.Count)
            {
                var uvArr = new Il2CppStructArray<Vector2>(outUv.Count);
                for (int i = 0; i < outUv.Count; i++) uvArr[i] = outUv[i];
                mesh.uv = uvArr;
            }

            mesh.subMeshCount = groups.Count;
            for (int g = 0; g < groups.Count; g++)
            {
                var tris = groups[g].Value;
                var arr = new Il2CppStructArray<int>(tris.Count);
                for (int i = 0; i < tris.Count; i++) arr[i] = tris[i];
                mesh.SetTriangles(arr, g);
            }

            if (sawNormals && outNor.Count == outPos.Count)
            {
                var norArr = new Il2CppStructArray<Vector3>(outNor.Count);
                for (int i = 0; i < outNor.Count; i++) norArr[i] = outNor[i];
                mesh.normals = norArr;
            }
            else
            {
                mesh.RecalculateNormals();
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            // ---- materials ----------------------------------------------------------------------
            var mtl = mtlLib != null
                ? ParseMtl(Path.Combine(Path.GetDirectoryName(objPath) ?? ".", mtlLib))
                : new Dictionary<string, MtlEntry>(StringComparer.Ordinal);

            var shader = Shader.Find(GameNames.Shader.HdrpLit);
            if (shader == null)
                MelonLogger.Warning($"[POGLib] '{GameNames.Shader.HdrpLit}' not found — OBJ materials will be untextured defaults.");

            var materials = new Il2CppReferenceArray<Material>(groups.Count);
            var names = new string[groups.Count];
            for (int g = 0; g < groups.Count; g++)
            {
                names[g] = groups[g].Key;
                mtl.TryGetValue(groups[g].Key, out var entry);
                materials[g] = BuildMaterial(shader, groups[g].Key, entry ?? new MtlEntry());
            }

            // ---- collision hulls ------------------------------------------------------------------
            var collisionMeshes = new List<Mesh>();
            foreach (var pair in hulls)
            {
                if (pair.Value.Tris.Count == 0) continue;
                var hull = new Mesh { name = pair.Key };
                hull.hideFlags = HideFlags.DontUnloadUnusedAsset;

                var hv = new Il2CppStructArray<Vector3>(pair.Value.Pos.Count);
                for (int i = 0; i < pair.Value.Pos.Count; i++) hv[i] = pair.Value.Pos[i];
                hull.vertices = hv;

                var ht = new Il2CppStructArray<int>(pair.Value.Tris.Count);
                for (int i = 0; i < pair.Value.Tris.Count; i++) ht[i] = pair.Value.Tris[i];
                hull.SetTriangles(ht, 0);
                hull.RecalculateBounds();
                collisionMeshes.Add(hull);
            }

            int triCount = 0;
            foreach (var g in groups) triCount += g.Value.Count / 3;
            var summary = $"[POGLib] OBJ loaded '{mesh.name}': {outPos.Count} verts, {triCount} tris, " +
                          $"{groups.Count} submesh(es) [{string.Join(", ", names)}]";
            if (collisionMeshes.Count > 0)
            {
                summary += $"; {collisionMeshes.Count} collision hull(s) [";
                for (int i = 0; i < collisionMeshes.Count; i++)
                    summary += (i > 0 ? ", " : "") + collisionMeshes[i].name;
                summary += "]";
            }
            MelonLogger.Msg(summary + ".");

            return new Model
            {
                Mesh = mesh,
                Materials = materials,
                MaterialNames = names,
                CollisionMeshes = collisionMeshes.ToArray(),
            };
        }

        private static Material BuildMaterial(Shader shader, string name, MtlEntry e)
        {
            var m = shader != null ? new Material(shader) : new Material(Shader.Find("Standard"));
            m.name = $"MOD_{name}";
            m.hideFlags = HideFlags.DontUnloadUnusedAsset;

            if (m.HasProperty(GameNames.Shader.BaseColor)) m.SetColor(GameNames.Shader.BaseColor, e.BaseColor);
            else if (m.HasProperty(GameNames.Shader.Color)) m.SetColor(GameNames.Shader.Color, e.BaseColor);

            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", e.Metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", e.Smoothness);
            else if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", e.Smoothness);

            if (e.Emissive.maxColorComponent > 0f)
            {
                if (m.HasProperty(GameNames.Shader.EmissiveColor))
                    m.SetColor(GameNames.Shader.EmissiveColor, e.Emissive);
                else if (m.HasProperty(GameNames.Shader.EmissionColor))
                {
                    m.SetColor(GameNames.Shader.EmissionColor, e.Emissive);
                    m.EnableKeyword(GameNames.Shader.EmissionKeyword);
                }
            }
            return m;
        }

        private static Dictionary<string, MtlEntry> ParseMtl(string path)
        {
            var result = new Dictionary<string, MtlEntry>(StringComparer.Ordinal);
            if (!File.Exists(path))
            {
                MelonLogger.Warning($"[POGLib] MTL not found next to the OBJ: {path} — using default materials.");
                return result;
            }

            MtlEntry current = null;
            foreach (var raw in File.ReadLines(path))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                switch (parts[0])
                {
                    case "newmtl":
                        current = new MtlEntry();
                        result[line.Substring(6).Trim()] = current;
                        break;
                    case "Kd":
                        if (current != null) current.BaseColor = new Color(F(parts, 1), F(parts, 2), F(parts, 3), current.BaseColor.a);
                        break;
                    case "Ke":
                        if (current != null) current.Emissive = new Color(F(parts, 1), F(parts, 2), F(parts, 3));
                        break;
                    case "d":
                        if (current != null) current.BaseColor.a = F(parts, 1);
                        break;
                    case "Pm":   // PBR extension: metallic
                        if (current != null) current.Metallic = F(parts, 1);
                        break;
                    case "Pr":   // PBR extension: roughness -> HDRP wants smoothness
                        if (current != null)
                        {
                            current.Smoothness = Mathf.Clamp01(1f - F(parts, 1));
                            current.SawRoughness = true;
                        }
                        break;
                    case "Ns":   // fallback when Pr is absent: specular exponent, 0..1000
                        if (current != null && !current.SawRoughness)
                            current.Smoothness = Mathf.Clamp01(Mathf.Sqrt(Mathf.Max(0f, F(parts, 1)) / 1000f));
                        break;
                }
            }
            return result;
        }

        private static List<int> GroupFor(List<KeyValuePair<string, List<int>>> groups, string name)
        {
            for (int i = 0; i < groups.Count; i++)
                if (groups[i].Key == name) return groups[i].Value;
            var list = new List<int>();
            groups.Add(new KeyValuePair<string, List<int>>(name, list));
            return list;
        }

        /// <summary>Triangulate an n-gon face as a fan and deduplicate its vertices.</summary>
        private static void EmitFace(string[] parts, List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
                                     Dictionary<string, int> vertexIndex, List<Vector3> outPos, List<Vector3> outNor,
                                     List<Vector2> outUv, List<int> triangles)
        {
            int corners = parts.Length - 1;
            if (corners < 3) return;

            Span<int> resolved = corners <= 16 ? stackalloc int[corners] : new int[corners];
            bool named = true;   // every corner names a normal
            for (int c = 0; c < corners; c++)
            {
                resolved[c] = ResolveVertex(parts[c + 1], positions, normals, uvs, vertexIndex, outPos, outNor, outUv);
                var bits = parts[c + 1].Split('/');
                int ni = Idx(bits, 2, normals.Count);
                named &= ni >= 0 && ni < normals.Count;
            }

            for (int c = 1; c < corners - 1; c++)
            {
                int i0 = resolved[0], i1 = resolved[c], i2 = resolved[c + 1];
                bool turn = ObjWinding.MustTurn(Xyz(outPos[i0]), Xyz(outPos[i1]), Xyz(outPos[i2]),
                                                named ? Xyz(outNor[i0] + outNor[i1] + outNor[i2]) : null);
                triangles.Add(i0);
                triangles.Add(turn ? i2 : i1);
                triangles.Add(turn ? i1 : i2);
            }
        }

        private static int ResolveVertex(string token, List<Vector3> positions, List<Vector3> normals, List<Vector2> uvs,
                                         Dictionary<string, int> vertexIndex, List<Vector3> outPos, List<Vector3> outNor,
                                         List<Vector2> outUv)
        {
            if (vertexIndex.TryGetValue(token, out int existing)) return existing;

            var bits = token.Split('/');
            int vi = Idx(bits, 0, positions.Count);
            int ti = Idx(bits, 1, uvs.Count);
            int ni = Idx(bits, 2, normals.Count);

            outPos.Add(vi >= 0 && vi < positions.Count ? positions[vi] : Vector3.zero);
            outUv.Add(ti >= 0 && ti < uvs.Count ? uvs[ti] : Vector2.zero);
            outNor.Add(ni >= 0 && ni < normals.Count ? normals[ni] : Vector3.up);

            int index = outPos.Count - 1;
            vertexIndex[token] = index;
            return index;
        }

        private static float[] Xyz(Vector3 v) => new[] { v.x, v.y, v.z };

        /// <summary>OBJ indices are 1-based, and negative values count back from the end.</summary>
        private static int Idx(string[] bits, int slot, int count)
        {
            if (slot >= bits.Length || bits[slot].Length == 0) return -1;
            if (!int.TryParse(bits[slot], NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return -1;
            return v > 0 ? v - 1 : count + v;
        }

        private static float F(string[] parts, int i)
        {
            if (i >= parts.Length) return 0f;
            return float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }
    }
}
