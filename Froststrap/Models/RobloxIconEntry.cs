using Avalonia.Media;

namespace Froststrap.Models
{
    public class RobloxIconEntry
    {
        public RobloxIcon IconType { get; set; }
        public IImage ImageSource => IconType.GetIcon()!.GetImageSource();
    }
}