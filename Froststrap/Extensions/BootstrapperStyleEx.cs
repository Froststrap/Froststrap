namespace Froststrap.Extensions
{
    static class BootstrapperStyleEx
    {
        public static async Task<IBootstrapperDialog> GetNew(this BootstrapperStyle bootstrapperStyle) => await Frontend.GetBootstrapperDialog(bootstrapperStyle);

        public static IReadOnlyCollection<BootstrapperStyle> Selections => new BootstrapperStyle[]
        {
            BootstrapperStyle.FluentAeroDialog,
            BootstrapperStyle.FluentDialog,
            BootstrapperStyle.ClassicFluentDialog,
            BootstrapperStyle.ByfronDialog,
            BootstrapperStyle.TwentyFiveDialog,
            BootstrapperStyle.CustomDialog
        };
    }
}