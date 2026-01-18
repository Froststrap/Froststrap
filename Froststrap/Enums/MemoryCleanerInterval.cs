namespace Froststrap.Enums
{
    public enum MemoryCleanerInterval
    {
        [EnumName(StaticName = "Never")]
        Never,

        [EnumName(StaticName = "10 minutes")]
        TenMinutes,

        [EnumName(StaticName = "15 minutes")]
        FifteenMinutes,

        [EnumName(StaticName = "20 minutes")]
        TwentyMinutes,

        [EnumName(StaticName = "30 minutes")]
        ThirtyMinutes,

        [EnumName(StaticName = "1 hour")]
        OneHour
    }
}