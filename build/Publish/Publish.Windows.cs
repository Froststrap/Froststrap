public partial class Build : FalloutBuild
{
    void PublishWindows()
    {
        if (NoInstallers) return;
        PackVelopack();
    }
}
