using System;
using System.Reflection;
using System.Text;
using Il2Cpp;
using Il2CppGame.Gameplay;
using Il2CppGame.Players;
using Il2CppI2.Loc;
using Il2CppInventory;
using Il2CppItems;
using MelonLoader;
using Unity.Netcode;

namespace POGLib.Core
{
    /// <summary>
    /// Load-time sanity check. IL2CPP mods break when a game update renames/removes a member the
    /// mod binds to by name; this reflects over the current interop and turns a silent mid-game
    /// NRE (or a PatchAll throw) into one clear, actionable log line naming exactly what is missing.
    /// Run it early in OnInitializeMelon, BEFORE Harmony PatchAll. A sibling mod (Shops) runs it on its own list.
    /// </summary>
    internal static class CompatibilityProbe
    {
        private const BindingFlags Flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        // The load-bearing game members the framework accesses by name or Harmony-patches.
        private static readonly (Type type, string[] members)[] Expected =
        {
            (typeof(InventoryItem), new[]
            {
                "m_itemID", "m_itemType", "m_mapIconType", "m_goldValue", "m_foodValue", "m_shopCost",
                "m_doConsume", "m_isConsumed", "m_itemIcon", "m_spawnOnDestroy", "m_spawnAmount", "m_spawnChance",
                "m_isBigItem", "m_staminaPenaltyOnPickup", "m_movementFactorWhenHeld", // handling / "weight"
                GameNames.Methods.InventoryItem_Interact, GameNames.Methods.InventoryItem_OnNetworkSpawn,
                GameNames.Methods.InventoryItem_StartPrimaryAction,
                "m_itemTooltipID", "m_itemTooltipDescriptionID", "RefreshMeshInfo",
                "Entity", "IsInHands", "ConsumeItem_Owner",
            }),
            // Capability components (EXPERIMENTAL attach API) — scalar fields the Lib sets.
            (typeof(ActiveItem_Eat), new[]
            {
                "m_healthOnEating", "m_damageOnEating", "m_staminaRefill", "m_minStaminaBonus", "m_maxStaminaBonus",
                "m_stomachFullTime", "m_useStaminaMin", "m_eatVfx",
                // 2026-11-06 food rework: stats and buffs instead of hunger.
                "m_bodyValue", "m_staminaValue", "m_strengthValue", "m_applyBuff", "m_buffValueToAdd", "m_buffValueToStart",
            }),
            (typeof(ActiveItem_MeleeWeapon), new[]
                { "m_maxDurability", "m_durabilityLossOnEntityHit", "m_durabilityLossOnDestructibleHit", "m_meleeHitCooldown" }),
            (typeof(ActiveItem_Throwable), new[] { "m_healthMax", "m_healthMin", "m_bounceFactor" }),
            (typeof(LootTable), new[] { "m_treasureItems", "m_foodItems", "m_usefulItems", "m_bigItems", "m_shopItems" }),
            (typeof(InventoryLootTable), new[] { "Items" }),
            (typeof(ItemContainer), new[] { GameNames.Methods.ItemContainer_FromContainerData }),
            (typeof(SpeakingStone), new[] { GameNames.Methods.SpeakingStone_Start, GameNames.Methods.SpeakingStone_SetEnabled }),
            (typeof(LocalizationManager), new[] { GameNames.Methods.LocalizationManager_GetTranslation }),
            // Cosmetics.
            (typeof(CustomGoblinService), new[]
            {
                "m_horns", "m_hairs", "m_chests", "m_legs", "m_feet", "m_accessories", "m_partCache",
                nameof(CustomGoblinService.OnAwake), nameof(CustomGoblinService.GetPartList),
            }),
            (typeof(CosmeticItem), new[] { "m_id", "m_thumbnail", "m_itemType", "m_lockType" }),
            (typeof(EntityCosmetics), new[] { nameof(EntityCosmetics.Awake), nameof(EntityCosmetics.SetObject), "m_currentPartsFP", "m_goblinCosmetics", "m_rigObjectTP", "m_headAnchor_TP" }),
            (typeof(NetworkManager), new[]
            {
                "AddNetworkPrefab", nameof(NetworkManager.StartHost), nameof(NetworkManager.StartClient),
                nameof(NetworkManager.StartServer), nameof(NetworkManager.Shutdown), "NetworkConfig", "CustomMessagingManager",
            }),
            (typeof(NetworkObject), new[] { "GlobalObjectIdHash" }),
            (typeof(NetworkConfig), new[] { "ForceSamePrefabs", "Prefabs" }),
        };

