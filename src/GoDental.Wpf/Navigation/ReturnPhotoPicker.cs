using System.IO;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using GoDental.Wpf.ViewModels.Operations;
namespace GoDental.Wpf.Navigation;
public sealed class ReturnPhotoPicker:IReturnPhotoPicker
{
    public async Task<IReadOnlyList<ReturnPhotoInput>> PickAsync()
    {
        var dialog=new OpenFileDialog{Title="Adjuntar fotos de la devolución",Filter="Fotos PNG/JPEG|*.png;*.jpg;*.jpeg",Multiselect=true};
        if(dialog.ShowDialog()!=true)return [];
        if(dialog.FileNames.Length>5)throw new ArgumentException("Selecciona hasta cinco fotos por línea.");
        var result=new List<ReturnPhotoInput>();
        foreach(var file in dialog.FileNames)
        {
            if(new FileInfo(file).Length>10*1024*1024)throw new ArgumentException("Cada foto debe pesar como máximo 10 MB.");
            var bytes=await File.ReadAllBytesAsync(file);
            // Normaliza a PNG y limita resolución; el archivo original no se copia ni se ejecuta.
            var normalized=await Task.Run(()=>NormalizeInput(bytes));
            result.Add(new(Path.GetFileNameWithoutExtension(file)+".png",normalized));
        }
        return result;
    }
    public static byte[] NormalizeInput(byte[] bytes)
    {
        if(bytes.Length>10*1024*1024 || !(bytes.Length>=8&&bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}) || bytes.Length>=3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255))
            throw new ArgumentException("Selecciona una foto PNG/JPEG de hasta 10 MB.");
        using var header=new MemoryStream(bytes);
        var frame=BitmapDecoder.Create(header,BitmapCreateOptions.DelayCreation,BitmapCacheOption.None).Frames[0];
        var width=frame.PixelWidth;var height=frame.PixelHeight;
        if(width<=0||height<=0||width>20000||height>20000||(long)width*height>100_000_000)
            throw new ArgumentException("La resolución de la foto es demasiado grande. Reduce sus dimensiones antes de adjuntarla.");
        using var input=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;
        if(width>=height)bitmap.DecodePixelWidth=Math.Min(1600,width);else bitmap.DecodePixelHeight=Math.Min(1600,height);
        bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=new MemoryStream();encoder.Save(output);return output.ToArray();
    }

}
