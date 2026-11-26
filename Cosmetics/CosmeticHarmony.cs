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

    /// <summary>The wardrobe decides each button's padlock here, after it has set the button's picture (Initialize).</summary>
    [HarmonyPatch(typeof(Il2CppGame.Menu.CustomizationItem), nameof(Il2CppGame.Menu.CustomizationItem.SetLocked))]
    internal static class Patch_CustomizationItem_SetLocked
    {
        private static bool _reported;

        static void Prefix(Il2CppGame.Menu.CustomizationItem __instance, ref bool __0)
        {
            try
            {
                if (__0 || CosmeticsPlugin.Instance?.Any != true || __instance == null || __instance.m_thumbnail == null
                    || !CosmeticsPlugin.Instance.Padlocked(__instance.m_thumbnail.sprite)) return;
                __0 = true;
                // Among the game's own locked parts, below its divider (the game counts that section after its list is made).
                var menu = __instance.m_menu;
                if (menu != null && menu.m_scrollViewContentLocked != null && __instance.transform.parent != menu.m_scrollViewContentLocked)
                    __instance.transform.SetParent(menu.m_scrollViewContentLocked, false);
            }
            catch (Exception ex)
            {
                if (!_reported) MelonLogger.Error($"[POGLib.Cosmetics] a wardrobe padlock: {ex}");
                _reported = true;
            }
        }
    }

    /// <summary>A padlocked part of ours cannot be put on, whatever the game would let a click do.</summary>
    [HarmonyPatch(typeof(Il2CppGame.Menu.CustomizationItem), nameof(Il2CppGame.Menu.CustomizationItem.OnValueChanged))]
    internal static class Patch_CustomizationItem_OnValueChanged
    {
        static bool Prefix(Il2CppGame.Menu.CustomizationItem __instance, bool __0)
        {
            try
            {
                if (!__0 || __instance == null || !__instance.m_isLocked || CosmeticsPlugin.Instance?.Any != true || __instance.m_thumbnail == null
                    || !CosmeticsPlugin.Instance.Padlocked(__instance.m_thumbnail.sprite)) return true;
                __instance.m_ignoreValueChanged = true;
                if (__instance.m_toggle != null) __instance.m_toggle.isOn = false;
                return false;
            }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib.Cosmetics] a click on a padlocked part: {ex.GetType().Name}: {ex.Message}"); return true; }
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
