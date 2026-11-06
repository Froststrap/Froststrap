// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using CommunityToolkit.Mvvm.Input;
using Froststrap.Backend;
using Froststrap.Integrations.AccountManager;
using Froststrap.UI.Elements.Base;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels.Dialogs
{
    /// Export: pick accounts and a password, closes with the sealed bytes.
    /// Import: asks for the file's password, closes with its entries.
    internal partial class AccountTransferDialogViewModel : NotifyPropertyChangedViewModel
    {
        private readonly AvaloniaWindow _window;
        private readonly byte[]? _importData;

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
                    OnPropertyChanged(nameof(IsIdle));
            }
        }

        public bool IsIdle => !IsBusy;

        public byte[]? ExportedData { get; private set; }
        public List<AccountExportEntry>? ImportedAccounts { get; private set; }

        public AccountTransferDialogViewModel(AvaloniaWindow window, IEnumerable<AccountManagerAccount> accounts)
        {
            _window = window;
            foreach (var account in accounts)
                Accounts.Add(new SelectableAccount(account));
        }

        public AccountTransferDialogViewModel(AvaloniaWindow window, byte[] importData)
        {
            _window = window;
            _importData = importData;
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

                    ExportedData = data;
                }
                else
                {
                    var (result, entries) = await AccountTransfer.ReadAsync(_importData!, Password);

                    if (result != AccountVaultResult.Ok)
                    {
                        Error = Describe(result);
                        return;
                    }

                    ImportedAccounts = entries;
                }

                _window.Close(true);
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

        [RelayCommand]
        private void Cancel() => _window.Close(false);

        private bool CheckExportInput()
        {
            if (!Accounts.Any(a => a.IsSelected))
                Error = Strings.Menu_AccountTransfer_Error_NoAccounts;
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
