using Froststrap.UI.ViewModels;
using System.Collections.ObjectModel;

namespace Froststrap.Models.Overlay
{
    internal class ChatTab : NotifyPropertyChangedViewModel
    {
        public required ChatFriend Friend { get; init; }

        public ObservableCollection<object> Rows { get; } = [];

        public HashSet<string> KnownMessages { get; } = new(StringComparer.Ordinal);

        public bool Loaded { get; set; }

        public ChatTab()
        {
            Rows.CollectionChanged += (_, _) => OnPropertyChanged(nameof(ShowEmpty));
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;

                _isSelected = value;

                OnPropertyChanged(nameof(IsSelected));
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading == value)
                    return;

                _isLoading = value;

                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(ShowEmpty));
            }
        }

        private string _draft = String.Empty;
        public string Draft
        {
            get => _draft;
            set
            {
                if (_draft == value)
                    return;

                _draft = value;

                OnPropertyChanged(nameof(Draft));
                OnPropertyChanged(nameof(CanSend));
            }
        }

        public bool CanSend => !String.IsNullOrWhiteSpace(_draft);

        public bool ShowEmpty => !_isLoading && Rows.Count == 0;

        public string EmptyText => String.Format(Locale.CurrentCulture, Strings.Menu_Overlay_Messages_NoHistory, Friend.DisplayName);
    }
}