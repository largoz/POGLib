using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using MelonLoader;
using POGLib.Core;

namespace POGLib.Items
{
    /// <summary>
    /// Public item-registration facade for content packs.
    /// A content mod calls Content.Items.Register(...) / RegisterUseHandler(...) in its own
    /// OnInitializeMelon (which runs AFTER POGLib thanks to MelonPriority).
    /// </summary>
    public sealed class ItemsApi
    {
        internal ItemsApi() { }

        internal ItemsPlugin Plugin;

        private bool Guard()
        {
            if (Plugin != null) return true;
            MelonLogger.Error("[POGLib] Content.Items used before POGLib booted.");
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Register(ModItemDefinition definition) => RegisterFrom(definition, Assembly.GetCallingAssembly());

        private void RegisterFrom(ModItemDefinition definition, Assembly caller)
        {
            ContentOwners.Note(definition?.ModId, caller);
            if (Guard()) Plugin.RegisterItem(definition);
        }

        public void RegisterUseHandler(string handlerId, ModItemUseHandler handler)
        {
            if (Guard()) Plugin.RegisterUseHandler(handlerId, handler);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public void Register(ModItemDefinition definition, string handlerId, ModItemUseHandler handler)
        {
            RegisterFrom(definition, Assembly.GetCallingAssembly());
            RegisterUseHandler(handlerId, handler);
        }
    }

    /// <summary>Root facade for the domain APIs.</summary>
    public static class Content
    {
        public static ItemsApi Items { get; } = new ItemsApi();
        /// <summary>
        /// The assemblies (simple names, e.g. "ShopDemo") that registered network content through this library, plus
        /// POGLib itself once any did. Every player needs these mods, with the same content: a mod-list check
        /// (such as POG Mod Check) reads this to tell "required for everyone" from "own only".
        /// </summary>
        public static IReadOnlyCollection<string> ContentAssemblies => ContentOwners.Assemblies;
        /// <summary>Runtime inspection of vanilla visuals (lights/pulse/emissive). See DiagnosticsApi.</summary>
        internal static DiagnosticsApi Diagnostics { get; } = new DiagnosticsApi();
        // TODO(v2): public static MobsApi Mobs { get; }
        // TODO(v3): public static LevelsApi Levels { get; }
    }
}
