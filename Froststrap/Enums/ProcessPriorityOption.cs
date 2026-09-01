namespace Froststrap.Enums
{
    internal enum ProcessPriorityOption
    {
        [EnumName(FromTranslation = "Enums.ProcessPriorityOption.Low")]
        Low,

        [EnumName(FromTranslation = "Enums.ProcessPriorityOption.BelowNormal")]
        BelowNormal,

        [EnumName(FromTranslation = "Enums.ProcessPriorityOption.Normal")]
        Normal,

        [EnumName(FromTranslation = "Enums.ProcessPriorityOption.AboveNormal")]
        AboveNormal,

        [EnumName(FromTranslation = "Enums.ProcessPriorityOption.High")]
        High,

        [EnumName(FromTranslation = "Enums.ProcessPriorityOption.RealTime")]
        RealTime
    }
}