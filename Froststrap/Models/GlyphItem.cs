using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace Froststrap.Models
{
    public class GlyphItem : INotifyPropertyChanged
    {
        private Geometry? _data;
        private SolidColorBrush? _colorBrush;

        public Geometry? Data
        {
            get => _data;
            set
            {
                if (Equals(_data, value)) return;
                _data = value;
                OnPropertyChanged();
            }
        }

        public SolidColorBrush? ColorBrush
        {
            get => _colorBrush;
            set
            {
                if (Equals(_colorBrush, value)) return;
                _colorBrush = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}