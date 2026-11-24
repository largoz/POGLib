// The goblin's skeleton and meshes as one JSON file, to model clothes around it in Blender (driver.txt: rigdump=<path>,
// a Windows path, e.g. Z:\run\media\...\rig.json; taken from the first goblin Outfit.cs made). The file holds the
// game's own geometry: it stays on this machine, never in a repository.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Il2CppEntities;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class RigDump
    {
        static string N(float f) => float.IsFinite(f) ? f.ToString("R", CultureInfo.InvariantCulture) : "0";
        static string S(string s) => "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        static string V(Vector3 v) => $"[{N(v.x)},{N(v.y)},{N(v.z)}]";
        static string Q(Quaternion q) => $"[{N(q.x)},{N(q.y)},{N(q.z)},{N(q.w)}]";
        static string M(Matrix4x4 m)
        {
            var sb = new StringBuilder("[");
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) { if (r + c > 0) sb.Append(','); sb.Append(N(m[r, c])); }
            return sb.Append(']').ToString();
        }

        internal static void Write(NPCEntity npc, string path, Action<string> log)
        {
            var app = npc.Appearance;
            var root = app.m_rigObjectTP.transform;
            var sb = new StringBuilder("{\n");
            sb.Append("\"rigRoot\":").Append(S(PathOf(root, npc.transform))).Append(",\n");
            sb.Append("\"rigRootWorld\":").Append(M(root.localToWorldMatrix)).Append(",\n");
            var anchor = app.m_headAnchor_TP;
            sb.Append("\"headAnchor\":").Append(anchor != null ? S(PathOf(anchor, npc.transform)) : "null").Append(",\n");
            if (anchor != null) sb.Append("\"headAnchorWorld\":").Append(M(anchor.localToWorldMatrix)).Append(",\n");
            sb.Append("\"transforms\":[\n");
            bool first = true;
            foreach (var t in npc.GetComponentsInChildren<Transform>(true))
            {
                if (!first) sb.Append(",\n"); first = false;
                sb.Append($"{{\"path\":{S(PathOf(t, npc.transform))},\"pos\":{V(t.localPosition)},\"rot\":{Q(t.localRotation)},\"scale\":{V(t.localScale)},\"world\":{M(t.localToWorldMatrix)},\"active\":{(t.gameObject.activeInHierarchy ? "true" : "false")}}}");
            }
            sb.Append("],\n\"meshes\":[\n");
            first = true;
            foreach (var r in npc.GetComponentsInChildren<Renderer>(true))
            {
                var smr = r.TryCast<SkinnedMeshRenderer>();
                Mesh mesh = smr != null ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                if (!first) sb.Append(",\n"); first = false;
                sb.Append($"{{\"name\":{S(r.name)},\"path\":{S(PathOf(r.transform, npc.transform))},\"mesh\":{S(mesh.name)},\"active\":{(r.gameObject.activeInHierarchy && r.enabled ? "true" : "false")},\"readable\":{(mesh.isReadable ? "true" : "false")},\"world\":{M(r.transform.localToWorldMatrix)},");
                var mat = r.sharedMaterial;
                sb.Append($"\"material\":{S(mat != null ? mat.name : "")},\"shader\":{S(mat != null && mat.shader != null ? mat.shader.name : "")},\"subMeshes\":{mesh.subMeshCount},\"vertexCount\":{mesh.vertexCount}");
                if (smr != null)
                {
                    sb.Append(",\"rootBone\":").Append(S(smr.rootBone != null ? smr.rootBone.name : "")).Append(",\"bones\":[");
                    var bones = smr.bones;
                    for (int i = 0; i < bones.Length; i++) sb.Append(i > 0 ? "," : "").Append(S(bones[i] != null ? bones[i].name : ""));
                    sb.Append("],\"bindposes\":[");
                    var bp = mesh.bindposes;
                    for (int i = 0; i < bp.Length; i++) sb.Append(i > 0 ? "," : "").Append(M(bp[i]));
                    sb.Append(']');
                }
                try
                {
                    Mesh src = mesh;
                    bool baked = false;
                    if (!mesh.isReadable && smr != null) { src = new Mesh(); smr.BakeMesh(src); baked = true; }
                    if (src.isReadable)
                    {
                        sb.Append($",\"baked\":{(baked ? "true" : "false")},\"v\":[");
                        var vs = src.vertices;
                        for (int i = 0; i < vs.Length; i++) sb.Append(i > 0 ? "," : "").Append(V(vs[i]));
                        sb.Append("],\"t\":[");
                        var ts = src.triangles;
                        for (int i = 0; i < ts.Length; i++) sb.Append(i > 0 ? "," : "").Append(ts[i]);
                        sb.Append(']');
                        if (!baked && smr != null)
                        {
                            sb.Append(",\"w\":[");
                            var ws = src.boneWeights;
                            for (int i = 0; i < ws.Length; i++)
                            {
                                var w = ws[i];
                                sb.Append(i > 0 ? "," : "").Append($"[{w.boneIndex0},{N(w.weight0)},{w.boneIndex1},{N(w.weight1)},{w.boneIndex2},{N(w.weight2)},{w.boneIndex3},{N(w.weight3)}]");
                            }
                            sb.Append(']');
                        }
                    }
                }
                catch (Exception ex) { sb.Append(",\"error\":").Append(S(ex.GetType().Name + ": " + ex.Message)); }
                sb.Append('}');
            }
            sb.Append("]\n}\n");
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            File.WriteAllText(path, sb.ToString());
            log($"rigdump: {new FileInfo(path).Length} bytes to {path}");
        }

        static string PathOf(Transform t, Transform stop)
        {
            var parts = new List<string>();
            for (var x = t; x != null && x != stop; x = x.parent) parts.Add(x.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
