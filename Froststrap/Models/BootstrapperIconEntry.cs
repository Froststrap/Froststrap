using Avalonia.Media;
using Froststrap.UI.ViewModels;

namespace Froststrap.Models
{
    public class BootstrapperIconEntry : NotifyPropertyChangedViewModel
    {
        public BootstrapperIcon IconType { get; set; }
        public IImage ImageSource => IconType.GetIcon().GetImageSource();
        public void RefreshImage() => OnPropertyChanged(nameof(ImageSource));
    }
}