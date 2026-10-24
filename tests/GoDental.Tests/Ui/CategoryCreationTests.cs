using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.Common;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.ViewModels;
using Microsoft.Extensions.Logging.Abstractions;
namespace GoDental.Tests.Ui;
public sealed class CategoryCreationTests
{
    [Fact] public async Task DirectCreateUsesAuthorizedProductServiceAndRetainsDraftOnFailure()
    {
        var session = new UserSession(); session.Begin(new(1, "test", "Test", 1, [], [ProductService.ReadPermission, ProductService.ManagePermission], false));
        var repository = new Repository();
        var vm = new CategoriesViewModel(new(repository, session), session, NullLogger<CategoriesViewModel>.Instance, new(repository, session));
        vm.NewCategoryCommand.Execute(null); Assert.True(vm.IsCreating); Assert.True(vm.HasPendingChanges); Assert.False(vm.CanSetActive);
        vm.Name = " Nueva categoría "; await vm.SaveCommand.ExecuteAsync(null);
        Assert.Equal("Nueva categoría", repository.Name); Assert.True(vm.IsCreating); Assert.Equal(" Nueva categoría ", vm.Name);
        repository.Fail = false; var saved = false; vm.Saved += (_, _) => saved = true;
        await vm.SaveCommand.ExecuteAsync(null); Assert.True(saved); Assert.False(vm.IsCreating); Assert.Equal(0, repository.Edits);
        session.Clear(); Assert.False(vm.CanCreate); vm.NewCategoryCommand.Execute(null); Assert.False(vm.IsCreating);
    }
    private sealed class Repository : ICategoryRepository, IProductRepository
    {
        public string? Name; public bool Fail = true; public int Edits;
        public Task<Result<int>> CreateCategoryAsync(string name, int userId, int? workstationId, CancellationToken cancellationToken = default) { Name = name; return Task.FromResult(Fail ? Result<int>.Failure("DUPLICATE_CATEGORY", "Nombre duplicado") : Result<int>.Success(7)); }
        public Task<IReadOnlyList<CategoryData>> ListAsync(int userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<CategoryData>>([]);
        public Task<Result<int>> SaveAsync(CategoryChange change, int userId, int? workstationId, CancellationToken ct = default) { Edits++; return Task.FromResult(Result<int>.Success(change.Id)); }
        public Task<ProductPage> SearchAsync(ProductSearch search, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<ProductCatalogs> GetCatalogsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Result<int>> SaveAsync(ProductData product, int userId, int? workstationId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
