namespace Froststrap.Enums
{
    public enum NavigationViewPaneDisplayMode
    {
        //Made custom enum cuz i dont wanna add left minimal, its buggy with our ui
        [EnumName(FromTranslation = "Common.Auto")]
        Auto,

        [EnumName(FromTranslation = "Enums.NavigationViewPaneDisplayMode.Left")]
        Left,

        [EnumName(FromTranslation = "Enums.NavigationViewPaneDisplayMode.Top")]
        Top,

        [EnumName(FromTranslation = "Enums.NavigationViewPaneDisplayMode.LeftCompact")]
        LeftCompact
    }
}