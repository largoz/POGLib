namespace POGLib.Core
{
    /// <summary>
    /// The library's version, declared once: MelonInfo (Plugin.cs) and the Thunderstore manifest (the publish script
    /// reads this line) take it from here, and POGShops compiles it in to check it is running with the core it was
    /// built for.
    /// </summary>
    internal static class LibraryVersion
    {
        internal const string VERSION = "0.1.1";
    }
}
