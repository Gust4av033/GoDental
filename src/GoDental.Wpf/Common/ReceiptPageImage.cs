using System.IO;
using System.Windows.Media.Imaging;

namespace GoDental.Wpf.Common;

public static class ReceiptPageImage
{
    public static BitmapImage Load(byte[] bytes, bool preview)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
        if (preview) image.DecodePixelWidth = 1275;
        image.StreamSource = stream; image.EndInit(); image.Freeze();
        return image;
    }
}
