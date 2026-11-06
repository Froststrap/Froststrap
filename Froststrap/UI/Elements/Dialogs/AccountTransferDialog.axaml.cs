// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Froststrap.UI.ViewModels.Dialogs;

namespace Froststrap.UI.Elements.Dialogs
{
    internal partial class AccountTransferDialog : Base.AvaloniaWindow
    {
        public AccountTransferDialogViewModel ViewModel { get; }

        /// Export the given accounts.
        public AccountTransferDialog(IEnumerable<AccountManagerAccount> accounts)
        {
            ViewModel = new AccountTransferDialogViewModel(this, accounts);
            DataContext = ViewModel;
            InitializeComponent();
        }

        /// Unlock an export file and import it into `manager`.
        public AccountTransferDialog(byte[] importData, Integrations.AccountManager.AccountManager manager)
        {
            ViewModel = new AccountTransferDialogViewModel(this, importData, manager);
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
