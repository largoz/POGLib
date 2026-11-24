// Outfit parts of our own, put where the game looks parts up (CustomGoblinService.m_partCache), to see whether the game's
// own code wears them. driver.txt: hat=<hat.obj> coat=<coat.obj> (coat.weights.json beside it); a look names them
// "#hat" (an accessory, put on the head) and "#coat" (a chest part, bound to the skeleton by bone name).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class Wearables
    {
        internal static readonly Dictionary<string, uint> Ids = new Dictionary<string, uint> { ["#hat"] = 3900000001u, ["#coat"] = 3900000002u };
        private static GameObject _holder;

        internal static void Register(CustomGoblinService s, GameObject npcPrefab, IDictionary<string, string> cfg, Action<string> log)
        {
            _holder = new GameObject("DriverWearables"); _holder.SetActive(false); UnityEngine.Object.DontDestroyOnLoad(_holder);
            if (cfg.TryGetValue("hat", out var hat)) Put(s, "#hat", Hat(hat), CustomGoblinService.ItemType.Static, log);
            if (cfg.TryGetValue("coat", out var coat)) Put(s, "#coat", Coat(coat, npcPrefab, log), CustomGoblinService.ItemType.Stitched, log);
        }

        private static void Put(CustomGoblinService s, string key, GameObject go, CustomGoblinService.ItemType type, Action<string> log)
        {
            if (go == null) { log($"wearable {key}: not built"); return; }
            var c = go.AddComponent<CosmeticItem>();
            c.m_id = Ids[key]; c.m_itemType = type; c.m_lockType = CustomGoblinService.LockType.Unlocked;
            s.m_partCache[Ids[key]] = c;
            var back = s.GetPartObject(Ids[key]);
            log($"wearable {key}: id {Ids[key]} type {type}; the game finds it: {(back != null ? back.name : "NO")}");
        }

        private static GameObject Hat(string path)
        {
            var m = POGLib.Core.ObjMesh.Load(path); if (m == null) return null;
            var go = new GameObject("Noir_Hat"); go.transform.SetParent(_holder.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = m.Mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = m.Materials;
            return go;
        }

        private static GameObject Coat(string path, GameObject npcPrefab, Action<string> log)
        {
            var m = POGLib.Core.ObjMesh.Load(path); if (m == null) return null;
            var body = npcPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "CH_Goblin_Default");
            if (body == null) { log("wearable #coat: no CH_Goblin_Default under " + npcPrefab.name); return null; }
            var bodyBones = body.bones; var bodyBind = body.sharedMesh.bindposes;
            var index = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < bodyBones.Length; i++) if (bodyBones[i] != null) index[bodyBones[i].name] = i;

            // weights by vertex place, as the modelling script wrote them (OBJ axes; the loader negated z)
            var table = new Dictionary<(int, int, int), List<(int bone, float w)>>();
            using (var doc = JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "coat.weights.json"))))
                foreach (var e in doc.RootElement.EnumerateObject())
                {
                    var p = e.Name.Split(',').Select(x => float.Parse(x, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                    var ws = new List<(int, float)>();
                    foreach (var w in e.Value.EnumerateArray())
                        if (index.TryGetValue(w[0].GetString(), out int b)) ws.Add((b, w[1].GetSingle()));
                        else log("wearable #coat: no bone " + w[0].GetString());
                    table[Q(p[0], p[1], p[2])] = ws;
                }

            var src = m.Mesh.vertices;
            var verts = new Il2CppStructArray<Vector3>(src.Length);
            var weights = new Il2CppStructArray<BoneWeight>(src.Length);
            int missing = 0;
            for (int i = 0; i < src.Length; i++)
            {
                var v = src[i];
                verts[i] = v * 0.01f;          // the goblin's mesh space is 1/100 of a metre
                var bw = new BoneWeight();
                if (table.TryGetValue(Q(v.x, v.y, -v.z), out var ws))
                {
                    if (ws.Count > 0) { bw.boneIndex0 = ws[0].bone; bw.weight0 = ws[0].w; }
                    if (ws.Count > 1) { bw.boneIndex1 = ws[1].bone; bw.weight1 = ws[1].w; }
                    if (ws.Count > 2) { bw.boneIndex2 = ws[2].bone; bw.weight2 = ws[2].w; }
                    if (ws.Count > 3) { bw.boneIndex3 = ws[3].bone; bw.weight3 = ws[3].w; }
                }
                else { missing++; bw.boneIndex0 = index["mixamorig:Spine2"]; bw.weight0 = 1f; }
                weights[i] = bw;
            }
            var mesh = new Mesh { name = "Noir_Coat" };
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            mesh.vertices = verts;
            mesh.normals = m.Mesh.normals;
            mesh.subMeshCount = m.Mesh.subMeshCount;
            for (int i = 0; i < m.Mesh.subMeshCount; i++) mesh.SetTriangles(m.Mesh.GetTriangles(i), i);
            mesh.boneWeights = weights;
            mesh.bindposes = bodyBind;
            mesh.RecalculateBounds();

            // The prefab the game stitches from: a skinned renderer and bones that carry the goblin's bone names.
            var go = new GameObject("Noir_Coat"); go.transform.SetParent(_holder.transform, false);
            var bones = new Il2CppReferenceArray<Transform>(bodyBones.Length);
            for (int i = 0; i < bodyBones.Length; i++)
            {
                var b = new GameObject(bodyBones[i] != null ? bodyBones[i].name : "none");
                b.transform.SetParent(go.transform, false);
                bones[i] = b.transform;
            }
            var part = new GameObject("Noir_Coat_Mesh"); part.transform.SetParent(go.transform, false);
            var smr = part.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.sharedMaterials = m.Materials;
            smr.bones = bones;
            smr.rootBone = bones[body.rootBone != null && index.TryGetValue(body.rootBone.name, out int rb) ? rb : 0];
            smr.updateWhenOffscreen = true;
            log($"wearable #coat: {src.Length} verts, {m.Mesh.subMeshCount} materials [{string.Join(", ", m.MaterialNames)}], without weights {missing}, root bone {smr.rootBone.name}");
            return go;
        }

        private static (int, int, int) Q(float x, float y, float z) => (Mathf.RoundToInt(x * 1e4f), Mathf.RoundToInt(y * 1e4f), Mathf.RoundToInt(z * 1e4f));
    }
}
