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
        private readonly LaunchMode _launchMode;
        private readonly ObservableCollection<VersionEntry> _versions = [];
        private bool _isLoading;

        public ObservableCollection<VersionEntry> Versions => _versions;

        public VersionOverrideDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public VersionOverrideDialog(LaunchMode mode) : this()
        {
            _launchMode = mode;
            VersionsDataGrid.SelectionChanged += (s, e) =>
                CopyHashButton.IsEnabled = VersionsDataGrid.SelectedItem != null;

            CopyHashButton.Click += CopyHashButton_Click;
            RefreshButton.Click += RefreshButton_Click;
            CloseButton.Click += (s, e) => Close();

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

        private static List<VersionEntry> ParseDeployHistory(string content, string binaryType)
        {
            var results = new List<VersionEntry>();

            string pattern = $@"New\s+{binaryType}(?:64)?\s+version-(?<hash>[a-f0-9]+|hidden)\s+at\s+(?<date>[^,]+),\s+file\s+version:\s+(?<fileVer>[^,]+(?:,\s*[0-9]+)*)";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
            if (VersionsDataGrid.SelectedItem is VersionEntry selected)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.Clipboard is IClipboard clipboard)
                {
                    await clipboard.SetTextAsync($"version-{selected.Hash}");
                }
            }
        }

        private async void RefreshButton_Click(object? sender, EventArgs e)
        {
            await LoadVersionsAsync();
        }
    }
}