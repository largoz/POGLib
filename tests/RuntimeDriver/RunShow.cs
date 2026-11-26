// How clothes move: the game's own animations, played at their own speed on a goblin in the coat that runs in a circle on the stage
// (Stage.cs: the king's plate) (driver.txt: runshow=<clip,clip,...>, exact clip names), SegmentSeconds each. Between "runshow: recording" and "runshow: recorded" the window is worth
// filming (cosm.sh does, with ffmpeg; the game's own ScreenCapture cannot be called on this build: ReadOnlySpan).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace POGLib.RuntimeDriver
{
    internal static class RunShow
    {
        internal static bool Wanted, Done;
        static Action<string> _log;
        static List<string> _names;
        static readonly Dictionary<string, AnimationClip> _clips = new Dictionary<string, AnimationClip>();
        static GameObject _who;
        static AnimationClip _playing;
        static int _clip;
        static float _t0, _turn0;
        const float SegmentSeconds = 6f;
        static bool _ready, _told;

        /// <summary>runshowturn=N: the goblin stays on the spot and turns once in N seconds, the clip playing (for a picture of the clothes).</summary>
        internal static float Turn;

        internal static void Init(string clips, Action<string> log) { Wanted = true; _log = log; _names = clips.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList(); }

        static Transform _npc;

        static float Speed(string clip) => clip.Contains("Crouch") ? 1.3f : clip.Contains("Sprint") ? 3.6f : clip.Contains("Slow") ? 1.8f : 2.8f;

        // Every frame, after the game's own animation: the sampled pose stands, and the goblin is where the circle says.
        internal static void LateUpdate()
        {
            if (_playing == null || _who == null || _npc == null) return;
            float s = Time.unscaledTime - _t0, a = s * Speed(_playing.name) / Stage.Radius;
            if (Turn > 0f)
            {
                _npc.position = Stage.Spot;
                _playing.SampleAnimation(_who, Mathf.Repeat(s, _playing.length));
                _who.transform.SetPositionAndRotation(Stage.Spot, Quaternion.LookRotation(Stage.CamDir) * Quaternion.Euler(0f, 360f * (Time.unscaledTime - _turn0) / Turn, 0f));
                return;
            }
            _npc.position = Stage.Spot + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * Stage.Radius;
            _playing.SampleAnimation(_who, Mathf.Repeat(s, _playing.length));
            _who.transform.SetPositionAndRotation(_npc.position, Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a))));
        }

        static bool Prepare()
        {
            foreach (var c in Resources.FindObjectsOfTypeAll<AnimationClip>()) if (c != null && _names.Contains(c.name)) _clips[c.name] = c;
            uint coat = POGLib.Items.Content.Cosmetics.IdOf("POGBlackMarket:Coat");
            foreach (var g in Outfit.Made)
            {
                if (g.Npc == null || coat == 0 || g.Npc.CosmeticsData.ChestId != coat) continue;
                var a = g.Npc.GetComponentsInChildren<Animator>(true).FirstOrDefault(x => x.avatar != null && x.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(r => r.name == "CH_Goblin_Default"));
                if (a == null) continue;
                _npc = g.Npc.transform; _who = a.gameObject; a.enabled = false;
                break;
            }
            if (_who != null && !Stage.Ready(_log)) _who = null;
            _names = _names.Where(_clips.ContainsKey).ToList();
            _log($"runshow: goblin {(_who != null ? _npc.name : "none")}, spot {Stage.Spot:F1}; clips {string.Join(", ", _names.Select(n => $"{n} {_clips[n].length:0.00} s"))}");
            return _who != null && _names.Count > 0;
        }

        // Every bone's place in the rig, clip by clip, PoseSteps moments each (driver.txt: posedump=<Windows path>): to fit clothes to the
        // game's own movements outside the game. The file holds the game's animation: it stays on this machine.
        const int PoseSteps = 16;
        internal static string PoseDump;

        static void Dump()
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder("{");
            for (int i = 0; i < _names.Count; i++)
            {
                var clip = _clips[_names[i]];
                sb.Append(i > 0 ? "," : "").Append('"').Append(_names[i]).Append("\":[");
                for (int k = 0; k < PoseSteps; k++)
                {
                    clip.SampleAnimation(_who, clip.length * k / PoseSteps);
                    var inv = _who.transform.worldToLocalMatrix;
                    sb.Append(k > 0 ? "," : "").Append('{');
                    bool first = true;
                    foreach (var b in _who.GetComponentsInChildren<Transform>(true))
                    {
                        if (!b.name.StartsWith("mixamorig:") || b.name.EndsWith("_end")) continue;
                        var m = inv * b.localToWorldMatrix;
                        sb.Append(first ? "" : ",").Append('"').Append(b.name).Append("\":[");
                        for (int r = 0; r < 4; r++) for (int col = 0; col < 4; col++) sb.Append(r + col > 0 ? "," : "").Append(m[r, col].ToString("R", c));
                        sb.Append(']');
                        first = false;
                    }
                    sb.Append('}');
                }
                sb.Append(']');
            }
            System.IO.File.WriteAllText(PoseDump, sb.Append('}').ToString());
            _log($"runshow: poses to {PoseDump}");
        }

        static int _fp;
        static SkinnedMeshRenderer _fpChest;
        static Mesh _less;
        static int Triangles(Mesh m) { int n = 0; for (int i = 0; m != null && i < m.subMeshCount; i++) n += (int)m.GetIndexCount(i) / 3; return n; }

        // What the player sees of their own clothes, when they came into the game in them (not dressed by a test afterwards: the game
        // puts the own third-person parts on a layer the own camera does not draw, in SetAppearance only).
        static float FirstPerson(Action<Vector3, Quaternion> camera, Action free)
        {
            switch (_fp++)
            {
                case 0:
                {
                    free();
                    Stage.BringMe();
                    Il2CppEntities.EntityPlayer me = null;
                    foreach (var p in UnityEngine.Object.FindObjectsOfType<Il2CppEntities.EntityPlayer>()) if (p != null && p.IsOwner) me = p;
                    var e = me != null ? me.GetComponentInChildren<Il2CppGame.Players.EntityCosmetics>(true) : null;
                    for (int pt = 0; e != null && pt <= 18; pt++)
                        foreach (var (which, d) in new[] { ("TP", e.m_currentPartsTP), ("FP", e.m_currentPartsFP) })
                            if (d != null && d.ContainsKey((Il2Cpp.CustomGoblinService.Part)pt)) { var go = d[(Il2Cpp.CustomGoblinService.Part)pt]; if (go != null) { var sm = go.GetComponent<SkinnedMeshRenderer>(); _log($"fp: {which} {(Il2Cpp.CustomGoblinService.Part)pt} = {go.name}, layer {LayerMask.LayerToName(go.layer)}{(sm != null ? $", mesh '{sm.sharedMesh?.name}' drawn {sm.enabled}, triangles {Triangles(sm.sharedMesh)}" : "")}"); if (which == "FP" && pt == 4) _fpChest = sm; } }
                    return 2f;
                }
                case 1: _log("shot fp_ahead"); return 4f;
                case 2: Down(camera); return 1.5f;
                case 3: _log("shot fp_down"); return 4f;
                // The same with nothing hidden, to see what the hiding is worth.
                case 4:
                {
                    free();
                    var whole = UnityEngine.Resources.FindObjectsOfTypeAll<Mesh>().FirstOrDefault(m => m != null && m.name == "POGBlackMarket:Coat");
                    if (_fpChest != null && whole != null) { _less = _fpChest.sharedMesh; _fpChest.sharedMesh = whole; _log($"fp: the whole coat, {Triangles(whole)} triangles instead of {Triangles(_less)}"); }
                    return 1.5f;
                }
                case 5: _log("shot fp_whole_ahead"); return 4f;
                case 6: Down(camera); return 1.5f;
                case 7: _log("shot fp_whole_down"); return 4f;
                default: free(); Done = true; _log("runshow: done"); return -1;
            }
        }

        static void Down(Action<Vector3, Quaternion> camera)
        {
            var eye = Camera.main.transform;
            camera(eye.position, Quaternion.LookRotation((eye.forward * 0.45f + Vector3.down).normalized, eye.forward));
        }

        /// <summary>Seconds to wait, or -1 when it is over.</summary>
        internal static float Tick(Action<Vector3, Quaternion> camera, Action free)
        {
            if (!_ready)
            {
                _ready = true;
                if (!Prepare()) { Done = true; return -1; }
                if (PoseDump != null) { Dump(); Done = true; _log("runshow: done"); return -1; }
                return 0.2f;
            }
            if (_clip >= _names.Count) { if (_playing != null) _log("runshow: recorded"); _playing = null; return FirstPerson(camera, free); }
            _playing = _clips[_names[_clip]];
            var eye = Turn > 0f ? Stage.Spot + Stage.CamDir * 2.1f + Vector3.up * 0.75f : Stage.Spot + Stage.CamDir * (Stage.Radius + 1.9f) + Vector3.up * 1.3f;
            camera(eye, Quaternion.LookRotation(Stage.Spot + Vector3.up * (Turn > 0f ? 0.5f : 0.42f) - eye));
            if (_clip == 0 && !_told) { _told = true; _t0 = _turn0 = Time.unscaledTime; _log("runshow: recording"); return 3f; }
            _log($"runshow: segment {_names[_clip]}");
            _t0 = Time.unscaledTime;
            _clip++;
            return Turn > 0f ? Turn : SegmentSeconds;
        }
    }
}
