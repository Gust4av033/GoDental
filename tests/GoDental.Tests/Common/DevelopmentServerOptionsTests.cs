namespace GoDental.Tests.Common;

public sealed class DevelopmentServerOptionsTests
{
    [Theory]
    [InlineData(".")]
    [InlineData("localhost")]
    [InlineData(@".\SQLEXPRESS")]
    [InlineData(@"LOCALHOST\SQLEXPRESS")]
    public void AllowsLocalInstances(string server) => Assert.Equal(server, DevelopmentServerOptions.Parse(["--install-development", "--server", server, "."]));

    [Fact]
    public void KeepsDefaultAndLocalDbSelection()
    {
        Assert.Equal("localhost", DevelopmentServerOptions.Parse(["--install-development", "."]));
        Assert.Equal(@"(localdb)\MSSQLLocalDB", DevelopmentServerOptions.Parse(["--run-customers", "--localdb", "."]));
    }

    [Theory]
    [InlineData("otro-servidor")]
    [InlineData(@"otro-servidor\SQLEXPRESS")]
    [InlineData("localhost;User ID=sa")]
    [InlineData(@".\SQLEXPRESS\OTRA")]
    [InlineData(@".\")]
    [InlineData("tcp:localhost,1433")]
    public void RejectsRemoteOrMalformedTargets(string server) => Assert.Throws<ArgumentException>(() => DevelopmentServerOptions.Parse(["--server", server]));

    [Fact]
    public void RejectsMissingAndAmbiguousSelections()
    {
        Assert.Throws<ArgumentException>(() => DevelopmentServerOptions.Parse(["--server"]));
        Assert.Throws<ArgumentException>(() => DevelopmentServerOptions.Parse(["--server", "--install-development"]));
        Assert.Throws<ArgumentException>(() => DevelopmentServerOptions.Parse(["--localdb", "--server", @".\SQLEXPRESS"]));
        Assert.Throws<ArgumentException>(() => DevelopmentServerOptions.Parse(["--server", ".", "--server", "localhost"]));
    }

    [Theory]
    [InlineData(@".\SQLEXPRESS")]
    [InlineData(@"localhost\SQLEXPRESS")]
    public void NamedLocalInstanceSupportsDevelopmentCertificate(string server) =>
        Assert.Null(GoDental.Application.Validators.WorkstationSettingsValidator.Validate(new(server, "GoDental_Development", "Light", true)));

    [Theory]
    [InlineData(@"otro-servidor\SQLEXPRESS", "GoDental_Development")]
    [InlineData(@"localhost.example\SQLEXPRESS", "GoDental_Development")]
    [InlineData(@".\SQLEXPRESS", "GoDental")]
    [InlineData(@".\SQLEXPRESS;User ID=sa", "GoDental_Development")]
    public void DevelopmentCertificateRemainsLimitedToLocalTestDatabases(string server, string database) =>
        Assert.NotNull(GoDental.Application.Validators.WorkstationSettingsValidator.Validate(new(server, database, "Light", true)));
}
