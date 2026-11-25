using System.Collections.Generic;

namespace POGLib.Cosmetics
{
    /// <summary>
    /// What a goblin wears, for <see cref="CosmeticsApi.LookJson"/>. A part is named by a mod's id, "Mod:Part", or by the game's
    /// own name ("CC_Goblin_..."). Null or empty: none. Colours are indexes into the game's palettes.
    /// </summary>
    public sealed class GoblinLook
    {
        public string Horns { get; set; }
        public string Hair { get; set; }
        public string Chest { get; set; }
        public string Legs { get; set; }
        public string Feet { get; set; }
        /// <summary>Six at most, as in the game.</summary>
        public List<string> Accessories { get; } = new List<string>();

        public int HornColor { get; set; }
        public int HairColor { get; set; }
        public int ChestColor { get; set; }
        public int LegsColor { get; set; }
        public int EyeLeft { get; set; }
        public int EyeRight { get; set; }
        public int EyeColor { get; set; }
        public int Skin { get; set; }
    }
}
