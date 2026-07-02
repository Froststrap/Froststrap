using Avalonia.Media;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class GradientStops : NotifyPropertyChangedViewModel 
    {
        private double _offset;
        public double Offset
        {
            get => _offset;
            set
            {
                _offset = value;
                OnPropertyChanged(nameof(Offset));
            }
        }

        private string _color = "#FFFFFF";

        public string Color
        {
            get => _color;
            set
            {
                var cleaned = string.IsNullOrWhiteSpace(value)
                    ? value
                    : string.Concat(value.Where(c => !char.IsWhiteSpace(c)));

                if (_color != cleaned)
                {
                    _color = cleaned;
                    OnPropertyChanged();
                }
            }
        }
    }
}