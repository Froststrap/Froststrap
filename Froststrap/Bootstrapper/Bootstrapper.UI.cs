// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

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
        if (Dialog is null) return;

        long current = Math.Max(0, Interlocked.Read(ref _totalDownloadedBytes));
        long total = Interlocked.Read(ref _totalPackagedBytes);

        if (updateStatus && total > 0)
        {
            SetStatus(string.Format(CultureInfo.InvariantCulture,
                Strings.Bootstrapper_Status_DownloadingPackages,
                FormatBytes(Math.Min(current, total)), FormatBytes(total)));
        }

        if (double.IsFinite(_progressIncrement))
        {
            int progressValue = (int)Math.Floor(_progressIncrement * current);
            progressValue = Math.Clamp(progressValue, 0, ProgressBarMaximum);
            Dialog.ProgressValue = progressValue;
        }

        if (double.IsFinite(_taskbarProgressIncrement))
        {
            double taskbarProgressValue = _taskbarProgressIncrement * current;
            taskbarProgressValue = Math.Clamp(taskbarProgressValue, 0, _taskbarProgressMaximum);
            Dialog.TaskbarProgressValue = taskbarProgressValue;
        }
    }

    private void RecalculateProgressIncrements()
    {
        long total = Interlocked.Read(ref _totalPackagedBytes);

        _taskbarProgressMaximum = TaskbarProgressMaximum;

        if (total <= 0)
        {
            _progressIncrement = 0;
            _taskbarProgressIncrement = 0;
            return;
        }

        _progressIncrement = (double)ProgressBarMaximum / total;
        _taskbarProgressIncrement = _taskbarProgressMaximum / total;
    }

    private void AddProgressTotal(long bytes)
    {
        if (bytes <= 0) return;

        Interlocked.Add(ref _totalPackagedBytes, bytes);
        RecalculateProgressIncrements();
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