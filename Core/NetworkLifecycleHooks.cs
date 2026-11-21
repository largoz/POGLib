using System;
using HarmonyLib;
using MelonLoader;
using Unity.Netcode;

namespace POGLib.Core
{
    /// <summary>
    /// NGO lifecycle Harmony hooks. A prefix on Start* registers the mod prefabs before the session
    /// starts; a postfix on Shutdown resets session-scoped state so a rejoin registers content again.
    /// Telling players that their mods differ is POG Mod Check's job (a mod of its own), not this library's.
    /// </summary>
    internal static class NetworkLifecycleHooks
    {
        internal static void OnBeforeSessionStart()
        {
            if (!CoreServices.Ready) return;
            // The game normally loads its prefab list just before this (and GamePrefabsLoaded has built the
            // mod templates); make sure of it, so registration below never runs with nothing built.
            if (!GamePrefabs.IsLoaded && GamePrefabs.EnsureLoaded()) OnGamePrefabsLoaded();
            CoreServices.Network.RegisterAll();
            PluginRegistry.Dispatch(PluginStage.BeforeSessionStart);
        }

        private static int _dispatchedList;

        /// <summary>The game's prefab list is loaded: run GamePrefabsLoaded once per loaded list object.</summary>
        internal static void OnGamePrefabsLoaded()
        {
            if (!CoreServices.Ready) return;
            var list = GamePrefabs.Loaded;
            if (list == null) return;
            int id = list.GetInstanceID();
            if (id == _dispatchedList) return;
            _dispatchedList = id;
            MelonLogger.Msg($"[POGLib] The game's network prefab list is loaded ({list.List?.Count} entries).");
            PluginRegistry.Dispatch(PluginStage.GamePrefabsLoaded);
        }

        /// <summary>The session has its NetworkManager set up: the library's messages can be registered.</summary>
        internal static void OnSessionStarted()
        {
            if (!CoreServices.Ready) return;
            CoreServices.Bridge?.Hook();
        }

        internal static void OnSessionStop()
        {
            if (!CoreServices.Ready) return;
            CoreServices.Bridge?.Unhook();
            CoreServices.Network.Reset();
            PluginRegistry.Dispatch(PluginStage.SessionStop);
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartHost))]
    internal static class Patch_StartHost
    {
        static void Prefix() => NetworkLifecycleHooks.OnBeforeSessionStart();
        static void Postfix() { try { NetworkLifecycleHooks.OnSessionStarted(); } catch (Exception ex) { MelonLogger.Warning($"[POGLib] After StartHost: {ex.Message}"); } }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartServer))]
    internal static class Patch_StartServer
    {
        static void Prefix() => NetworkLifecycleHooks.OnBeforeSessionStart();
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartClient))]
    internal static class Patch_StartClient
    {
        static void Prefix() => NetworkLifecycleHooks.OnBeforeSessionStart();
        static void Postfix() { try { NetworkLifecycleHooks.OnSessionStarted(); } catch (Exception ex) { MelonLogger.Warning($"[POGLib] After StartClient: {ex.Message}"); } }
    }

    [HarmonyPatch(typeof(Il2CppGame.Networking.NetworkHandler), nameof(Il2CppGame.Networking.NetworkHandler.EnsureNetworkPrefabsRegistered))]
    internal static class Patch_EnsureNetworkPrefabsRegistered
    {
        static void Postfix()
        {
            try { NetworkLifecycleHooks.OnGamePrefabsLoaded(); }
            catch (Exception ex) { MelonLogger.Error($"[POGLib] GamePrefabsLoaded failed: {ex}"); }
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.Shutdown))]
    internal static class Patch_Shutdown
    {
        static void Postfix() => NetworkLifecycleHooks.OnSessionStop();
    }
}
