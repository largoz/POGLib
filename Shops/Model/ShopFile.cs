using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace POGLib.Shops.Model
{
    /// <summary>
    /// One shop file (pogshop.json): what a mod sells and where. Data contract, versioned by
    /// <see cref="Format"/>; see docs/design/shops.md §8 and SHOPS.md for the modder-facing description.
    /// </summary>
    public sealed class ShopFile
    {
        /// <summary>The format version this library reads and writes.</summary>
        public const int CurrentFormat = 1;

        public int Format { get; set; }
        /// <summary>Owner of every unqualified id in this file.</summary>
        public string Mod { get; set; }
        /// <summary>Mods this file patches (needed for <see cref="Patch"/> on their slots or items).</summary>
        public List<string> Requires { get; set; } = new List<string>();
        /// <summary>New slots this mod places.</summary>
        public List<SlotDef> Slots { get; set; } = new List<SlotDef>();
        /// <summary>Entries this mod adds to any slot (its own, another mod's or a vanilla one).</summary>
        public List<AddOp> Add { get; set; } = new List<AddOp>();
        /// <summary>Changes to entries other content put into a slot (invasive; see <see cref="Requires"/>).</summary>
        public List<PatchOp> Patch { get; set; } = new List<PatchOp>();
        /// <summary>Dungeon merchants this mod adds (a copy of a vanilla merchant preset with its own stock).</summary>
        public List<SellerDef> Sellers { get; set; } = new List<SellerDef>();
        /// <summary>How this mod's own items stand on a shop counter (their standing point).</summary>
        public List<ItemDisplayDef> Items { get; set; } = new List<ItemDisplayDef>();

        /// <summary>Where the file was read from (not serialized).</summary>
        [JsonIgnore] internal string SourcePath { get; set; }
    }

    public sealed class SlotDef
    {
        /// <summary>Local id ("berry_stand"); the full id is "&lt;mod&gt;:&lt;id&gt;".</summary>
        public string Id { get; set; }
        /// <summary>Scene the slot is placed in, e.g. "PitOfGoblin_InBetween".</summary>
        public string Scene { get; set; }
        /// <summary>Seller the slot belongs to (a vanilla seller id); it gives the slot its faction and NPC.</summary>
        public string Seller { get; set; }
        /// <summary>Object the position is relative to (a vanilla seller or slot id). Default: the seller.</summary>
        public string Anchor { get; set; }
        /// <summary>
        /// Metres in the anchor's space. With <see cref="After"/>: a shift from the slot's place in the row, in the slot's
        /// own axes (x sideways, y up, z forward), after it has been stood on the ground.
        /// </summary>
        public float[] Pos { get; set; }
        /// <summary>
        /// Instead of <see cref="Pos"/>: <c>"left"</c> or <c>"right"</c> (the end of the seller's row on that side, as a
        /// customer facing the seller sees it), or a slot id at one end of that row. The new slot goes one step past it
        /// along the row (a second one after the same end a step further), at its height and turn (<see cref="SlotRow"/>).
        /// No coordinates to find; "left"/"right" name no slot of the game, so a game update that renames slots or
        /// reorders the seller's list does not break them.
        /// </summary>
        public string After { get; set; }

        /// <summary>"left" or "right" in <see cref="After"/>: true for right, false for left, null for a slot id.</summary>
        internal static bool? SideOf(string after) =>
            string.Equals(after, "right", StringComparison.OrdinalIgnoreCase) ? true
            : string.Equals(after, "left", StringComparison.OrdinalIgnoreCase) ? false : (bool?)null;
        public float[] Rot { get; set; }
        /// <summary>Price tag offset relative to the slot. Null = where the template slot has it.</summary>
        public TagDef PriceTag { get; set; }
        /// <summary>Vanilla slot to copy the look from (table, tag). Null = the first slot of the seller, or any.</summary>
        public string Template { get; set; }
        /// <summary>Faction for a slot with no seller (a Faction enum name). Ignored when Seller is set.</summary>
        public string Faction { get; set; }
        public List<PoolEntry> Pool { get; set; } = new List<PoolEntry>();
    }

    public sealed class TagDef
    {
        public float[] Pos { get; set; }
        public float[] Rot { get; set; }
    }

    public sealed class PoolEntry
    {
        public string Item { get; set; }
        public double Weight { get; set; } = 1;
    }

    public sealed class AddOp
    {
        public string Slot { get; set; }
        public string Item { get; set; }
        public double Weight { get; set; } = 1;
    }

    /// <summary>Exactly one of <see cref="Weight"/>, <see cref="Remove"/>, <see cref="ReplaceWith"/>.</summary>
    public sealed class PatchOp
    {
        public string Slot { get; set; }
        /// <summary>Which entries: every entry of this item in the slot.</summary>
        public string Item { get; set; }
        public double? Weight { get; set; }
        public bool Remove { get; set; }
        public string ReplaceWith { get; set; }
    }

    public sealed class SellerDef
    {
        /// <summary>Local id of the new merchant.</summary>
        public string Id { get; set; }
        /// <summary>Vanilla merchant preset to copy, e.g. "EntranceCave_Shopkeeper_Curiosa".</summary>
        public string Preset { get; set; }
        /// <summary>Chance weight next to the vanilla merchants and dead adventurers of the entrance cave list.</summary>
        public double Weight { get; set; } = 1;
        /// <summary>Stock per slot of the copy, in the preset's slot order. Missing = keep the preset's stock.</summary>
        public List<List<PoolEntry>> Slots { get; set; } = new List<List<PoolEntry>>();
    }

    /// <summary>
    /// Where an item stands on a counter: the point of the item that a slot puts on its holder (the game's
    /// InventoryItem.m_standingTransform), in the item's own space. Only for the file's own items.
    /// </summary>
    public sealed class ItemDisplayDef
    {
        public string Item { get; set; }
        public TagDef Standing { get; set; }
    }

    internal enum Severity { Info, Warning, Error }

    internal sealed class Diagnostic
    {
        public Severity Severity { get; }
        public string Source { get; }
        public string Message { get; }
        public Diagnostic(Severity severity, string source, string message) { Severity = severity; Source = source; Message = message; }
        public override string ToString() => $"{Severity} [{Source}] {Message}";
    }

    /// <summary>Reads and writes shop files, with checks that name the exact problem for the modder.</summary>
    internal static class ShopFileIO
    {
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };

        private static readonly JsonSerializerOptions WriteOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        // Known keys per object kind, to flag typos ("pirceTag") that JSON would otherwise ignore silently. Taken from the
        // model's own properties, so a key added to the model is known here too (2026-11-21: a hand-kept list missed
        // "after" and warned "typo?" about a valid file).
        private static readonly Dictionary<string, HashSet<string>> KnownKeys = new Dictionary<string, HashSet<string>>
        {
            ["file"] = Keys(typeof(ShopFile)),
            ["display"] = Keys(typeof(ItemDisplayDef)),
            ["slot"] = Keys(typeof(SlotDef)),
            ["tag"] = Keys(typeof(TagDef)),
            ["entry"] = Keys(typeof(PoolEntry)),
            ["add"] = Keys(typeof(AddOp)),
            ["patch"] = Keys(typeof(PatchOp)),
            ["seller"] = Keys(typeof(SellerDef)),
        };

        private static HashSet<string> Keys(Type model) => new HashSet<string>(
            model.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                 .Where(p => p.GetCustomAttributes(typeof(JsonIgnoreAttribute), true).Length == 0)
                 .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name)),
            StringComparer.OrdinalIgnoreCase);

        /// <summary>Parse and check one file. Returns null when the file cannot be used at all.</summary>
        public static ShopFile Parse(string json, string sourcePath, List<Diagnostic> diagnostics)
        {
            ShopFile file;
            try
            {
                using (var doc = JsonDocument.Parse(json, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }))
                    CheckKeys(doc.RootElement, "file", "", sourcePath, diagnostics);
                file = JsonSerializer.Deserialize<ShopFile>(json, ReadOptions);
            }
            catch (JsonException ex)
            {
                diagnostics.Add(new Diagnostic(Severity.Error, sourcePath, $"not valid JSON: {ex.Message}"));
                return null;
            }
            if (file == null) { diagnostics.Add(new Diagnostic(Severity.Error, sourcePath, "empty file")); return null; }
            file.SourcePath = sourcePath;
            file.Requires ??= new List<string>();
            file.Slots ??= new List<SlotDef>();
            file.Add ??= new List<AddOp>();
            file.Patch ??= new List<PatchOp>();
            file.Sellers ??= new List<SellerDef>();
            file.Items ??= new List<ItemDisplayDef>();
            return Validate(file, diagnostics) ? file : null;
        }

        public static string Write(ShopFile file) => JsonSerializer.Serialize(file, WriteOptions);

        private static void CheckKeys(JsonElement e, string kind, string path, string source, List<Diagnostic> diagnostics)
        {
            if (e.ValueKind != JsonValueKind.Object) return;
            foreach (var p in e.EnumerateObject())
            {
                string here = path.Length == 0 ? p.Name : path + "." + p.Name;
                if (!KnownKeys[kind].Contains(p.Name))
                {
                    diagnostics.Add(new Diagnostic(Severity.Warning, source, $"unknown key '{here}' is ignored (typo?)"));
                    continue;
                }
                string child = (kind, p.Name.ToLowerInvariant()) switch
                {
                    ("file", "slots") => "slot",
                    ("file", "add") => "add",
                    ("file", "patch") => "patch",
                    ("file", "sellers") => "seller",
                    ("file", "items") => "display",
                    ("display", "standing") => "tag",
                    ("slot", "pool") => "entry",
                    ("slot", "pricetag") => "tag",
                    _ => null,
                };
                if (child == null) continue;
                if (p.Value.ValueKind == JsonValueKind.Array)
                {
                    int i = 0;
                    foreach (var item in p.Value.EnumerateArray()) CheckKeys(item, child, $"{here}[{i++}]", source, diagnostics);
                }
                else CheckKeys(p.Value, child, here, source, diagnostics);
            }
        }

        /// <summary>Check a parsed file; adds diagnostics and returns false when it cannot be used.</summary>
        /// <summary>
        /// What a mod's own shop file may hold in 0.1 (the user's decisions, 2026-11-19: slots are private, nothing vanilla
        /// is displaced in the first release): its own slots (<c>slots</c>, each with its pool) and how its items stand
        /// (<c>items</c>). <c>add</c>, <c>patch</c>, <c>requires</c> and <c>sellers</c> reach the game's or other mods'
        /// content and stay with the host's own files (UserData/POGLib/Shops) until a later version opens them. A file
        /// that has any of them is refused as a whole, each reason named, so a modder sees it before players do.
        /// </summary>
        internal static bool CheckModScope(ShopFile f, List<Diagnostic> d)
        {
            bool ok = true;
            void Err(string m) { d.Add(new Diagnostic(Severity.Error, f.SourcePath, m)); ok = false; }
            const string Rest = "a mod's shop file may only place its own slots (\"slots\") and say how its items stand (\"items\") in this version; the file is not used";
            if (f.Add?.Count > 0) Err($"\"add\" puts items into slots that are not this mod's ({f.Add.Count} rule(s)): {Rest}. Put the items in a slot of your own instead");
            if (f.Patch?.Count > 0) Err($"\"patch\" changes what others sell ({f.Patch.Count} rule(s)): {Rest}");
            if (f.Requires?.Count > 0) Err($"\"requires\" only serves \"patch\": {Rest}");
            if (f.Sellers?.Count > 0) Err($"\"sellers\" (dungeon merchants) changes the game's list of who stands at the entrance ({f.Sellers.Count} merchant(s)): {Rest}");
            return ok;
        }

        public static bool Validate(ShopFile f, List<Diagnostic> d)
        {
            string src = f.SourcePath ?? f.Mod ?? "?";
            bool ok = true;
            void Err(string m) { d.Add(new Diagnostic(Severity.Error, src, m)); ok = false; }
            void Warn(string m) => d.Add(new Diagnostic(Severity.Warning, src, m));

            if (f.Format == 0) Err("missing \"format\" (use 1)");
            else if (f.Format > ShopFile.CurrentFormat) Err($"format {f.Format} is newer than this POGLib reads ({ShopFile.CurrentFormat}); update POGLib");
            else if (f.Format < 1) Err($"format {f.Format} is not a valid format");
            if (!ShopIds.IsValidModId(f.Mod)) Err($"\"mod\" must be your mod id (letters, digits, _ . -), got '{f.Mod}'");
            if (!ok) return false;

            var slotIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < f.Slots.Count; i++)
            {
                var s = f.Slots[i];
                string at = $"slots[{i}]";
                if (s == null) { Err($"{at} is null"); continue; }
                if (string.IsNullOrEmpty(s.Id) || s.Id.IndexOf(':') >= 0) Err($"{at}.id must be a local id without ':' (got '{s.Id}')");
                else if (!slotIds.Add(s.Id)) Err($"{at}.id '{s.Id}' is used twice");
                if (string.IsNullOrEmpty(s.Scene)) Err($"{at}.scene is required");
                if (!string.IsNullOrEmpty(s.After))
                {
                    if (s.Pos != null && !IsVec3(s.Pos)) Err($"{at}.pos must be [x, y, z]: with \"after\" it moves the slot from its place in the row");
                    if (!string.IsNullOrEmpty(s.Anchor)) Err($"{at} has both \"after\" and \"anchor\": \"after\" places it next to that slot, so leave \"anchor\" out");
                    if (SlotDef.SideOf(s.After) != null && string.IsNullOrEmpty(s.Seller)) Err($"{at}.after \"{s.After}\" is an end of the seller's row, so the slot needs a \"seller\"");
                }
                else
                {
                    if (string.IsNullOrEmpty(s.Seller) && string.IsNullOrEmpty(s.Anchor)) Err($"{at} needs a seller or an anchor to be placed relative to (or \"after\": a slot to stand next to)");
                    if (!IsVec3(s.Pos)) Err($"{at}.pos must be [x, y, z] (or use \"after\": a slot to stand next to)");
                }
                if (s.Rot != null && !IsVec3(s.Rot)) Err($"{at}.rot must be [x, y, z] degrees");
                if (s.PriceTag != null && ((s.PriceTag.Pos != null && !IsVec3(s.PriceTag.Pos)) || (s.PriceTag.Rot != null && !IsVec3(s.PriceTag.Rot))))
                    Err($"{at}.priceTag pos/rot must be [x, y, z]");
                if (s.Pool == null || s.Pool.Count == 0) Warn($"{at} '{s.Id}' has an empty pool: it stays empty unless another mod adds to it");
                else for (int j = 0; j < s.Pool.Count; j++) CheckEntry(s.Pool[j]?.Item, s.Pool[j]?.Weight ?? -1, $"{at}.pool[{j}]", f.Mod, Err);
            }
            for (int i = 0; i < f.Add.Count; i++)
            {
                var a = f.Add[i];
                if (a == null || string.IsNullOrEmpty(a.Slot)) { Err($"add[{i}].slot is required"); continue; }
                CheckEntry(a.Item, a.Weight, $"add[{i}]", f.Mod, Err);
            }
            for (int i = 0; i < f.Patch.Count; i++)
            {
                var p = f.Patch[i];
                if (p == null || string.IsNullOrEmpty(p.Slot) || string.IsNullOrEmpty(p.Item)) { Err($"patch[{i}] needs slot and item"); continue; }
                int kinds = (p.Weight.HasValue ? 1 : 0) + (p.Remove ? 1 : 0) + (!string.IsNullOrEmpty(p.ReplaceWith) ? 1 : 0);
                if (kinds != 1) Err($"patch[{i}] must have exactly one of weight, remove, replaceWith");
                if (p.Weight.HasValue && !IsWeight(p.Weight.Value)) Err($"patch[{i}].weight must be a number >= 0");
            }
            var sellerIds = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < f.Sellers.Count; i++)
            {
                var s = f.Sellers[i];
                if (s == null || string.IsNullOrEmpty(s.Id) || s.Id.IndexOf(':') >= 0) { Err($"sellers[{i}].id must be a local id without ':'"); continue; }
                if (!sellerIds.Add(s.Id)) Err($"sellers[{i}].id '{s.Id}' is used twice");
                if (string.IsNullOrEmpty(s.Preset)) Err($"sellers[{i}].preset is required (a vanilla merchant such as EntranceCave_Shopkeeper_Curiosa)");
                if (!IsWeight(s.Weight)) Err($"sellers[{i}].weight must be a number >= 0");
                if (s.Slots != null)
                    for (int j = 0; j < s.Slots.Count; j++)
                        if (s.Slots[j] != null)
                            for (int k = 0; k < s.Slots[j].Count; k++)
                                CheckEntry(s.Slots[j][k]?.Item, s.Slots[j][k]?.Weight ?? -1, $"sellers[{i}].slots[{j}][{k}]", f.Mod, Err);
            }
            for (int i = 0; i < (f.Items?.Count ?? 0); i++)
            {
                var it = f.Items[i];
                string id = ShopIds.Qualify(it?.Item, f.Mod);
                if (it == null || string.IsNullOrEmpty(it.Item)) { Err($"items[{i}].item is required"); continue; }
                if (ShopIds.OwnerOf(id) != f.Mod) Err($"items[{i}]: '{id}' is not {f.Mod}'s item; a shop file sets how its own items stand only");
                if (it.Standing == null || (it.Standing.Pos != null && !IsVec3(it.Standing.Pos)) || (it.Standing.Rot != null && !IsVec3(it.Standing.Rot)))
                    Err($"items[{i}].standing needs pos and/or rot as [x, y, z]");
            }
            return ok;
        }

        private static void CheckEntry(string item, double weight, string at, string mod, Action<string> err)
        {
            if (string.IsNullOrEmpty(item)) err($"{at}.item is required");
            else if (item != ShopIds.Nothing && !ShopIds.TrySplit(ShopIds.Qualify(item, mod), out _, out _)) err($"{at}.item '{item}' is not an item id");
            if (!IsWeight(weight)) err($"{at}.weight must be a number >= 0");
        }

        private static bool IsVec3(float[] v) => v != null && v.Length == 3 && v.All(x => !float.IsNaN(x) && !float.IsInfinity(x));
        private static bool IsWeight(double w) => w >= 0 && !double.IsNaN(w) && !double.IsInfinity(w);
    }
}
