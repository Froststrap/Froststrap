// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.Clients.Model;
using Froststrap.Clients.Runtimes;
using Froststrap.Clients.Sources;
using Froststrap.Utility;

namespace Froststrap.Bootstrapping;

internal sealed class SoberFlatpakFlow(Func<IBootstrapperDialog?> dialog, BootstrapperProgress reporter)
{
    private const string SoberFlatpakId = SoberClient.FlatpakId;

    private static FlatpakSource SoberSource => new(SoberFlatpakId);

    private IBootstrapperDialog? Dialog => dialog();

    public async Task<bool> EnsureInstalledAsync()
    {
        reporter.SetStatus(Strings.Bootstrapper_Status_CheckingFlatpak);

        try
        {
            await FlatpakSource.EnsureFlatpakAvailableAsync();
        }
        catch (TimeoutException ex)
        {
            App.Logger.Error(ex, "Timed out while checking Flatpak installation.");
            await Frontend.ShowMessageBox(
                "Timed out while checking Flatpak installation. Please make sure Flatpak is working and try again.",
                MessageBoxImage.Error
            );
            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return false;
        }
        catch (Exception ex)
        {
            App.Logger.Error(ex, "Flatpak not found.");
            await Frontend.ShowMessageBox(
                "Flatpak is required on Linux.\n\nPlease install Flatpak first, then launch Froststrap again.",
                MessageBoxImage.Error
            );
            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return false;
        }

        if (await SoberSource.IsInstalledAsync())
        {
            App.Logger.Info("Sober is already installed.");
            return true;
        }

        App.Logger.Info("Installing Sober...");

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = true;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Indeterminate;
        }

        reporter.SetStatus(Strings.Bootstrapper_Status_InstallingSober);

        InstallResult result = await SoberSource.InstallAsync(
            new InlineProgress<InstallProgress>(step => reporter.SetStatus(step.Message)));

        if (!result.Success)
        {
            if (result.Error is null)
            {
                await Frontend.ShowMessageBox(
                    "Failed to start Sober installation via Flatpak.",
                    MessageBoxImage.Error
                );
            }
            else
            {
                string detailsPart = string.IsNullOrWhiteSpace(result.Error) ? "" : $"\n\n{result.Error}";
                string message = string.Format(CultureInfo.InvariantCulture, Strings.Sober_FlatpakInstallFailed, SoberFlatpakId, detailsPart);
                await Frontend.ShowMessageBox(message, MessageBoxImage.Error);
            }

            App.Terminate(ErrorCode.ERROR_CANCELLED);
            return false;
        }

        App.Logger.Info("Sober installation complete.");
        reporter.SetStatus(Strings.Bootstrapper_Status_StartingSober);
        return true;
    }

    public async Task UpdateAsync(CancellationToken cancellationToken)
    {
        reporter.SetStatus(Strings.Bootstrapper_Status_UpdatingSober);

        if (Dialog is not null)
        {
            Dialog.ProgressIndeterminate = false;
            Dialog.ProgressMaximum = 100;
            Dialog.ProgressValue = 0;
            Dialog.TaskbarProgressState = TaskbarItemProgressState.Normal;
            Dialog.TaskbarProgressValue = 0.0;
        }

        try
        {
            await SoberSource.UpdateAsync(new InlineProgress<InstallProgress>(OnSoberUpdateProgress), cancellationToken);
        }
        finally
        {
            if (Dialog is not null)
            {
                Dialog.ProgressIndeterminate = true;
                Dialog.ProgressValue = 0;
                Dialog.TaskbarProgressValue = 0.0;
                Dialog.TaskbarProgressState = TaskbarItemProgressState.None;
            }
        }
    }

    private void OnSoberUpdateProgress(InstallProgress progress)
    {
        if (progress.Phase == InstallPhase.Finalizing)
        {
            if (Dialog is not null)
            {
                Dialog.ProgressValue = 100;
                Dialog.TaskbarProgressValue = 1.0;
                reporter.SetStatus(Strings.Bootstrapper_Status_SoberUpdateComplete);
            }

            return;
        }

        if (progress.Step <= 0)
        {
            reporter.SetStatus(progress.Message);
            return;
        }

        if (Dialog is null)
            return;

        Dialog.ProgressValue = (int)progress.Current;
        Dialog.TaskbarProgressValue = progress.Current / 100.0;

        reporter.SetStatus(progress.StepPercent >= 0
            ? string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberProgress, progress.Step, progress.StepCount, progress.StepPercent)
            : string.Format(CultureInfo.InvariantCulture, Strings.Bootstrapper_Status_UpdatingSoberBasic, progress.Step, progress.StepCount));
    }
}
