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

        private string _color = "#000000";
        public string Color
        {
            get => _color;
            set
            {
                _color = value;
                OnPropertyChanged(nameof(Color));
            }
        }
    }
}