        // Types this DLL references at compile time. A game update can MOVE a type to another assembly or
        // nest/un-nest it while keeping every member name: the by-name checks above still pass, but the
        // DLL's compiled references no longer resolve and whatever touches them fails later (2026-11-06:
        // SteamRuntimeManager moved to the SteamCommon assembly, MapIconType moved out of PassiveItem_Map;
        // the old build said "Compatibility OK" and then three patches failed). Each lambda is its own
        // method, so the runtime resolves its type when the lambda is first called, and a broken reference
        // surfaces here as one exception per type.
        private static readonly (string name, Func<Type> touch)[] CompiledBindings =
        {
            ("InventoryItem", () => typeof(InventoryItem)),
            ("ActiveItem_Eat", () => typeof(ActiveItem_Eat)),
            ("ActiveItem_MeleeWeapon", () => typeof(ActiveItem_MeleeWeapon)),
            ("ActiveItem_Throwable", () => typeof(ActiveItem_Throwable)),
            ("ActiveItem_ReadyItem", () => typeof(ActiveItem_ReadyItem)),
            ("MapIconType", () => typeof(MapIconType)),
            ("ItemType", () => typeof(ItemType)),
            ("LootTable", () => typeof(LootTable)),
            ("InventoryLootTable", () => typeof(InventoryLootTable)),
            ("ItemContainer", () => typeof(ItemContainer)),
            ("SpeakingStone", () => typeof(SpeakingStone)),
            ("LocalizationManager", () => typeof(LocalizationManager)),
            ("NetworkManager", () => typeof(NetworkManager)),
            ("CustomGoblinService", () => typeof(CustomGoblinService)),
            ("CosmeticItem", () => typeof(CosmeticItem)),
            ("EntityCosmetics", () => typeof(EntityCosmetics)),
        };

        /// <summary>Check the library's own members; log a clear warning listing any that are missing.</summary>
        internal static void Check() => Check("[POGLib]", Expected, CompiledBindings);

        /// <summary>Check the given members; log one line either way, naming every missing one.</summary>
        internal static void Check(string tag, (Type type, string[] members)[] expected, (string name, Func<Type> touch)[] compiledBindings)
        {
            var missing = new StringBuilder();
            int miss = 0, total = 0;

            foreach (var (name, touch) in compiledBindings)
            {
                total++;
                try { if (touch() == null) throw new TypeLoadException("resolved to null"); }
                catch (Exception ex)
                {
                    miss++;
                    missing.Append($"\n  - compiled reference to {name} does not resolve ({ex.GetType().Name}: {ex.Message}) " +
                                   "— the game moved or changed this type; this DLL must be rebuilt against the current game");
                }
            }
            foreach (var (type, members) in expected)
            {
                foreach (var m in members)
                {
                    total++;
                    try
                    {
                        if (type.GetMember(m, Flags).Length == 0)
                        {
                            miss++;
                            missing.Append($"\n  - {type.Name}.{m}");
                        }
                    }
                    catch (Exception ex)
                    {
                        miss++;
                        missing.Append($"\n  - {type.Name}.{m} (probe error: {ex.GetType().Name})");
                    }
                }
            }

            if (miss == 0)
            {
                MelonLogger.Msg($"{tag} Compatibility OK ({total} game members resolved).");
            }
            else
            {
                MelonLogger.Warning(
                    $"{tag} COMPATIBILITY: {miss}/{total} expected game members NOT FOUND — the game may " +
                    $"have updated. The framework may misbehave; update GameNames/bindings for this version. Missing:{missing}");
            }
        }
    }
}
