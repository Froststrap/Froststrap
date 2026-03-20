using Avalonia.Media.Imaging;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class FastFlag : NotifyPropertyChangedViewModel
    {
        // public bool Enabled { get; set; }
        private Bitmap _preset = null!;
        private string _name = string.Empty;
        private string _value = string.Empty;

        public Bitmap Preset
        {
            get => _preset;
            set => SetProperty(ref _preset, value);
        }

        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
    }
}