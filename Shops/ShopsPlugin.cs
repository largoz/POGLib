using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using POGLib.Core;
using POGLib.Shops.Model;

namespace POGLib.Shops
{
    /// <summary>
    /// Shops domain plugin (docs/design/shops.md). Reads every shop file, folds them into one
    /// <see cref="ShopCatalog"/>, builds the host's templates for new slots (copies of the game's own slot prefab,
    /// nothing new to register), and applies the rules host-side when shop scenes load. Only the host needs the
    /// shop files: clients learn what a new slot is from the host (<see cref="SlotSync"/>).
    ///
    /// Shop files are found:
    ///   • anywhere under Mods/ named "pogshop.json" or "*.pogshop.json" (how mod packages ship them —
    ///     Gale keeps Mods/&lt;Author&gt;-&lt;Name&gt;/…, r2modman nests packages in Mods/&lt;package&gt;/);
    ///   • in UserData/POGLib/Shops/*.json (local files, e.g. the dev tools' export);
    ///   • added from code with ShopsApi.AddFile(...).
    /// The catalog is built on the first scene load, after every mod's OnInitializeMelon has run.
    /// </summary>
    internal sealed class ShopsPlugin : IPOGPlugin
    {
        public string Name => "Shops";
        public string Version => "0.1.0";

        public static ShopsPlugin Instance { get; private set; }

        public ShopCatalog Catalog { get; private set; }
        public IReadOnlyList<ShopFile> Files => _files;
        public IReadOnlyList<Diagnostic> LoadDiagnostics => _loadDiagnostics;

        private readonly List<ShopFile> _codeFiles = new List<ShopFile>();
        private List<ShopFile> _files = new List<ShopFile>();
        private readonly List<Diagnostic> _loadDiagnostics = new List<Diagnostic>();

        public static string UserShopsDirectory => Path.Combine(MelonEnvironment.UserDataDirectory, "POGLib", "Shops");

        public const string PreferencesCategory = "POGLib_Shops";
        private MelonPreferences_Entry<double> _vanillaWeight;

        public void Build(PluginGraph graph)
        {
            Instance = this;
            SlotSync.Register();
            var cat = MelonPreferences.CreateCategory(PreferencesCategory, "POGLib shops");
            _vanillaWeight = cat.CreateEntry("VanillaWeight", ShopCatalogOptions.StandardVanillaWeight, "Vanilla item weight",
                $"When a mod adds items to a game shop slot, each of the game's own items there gets this weight. " +
                $"Mods weigh their items against {ShopCatalogOptions.StandardVanillaWeight:0}: a mod item of weight {ShopCatalogOptions.StandardVanillaWeight:0} " +
                "is as likely as a vanilla one. 0 = only the mods' items in such slots. Applies on the host, from the next scene.");
            MerchantSettings.Init(cat);

            graph.AddSystem(PluginStage.SceneLoaded, Name, () =>
            {
                if (Catalog == null || Catalog.Options.DefaultVanillaWeight != Options().DefaultVanillaWeight) Reload();
                ItemDisplays.Apply(Catalog);
                SlotTemplates.BuildPending(Catalog);
                MerchantStalls.OnSceneLoaded();
                ShopsPage.OnSceneLoaded();
                ShopRuntime.OnSceneLoaded();
            });
            // The game's prefab list (2026-11-14 build): item templates now exist, and the donor slot for new
            // slots may only be in memory from here on.
            graph.AddSystem(PluginStage.GamePrefabsLoaded, Name, () =>
            {
                if (Catalog == null) Reload();
                ItemDisplays.Apply(Catalog);
                SlotTemplates.BuildPending(Catalog);
            });
            // Items are registered by now (they must be, to join a session): a rule naming a missing or
            // unpriced mod item is reported here, before anyone looks for it in a shop.
            graph.AddSystem(PluginStage.BeforeSessionStart, Name, () => ShopChecks.CheckModItems(Catalog));
            graph.AddSystem(PluginStage.SessionStop, Name, () => { ShopRuntime.Reset(); ShopEmptySlots.Reset(); SlotSync.Reset(); });
        }

        private bool _badWeightReported;

        /// <summary>The catalog options from the player's settings (a bad value falls back to the standard one).</summary>
        public ShopCatalogOptions Options()
        {
            double w = _vanillaWeight?.Value ?? ShopCatalogOptions.StandardVanillaWeight;
            if (ShopCatalogOptions.IsValidVanillaWeight(w)) return new ShopCatalogOptions { DefaultVanillaWeight = w };
            if (!_badWeightReported)
            {
                _badWeightReported = true;
                MelonLogger.Warning($"[POGLib] [Shops] {PreferencesCategory}.VanillaWeight = {w} is not a number >= 0; using {ShopCatalogOptions.StandardVanillaWeight:0}.");
            }
            return new ShopCatalogOptions();
        }

        /// <summary>Add a shop file from code (validated like a file on disk). Call from OnInitializeMelon.</summary>
        public bool AddFile(ShopFile file)
        {
            var d = new List<Diagnostic>();
            file.SourcePath ??= "code:" + file.Mod;
            bool ok = ShopFileIO.Validate(file, d) && ShopFileIO.CheckModScope(file, d);   // a file from a mod's code is a mod's file
            Log(d);
            if (!ok) return false;
            _codeFiles.Add(file);
            if (Catalog != null) Reload();
            return true;
        }

