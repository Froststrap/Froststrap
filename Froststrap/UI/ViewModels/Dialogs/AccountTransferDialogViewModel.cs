// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using Froststrap.Backend;
using Froststrap.Integrations.AccountManager;
using Froststrap.UI.Elements.Base;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Dialogs
{
    /// Export: pick accounts and a password, closes with the sealed bytes.
    /// Import: asks for the file's password, confirms replacements, then imports with progress.
    internal partial class AccountTransferDialogViewModel : NotifyPropertyChangedViewModel
    {
        private readonly AvaloniaWindow _window;
        private readonly byte[]? _importData;
        private readonly AccountManager? _manager;
        private CancellationTokenSource? _importCancel;
        private bool _cancelRequested;
        private bool _allowClose;

        public bool IsExport => _importData is null;
        public bool IsImport => !IsExport;

        public string Title => IsExport ? Strings.Menu_AccountTransfer_Export_Title : Strings.Menu_AccountTransfer_Import_Title;
        public string Description => IsExport ? Strings.Menu_AccountTransfer_Export_Description : Strings.Menu_AccountTransfer_Import_Description;
        public string ConfirmText => IsExport ? Strings.Menu_AccountTransfer_Export_Confirm : Strings.Menu_AccountTransfer_Import_Confirm;

        public ObservableCollection<SelectableAccount> Accounts { get; } = [];

        private string _password = "";
        public string Password
        {
            get => _password;
            set
            {
                if (SetProperty(ref _password, value))
                    Error = "";
            }
        }

        private string _confirmPassword = "";
        public string ConfirmPassword
        {
            get => _confirmPassword;
            set
            {
                if (SetProperty(ref _confirmPassword, value))
                    Error = "";
            }
        }

        private string _error = "";
        public string Error
        {
            get => _error;
            set
            {
                if (SetProperty(ref _error, value))
                    OnPropertyChanged(nameof(HasError));
            }
        }

        public bool HasError => Error.Length > 0;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    OnPropertyChanged(nameof(IsIdle));
                    OnPropertyChanged(nameof(CanCancel));
                }
            }
        }

        public bool IsIdle => !IsBusy;

        private string _status = "";
        public string Status
        {
            get => _status;
            set
            {
                if (SetProperty(ref _status, value))
                    OnPropertyChanged(nameof(HasStatus));
            }
        }

        public bool HasStatus => Status.Length > 0;

        // the key derivation can't be interrupted, but checking accounts can
        private bool _isImporting;
        public bool CanCancel => IsIdle || _isImporting;

        public byte[]? ExportedData { get; private set; }
        public List<AccountImportItem>? ImportResults { get; private set; }

        public AccountTransferDialogViewModel(AvaloniaWindow window, IEnumerable<AccountManagerAccount> accounts)
        {
            _window = window;
            foreach (var account in accounts)
                Accounts.Add(new SelectableAccount(account));

            _window.Closing += OnClosing;
        }

        public AccountTransferDialogViewModel(AvaloniaWindow window, byte[] importData, AccountManager manager)
        {
            _window = window;
            _importData = importData;
            _manager = manager;
            _window.Closing += OnClosing;
        }

        [RelayCommand]
        private async Task ConfirmAsync()
        {
            if (IsBusy)
                return;

            if (IsExport && !CheckExportInput())
                return;

            if (IsImport && Password.Length == 0)
            {
                Error = Strings.Menu_AccountTransfer_Error_EnterPassword;
                return;
            }

            IsBusy = true;

            try
            {
                if (IsExport)
                {
                    var selected = Accounts.Where(a => a.IsSelected).Select(a => a.Account);
                    var (result, data) = await AccountTransfer.ExportAsync(selected, Password);

                    if (result != AccountVaultResult.Ok)
                    {
                        Error = Describe(result);
                        return;
                    }

                    if (_cancelRequested)
                    {
                        CloseDialog(false);
                        return;
                    }

                    ExportedData = data;
                }
                else
                {
                    var (result, entries) = await AccountTransfer.ReadAsync(_importData!, Password);

                    if (_cancelRequested)
                    {
                        CloseDialog(false);
                        return;
                    }

                    if (result != AccountVaultResult.Ok)
                    {
                        Error = Describe(result);
                        return;
                    }

                    if (await AskReplaceAsync(entries) is not bool replace || _cancelRequested)
                    {
                        if (_cancelRequested)
                            CloseDialog(false);
                        return;
                    }

                    ImportResults = await RunImportAsync(entries, replace);
                }

                CloseDialog(true);
            }
            catch (Exception ex)
            {
                App.Logger.Error("Account transfer failed: ", ex);
                Error = Strings.Menu_AccountTransfer_Error_Internal;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task<bool?> AskReplaceAsync(List<AccountExportEntry> entries)
        {
            var existing = AccountTransfer.FindExisting(_manager!, entries);
            if (existing.Count == 0)
                return false;

            string names = string.Join(", ", existing.Select(a => AccountTransfer.EscapeMarkdown(a.Username)));
            var answer = await Frontend.ShowMessageBox(
                string.Format(CultureInfo.InvariantCulture, Strings.Menu_AccountTransfer_Import_ReplacePrompt, existing.Count, names),
                MessageBoxImage.Question,
                MessageBoxButton.YesNoCancel,
                MessageBoxResult.No);

            return answer switch
            {
                MessageBoxResult.Yes => true,
                MessageBoxResult.No => false,
                _ => null,
            };
        }

        private async Task<List<AccountImportItem>> RunImportAsync(List<AccountExportEntry> entries, bool replace)
        {
            int total = entries.DistinctBy(e => e.UserId).Count();
            var progress = new Progress<int>(done =>
                Status = string.Format(CultureInfo.InvariantCulture, Strings.Menu_AccountTransfer_Import_Progress, Math.Min(done + 1, total), total));

            using var cancel = new CancellationTokenSource();
            _importCancel = cancel;
            _isImporting = true;
            OnPropertyChanged(nameof(CanCancel));

            try
            {
                return await AccountTransfer.ImportAsync(_manager!, entries, replace, progress, cancel.Token);
            }
            finally
            {
                _importCancel = null;
                _isImporting = false;
                Status = "";
                OnPropertyChanged(nameof(CanCancel));
            }
        }

        // closing while busy (encrypting, unlocking or importing) cancels instead; the dialog then
        // closes itself, and nothing more is saved or imported
        private void OnClosing(object? sender, WindowClosingEventArgs e)
        {
            if (_allowClose || !IsBusy)
                return;

            e.Cancel = true;
            _cancelRequested = true;
            _importCancel?.Cancel();
        }

        private void CloseDialog(bool result)
        {
            _allowClose = true;
            _window.Close(result);
        }

        [RelayCommand]
        private void Cancel()
        {
            // stop checking accounts; what was imported so far is still reported
            if (_isImporting)
                _importCancel?.Cancel();
            else
                CloseDialog(false);
        }

        private bool CheckExportInput()
        {
            int selected = Accounts.Count(a => a.IsSelected);
            if (selected == 0)
                Error = Strings.Menu_AccountTransfer_Error_NoAccounts;
            else if (selected > AccountTransfer.MaxAccounts)
                Error = string.Format(CultureInfo.InvariantCulture, Strings.Menu_AccountTransfer_Error_TooMany, AccountTransfer.MaxAccounts);
            else if (Password.Length < AccountTransfer.MinPasswordLength)
                Error = string.Format(CultureInfo.InvariantCulture, Strings.Menu_AccountTransfer_Error_ShortPassword, AccountTransfer.MinPasswordLength);
            else if (Password != ConfirmPassword)
                Error = Strings.Menu_AccountTransfer_Error_Mismatch;
            else
                return true;

            return false;
        }

        private static string Describe(AccountVaultResult result) => result switch
        {
            AccountVaultResult.WrongPassword => Strings.Menu_AccountTransfer_Error_WrongPassword,
            AccountVaultResult.NotAVault => Strings.Menu_AccountTransfer_Error_NotAnExport,
            AccountVaultResult.UnsupportedVersion => Strings.Menu_AccountTransfer_Error_NewerVersion,
            AccountVaultResult.LibraryUnavailable => Strings.Menu_AccountTransfer_Error_Unavailable,
            _ => Strings.Menu_AccountTransfer_Error_Internal,
        };
    }

    /// A checkbox row in the export list.
    internal sealed class SelectableAccount(AccountManagerAccount account) : NotifyPropertyChangedViewModel
    {
        public AccountManagerAccount Account { get; } = account;
        public string Name => $"{Account.DisplayName} (@{Account.Username})";

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
