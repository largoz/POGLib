namespace POGLib.Items
{
    /// <summary>
    /// Recommended shells. Diamond is the default (has InventoryItem+NetworkObject+Rigidbody,
    /// no VoIP/VFX/spawnOnDestroy). SpeakingStone is deliberately NOT offered as a shell —
    /// it drags voice/VFX and m_spawnOnDestroy (infinite-spawn risk).
    /// </summary>
    public enum ItemShellKind
    {
        Diamond,     // default
        Coin,
        Gem,
        /// <summary>A big item, carried in both hands (the item's <c>Big</c> picks it on its own).</summary>
        BigItem,
    }
}
