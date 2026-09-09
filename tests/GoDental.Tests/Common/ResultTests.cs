using GoDental.Application.Common;

namespace GoDental.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_CreatesSuccessfulResult()
    {
        var result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Failure_PreservesErrorInformation()
    {
        var result = Result.Failure("VALIDATION_ERROR", "Dato inválido");

        Assert.False(result.IsSuccess);
        Assert.Equal("VALIDATION_ERROR", result.ErrorCode);
        Assert.Equal("Dato inválido", result.Message);
    }
}
