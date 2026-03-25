using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class GlobalSetting : NotifyPropertyChangedViewModel
    {
        private string _name = string.Empty;
        private string _value = string.Empty;
        private string? _vectorX;
        private string? _vectorY;

        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public string Value { get => _value; set => SetProperty(ref _value, value); }
        public string? VectorX { get => _vectorX; set => SetProperty(ref _vectorX, value); }
        public string? VectorY { get => _vectorY; set => SetProperty(ref _vectorY, value); }
        public bool IsVector { get; set; }
    }
}