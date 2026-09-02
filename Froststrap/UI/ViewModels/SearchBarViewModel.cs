using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LucideAvalonia.Enum;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels
{
    internal partial class SearchBarViewModel : ObservableObject, IDisposable
    {
        private string _searchQuery = string.Empty;
        private CancellationTokenSource? _debounceCts;
        private bool _isDropDownOpen;
        private bool _disposed;

        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                if (SetProperty(ref _searchQuery, value))
                {
                    _debounceCts?.Cancel();
                    _debounceCts = new CancellationTokenSource();
                    var token = _debounceCts.Token;

                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(150, token);
                        if (!token.IsCancellationRequested)
                            FilterSearchResults();
                    }, token);
                }
            }
        }

        public bool IsDropDownOpen
        {
            get => _isDropDownOpen;
            set => SetProperty(ref _isDropDownOpen, value);
        }

        private ObservableCollection<SearchBarItem> _filteredSearchResults = [];
        public ObservableCollection<SearchBarItem> FilteredSearchResults
        {
            get => _filteredSearchResults;
            private set
            {
                SetProperty(ref _filteredSearchResults, value);
                // Auto‑open when results exist and query not empty
                IsDropDownOpen = !string.IsNullOrWhiteSpace(SearchQuery) && value.Count > 0;
            }
        }

        private List<SearchBarItem> _searchIndex = [];

        public IRelayCommand<SearchBarItem> SearchResultSelectedCommand { get; }
        public IRelayCommand ClearSearchCommand { get; }
        public IRelayCommand ToggleSearchListCommand { get; }

        public event EventHandler<SearchBarItem>? SearchResultSelected;

        public SearchBarViewModel()
        {
            SearchResultSelectedCommand = new RelayCommand<SearchBarItem>(HandleSearchResultSelected);
            ClearSearchCommand = new RelayCommand(Clear);
            ToggleSearchListCommand = new RelayCommand(ToggleDropdown);
        }

        private void ToggleDropdown() => IsDropDownOpen = !IsDropDownOpen;

        public void SetSearchIndex(List<SearchBarItem> searchIndex)
        {
            _searchIndex = searchIndex ?? [];
            FilterSearchResults();
        }

        public List<SearchBarItem> GetSearchIndex() => _searchIndex;

        public void RefreshSearchResults() => FilterSearchResults();

        private void FilterSearchResults()
        {
            var query = SearchQuery;

            if (string.IsNullOrWhiteSpace(query))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    FilteredSearchResults.Clear();
                    IsDropDownOpen = false;
                });
                return;
            }

            var filtered = _searchIndex
                .Where(item => item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                FilteredSearchResults = new ObservableCollection<SearchBarItem>(filtered);
                IsDropDownOpen = filtered.Count > 0;
            });
        }

        private void HandleSearchResultSelected(SearchBarItem? item)
        {
            if (item == null) return;
            SearchQuery = string.Empty;
            IsDropDownOpen = false;
            SearchResultSelected?.Invoke(this, item);
        }

        public void Clear()
        {
            SearchQuery = string.Empty;
            Dispatcher.UIThread.Post(() =>
            {
                FilteredSearchResults.Clear();
                IsDropDownOpen = false;
            });
        }

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = null;
            }
            _disposed = true;
        }
        #endregion
    }
}