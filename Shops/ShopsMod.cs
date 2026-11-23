using System;
using System.Reflection;
using HarmonyLib;
using Il2CppGame.Spawning;
using MelonLoader;
using POGLib.Core;
using POGLib.Shops;

[assembly: MelonInfo(typeof(ShopsMod), "POGShops", ShopsMod.VERSION, "largo")]
[assembly: MelonGame(null, "Pit of Goblin")]
// After POGLib (-1000), before content mods (0): a content mod may call ShopsApi.AddFile from its OnInitializeMelon.
[assembly: MelonPriority(-900)]
[assembly: MelonAdditionalDependencies("POGLib")]
// Patched one class at a time (HarmonyPatcher.PatchEachClass), never by MelonLoader's own PatchAll as well (D15).
[assembly: HarmonyDontPatchAll]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("POGLibRuntimeDriver")]
// POG Mod Check: when both players have POGShops, the versions must match (host and client speak "shops.slot"); a
// player without it can still join a host that has it (new slots show, their price tags may sit wrong). The packs
// that sell items are required by themselves (network content through POGLib).
[assembly: System.Reflection.AssemblyMetadata("POGModCheck", "same-version")]

namespace POGLib.Shops
{
    /// <summary>
    /// POGShops: shop slots for mod items, a mod of its own on POGLib. It uses POGLib's internals and is released
    /// together with it, so it starts only with the POGLib major.minor version it was built against; with another one
    /// it says so in the log and stays off (mod items still work, they are just not sold in new slots).
    /// </summary>
    public sealed class ShopsMod : MelonMod
    {
        internal const string VERSION = "0.1.1";
        private const string Tag = "[POGShops]";

        // Compiled in from the POGLib this was built against (a const is copied into this assembly at build time).
        private const string BuiltForCore = LibraryVersion.VERSION;

        // The game members only the shops use; POGLib's own probe checks the rest.
        private static readonly (Type type, string[] members)[] Expected =
        {
            // 2026-11-14: spawners load by address (m_networkPrefab / m_lootTable became references).
            (typeof(ShopLocation), new[] { "m_lootTableRef", "m_itemHolder", "m_priceTagText", "LoadLootTable", "CreateShopItems" }),
            (typeof(SpawnerBase), new[] { "m_networkPrefabRef", "m_loadedPrefab", "LoadNetworkPrefab", "HasNetworkPrefab" }),
        };
        private static readonly (string name, Func<Type> touch)[] CompiledBindings =
        {
            ("ShopLocation", () => typeof(ShopLocation)),
            ("NpcSpawner", () => typeof(NpcSpawner)),
        };

        private bool _on;
        private bool _patchesChecked;

        public override void OnInitializeMelon()
        {
            string core = typeof(POGLib.Plugin).Assembly.GetCustomAttribute<MelonInfoAttribute>()?.Version ?? "?";
            if (MajorMinor(core) != MajorMinor(BuiltForCore))
            {
                MelonLogger.Error($"{Tag} This POGShops {VERSION} is built for POGLib {MajorMinor(BuiltForCore)}.x, but POGLib {core} " +
                                  "is installed: shops stay off (mod items still work). Install the POGShops that matches this POGLib.");
                return;
            }
            if (!CoreServices.Ready)
            {
                MelonLogger.Error($"{Tag} POGLib did not start (see its lines above): shops stay off.");
                return;
            }
            _on = true;
            CompatibilityProbe.Check(Tag, Expected, CompiledBindings);
            PluginRegistry.Add(new ShopsPlugin());
            HarmonyPatcher.PatchEachClass(typeof(ShopsMod).Assembly, HarmonyInstance, Tag);
            POGLib.Shops.Dev.ShopDevTools.Init();
            MelonLogger.Msg($"{Tag} {VERSION} started on POGLib {core}.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!_on || _patchesChecked) return;
            _patchesChecked = true;
            HarmonyPatcher.CheckEachPatchOnce(HarmonyInstance, Tag);
        }

        public override void OnUpdate() { if (_on) POGLib.Shops.Dev.ShopDevTools.OnUpdate(); }

        public override void OnGUI() { if (_on) POGLib.Shops.Dev.ShopDevTools.OnGUI(); }

        private static string MajorMinor(string version)
        {
            var parts = (version ?? "").Split('.');
            return parts.Length >= 2 ? parts[0] + "." + parts[1] : version;
        }
    }
}
