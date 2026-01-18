using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Media;

namespace Froststrap.Extensions
{
    public static class IconEx
    {
        public static Bitmap GetSized(this Bitmap bitmap, int width, int height)
        {
            return bitmap.CreateScaledBitmap(new PixelSize(width, height));
        }

        public static IImage GetImageSource(this Bitmap bitmap, bool handleException = true)
        {
            return bitmap;
        }

        public static Bitmap GetBitmapFromStream(Stream stream, bool handleException = true)
        {
            if (handleException)
            {
                try
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return new Bitmap(stream);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteException("IconEx::GetBitmapFromStream", ex);
                    Frontend.ShowMessageBox(string.Format(Strings.Dialog_IconLoadFailed, ex.Message));
                    return BootstrapperIcon.IconFroststrap.GetIcon();
                }
            }
            else
            {
                stream.Seek(0, SeekOrigin.Begin);
                return new Bitmap(stream);
            }
        }
    }
}