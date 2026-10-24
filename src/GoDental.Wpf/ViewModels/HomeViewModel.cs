using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels;

public sealed partial class HomeMetric(string label, string hint, string icon, string tone) : ObservableObject
{
    public string Label { get; } = label;
    public string Hint { get; } = hint;
    public string Icon { get; } = icon;
    public string Tone { get; } = tone;
    [ObservableProperty] private int? value;
}

// Resumen de lectura independiente de los filtros/carritos de los módulos operativos.
public sealed partial class HomeViewModel : ObservableObject
{
    private readonly ReportService? reports;
    [ObservableProperty] private string dailySummary="Consulta Reportes para revisar los indicadores por período.";
    private readonly ProductService products;
    private readonly SalesService sales;
    private readonly OrderService orders;
    private readonly ISessionController session;
    private readonly ILogger<HomeViewModel> logger;
    private CancellationTokenSource? reading;
    private readonly HomeMetric salesMetric = new("Ventas registradas", "Histórico · todos los estados", "\uE8A5", "Sales");
    private readonly HomeMetric productsMetric = new("Productos activos", "Catálogo completo", "\uE7B8", "Products");
    private readonly HomeMetric stockMetric = new("Stock bajo o agotado", "Al mínimo o sin unidades", "\uE7BA", "Stock");
    private readonly HomeMetric ordersMetric = new("Pedidos registrados", "Histórico · todos los estados", "\uE7F4", "Orders");
    public ObservableCollection<HomeMetric> Metrics { get; } = [];
    public ObservableCollection<SaleData> RecentSales { get; } = [];
    public ObservableCollection<OrderData> RecentOrders { get; } = [];
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanReload)), NotifyCanExecuteChangedFor(nameof(ReloadCommand))] private bool isBusy;
    [ObservableProperty] private string statusMessage = "Actualiza el resumen para consultar tu operación.";
    [ObservableProperty] private string salesMessage = "Sin consultar.";
    [ObservableProperty] private string ordersMessage = "Sin consultar.";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(UpdatedLabel))] private DateTimeOffset? updatedAt;
    public bool CanReload => session.IsAuthenticated && !IsBusy;
    public bool CanReadSales => sales.CanRead;
    public string Greeting => session.IsAuthenticated && !string.IsNullOrWhiteSpace(session.DisplayName)
        ? "Hola, " + session.DisplayName.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)[0] : "Bienvenido a GoDental";
    public string TodayLabel => DateTime.Now.ToString("dddd, d 'de' MMMM 'de' yyyy", CultureInfo.GetCultureInfo("es-SV"));
    public string UpdatedLabel => UpdatedAt is { } date ? $"Consulta: {date:HH:mm}" : "Sin actualizar";

    public HomeViewModel(ProductService products, SalesService sales, OrderService orders, ISessionController session, ILogger<HomeViewModel> logger, ReportService? reports = null)
    {
        this.reports=reports; this.products = products; this.sales = sales; this.orders = orders; this.session = session; this.logger = logger;
        Reset();
        session.Changed += (_, _) =>
        {
            reading?.Cancel(); reading = null; IsBusy = false; Reset();
            OnPropertyChanged(nameof(CanReadSales)); OnPropertyChanged(nameof(CanReload)); OnPropertyChanged(nameof(Greeting));
            ReloadCommand.NotifyCanExecuteChanged();
        };
    }
    private void Reset()
    {
        DailySummary="Consulta Reportes para revisar los indicadores por período."; Metrics.Clear(); RecentSales.Clear(); RecentOrders.Clear(); UpdatedAt = null;
        foreach (var metric in new[] { salesMetric, productsMetric, stockMetric, ordersMetric }) metric.Value = null;
        if (sales.CanRead) Metrics.Add(salesMetric);
        if (products.CanRead) { Metrics.Add(productsMetric); Metrics.Add(stockMetric); }
        if (orders.CanRead) Metrics.Add(ordersMetric);
        SalesMessage = OrdersMessage = "Sin consultar.";
        StatusMessage = Metrics.Count == 0 ? "Los resúmenes disponibles dependen de los permisos de tu cuenta." : "Actualiza el resumen para consultar tu operación.";
    }
    [RelayCommand(CanExecute = nameof(CanReload), AllowConcurrentExecutions = true)]
    private async Task ReloadAsync()
    {
        if (!CanReload) return;
        using var cancellation = new CancellationTokenSource(); reading = cancellation;
        var revision = session.Revision;
        Reset(); IsBusy = true; OnPropertyChanged(nameof(TodayLabel));
        StatusMessage = "Consultando el resumen…"; SalesMessage = OrdersMessage = "Consultando…";
        bool Current() => revision == session.Revision && !cancellation.IsCancellationRequested;
        async Task<bool> QueryAsync(Func<Task> query, Action? onFailure = null)
        {
            try { await query(); return true; }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return false; }
            catch (Exception ex)
            {
                logger.LogWarning("No se pudo consultar Inicio: {ErrorType}.", ex.GetType().Name);
                if (Current()) onFailure?.Invoke();
                return false;
            }
        }
        try
        {
            var queries = new List<Task<bool>>();
            if(reports?.CanReadKind(OperationalReport.Sales)==true) queries.Add(QueryAsync(async()=>
            {
                var today=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-6)).Date;
                var report=await reports.ReadAsync(new(OperationalReport.Sales,today,today),cancellation.Token);
                if(Current())DailySummary=$"Hoy (El Salvador): total neto de devoluciones USD {report.Total:N2} · mercadería sin impuesto USD {report.Net:N2} · entrega USD {report.Delivery:N2}";
            },()=>DailySummary="Resumen diario no disponible. Consulta Reportes para reintentar."));
            if (products.CanRead)
            {
                foreach (var low in new[] { false, true })
                {
                    var metric = low ? stockMetric : productsMetric;
                    queries.Add(QueryAsync(async () =>
                    {
                        var result = await products.SearchAsync(new(PageSize: 1, LowStockOnly: low), cancellation.Token);
                        if (!result.IsSuccess) throw new InvalidOperationException("Consulta de catálogo no disponible.");
                        if (Current()) metric.Value = result.Value!.Total;
                    }));
                }
            }
            if (sales.CanRead) queries.Add(QueryAsync(async () =>
            {
                var result = await sales.SearchAsync(new(PageSize: 5), cancellation.Token);
                if (!Current()) return;
                salesMetric.Value = result.Total;
                foreach (var item in result.Items.Take(5)) RecentSales.Add(item);
                SalesMessage = result.Items.Count == 0 ? "Aún no hay ventas registradas." : "";
            }, () => SalesMessage = "No se pudieron consultar las ventas. Vuelve a actualizar."));
            if (orders.CanRead) queries.Add(QueryAsync(async () =>
            {
                var result = await orders.SearchAsync(null, new(PageSize: 5), cancellation.Token);
                if (!Current()) return;
                ordersMetric.Value = result.Total;
                foreach (var item in result.Items.Take(5)) RecentOrders.Add(item);
                OrdersMessage = result.Items.Count == 0 ? "Aún no hay pedidos registrados." : "";
            }, () => OrdersMessage = "No se pudieron consultar los pedidos. Vuelve a actualizar."));
            var results = await Task.WhenAll(queries);
            if (!Current()) return;
            UpdatedAt = results.Any(result => result) ? DateTimeOffset.Now : null;
            StatusMessage = queries.Count == 0 ? "Tu cuenta no tiene resúmenes habilitados. Usa los accesos disponibles."
                : results.All(result => result) ? "Información actualizada. Los indicadores muestran el histórico registrado."
                : "Parte del resumen no está disponible. Los valores sin consultar se muestran como —; puedes volver a actualizar.";
        }
        finally { if (ReferenceEquals(reading, cancellation)) { reading = null; IsBusy = false; } }
    }
}
