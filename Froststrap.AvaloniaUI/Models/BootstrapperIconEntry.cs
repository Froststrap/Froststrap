using Avalonia.Media;

namespace Froststrap.Models
{
    public class BootstrapperIconEntry
    {
        public BootstrapperIcon IconType { get; set; }
        public IImage ImageSource => IconType.GetIcon().GetImageSource();
    }
}