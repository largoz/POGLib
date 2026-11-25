using MelonLoader;

namespace POGLib.Cosmetics
{
    /// <summary>
    /// Clothes, hair, horns and hats of mods: <c>Content.Cosmetics</c>. Register in your own OnInitializeMelon. Only the look is
    /// added: a player without your mod sees the goblin without your part, and nothing of it enters the game's own lists.
    /// </summary>
    public sealed class CosmeticsApi
    {
        internal CosmeticsApi() { }

        internal CosmeticsPlugin Plugin;

        private bool Guard()
        {
            if (Plugin != null) return true;
            MelonLogger.Error("[POGLib] Content.Cosmetics used before POGLib booted.");
            return false;
        }

        /// <summary>False when it is refused; the log says why. The files are read now, so a fault in them shows at the start.</summary>
        public bool Register(CosmeticDefinition definition) => Guard() && Plugin.Register(definition);

        /// <summary>
        /// The number the game keeps in a look for this part ("Mod:Part"), the same on every machine; 0 when no such part is
        /// registered. Compare it with a look's <c>ChestId</c>, <c>HairId</c>, ... to see who wears it.
        /// </summary>
        public uint IdOf(string fullId) => Guard() ? Plugin.IdOf(fullId) : 0u;

        /// <summary>
        /// A locked part ("Mod:Part") is in this player's wardrobe from now on, kept in UserData/POGLib/Cosmetics.json. True when
        /// it was not there before.
        /// </summary>
        public bool Unlock(string fullId) => Guard() && Plugin.Unlock(fullId);

        /// <summary>Whether this player finds the part in the wardrobe (always true for one that is not locked).</summary>
        public bool IsUnlocked(string fullId) => Guard() && Plugin.IsUnlocked(fullId);

        /// <summary>
        /// The look as the game stores it for an NPC (<c>NPCEntity.m_predefinedCosmeticsJson</c>, or the look of a POGShops
        /// merchant). A part that is not there is left out and named in the log. Null while the game's parts are not loaded.
        /// </summary>
        public string LookJson(GoblinLook look) => Guard() ? Plugin.LookJson(look) : null;
    }
}
