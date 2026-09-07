namespace Froststrap;

internal partial class Bootstrapper
{
    public IBootstrapperDialog? Dialog { get; set; }

    private double _progressIncrement;
    private double _taskbarProgressIncrement;
    private double _taskbarProgressMaximum;
    private long _totalDownloadedBytes;
    private long _totalPackagedBytes;

    public void SetStatus(string message)
    {
        message = message.Replace("{product}", AppData.ProductName, StringComparison.Ordinal);
        Dialog?.Message = message;
    }

    private void UpdateProgressBar(bool updateStatus = true)
    {
        long current = Interlocked.Read(ref _totalDownloadedBytes);
        if (Dialog is null) return;

        if (updateStatus)
        {
            SetStatus(string.Format(CultureInfo.InvariantCulture,
                Strings.Bootstrapper_Status_DownloadingPackages,
                FormatBytes(current), FormatBytes(_totalPackagedBytes)));
        }

        int progressValue = (int)Math.Floor(_progressIncrement * current);
        progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);
        Dialog.ProgressValue = progressValue;

        double taskbarProgressValue = _taskbarProgressIncrement * current;
        taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);
        Dialog.TaskbarProgressValue = taskbarProgressValue;
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
