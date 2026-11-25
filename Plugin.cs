using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;
using MelonLoader;
using POGLib.Core;
using POGLib.Cosmetics;
using POGLib.Items;

[assembly: MelonInfo(typeof(POGLib.Plugin), "POGLib", POGLib.Core.LibraryVersion.VERSION, "largo")]
[assembly: MelonGame(null, "Pit of Goblin")]
// Load earlier than content mods so their OnInitializeMelon can call Content.*.
[assembly: MelonPriority(-1000)]
// The patches are applied one class at a time (HarmonyPatcher.PatchEachClass); without this MelonLoader patched the
// whole assembly too, and every prefix and postfix ran twice (CheckEachPatchOnce, 2026-11-19: 26 of 26).
[assembly: HarmonyDontPatchAll]
// The mods of this family that are released together with the library reach its internals; nothing else does. A pack
// uses only what is public (README, "What you can call").
[assembly: InternalsVisibleTo("POGShops")]
[assembly: InternalsVisibleTo("POGLibRuntimeDriver")]

namespace POGLib
{
    /// <summary>
    /// POGLib entry point. This is a MelonMod (NOT a MelonPlugin): working with Il2Cpp
    /// types, NetworkManager and component injection is only possible after the game has
    /// initialized — i.e. in OnInitializeMelon and later events, not in loader pre-init.
    ///
    /// Drives PluginRegistry stages from real events:
    ///   OnInitializeMelon        -> Boot
    ///   OnSceneWasLoaded         -> SceneLoaded
    ///   (Harmony on Start*)      -> BeforeSessionStart   [in NetworkLifecycleHooks]
    ///   (Harmony on Shutdown)    -> SessionStop          [in NetworkLifecycleHooks]
    /// </summary>
    public class Plugin : MelonMod
    {
        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[POGLib] Initializing (MelonMod)...");

            // 1) Injected Il2Cpp types — BEFORE any AddComponent<T>.
            try
            {
                ClassInjector.RegisterTypeInIl2Cpp<ModItemHandle>();
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[POGLib] ClassInjector failed: {ex.Message}");
            }

            // 2) Compatibility probe — verify the game members we bind to exist on this build,
            //    BEFORE PatchAll (so a game update surfaces as a clear log line, not a silent break).
            CompatibilityProbe.Check();

            // 3) Shared core services (reachable from the static Harmony patches).
            CoreServices.Init();

            // 4) Built-in domain plugins (each is a separate module). Shops is a mod of its own (POGShops): it adds its
            //    plugin from its own OnInitializeMelon, after this one.
            PluginRegistry.Add(new ItemsPlugin());
            PluginRegistry.Add(new CosmeticsPlugin());
            // TODO(v2): PluginRegistry.Add(new MobsPlugin());
            // TODO(v3): PluginRegistry.Add(new LevelsPlugin());

            // 5) Build the graph and run Boot (register services/handlers).
            PluginRegistry.Boot();

            // 6) Harmony: NGO lifecycle hooks (Start*/Shutdown) + the reward-chest save guard, one class at a time.
            HarmonyPatcher.PatchEachClass(typeof(Plugin).Assembly, HarmonyInstance, Tag);

            MelonLogger.Msg("[POGLib] Boot done. Waiting for scene + session.");
        }

        private const string Tag = "[POGLib]";
        private bool _patchesChecked;

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            if (!_patchesChecked) { _patchesChecked = true; HarmonyPatcher.CheckEachPatchOnce(HarmonyInstance, Tag); }
            // Game shell prefabs are now in memory — build any templates not built yet.
            PluginRegistry.Dispatch(PluginStage.SceneLoaded);
        }
    }
}
