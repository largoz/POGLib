using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using MelonLoader;

namespace POGLib.Core
{
    /// <summary>
    /// How POGLib and its sibling mods (Shops) apply their Harmony patches. Each such assembly says
    /// <c>[assembly: HarmonyDontPatchAll]</c> and calls <see cref="PatchEachClass"/> from its OnInitializeMelon, then
    /// <see cref="CheckEachPatchOnce"/> at the first scene load.
    /// </summary>
    internal static class HarmonyPatcher
    {
        /// <summary>
        /// Apply every [HarmonyPatch] class in <paramref name="assembly"/> one at a time. PatchAll aborts the whole batch
        /// on the first failure, which once cost every patch in the assembly because of one bad parameter name; one at a
        /// time, a patch the current game build rejects turns off that one feature only. The log names the class and
        /// the whole chain of reasons (Harmony wraps the real one two or three levels deep behind "IL Compile Error").
        /// </summary>
        internal static void PatchEachClass(Assembly assembly, HarmonyLib.Harmony harmony, string tag)
        {
            int ok = 0, failed = 0;
            foreach (var type in assembly.GetTypes())
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try
                {
                    new PatchClassProcessor(harmony, type).Patch();
                    ok++;
                }
                catch (Exception ex)
                {
                    failed++;
                    var reason = new System.Text.StringBuilder();
                    for (var e = ex; e != null; e = e.InnerException)
                        reason.Append("\n    ").Append(e.GetType().Name).Append(": ").Append(e.Message);
                    MelonLogger.Error($"{tag} Harmony patch FAILED for {type.Name}:{reason}");
                }
            }

            if (failed == 0) MelonLogger.Msg($"{tag} Harmony: {ok} patch class(es) applied.");
            else MelonLogger.Warning($"{tag} Harmony: {ok} applied, {failed} FAILED (those features are off; the rest work).");
        }

        /// <summary>
        /// Every patch must run once per call of the game's method. MelonLoader patches a mod's whole assembly by itself
        /// unless the assembly opts out, and <see cref="PatchEachClass"/> patches it again: then each prefix and postfix
        /// ran twice (found 2026-11-19: a client asked the host twice about one slot). Call it at the first scene, when
        /// every patching has happened; the log says either way.
        /// </summary>
        internal static void CheckEachPatchOnce(HarmonyLib.Harmony harmony, string tag)
        {
            try
            {
                int methods = 0, twice = 0;
                foreach (var original in harmony.GetPatchedMethods())
                {
                    var info = HarmonyLib.Harmony.GetPatchInfo(original);
                    if (info == null) continue;
                    var mine = info.Prefixes.Concat(info.Postfixes).Concat(info.Transpilers).Concat(info.Finalizers)
                        .Where(p => p.owner == harmony.Id).ToList();
                    if (mine.Count == 0) continue;
                    methods++;
                    foreach (var g in mine.GroupBy(p => p.PatchMethod).Where(g => g.Count() > 1))
                    {
                        twice++;
                        MelonLogger.Warning($"{tag} Harmony: {original.DeclaringType?.Name}.{original.Name} has {g.Key.DeclaringType?.Name}.{g.Key.Name} {g.Count()} times.");
                    }
                }
                MelonLogger.Msg($"{tag} Harmony: {methods} game method(s) patched; {(twice == 0 ? "each patch once" : $"{twice} patch(es) more than once")}.");
            }
            catch (Exception ex) { MelonLogger.Warning($"{tag} Harmony: could not check the patches: {ex.Message}"); }
        }
    }
}
