using System.Windows.Media;

namespace Froststrap.Models
{
    public class RobloxIconEntry
    {
        public RobloxIcon IconType { get; set; }
        public ImageSource ImageSource => IconType.GetIcon()!.GetImageSource();
    }
}
