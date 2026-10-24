using System.Text.Json;
using GoDental.Application.DTOs;

namespace GoDental.Tests.Security;

public sealed class FiscalSecretModelsTests
{
    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 4, false)]
    [InlineData(3, 1, false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 2, true)]
    [InlineData(1, 3, true)]
    [InlineData(2, 1, true)]
    public void ReferenceRejectsUndefinedPartitionOrPurpose(int environment, int purpose, bool expected)
    {
        var reference = new FiscalSecretReference(new(Guid.NewGuid(), (FiscalSecretEnvironment)environment),
            (FiscalSecretPurpose)purpose, Guid.NewGuid());
        Assert.Equal(expected, reference.IsValid);
    }

    [Fact]
    public void ReferenceRequiresIssuerAndVersion()
    {
        var scope = new FiscalSecretScope(Guid.NewGuid(), FiscalSecretEnvironment.Test);
        Assert.False(new FiscalSecretReference(default, FiscalSecretPurpose.ApiCredential, Guid.NewGuid()).IsValid);
        Assert.False(new FiscalSecretReference(scope, FiscalSecretPurpose.ApiCredential, Guid.Empty).IsValid);
    }

    [Fact]
    public void BufferCopiesAndZeroesOwnedMemoryOnDispose()
    {
        byte[] input = [2, 4, 8];
        var material = SecretBuffer.CopyFrom(input);
        var retained = material.Memory;
        input[0] = 9;
        Assert.Equal(2, retained.Span[0]);
        material.Dispose();
        material.Dispose();
        Assert.Equal(new byte[3], retained.ToArray());
        Assert.Throws<ObjectDisposedException>(() => material.Memory);
    }

    [Fact]
    public void BufferCannotSerializeOrStringifyMaterial()
    {
        using var material = SecretBuffer.CopyFrom("SYNTHETIC_PRIVATE_BYTES"u8);
        Assert.Equal("{}", JsonSerializer.Serialize(material));
        Assert.DoesNotContain("SYNTHETIC", material.ToString());
        var error = new SecretStoreException(SecretStoreFailure.Unavailable);
        Assert.Null(error.InnerException);
        Assert.DoesNotContain("C:", error.ToString());
    }
}
