using FluentIcons.Common;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class FastFlag : NotifyPropertyChangedViewModel
    {
        // public bool Enabled { get; set; }
        private Symbol _preset;
        private string _name = string.Empty;
        private string _value = string.Empty;

        public Symbol Preset
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