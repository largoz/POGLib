using System;
using HarmonyLib;
using Il2Cpp;
using Il2CppGame.Players;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using POGLib.Core;
using UnityEngine;

namespace POGLib.Cosmetics
{
    /// <summary>A goblin (a player's, an NPC, the one in the menu) is there and not dressed yet.</summary>
    [HarmonyPatch(typeof(EntityCosmetics), nameof(EntityCosmetics.Awake))]
    internal static class Patch_EntityCosmetics_Awake
    {
        private static bool _reported;

        static void Postfix(EntityCosmetics __instance)
        {
            try { if (CosmeticsPlugin.Instance?.Any == true && __instance != null) CosmeticsPlugin.Instance.GoblinWoke(__instance); }
            catch (Exception ex)
            {
                if (!_reported) MelonLogger.Error($"[POGLib.Cosmetics] a goblin woke: {ex}");
                _reported = true;
            }
        }
    }

    [HarmonyPatch(typeof(CustomGoblinService), nameof(CustomGoblinService.OnAwake))]
    internal static class Patch_CustomGoblinService_OnAwake
    {
        static void Postfix(CustomGoblinService __instance)
        {
            try { if (CosmeticsPlugin.Instance?.Any == true && __instance != null) CosmeticsPlugin.Instance.CacheFilled(__instance); }
            catch (Exception ex) { MelonLogger.Error($"[POGLib.Cosmetics] the game's part cache: {ex}"); }
        }
    }

    /// <summary>Every way a part is put on goes through here, the whole look at once too (SetAppearance).</summary>
    [HarmonyPatch(typeof(EntityCosmetics), nameof(EntityCosmetics.SetObject))]
    internal static class Patch_EntityCosmetics_SetObject
    {
        private static bool _reported;

        static void Postfix(EntityCosmetics __instance, CustomGoblinService.Part part, uint id)
        {
            try { if (id != 0 && CosmeticsPlugin.Instance?.Any == true && __instance != null) CosmeticsPlugin.Instance.PutOn(__instance, part, id); }
            catch (Exception ex)
            {
                if (!_reported) MelonLogger.Error($"[POGLib.Cosmetics] a part put on: {ex}");
                _reported = true;
            }
        }
    }

    /// <summary>Only the wardrobe asks for these lists; the game draws random goblins from its arrays without it.</summary>
    [HarmonyPatch(typeof(CustomGoblinService), nameof(CustomGoblinService.GetPartList))]
    internal static class Patch_CustomGoblinService_GetPartList
    {
        private static bool _reported;

        static void Postfix(CustomGoblinService.Category category, ref Il2CppReferenceArray<GameObject> __result)
        {
            try { if (CosmeticsPlugin.Instance?.Any == true) __result = CosmeticsPlugin.Instance.Wardrobe(category, __result); }
            catch (Exception ex)
            {
                if (!_reported) MelonLogger.Error($"[POGLib.Cosmetics] the wardrobe: {ex}");
                _reported = true;
            }
        }
    }
}
