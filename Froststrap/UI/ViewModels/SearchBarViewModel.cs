// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LucideAvalonia.Enum;
using System.Collections.ObjectModel;

namespace Froststrap.UI.ViewModels
{
    internal class SearchResultEntry
    {
        public required SearchBarItem Item { get; init; }
        public required bool IsLastInGroup { get; init; }

        public string Connector => IsLastInGroup ? "└" : "├";
        public string DisplayName => Item.DisplayName;
        public string Description => Item.Description ?? string.Empty;
        public bool HasDescription => !string.IsNullOrEmpty(Item.Description);
    }

    internal class SearchResultGroup
    {
        public string PageName { get; init; } = string.Empty;
        public LucideIconNames IconSymbol { get; init; }
        public ObservableCollection<SearchResultEntry> Items { get; init; } = [];
    }

    internal partial class SearchBarViewModel : ObservableObject, IDisposable
    {
        private string _searchQuery = string.Empty;
        private CancellationTokenSource? _debounceCts;
        private bool _isDropDownOpen;
        private bool _disposed;
        private bool _isIndexing;

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

        public bool IsIndexing
        {
            get => _isIndexing;
            set
            {
                if (SetProperty(ref _isIndexing, value))
                {
                    OnPropertyChanged(nameof(ShowEmptyState));
                    OnPropertyChanged(nameof(ShowNoResults));
                    OnPropertyChanged(nameof(ShowResults));
                }
            }
        }

        private ObservableCollection<SearchBarItem> _filteredSearchResults = [];
        public ObservableCollection<SearchBarItem> FilteredSearchResults
        {
            get => _filteredSearchResults;
            private set
            {
                SetProperty(ref _filteredSearchResults, value);
                IsDropDownOpen = !string.IsNullOrWhiteSpace(SearchQuery) && value.Count > 0;
            }
        }

        private ObservableCollection<SearchResultGroup> _groupedSearchResults = [];
        public ObservableCollection<SearchResultGroup> GroupedSearchResults
        {
            get => _groupedSearchResults;
            private set
            {
                SetProperty(ref _groupedSearchResults, value);
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(ResultsCountText));
                OnPropertyChanged(nameof(ShowNoResults));
                OnPropertyChanged(nameof(ShowResults));
            }
        }

        public bool HasResults => GroupedSearchResults.Count > 0;
        public bool HasQuery => !string.IsNullOrWhiteSpace(SearchQuery);
        public bool ShowEmptyState => !HasQuery && !IsIndexing;
        public bool ShowNoResults => HasQuery && !HasResults && !IsIndexing;
        public bool ShowResults => HasResults && !IsIndexing;

        public string ResultsCountText
        {
            get
            {
                if (!HasQuery) return string.Empty;

                int count = _searchIndex.Count(item =>
                    item.DisplayName.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));

                string word = count == 1 ? "result" : "results";
                return $"{count} {word} for {SearchQuery}";
            }
        }

        private List<SearchBarItem> _searchIndex = [];

        public IRelayCommand<SearchBarItem> SearchResultSelectedCommand { get; }
        public IRelayCommand ClearSearchCommand { get; }

        public event EventHandler<SearchBarItem>? SearchResultSelected;

        public SearchBarViewModel()
        {
            SearchResultSelectedCommand = new RelayCommand<SearchBarItem>(HandleSearchResultSelected);
            ClearSearchCommand = new RelayCommand(Clear);
        }

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
                    GroupedSearchResults.Clear();
                    IsDropDownOpen = false;
                    OnPropertyChanged(nameof(ResultsCountText));
                    OnPropertyChanged(nameof(ShowEmptyState));
                    OnPropertyChanged(nameof(ShowNoResults));
                    OnPropertyChanged(nameof(ShowResults));
                });
                return;
            }

            var filtered = _searchIndex
                .Where(item => item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var groups = filtered
                .GroupBy(item => item.PageName ?? "Other")
                .Select(g =>
                {
                    var items = g.ToList();
                    return new SearchResultGroup
                    {
                        PageName = g.Key,
                        IconSymbol = g.First().IconSymbol ?? LucideIconNames.CircleQuestionMark,
                        Items = new ObservableCollection<SearchResultEntry>(
                            items.Select((item, i) => new SearchResultEntry
                            {
                                Item = item,
                                IsLastInGroup = i == items.Count - 1
                            }))
                    };
                })
                .ToList();

            Dispatcher.UIThread.Post(() =>
            {
                FilteredSearchResults = new ObservableCollection<SearchBarItem>(filtered);
                GroupedSearchResults = new ObservableCollection<SearchResultGroup>(groups);
                IsDropDownOpen = filtered.Count > 0;
                OnPropertyChanged(nameof(ResultsCountText));
                OnPropertyChanged(nameof(ShowEmptyState));
                OnPropertyChanged(nameof(ShowNoResults));
                OnPropertyChanged(nameof(ShowResults));
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
                GroupedSearchResults.Clear();
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