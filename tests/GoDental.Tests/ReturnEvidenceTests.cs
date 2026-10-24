using GoDental.Infrastructure.Data;
namespace GoDental.Tests;
public sealed class ReturnEvidenceTests
{
    private static byte[] Image()=>Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jRusAAAAASUVORK5CYII=");
    [Fact]public async Task FilesAreSharedImmutableAndVerifiedByHashAndOwner()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoDental-Evidence-"+Guid.NewGuid().ToString("N"));
        try
        {
            var first=new FileReturnEvidenceStore(_=>Task.FromResult(root));var second=new FileReturnEvidenceStore(_=>Task.FromResult(root));
            await first.CheckRootAsync(root);var e=await first.SaveAsync(Image(),"defecto.png",7);var same=await second.SaveAsync(Image(),"otra foto.png",7);
            Assert.Equal(e.RelativePath,same.RelativePath);Assert.Equal(Image(),await second.ReadAsync(e));Assert.True(await second.VerifyAsync(e,7));Assert.False(await second.VerifyAsync(e,8));
            Assert.Single(Directory.GetFiles(root,"*.png",SearchOption.AllDirectories));Assert.Empty(Directory.GetFiles(root,"*.tmp",SearchOption.AllDirectories));
            var file=Path.Combine(root,e.RelativePath.Replace('/',Path.DirectorySeparatorChar));var bytes=Image();bytes[35]^=1;await File.WriteAllBytesAsync(file,bytes);
            Assert.False(await second.VerifyAsync(e,7));await Assert.ThrowsAsync<IOException>(()=>second.ReadAsync(e));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]public async Task RejectsTraversalUnconfiguredStorageAndInvalidPhotos()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoDental-Evidence-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new FileReturnEvidenceStore(_=>Task.FromResult(root));await store.CheckRootAsync(root);
            await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync([1,2,3],"not-a-photo.png",1));
            var e=await store.SaveAsync(Image(),"defecto.png",1);
            await Assert.ThrowsAsync<ArgumentException>(()=>store.ReadAsync(e with{RelativePath="../"+e.RelativePath}));
            Assert.False(await store.VerifyAsync(e with{Sha256=new string('A',64)},1));
            var empty=new FileReturnEvidenceStore(_=>Task.FromResult(""));await Assert.ThrowsAsync<ArgumentException>(()=>empty.SaveAsync(Image(),"foto.png",1));
            Assert.Throws<ArgumentException>(()=>FileReturnEvidenceStore.ValidateRoot("relative-folder"));
            var huge=Image();huge[16]=255;await Assert.ThrowsAsync<ArgumentException>(()=>store.SaveAsync(huge,"huge.png",1));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    [Fact]public async Task TransactionRootDoesNotOpenAnotherSqlConnectionDuringVerification()
    {
        var root=Path.Combine(Path.GetTempPath(),"GoDental-Evidence-"+Guid.NewGuid().ToString("N"));
        try
        {
            var initial=new FileReturnEvidenceStore(_=>Task.FromResult(root));await initial.CheckRootAsync(root);var e=await initial.SaveAsync(Image(),"foto.png",1);
            var guarded=new FileReturnEvidenceStore(_=>throw new InvalidOperationException("No abrir otra conexión bajo la transacción."));Assert.True(await guarded.VerifyAsync(e,1,root:root));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
