using Il2Cpp;
using POGLib.Items;
using POGLib.Shops.Model;
using UnityEngine;

namespace POGLib.Shops
{
    /// <summary>The shop files' standing points ("items": how an item stands in a slot).</summary>
    internal static class ItemDisplays
    {
        /// <summary>Apply the shop files' standing points to the item templates (every peer).</summary>
        public static void Apply(ShopCatalog catalog)
        {
            if (catalog == null) return;
            foreach (var kv in catalog.ItemDisplays)
            {
                var prefab = ShopScene.ResolveItemPrefab(kv.Key);
                var item = prefab != null ? prefab.GetComponent<InventoryItem>() : null;
                if (item == null) continue;   // not built yet; next scene load
                var st = kv.Value.standing;
                StandingPoints.Ensure(item,
                    st?.Pos != null ? new Vector3(st.Pos[0], st.Pos[1], st.Pos[2]) : (Vector3?)null,
                    st?.Rot != null ? new Vector3(st.Rot[0], st.Rot[1], st.Rot[2]) : (Vector3?)null);
            }
        }
    }
}
