using System.Collections.Generic;

namespace POGLib.Cosmetics
{
    /// <summary>Where a part is worn: the game's own groups in the wardrobe.</summary>
    public enum CosmeticSlot
    {
        Horns,
        Hair,
        Chest,
        Legs,
        Feet,
        /// <summary>Up to six at once.</summary>
        Accessory,
    }

    /// <summary>
    /// A piece of clothing (or hair, horns, a hat) of a mod's own. Every player who has the mod sees it on whoever wears it; a
    /// player without it sees the goblin without that piece.
    /// </summary>
    public sealed class CosmeticDefinition
    {
        public string ModId { get; set; }
        public string ContentId { get; set; }
        public CosmeticSlot Slot { get; set; }

        /// <summary>
        /// The model, a GLB file ("glTF Binary" in Blender's exporter), made around the goblin as it stands in its T-pose: feet
        /// at the origin, facing -Y in Blender, in metres. A model weighted to the goblin's bones moves with the body; one
        /// without weights sits on the head. <see cref="CosmeticSlot.Chest"/>, <see cref="CosmeticSlot.Legs"/> and
        /// <see cref="CosmeticSlot.Feet"/> need weights. The first material takes the wearer's colour (hair, horns, chest and
        /// legs; the game colours no feet and no accessory).
        /// </summary>
        public string ModelFile { get; set; }

        /// <summary>The picture in the wardrobe, a square PNG with a transparent background. The game's own are uncoloured.</summary>
        public string ThumbnailFile { get; set; }

        /// <summary>
        /// False (the default): every player who has the mod finds it in the wardrobe. True: only after
        /// <see cref="CosmeticsApi.Unlock"/> on that player's machine. An NPC can wear it either way.
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// For a <see cref="Locked"/> part, until it is unlocked: false (the default) shows it in the wardrobe with the game's own
        /// padlock, so players know it is there to be found; true keeps it out of the wardrobe (a secret).
        /// </summary>
        public bool HiddenWhileLocked { get; set; }

        /// <summary>
        /// The objects of the model (Blender's object names) that the wearer does not see in first person, such as a high
        /// collar; everyone else sees the part whole. All of them: the wearer sees nothing of it. The game itself draws only chest,
        /// legs and feet in first person, so hair, horns and accessories need no entry here.
        /// </summary>
        public List<string> HiddenInFirstPerson { get; } = new List<string>();

        internal string FullId => ModId + ":" + ContentId;
    }
}
