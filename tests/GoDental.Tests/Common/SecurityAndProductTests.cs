using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Infrastructure.Security;

namespace GoDental.Tests.Common;
public sealed class SecurityAndProductTests
{
    [Fact] public void PasswordHashesUseDistinctSaltsAndRejectWrongPassword()
    {
        var hasher = new Pbkdf2PasswordHasher(); const string password = "Frase para una prueba local";
        var first = hasher.Hash(password); var second = hasher.Hash(password);
        Assert.NotEqual(first, second); Assert.True(hasher.Verify(password, first)); Assert.False(hasher.Verify("Otra frase", first));
    }
    [Theory]
    [InlineData("")]
    [InlineData("bad$hash")]
    [InlineData("PBKDF2-SHA256$999999999$AA==$AA==")]
    [InlineData("PBKDF2-SHA256$600000$!!!$!!!")]
    public void InvalidHashCannotAuthenticate(string hash) => Assert.False(new Pbkdf2PasswordHasher().Verify("Un valor suficientemente largo", hash));
    [Theory]
    [InlineData("servidor-remoto", "GoDental_Development")]
    [InlineData("localhost", "GoDental")]
    [InlineData("localhost", "GoDental_Produccion")]
    public void DevelopmentCertificateIsRestricted(string server, string database) => Assert.NotNull(WorkstationSettingsValidator.Validate(new(server, database, "Light", true)));
    [Fact] public void LocalDevelopmentProfileIsAccepted() => Assert.Null(WorkstationSettingsValidator.Validate(new("localhost", "GoDental_Development", "Light", true)));
    [Theory]
    [InlineData("1234567890")]
    [InlineData("password123")]
    [InlineData("corta")]
    public void WeakPasswordsAreRejected(string password) => Assert.NotNull(IdentityValidator.ValidatePassword("usuario", password));
    [Fact] public void TemporaryPasswordGrantsNoSessionPermissions()
    {
        var session = new UserSession();
        session.Begin(new(1,"prueba","Prueba",1,["Administrador"],[ProductService.ManagePermission],true));
        Assert.False(session.IsAuthenticated); Assert.Empty(session.Permissions);
        session.Clear(); Assert.Null(session.UserId); Assert.Null(session.ConnectionScope);
    }
    [Fact] public void ProductValidationProtectsTraceabilityAndSqlPrecision()
    {
        var product = new ProductData { Sku="A",Name="Insumo",CategoryId=1,UnitId=1,Price=1.23m };
        Assert.Null(ProductValidator.Validate(product));
        product.Price=1.234m; Assert.NotNull(ProductValidator.Validate(product));
        product.Price=1m;product.TracksExpiry=true;Assert.NotNull(ProductValidator.Validate(product));
        product.TracksLot=true;Assert.Null(ProductValidator.Validate(product));
        product.Id=1;Assert.NotNull(ProductValidator.Validate(product));
    }
}
