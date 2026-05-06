namespace Froststrap.Extensions
{
    static class CleanerOptionsEx
    {
        public static IReadOnlyCollection<CleanerOptions> Selections =>
        [            
            CleanerOptions.Never,
            CleanerOptions.OneDay,
            CleanerOptions.OneWeek,
            CleanerOptions.OneMonth,
            CleanerOptions.TwoMonths
        ];

    }
}