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
            var normalized=await Task.Run(()=>
            {
                using var input=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=1600;bitmap.StreamSource=input;bitmap.EndInit();bitmap.Freeze();
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=new MemoryStream();encoder.Save(output);return output.ToArray();
            });
            result.Add(new(Path.GetFileNameWithoutExtension(file)+".png",normalized));
        }
        return result;
    }
}
