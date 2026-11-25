using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using MelonLoader;
using POGLib.Shops.Model;
using POGMods.Config;

namespace POGLib.Shops
{
    /// <summary>
    /// The optional POG Config page: the ceiling for all mods' dungeon merchants and a row per mod. Everything that touches POG Config's
    /// types is behind a NoInlining call, so POGShops loads and works without it. Shown when a mod has merchants; registered once
    /// every mod has started, and again when the classes change.
    /// </summary>
    internal static class ShopsPage
    {
        private const string Page = "POG Shops";
        // The steps both kinds of row go by, in percent.
        private static readonly int[] Steps = { 0, 5, 10, 15, 20, 25, 30, 40, 50, 60, 75, 100 };

        private static bool _started, _absent;
        private static bool _capturing;   // POG Config takes each row's value at registration as the one RESTORE DEFAULTS goes back to

        public static void OnSceneLoaded()
        {
            if (_started) return;
            _started = true;
            Changed();
        }

        public static void Changed()
        {
            if (!_started || _absent || MerchantStalls.Classes.Count == 0) return;
            _capturing = true;
            try { Register(); }
            catch (Exception ex) when (ex is FileNotFoundException || ex is TypeLoadException || ex is MissingMethodException)
            {
                _absent = true;
                MelonLogger.Msg("[POGShops] POG Config not installed: the host's settings for dungeon merchants are in UserData/MelonPreferences.cfg [POGLib_Shops] and [POGLib_Shops_Merchants].");
            }
            finally { _capturing = false; }
        }

        private static string Percent(double share) => $"{100 * share:0.#}%";

        private static int Nearest(double share) =>
            Enumerable.Range(0, Steps.Length).OrderBy(i => Math.Abs(Steps[i] / 100.0 - share)).First();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register()
        {
            var steps = Steps.Select(s => s + "%").ToArray();
            var rows = new List<ConfigEntry>
            {
                new HeaderEntry("Dungeon merchants (host)"),
                new OptionsSliderEntry("All mods, at most",
                    () => Nearest(_capturing ? MerchantDraw.StandardCeiling : MerchantSettings.Ceiling),
                    i => MerchantSettings.Ceiling = Steps[i] / 100.0, steps),
            };
            foreach (var c in MerchantStalls.Classes)
            {
                string mod = c.ModId;
                var options = new[] { $"as the mod asks ({Percent(c.Chance)})" }.Concat(steps.Select((s, i) => i == 0 ? "off" : s)).ToArray();
                rows.Add(new OptionsSliderEntry(c.Name,
                    () => _capturing || MerchantSettings.HostChance(mod) == MerchantSettings.AsTheModAsks ? 0 : 1 + Nearest(MerchantSettings.HostChance(mod)),
                    i => MerchantSettings.SetHostChance(mod, i == 0 ? MerchantSettings.AsTheModAsks : Steps[i - 1] / 100.0), options));
            }
            POGConfig.Register(Page, rows, new ModInfo(ShopsMod.VERSION,
                "How often a mod's merchant keeps a dungeon merchant's stall instead of the game's own. The mods' chances add up, never past the " +
                "first line; the rest of the stalls stay the game's. Your setting counts when you host, from the next dungeon.", "largo"));
        }
    }
}
