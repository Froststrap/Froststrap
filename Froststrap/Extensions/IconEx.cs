using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Media;

namespace Froststrap.Extensions
{
    internal static class IconHelpers
    {
        public static Bitmap GetSized(this Bitmap bitmap, int width, int height)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            return bitmap.CreateScaledBitmap(new PixelSize(width, height));
        }

        public static IImage GetImageSource(this Bitmap bitmap)
        {
            ArgumentNullException.ThrowIfNull(bitmap);
            return bitmap;
        }

        public static async Task<Bitmap> GetBitmapFromStream(Stream stream, bool handleException = true)
        {
            ArgumentNullException.ThrowIfNull(stream);

            if (handleException)
            {
                try
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    return new Bitmap(stream);
                }
                catch (Exception ex)
                {
                    App.Logger.Error("Unhandled exception: ", ex);
                    await Frontend.ShowMessageBox(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Strings.Dialog_IconLoadFailed,
                            ex.Message));
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