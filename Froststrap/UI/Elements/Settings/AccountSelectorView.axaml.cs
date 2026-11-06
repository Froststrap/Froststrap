// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Froststrap.UI.Elements.Dialogs;
using Froststrap.UI.ViewModels.Settings;

namespace Froststrap.UI.Elements.Settings
{
    internal enum PhotoSide
    {
        Left,
        Right
    }

    internal enum DropdownSide
    {
        Up,
        Down
    }
    
    internal partial class AccountSelectorView : UserControl, IDisposable
    {
        private readonly AccountSelectorViewModel? _viewModel;
        private bool _disposed;

        private AccountSelectorViewModel.AccountWithAvatar? _dragAccount;
        private Control? _dragGrip;
        private Control? _dragRow;
        private int _dragSourceIndex = -1;
        private int _dragCurrentIndex = -1;

        public AccountSelectorView()
        {
            InitializeComponent();

            _viewModel = new AccountSelectorViewModel();
            DataContext = _viewModel;
        }

        protected override void OnInitialized()
        {
            base.OnInitialized();

            _viewModel?.OnManualAddRequested += HandleManualAddRequested;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
        }

        private async void HandleManualAddRequested()
        {
            await ShowManualAccountDialogAsync();
        }

        private async Task ShowManualAccountDialogAsync()
        {
            try
            {
                App.Logger.Info("Showing manual cookie dialog");

                var dialog = new ManualCookieDialog();

                var desktop = Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                var parent = desktop?.MainWindow ?? (desktop?.Windows.Count > 0 ? desktop.Windows[0] : null);

                if (parent != null)
                {
                    var result = await dialog.ShowDialog<AccountManagerAccount?>(parent);

                    if (result != null)
                    {
                        App.Logger.Info($"Dialog returned account: {result.Username}");

                        _viewModel?.AddAccountDirect(result);
                    }
                }
                else
                {
                    App.Logger.Error("Could not find a parent window.");
                }
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Couldn't show manual dialog: {ex.Message}");
            }
            finally
            {
                _viewModel?.IsAddingAccount = false;
            }
        }

        private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not Control grip) return;
            if (grip.DataContext is not AccountSelectorViewModel.AccountWithAvatar account) return;
            if (_viewModel == null) return;
            if (!e.GetCurrentPoint(grip).Properties.IsLeftButtonPressed) return;

            _dragAccount = account;
            _dragGrip = grip;
            _dragRow = grip.Parent as Control;
            _dragSourceIndex = _viewModel.Accounts.IndexOf(account);
            _dragCurrentIndex = _dragSourceIndex;

            if (_dragRow != null)
                _dragRow.Opacity = 0.5;

            e.Pointer.Capture(grip);
            e.Handled = true;
        }

        private void OnDragHandlePointerMoved(object? sender, PointerEventArgs e)
        {
            if (_dragGrip == null || _viewModel == null || _dragAccount == null) return;

            var itemsControl = _dragGrip.FindAncestorOfType<ItemsControl>();
            if (itemsControl == null) return;

            var pointer = e.GetCurrentPoint(itemsControl);
            int target = ComputeTargetIndex(itemsControl, pointer.Position.Y);

            if (target >= 0 && target != _dragCurrentIndex && target < _viewModel.Accounts.Count)
            {
                _viewModel.MoveAccount(_dragCurrentIndex, target, persist: false);
                _dragCurrentIndex = target;
            }
        }

        private void OnDragHandlePointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_dragGrip != null)
            {
                try { e.Pointer.Capture(null); }
                catch { }
            }

            EndDrag(persist: true);
            e.Handled = true;
        }

        private void OnDragHandlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            EndDrag(persist: true);
        }

        private void EndDrag(bool persist)
        {
            if (_dragGrip == null && _dragRow == null) return;

            if (_dragRow != null)
                _dragRow.Opacity = 1;

            if (persist
                && _viewModel != null
                && _dragSourceIndex >= 0
                && _dragCurrentIndex >= 0
                && _dragSourceIndex != _dragCurrentIndex)
            {
                _viewModel.SaveAccountOrder();
            }

            _dragAccount = null;
            _dragGrip = null;
            _dragRow = null;
            _dragSourceIndex = -1;
            _dragCurrentIndex = -1;
        }

        private static int ComputeTargetIndex(ItemsControl itemsControl, double pointerY)
        {
            int best = -1;
            double bestDist = double.MaxValue;

            foreach (var container in itemsControl.GetRealizedContainers())
            {
                if (container is not Control c) continue;

                var transform = c.TransformToVisual(itemsControl);
                if (transform is not { } matrix) continue;

                var topLeft = matrix.Transform(new Point(0, 0));
                double top = topLeft.Y;
                double bottom = top + c.Bounds.Height;
                double center = (top + bottom) / 2.0;

                if (pointerY >= top && pointerY <= bottom)
                    return itemsControl.IndexFromContainer(c);

                double dist = Math.Abs(pointerY - center);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = itemsControl.IndexFromContainer(c);
                }
            }

            return best;
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            _viewModel?.OnManualAddRequested -= HandleManualAddRequested;
            base.OnUnloaded(e);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _viewModel?.Dispose();
            }

            _disposed = true;
        }
        public static readonly StyledProperty<PhotoSide> PhotoPositionProperty =
            AvaloniaProperty.Register<AccountSelectorView, PhotoSide>(nameof(PhotoPosition), PhotoSide.Left);

        public PhotoSide PhotoPosition
        {
            get => GetValue(PhotoPositionProperty);
            set => SetValue(PhotoPositionProperty, value);
        }

        static AccountSelectorView()
        {
            PhotoPositionProperty.Changed.AddClassHandler<AccountSelectorView>((view, _) => view.UpdatePhotoPosition());
            DropdownDirectionProperty.Changed.AddClassHandler<AccountSelectorView>((view, _) => view.UpdateDropdownPlacement());
        }

        private void UpdatePhotoPosition()
        {
            bool right = PhotoPosition == PhotoSide.Right;

            DockPanel.SetDock(AvatarHost, right ? Dock.Right : Dock.Left);
            AvatarHost.Margin = right ? new Thickness(8, 0, 0, 0) : new Thickness(0, 0, 8, 0);
        }

        public static readonly StyledProperty<DropdownSide> DropdownDirectionProperty =
            AvaloniaProperty.Register<AccountSelectorView, DropdownSide>(nameof(DropdownDirection), DropdownSide.Up);

        public DropdownSide DropdownDirection
        {
            get => GetValue(DropdownDirectionProperty);
            set => SetValue(DropdownDirectionProperty, value);
        }

        private void UpdateDropdownPlacement()
        {
            bool down = DropdownDirection == DropdownSide.Down;

            AccountDropdown.Placement = down ? PlacementMode.Bottom : PlacementMode.Top;
            AccountDropdown.VerticalOffset = down ? 6 : -6; // small gap away from the button
        }
    }
}
