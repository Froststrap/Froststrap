namespace Froststrap.Extensions
{
    static class BootstrapperStyleEx
    {
        public static async Task<IBootstrapperDialog> GetNew(this BootstrapperStyle bootstrapperStyle) => await Frontend.GetBootstrapperDialog(bootstrapperStyle);

        public static IReadOnlyCollection<BootstrapperStyle> Selections => new BootstrapperStyle[]
        {
            BootstrapperStyle.FroststrapDialog,
            BootstrapperStyle.FluentAeroDialog,
            BootstrapperStyle.FluentDialog,
            BootstrapperStyle.ClassicFluentDialog,
            BootstrapperStyle.ByfronDialog,
            BootstrapperStyle.ProgressDialog,
            BootstrapperStyle.LegacyDialog2011,
            BootstrapperStyle.LegacyDialog2008,
            BootstrapperStyle.VistaDialog,
            BootstrapperStyle.CustomDialog
        };
    }
}