        /// <summary>Re-read every file and rebuild the catalog (the dev tools call this after an export).</summary>
        public void Reload()
        {
            _loadDiagnostics.Clear();
            var files = new List<ShopFile>();
            foreach (var path in FindFiles())
            {
                try
                {
                    var f = ShopFileIO.Parse(File.ReadAllText(path), Relative(path), _loadDiagnostics);
                    if (f != null) files.Add(f);
                }
                catch (Exception ex)
                {
                    _loadDiagnostics.Add(new Diagnostic(Severity.Error, Relative(path), $"cannot read: {ex.Message}"));
                }
            }
            // A working copy in UserData/POGLib/Shops (what the shop tools save) replaces that mod's
            // packaged files, so editing a mod's shops never loads the same slots twice.
            string userDir = Path.GetFullPath(UserShopsDirectory);
            // Mods that ship a shop file under Mods/ (before any working copy replaces it).
            var packaged = new HashSet<string>(files.Where(f => !IsUserFile(f, userDir)).Select(f => f.Mod), StringComparer.Ordinal);
            var overridden = new HashSet<string>(files.Where(f => IsUserFile(f, userDir)).Select(f => f.Mod), StringComparer.Ordinal);
            foreach (var mod in overridden)
                foreach (var f in files.Where(f => f.Mod == mod && !IsUserFile(f, userDir)))
                    _loadDiagnostics.Add(new Diagnostic(Severity.Info, f.SourcePath, $"replaced by your working copy in UserData/POGLib/Shops ({mod})"));
            files = files.Where(f => !overridden.Contains(f.Mod) || IsUserFile(f, userDir)).ToList();
            // A mod's file (packaged under Mods/, or its working copy in UserData that replaced one) may hold less than a
            // host's own file (ShopFileIO.CheckModScope); a file in UserData of a mod id with no packaged file is the host's.
            files = files.Where(f => !packaged.Contains(f.Mod) || ShopFileIO.CheckModScope(f, _loadDiagnostics)).ToList();
            files.AddRange(_codeFiles);
            _files = files;
            Catalog = ShopCatalog.Build(files, Options());
            MerchantStalls.Rebuild(Catalog);
            Log(_loadDiagnostics);
            Log(Catalog.Diagnostics);
            MelonLogger.Msg($"[POGLib] [Shops] {files.Count} shop file(s) from {files.Select(f => f.Mod).Distinct().Count()} mod(s): " +
                            $"{Catalog.ModSlots.Count} new slot(s), {Catalog.TargetedSlots.Count()} slot(s) with rules, {MerchantStalls.Classes.Sum(c => c.Merchants.Count)} dungeon merchant(s) in {MerchantStalls.Classes.Count} class(es); order {string.Join(" > ", Catalog.ModOrder)}" +
                            (Catalog.Options.DefaultVanillaWeight != ShopCatalogOptions.StandardVanillaWeight ? $"; vanilla weight {Catalog.Options.DefaultVanillaWeight:0.##} (player setting)" : ""));
        }

        private static bool IsUserFile(ShopFile f, string userDir)
        {
            string full = Path.Combine(MelonEnvironment.GameRootDirectory, f.SourcePath ?? "");
            try { return Path.GetFullPath(full).StartsWith(userDir, StringComparison.OrdinalIgnoreCase) || Path.GetFullPath(f.SourcePath ?? "").StartsWith(userDir, StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        private static IEnumerable<string> FindFiles()
        {
            var found = new List<string>();
            try
            {
                if (Directory.Exists(MelonEnvironment.ModsDirectory))
                    found.AddRange(Directory.EnumerateFiles(MelonEnvironment.ModsDirectory, "*.json", SearchOption.AllDirectories)
                        .Where(p => { var n = Path.GetFileName(p).ToLowerInvariant(); return n == "pogshop.json" || n.EndsWith(".pogshop.json"); }));
                if (Directory.Exists(UserShopsDirectory))
                    found.AddRange(Directory.EnumerateFiles(UserShopsDirectory, "*.json", SearchOption.TopDirectoryOnly));
            }
            catch (Exception ex) { MelonLogger.Warning($"[POGLib] [Shops] searching for shop files: {ex.Message}"); }
            return found.OrderBy(p => p, StringComparer.Ordinal);
        }

        private static string Relative(string path)
        {
            string root = MelonEnvironment.GameRootDirectory;
            return path.StartsWith(root) ? path.Substring(root.Length).TrimStart('/', '\\') : path;
        }

        private static void Log(IEnumerable<Diagnostic> diagnostics)
        {
            foreach (var d in diagnostics)
            {
                string line = $"[POGLib] [Shops] {d.Source}: {d.Message}";
                switch (d.Severity)
                {
                    case Severity.Error: MelonLogger.Error(line); break;
                    case Severity.Warning: MelonLogger.Warning(line); break;
                    default: MelonLogger.Msg(line); break;
                }
            }
        }
    }
}
