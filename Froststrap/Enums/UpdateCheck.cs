namespace Froststrap.Enums
{
    public enum UpdateCheck
    {
        [EnumName(StaticName = "Disabled")]
        Disabled,

        [EnumName(StaticName = "Stable Releases")]
        Stable,

        [EnumName(StaticName = "Pre Releases")]
        Test,

        [EnumName(StaticName = "Both")]
        Both
    }
}