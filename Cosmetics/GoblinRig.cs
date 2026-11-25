using System;
using System.Collections.Generic;
using Il2CppGame.Players;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using POGLib.Core;
using POGLib.Cosmetics.Model;
using UnityEngine;

namespace POGLib.Cosmetics
{
    /// <summary>
    /// The goblin's skeleton as its body mesh has it, read from a goblin in the scene: the bones a part may be weighted to, their
    /// bind poses, and where the head anchor is in the mesh's own space.
    /// </summary>
    internal sealed class GoblinRig
    {
        public readonly List<string> Bones = new List<string>();
        public readonly Dictionary<string, int> Index = new Dictionary<string, int>(StringComparer.Ordinal);
        public Il2CppStructArray<Matrix4x4> BindPoses;
        public int Root;
        public Matrix4x4 HeadFromMesh;

        /// <summary>Null when this goblin has no body of the kind the game's clothes are made for.</summary>
        public static GoblinRig From(EntityCosmetics app)
        {
            var rigObject = app != null ? app.m_rigObjectTP : null;
            if (rigObject == null) return null;
            foreach (var r in rigObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r == null || r.name != GameNames.Goblin.Body || r.sharedMesh == null) continue;
                var bones = r.bones;
                var poses = r.sharedMesh.bindposes;
                if (bones == null || poses == null || bones.Length != poses.Length) continue;
                var rig = new GoblinRig();
                var kept = new List<Matrix4x4>();
                Transform head = null;
                Matrix4x4 headPose = Matrix4x4.identity;
                for (int i = 0; i < bones.Length; i++)
                {
                    var b = bones[i];
                    if (b == null || !b.name.StartsWith(CosmeticRules.BonePrefix, StringComparison.Ordinal)) continue;
                    rig.Index[b.name] = rig.Bones.Count;
                    rig.Bones.Add(b.name);
                    kept.Add(poses[i]);
                    if (b.name == GameNames.Goblin.Head) { head = b; headPose = poses[i]; }
                }
                if (head == null || !rig.Index.TryGetValue(GameNames.Goblin.Hips, out rig.Root)) continue;
                rig.BindPoses = new Il2CppStructArray<Matrix4x4>(kept.Count);
                for (int i = 0; i < kept.Count; i++) rig.BindPoses[i] = kept[i];
                // The anchor is the head bone itself on build 26791993; were it a child of it one day, this follows.
                var anchor = app.m_headAnchor_TP;
                rig.HeadFromMesh = anchor == null || anchor.Pointer == head.Pointer
                    ? headPose : (head.worldToLocalMatrix * anchor.localToWorldMatrix).inverse * headPose;
                return rig;
            }
            return null;
        }
    }
}
