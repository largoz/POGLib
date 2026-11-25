using System;
using System.Collections.Generic;
using MelonLoader;
using POGLib.Shops.Model;
using UnityEngine;

namespace POGLib.Shops
{
    /// <summary>
    /// The host's word on dungeon merchants, in MelonPreferences: the ceiling for all mods together and one chance per
    /// mod. The single source of truth with or without POG Config (its page only reads and writes these). Read when a stall is
    /// decided, so a change works from the next dungeon.
    /// </summary>
    internal static class MerchantSettings
    {
        private const string ClassCategory = "POGLib_Shops_Merchants";
        /// <summary>The saved value for "as the mod asks": a mod that changes what it asks is followed.</summary>
        public const double AsTheModAsks = -1;
        // A dragged slider changes the value many times a second; write the file once it settles.
        private const float SaveDelay = 1f;

        private static MelonPreferences_Category _shops, _classes;
        private static MelonPreferences_Entry<double> _ceiling;
        private static readonly Dictionary<string, MelonPreferences_Entry<double>> _byMod = new Dictionary<string, MelonPreferences_Entry<double>>(StringComparer.Ordinal);
        private static readonly HashSet<string> _reported = new HashSet<string>(StringComparer.Ordinal);
        private static float _saveAt;

        public static void Init(MelonPreferences_Category shops)
        {
            _shops = shops;
            _ceiling = shops.CreateEntry("MerchantCeiling", MerchantDraw.StandardCeiling, "Mod merchants, at most",
                "Host: the most of the dungeon merchants' stalls all mods together may take, 0 to 1. The mods' chances add up to it; past it all shrink in proportion.");
            _classes = MelonPreferences.CreateCategory(ClassCategory, "POGLib dungeon merchants");
        }

        public static void Add(MerchantClass c)
        {
            if (_byMod.ContainsKey(c.ModId)) return;
            // A dot in a key would be a table in the file. Two ids that differ only by it share the line.
            string key = c.ModId.Replace('.', '_');
            _byMod[c.ModId] = _classes.GetEntry<double>(key) ?? _classes.CreateEntry(key, AsTheModAsks, c.Name,
                $"Host: how often a merchant of {c.Name} keeps a stall, 0 (off) to 1; {AsTheModAsks} = as the mod asks.");
        }

        public static double Ceiling
        {
            get
            {
                double v = _ceiling?.Value ?? MerchantDraw.StandardCeiling;
                if (MerchantDraw.IsShare(v)) return v;
                if (_reported.Add("ceiling")) MelonLogger.Warning($"[POGLib] [Shops] MerchantCeiling = {v} is not from 0 to 1; using {MerchantDraw.StandardCeiling}.");
                return MerchantDraw.StandardCeiling;
            }
            set => Set(_ceiling, value);
        }

        /// <summary>The host's setting for a mod, <see cref="AsTheModAsks"/> when there is none.</summary>
        public static double HostChance(string modId)
        {
            double v = _byMod.TryGetValue(modId, out var e) ? e.Value : AsTheModAsks;
            if (MerchantDraw.IsShare(v) || v == AsTheModAsks) return v;
            if (_reported.Add(modId)) MelonLogger.Warning($"[POGLib] [Shops] {ClassCategory}.{modId} = {v} is not from 0 to 1 (or {AsTheModAsks}); as the mod asks.");
            return AsTheModAsks;
        }

        public static void SetHostChance(string modId, double value) { if (_byMod.TryGetValue(modId, out var e)) Set(e, value); }

        /// <summary>The chance a class goes into the draw with.</summary>
        public static double ChanceOf(MerchantClass c)
        {
            double host = HostChance(c.ModId);
            return host == AsTheModAsks ? c.Chance : host;
        }

        private static void Set(MelonPreferences_Entry<double> e, double value)
        {
            if (e == null || e.Value == value) return;
            e.Value = value;
            _saveAt = Time.unscaledTime + SaveDelay;
        }

        public static void SaveWhenSettled()
        {
            if (_saveAt == 0f || Time.unscaledTime < _saveAt) return;
            _saveAt = 0f;
            _shops.SaveToFile(false);
            _classes.SaveToFile(false);
        }
    }
}
