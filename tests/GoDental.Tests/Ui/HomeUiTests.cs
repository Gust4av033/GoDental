using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoDental.Tests.Ui;

public sealed class HomeUiTests
{
    [Fact]
    public async Task SummaryUsesUnfilteredServiceTotalsAndBoundedActivityWithoutWrites()
    {
        var c = new Context(); await c.Home.ReloadCommand.ExecuteAsync(null);
        Assert.Equal([1, 24, 3, 1], c.Home.Metrics.Select(metric => metric.Value));
        Assert.Equal("Hola, Persona", c.Home.Greeting);
        Assert.All(c.Products.Searches, query => { Assert.Empty(query.Search); Assert.False(query.IncludeInactive); Assert.Null(query.CategoryId); Assert.Null(query.BrandId); Assert.Equal(1, query.PageSize); });
        Assert.True(c.Products.Searches[1].LowStockOnly);
        Assert.Equal(5, c.Repository.LastSalesFilter!.PageSize); Assert.Equal(5, c.Repository.LastOrdersFilter!.PageSize);
        Assert.Same(c.Repository.Sale.Sale, c.Home.RecentSales.Single()); Assert.Same(c.Repository.Order.Order, c.Home.RecentOrders.Single());
        Assert.NotNull(c.Home.UpdatedAt); Assert.Empty(c.Repository.Writes);
    }
    [Fact]
    public async Task SummaryQueriesOnlyPermittedAreasAndClearsOnLogout()
    {
        var c = new Context(); c.Session.Begin(Identity("INVENTARIO.VER"));
        await c.Home.ReloadCommand.ExecuteAsync(null);
        Assert.Equal(2, c.Home.Metrics.Count); Assert.Equal(0, c.Repository.SalesReadCount); Assert.Equal(0, c.Repository.OrdersReadCount);
        c.Session.Clear();
        Assert.Empty(c.Home.Metrics); Assert.Empty(c.Home.RecentSales); Assert.Empty(c.Home.RecentOrders); Assert.Null(c.Home.UpdatedAt);
        Assert.False(c.Home.ReloadCommand.CanExecute(null));
    }
    [Fact]
    public async Task FailedRefreshDoesNotKeepOldSalesOrRepresentFailureAsZero()
    {
        var c = new Context(); await c.Home.ReloadCommand.ExecuteAsync(null);
        c.Repository.FailSales = true; await c.Home.ReloadCommand.ExecuteAsync(null);
        Assert.Null(c.Home.Metrics.Single(metric => metric.Label == "Ventas registradas").Value);
        Assert.Equal(24, c.Home.Metrics.Single(metric => metric.Label == "Productos activos").Value);
        Assert.Empty(c.Home.RecentSales); Assert.NotEmpty(c.Home.RecentOrders);
        Assert.Contains("No se pudieron", c.Home.SalesMessage); Assert.Contains("Parte del resumen", c.Home.StatusMessage);
    }
    [Fact]
    public async Task LatePreviousSessionResponseCannotOverwriteNewSummary()
    {
        var c = new Context(); c.Repository.PendingSales = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldRead = c.Home.ReloadCommand.ExecuteAsync(null); Assert.True(c.Home.IsBusy);
        c.Session.Clear(); c.Session.Begin(Identity("INVENTARIO.VER"));
        await c.Home.ReloadCommand.ExecuteAsync(null);
        var updated = c.Home.UpdatedAt;
        c.Repository.PendingSales.SetResult(new([c.Repository.Sale.Sale], 999)); await oldRead;
        Assert.Equal(2, c.Home.Metrics.Count); Assert.Empty(c.Home.RecentSales); Assert.Equal(updated, c.Home.UpdatedAt);
        Assert.Equal(24, c.Home.Metrics[0].Value); Assert.False(c.Home.IsBusy);
    }
    [Fact]
    public async Task EmptyAndNoPermissionAreDistinguishedFromFailure()
    {
        var c = new Context(); c.Repository.EmptyActivity = true; c.Products.Total = c.Products.LowStock = 0;
        await c.Home.ReloadCommand.ExecuteAsync(null);
        Assert.All(c.Home.Metrics, metric => Assert.Equal(0, metric.Value));
        Assert.Contains("Aún no hay", c.Home.SalesMessage); Assert.Contains("Aún no hay", c.Home.OrdersMessage);
        Assert.DoesNotContain("no está disponible", c.Home.StatusMessage);
        c.Session.Begin(Identity()); var before = c.Products.Searches.Count;
        await c.Home.ReloadCommand.ExecuteAsync(null);
        Assert.Empty(c.Home.Metrics); Assert.Equal(before, c.Products.Searches.Count); Assert.Contains("no tiene resúmenes", c.Home.StatusMessage);
    }
    private static UserIdentity Identity(params string[] permissions) => new(1, "prueba_inicio", "Persona de prueba", 1, [], permissions, false);
    private sealed class Context
    {
        public UserSession Session { get; } = new();
        public MvpUiRepository Repository { get; } = new();
        public ProductsRepository Products { get; } = new();
        public HomeViewModel Home { get; }
        public Context()
        {
            Session.Begin(Identity("VENTAS.VER", "INVENTARIO.VER"));
            Home = new(new(Products, Session), new(Repository, Session, new IdentityGateway()), new(Repository, Session), Session, NullLogger<HomeViewModel>.Instance);
        }
    }
    private sealed class ProductsRepository : IProductRepository
    {
        public int Total { get; set; } = 24;
        public int LowStock { get; set; } = 3;
        public List<ProductSearch> Searches { get; } = [];
        public Task<ProductPage> SearchAsync(ProductSearch query, CancellationToken ct = default)
        {
            Searches.Add(query); return Task.FromResult(new ProductPage([], query.LowStockOnly ? LowStock : Total));
        }
        public Task<ProductCatalogs> GetCatalogsAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<int>> SaveAsync(ProductData p, int u, int? w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<int>> CreateCategoryAsync(string name, int u, int? w, CancellationToken ct = default) => throw new NotSupportedException();
    }
    private sealed class IdentityGateway : IIdentityGateway
    {
        public Task<bool> NeedsAdministratorAsync(CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<UserIdentity>> CreateAdministratorAsync(string u, string d, string p, string w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result<UserIdentity>> SignInAsync(string u, string p, string w, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Result> ChangePasswordAsync(int id, string current, string next, CancellationToken ct = default) => throw new NotSupportedException();
        public Task SignOutAsync(int id, int? w, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
