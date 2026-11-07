// SPDX-FileCopyrightText: 2026 Froststrap
//
// SPDX-License-Identifier: MPL-2.0

using Avalonia.Controls;
using Avalonia.Input.Platform;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace Froststrap.UI.Elements.Dialogs
{
    internal class VersionEntry : INotifyPropertyChanged
    {
        private string _hash = string.Empty;
        private string _version = string.Empty;
        private string _date = string.Empty;
        private DateTime _releaseDateUtc;

        public string Hash
        {
            get => _hash;
            set { _hash = value; OnPropertyChanged(nameof(Hash)); }
        }

        public string Version
        {
            get => _version;
            set { _version = value; OnPropertyChanged(nameof(Version)); }
        }

        public string Date
        {
            get => _date;
            set { _date = value; OnPropertyChanged(nameof(Date)); }
        }

        public DateTime ReleaseDateUtc
        {
            get => _releaseDateUtc;
            set { _releaseDateUtc = value; OnPropertyChanged(nameof(ReleaseDateUtc)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    internal partial class VersionOverrideDialog : Base.AvaloniaWindow
    {
        private enum SortColumn { Version, Hash, Date }

        private readonly LaunchMode _launchMode;
        private readonly ObservableCollection<VersionEntry> _versions = [];
        private bool _isLoading;
        private SortColumn _sortColumn = SortColumn.Date;
        private bool _sortDescending = true;

        public ObservableCollection<VersionEntry> Versions => _versions;

        public VersionOverrideDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public VersionOverrideDialog(LaunchMode mode) : this()
        {
            _launchMode = mode;
            VersionsList.SelectionChanged += (s, e) =>
                CopyHashButton.IsEnabled = VersionsList.SelectedItem != null;

            SortVersionButton.Click += (s, e) => SortBy(SortColumn.Version);
            SortHashButton.Click += (s, e) => SortBy(SortColumn.Hash);
            SortDateButton.Click += (s, e) => SortBy(SortColumn.Date);

            CopyHashButton.Click += CopyHashButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            CloseButton.Click += (s, e) => Close();

            UpdateHeaders();
            _ = LoadVersionsAsync();
        }

        private async Task LoadVersionsAsync()
        {
            if (_isLoading) return;
            _isLoading = true;
            RefreshButton.IsEnabled = false;

            try
            {
                _versions.Clear();

                string url = OperatingSystem.IsMacOS()
                    ? "https://setup-rbxcdn.github.io/mac/DeployHistory.txt"
                    : "https://setup-rbxcdn.github.io/DeployHistory.txt";

                string binaryType = OperatingSystem.IsMacOS()
                    ? (_launchMode == LaunchMode.Player ? "Client" : "Studio")
                    : (_launchMode == LaunchMode.Player ? "WindowsPlayer" : "Studio");

                string content = await App.HttpClient.GetStringAsync(new Uri(url));
                var entries = ParseDeployHistory(content, binaryType);

                var oneYearAgo = DateTime.UtcNow.AddYears(-1);
                var filtered = entries
                    .Where(e => e.ReleaseDateUtc >= oneYearAgo && !string.IsNullOrEmpty(e.Hash))
                    .OrderByDescending(e => e.ReleaseDateUtc)
                    .DistinctBy(e => e.Hash)
                    .ToList();

                foreach (var entry in filtered)
                {
                    _versions.Add(entry);
                }

                ApplySort();
            }
            catch (Exception ex)
            {
                App.Logger.Error($"Failed to load deploy history: {ex.Message}");
            }
            finally
            {
                _isLoading = false;
                RefreshButton.IsEnabled = true;
            }
        }

        private void SortBy(SortColumn column)
        {
            if (_sortColumn == column)
            {
                _sortDescending = !_sortDescending;
            }
            else
            {
                _sortColumn = column;
                _sortDescending = column == SortColumn.Date; // newest first by default
            }

            ApplySort();
        }

        private void ApplySort()
        {
            var selected = VersionsList.SelectedItem;

            Func<VersionEntry, object> key = _sortColumn switch
            {
                SortColumn.Version => e => Version.TryParse(e.Version, out var v) ? v : new Version(0, 0),
                SortColumn.Hash => e => e.Hash,
                _ => e => e.ReleaseDateUtc,
            };

            var sorted = (_sortDescending
                ? _versions.OrderByDescending(key)
                : _versions.OrderBy(key)).ToList();

            _versions.Clear();
            foreach (var entry in sorted)
            {
                _versions.Add(entry);
            }

            VersionsList.SelectedItem = selected;
            UpdateHeaders();
        }

        private void UpdateHeaders()
        {
            SortVersionButton.Classes.Set("sorted", _sortColumn == SortColumn.Version);
            SortHashButton.Classes.Set("sorted", _sortColumn == SortColumn.Hash);
            SortDateButton.Classes.Set("sorted", _sortColumn == SortColumn.Date);
        }

        private static List<VersionEntry> ParseDeployHistory(string content, string binaryType)
        {
            var results = new List<VersionEntry>();

            string pattern = $@"New\s+{binaryType}(?:64)?\s+version-(?<hash>[a-f0-9]+|hidden)\s+at\s+(?<date>[^,]+),\s+file\s+version:\s+(?<fileVer>[^,]+(?:,\s*[0-9]+)*)";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            foreach (Match match in regex.Matches(content))
            {
                string hash = match.Groups["hash"].Value;
                if (hash.Equals("hidden", StringComparison.OrdinalIgnoreCase))
                    continue;

                string dateStr = match.Groups["date"].Value.Trim();
                string fileVerStr = match.Groups["fileVer"].Value.Trim();

                if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                    continue;

                var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();

                var fileVersionParts = fileVerStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .ToList();
                string fileVersion = string.Join(".", fileVersionParts);

                var entry = new VersionEntry
                {
                    Hash = hash,
                    Version = fileVersion,
                    Date = date.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture),
                    ReleaseDateUtc = utcDate
                };

                results.Add(entry);
            }

            return results;
        }

        private async void CopyHashButton_Click(object? sender, EventArgs e)
        {
            if (VersionsList.SelectedItem is not VersionEntry selected)
                return;

            var topLevel = TopLevel.GetTopLevel(this);

            if (topLevel?.Clipboard is not IClipboard clipboard)
                return;

            await clipboard.SetTextAsync($"version-{selected.Hash}");
        }

        private async void RefreshButton_Click(object? sender, EventArgs e)
        {
            await LoadVersionsAsync();
        }
    }
}
