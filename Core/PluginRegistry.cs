using System;
using System.Collections.Generic;
using MelonLoader;

namespace POGLib.Core
{
    /// <summary>
    /// Registry of domain plugins + stage driver. The entry point (Plugin.cs) calls Boot()
    /// once, then Dispatch(stage) from the relevant MelonLoader/NGO events.
    /// </summary>
    internal static class PluginRegistry
    {
        private static readonly List<IPOGPlugin> _plugins = new();
        private static PluginGraph _graph;
        private static bool _booted;

        /// <summary>
        /// Add a plugin. Before Boot() it joins the others; after it (a mod of its own, such as Shops, adds its plugin
        /// from its own OnInitializeMelon, which runs after POGLib's), it is built into the running graph and its Boot
        /// systems run at once. The later stages reach it like any other plugin.
        /// </summary>
        public static void Add(IPOGPlugin plugin)
        {
            if (plugin == null) return;
            foreach (var p in _plugins)
                if (p.Name == plugin.Name)
                {
                    MelonLogger.Warning($"[POGLib] A plugin named '{plugin.Name}' is already registered; the second one is ignored.");
                    return;
                }
            _plugins.Add(plugin);
            MelonLogger.Msg($"[POGLib] Registered plugin: {plugin.Name} v{plugin.Version}");
            if (!_booted) return;
            try { plugin.Build(_graph); }
            catch (Exception ex)
            {
                MelonLogger.Error($"[POGLib] Plugin '{plugin.Name}' Build() threw: {ex}");
                return;
            }
            _graph.RunStage(PluginStage.Boot, plugin.Name);
        }

        /// <summary>Build the graph from all plugins and run the Boot stage.</summary>
        public static void Boot()
        {
            if (_booted) return;
            _booted = true;
            _graph = new PluginGraph();

            foreach (var plugin in _plugins)
            {
                try { plugin.Build(_graph); }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[POGLib] Plugin '{plugin.Name}' Build() threw: {ex}");
                }
            }

            _graph.RunStage(PluginStage.Boot);
            MelonLogger.Msg($"[POGLib] Boot complete ({_plugins.Count} plugin(s)).");
        }

        /// <summary>Run a lifecycle stage (SceneLoaded / BeforeSessionStart / SessionStop).</summary>
        public static void Dispatch(PluginStage stage)
        {
            if (!_booted || _graph == null) return;
            _graph.RunStage(stage);
        }

        /// <summary>The graph (for external resource access).</summary>
        public static PluginGraph Graph => _graph;
    }
}
