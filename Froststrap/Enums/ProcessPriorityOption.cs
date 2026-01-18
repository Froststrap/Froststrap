namespace Froststrap.Enums
{
    public enum ProcessPriorityOption
    {
        [EnumName(StaticName = "Low")]
        Low,

        [EnumName(StaticName = "Below Normal")]
        BelowNormal,

        [EnumName(StaticName = "Normal")]
        Normal,

        [EnumName(StaticName = "Above Normal")]
        AboveNormal,

        [EnumName(StaticName = "High")]
        High,

        [EnumName(StaticName = "Real Time")]
        RealTime
    }
}