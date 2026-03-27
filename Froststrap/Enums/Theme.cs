namespace Froststrap.Enums
{
    public enum Theme
    {
        [EnumName(FromTranslation = "Common.SystemDefault")]
        Default,
        Dark,
        Light,
        [EnumName(FromTranslation = "Common.Custom")]
        Custom
    }
}
