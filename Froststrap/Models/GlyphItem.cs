using Avalonia.Media;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class GlyphItem : NotifyPropertyChangedViewModel
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
    }
}