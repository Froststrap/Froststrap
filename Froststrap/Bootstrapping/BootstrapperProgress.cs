// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Contracts;

namespace Froststrap.Bootstrapping;

internal sealed class BootstrapperProgress(Func<IBootstrapperDialog?> dialog, Func<string> productName) : IPackageReporter
{
    private const int ProgressBarMaximum = 10000;
    private const double TaskbarProgressMaximum = 1.0;

    public bool ExtractionSucceeded { get; private set; } = true;

    private IBootstrapperDialog? Dialog => dialog();

    private double _progressIncrement;
    private double _taskbarProgressIncrement;
    private double _taskbarProgressMaximum;
    private long _totalDownloadedBytes;
    private long _totalPackagedBytes;

    void IInstallReporter.SetIndeterminate(bool includeTaskbar)
    {
        Dialog?.ProgressIndeterminate = true;

        if (includeTaskbar)
            Dialog?.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
    }

    void IInstallReporter.SetProgress(double percent)
    {
        if (Dialog is null)
            return;

        Dialog.ProgressIndeterminate = false;
        Dialog.ProgressMaximum = 100;
        Dialog.ProgressValue = (int)percent;
        Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
        Dialog.TaskbarProgressValue = percent / 100.0;
    }

    void IInstallReporter.ClearProgress()
    {
        if (Dialog is null)
            return;

        Dialog.ProgressIndeterminate = true;
        Dialog.ProgressValue = 0;
        Dialog.TaskbarProgressValue = 0.0;
        Dialog.TaskbarProgressState = TaskbarItemProgressState.None;
    }

    Task IInstallReporter.DownloadAsync(string url, string destination) => DownloadFileWithProgressAsync(url, destination);

    void IPackageReporter.AddDownloaded(long bytes, bool updateProgressBar)
    {
        Interlocked.Add(ref _totalDownloadedBytes, bytes);

        if (updateProgressBar)
            UpdateProgressBar();
    }

    void IPackageReporter.AddTotal(long bytes) => AddProgressTotal(bytes);

    void IPackageReporter.MarkExtractionFailed() => ExtractionSucceeded = false;

    async Task IPackageReporter.OnChecksumFailedAsync(Exception exception)
    {
        await Frontend.ShowConnectivityDialog(
            Strings.Dialog_Connectivity_UnableToDownload,
            String.Format(CultureInfo.InvariantCulture, Strings.Dialog_Connectivity_UnableToDownloadReason, "[https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox](https://github.com/bloxstraplabs/bloxstrap/wiki/Bloxstrap-is-unable-to-download-Roblox)"),
            MessageBoxImage.Error,
            exception
        );

        App.Terminate(ErrorCode.ERROR_CANCELLED);
    }

    public void SetStatus(string message)
    {
        message = message.Replace("{product}", productName(), StringComparison.Ordinal);
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

    public async Task DownloadFileWithProgressAsync(string url, string destination)
    {
        using var response = await App.HttpClient.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long totalBytes = response.Content.Headers.ContentLength ?? -1;
        if (totalBytes <= 0)
            throw new InvalidOperationException("Unable to determine file size for progress reporting.");

        _totalDownloadedBytes = 0;
        _progressIncrement = (double)ProgressBarMaximum / totalBytes;
        _taskbarProgressIncrement = TaskbarProgressMaximum / totalBytes;

        if (Dialog != null)
        {
            Dialog.ProgressIndeterminate = false;
            Dialog.ProgressMaximum = ProgressBarMaximum;
            Dialog.ProgressValue = 0;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
            Dialog.TaskbarProgressValue = 0.0;
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        var buffer = new byte[81920];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
            totalRead += bytesRead;
            Interlocked.Add(ref _totalDownloadedBytes, bytesRead);
            UpdateProgressBar(false);
        }

        if (totalRead != totalBytes)
            throw new IOException($"Downloaded {totalRead} bytes but expected {totalBytes}");
    }

    public void BeginDownloadProgress(long totalBytes)
    {
        Interlocked.Exchange(ref _totalDownloadedBytes, 0);
        Interlocked.Exchange(ref _totalPackagedBytes, Math.Max(0, totalBytes));
        RecalculateProgressIncrements();

        if (Dialog is null)
            return;

        Dialog.ProgressIndeterminate = false;
        Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
        Dialog.ProgressMaximum = ProgressBarMaximum;
        Dialog.ProgressValue = 0;
        Dialog.TaskbarProgressValue = 0.0;
    }

    public void BeginInstallProgress(long totalPackagedBytes)
    {
        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = false;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;

            Dialog.ProgressMaximum = ProgressBarMaximum;

            // compute total bytes to download
            _totalPackagedBytes = totalPackagedBytes;
            _progressIncrement = (double)ProgressBarMaximum / _totalPackagedBytes;

            _taskbarProgressMaximum = TaskbarProgressMaximum;

            _taskbarProgressIncrement = _taskbarProgressMaximum / (double)_totalPackagedBytes;
        }
    }
}
