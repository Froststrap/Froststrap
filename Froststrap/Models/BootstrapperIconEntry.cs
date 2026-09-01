using Avalonia.Media;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    internal class BootstrapperIconEntry : NotifyPropertyChangedViewModel
    {
        public BootstrapperIcon IconType { get; set; }
        public IImage ImageSource => IconType.GetIcon().GetImageSource();
        public void RefreshImage() => OnPropertyChanged(nameof(ImageSource));
    }
}