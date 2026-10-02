using Froststrap.Enums.Overlay;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models.Overlay
{
    internal class FriendListHeader : NotifyPropertyChangedViewModel
    {
        public FriendSection Section { get; init; }

        public string Title { get; init; } = String.Empty;

        private int _count;
        public int Count
        {
            get => _count;
            set
            {
                if (_count == value)
                    return;

                _count = value;

                OnPropertyChanged(nameof(Count));
                OnPropertyChanged(nameof(CountText));
            }
        }

        public string CountText => $"({_count})";

        private bool _isExpanded = true;
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                    return;

                _isExpanded = value;

                OnPropertyChanged(nameof(IsExpanded));
            }
        }
    }
}