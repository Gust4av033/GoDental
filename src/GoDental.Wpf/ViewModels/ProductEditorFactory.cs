using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed class ProductEditorFactory(ProductService service, ISessionController session, ILogger<ProductEditorViewModel> logger)
{
    public ProductEditorViewModel Create(ProductData? product, ProductCatalogs catalogs) => new(service, session, logger, product, catalogs);
}
