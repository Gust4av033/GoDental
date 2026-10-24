using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Dapper;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;

namespace GoDental.Infrastructure.Data;
public sealed partial class FileReturnEvidenceStore:IReturnEvidenceStore
{
    private readonly Func<CancellationToken,Task<string>> rootReader;
    public FileReturnEvidenceStore(IDbConnectionFactory factory):this(async ct=>
    {
        await using var c=await factory.OpenConnectionAsync(ct);
        return await c.ExecuteScalarAsync<string>(new CommandDefinition("SELECT Valor FROM sis.Configuracion WHERE Clave=N'Archivos.RaizEvidencias';",cancellationToken:ct))??"";
    }){}
    public FileReturnEvidenceStore(Func<CancellationToken,Task<string>> rootReader)=>this.rootReader=rootReader;
    [GeneratedRegex(@"^returns/([1-9][0-9]{0,9})/([A-F0-9]{64})\.png$",RegexOptions.CultureInvariant)]private static partial Regex ManagedPath();
    public static string ValidateRoot(string root)
    {
        if(string.IsNullOrWhiteSpace(root)||root.Trim().Length>500||!Path.IsPathFullyQualified(root.Trim()))throw new ArgumentException("Indica una carpeta absoluta. Para varias laptops utiliza la misma ruta compartida UNC (\\\\servidor\\recurso\\Evidencias).");
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(root.Trim()));
    }
    public async Task CheckRootAsync(string root,CancellationToken ct=default)
    {
        root=ValidateRoot(root);Directory.CreateDirectory(root);RejectLink(root);
        var probe=Path.Combine(root,".godental-probe-"+Guid.NewGuid().ToString("N")+".tmp");
        try{await using(var f=new FileStream(probe,FileMode.CreateNew,FileAccess.Write,FileShare.None,4096,FileOptions.Asynchronous|FileOptions.WriteThrough)){await f.WriteAsync(new byte[]{1},ct);await f.FlushAsync(ct);}}
        finally{if(File.Exists(probe))File.Delete(probe);}
    }
    public async Task<ReturnEvidence> SaveAsync(byte[] image,string name,int userId,CancellationToken ct=default)
    {
        if(userId<=0)throw new ArgumentException("Falta el usuario de la evidencia.");
        ValidatePng(image);name=Path.GetFileName(name);
        if(string.IsNullOrWhiteSpace(name)||name.Length>255||name.Any(char.IsControl)||name.IndexOfAny(['\\','/',':'])>=0)throw new ArgumentException("Revisa el nombre de la foto.");
        var hash=Convert.ToHexString(SHA256.HashData(image));var relative=$"returns/{userId}/{hash}.png";
        var root=ValidateRoot(await rootReader(ct));var file=ManagedFile(root,relative,create:true);var temporary=Path.Combine(Path.GetDirectoryName(file)!,".upload-"+Guid.NewGuid().ToString("N")+".tmp");
        try
        {
            await using(var f=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous|FileOptions.WriteThrough))
            {await f.WriteAsync(image,ct);await f.FlushAsync(ct);f.Flush(true);}
            try{File.Move(temporary,file,false);}catch(IOException)when(File.Exists(file)){/* Otra carga del mismo contenido conserva el archivo inmutable. */}
            var evidence=new ReturnEvidence(name,"image/png",relative,hash);
            if(!await VerifyAsync(evidence,userId,ct))throw new IOException("No se pudo comprobar la foto guardada.");
            return evidence;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public async Task<bool> VerifyAsync(ReturnEvidence e,int userId,CancellationToken ct=default,string? root=null)
    {
        var match=ManagedPath().Match(e.RelativePath??"");
        if(!match.Success||match.Groups[1].Value!=userId.ToString(System.Globalization.CultureInfo.InvariantCulture))return false;
        try{await ReadCoreAsync(e,root,ct);return true;}
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){return false;}
    }
    public Task<byte[]> ReadAsync(ReturnEvidence e,CancellationToken ct=default)=>ReadCoreAsync(e,null,ct);
    private async Task<byte[]> ReadCoreAsync(ReturnEvidence e,string? configuredRoot,CancellationToken ct)
    {
        var match=ManagedPath().Match(e.RelativePath??"");
        if(!match.Success||e.MimeType!="image/png"||match.Groups[2].Value!=e.Sha256)throw new ArgumentException("Referencia de evidencia no válida.");
        var root=ValidateRoot(configuredRoot??await rootReader(ct));var file=ManagedFile(root,e.RelativePath??"",create:false);
        await using var f=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read,81920,FileOptions.Asynchronous);
        if(f.Length>10*1024*1024)throw new ArgumentException("La foto excede el tamaño permitido.");
        using var buffer=new MemoryStream();await f.CopyToAsync(buffer,ct);var bytes=buffer.ToArray();ValidatePng(bytes);
        if(Convert.ToHexString(SHA256.HashData(bytes))!=e.Sha256)throw new IOException("La foto cambió y no coincide con su hash registrado.");return bytes;
    }
    private static string ManagedFile(string root,string relative,bool create)
    {
        if(!ManagedPath().IsMatch(relative))throw new ArgumentException("Ruta de evidencia no válida.");
        RejectLink(root);var segments=relative.Split('/');var folder=root;
        foreach(var segment in segments.Take(2)){folder=Path.Combine(folder,segment);if(create)Directory.CreateDirectory(folder);RejectLink(folder);}
        var file=Path.Combine(folder,segments[2]);if(File.Exists(file))RejectLink(file);return file;
    }
    private static void RejectLink(string path)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ArgumentException("La carpeta y las fotos no admiten vínculos o redirecciones del sistema de archivos.");
    }
    private static void ValidatePng(byte[] bytes)
    {
        if(bytes.Length<45||bytes.Length>10*1024*1024||!bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})
           ||!bytes.AsSpan(12,4).SequenceEqual("IHDR"u8)||!bytes.AsSpan(bytes.Length-8,4).SequenceEqual("IEND"u8)
           ||BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(16,4)) is 0 or >8192||BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(20,4)) is 0 or >8192)
            throw new ArgumentException("Adjunta una foto PNG válida de hasta 10 MB y 8192 píxeles. El selector convierte JPEG a PNG automáticamente.");
    }
}
