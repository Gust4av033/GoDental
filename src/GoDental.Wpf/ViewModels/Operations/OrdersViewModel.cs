using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Domain.Rules;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class OrdersViewModel : MvpWorkspaceViewModel
{
    private readonly OrderService orders;
    private readonly CustomerService customers;
    private readonly ProductService products;
    private readonly IMvpDialogService dialogs;
    public ObservableCollection<OrderData> Items { get; } = [];
    public ObservableCollection<OrderEventData> Events { get; } = [];
    public IReadOnlyList<string> States { get; } = ["TODOS", "RECIBIDO", "PREPARANDO", "LISTO", "EN_RUTA", "ENTREGADO", "INCIDENCIA", "CANCELADO"];
    [ObservableProperty] private string state = "TODOS";
    [ObservableProperty] private OrderData? selected;
    [ObservableProperty] private OrderSnapshot? detail;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int page = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PageLabel))] private int total;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(EventsPageLabel))] private int eventsPage = 1;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(EventsPageLabel))] private int eventsTotal;
    [ObservableProperty] private bool isDetailOpen;
    public string PageLabel => $"{Total} pedidos · Página {Page}";
    public string EventsPageLabel => $"{EventsTotal} eventos · Página {EventsPage}";
    [RelayCommand] private void ReturnToList() { if (!IsBusy) IsDetailOpen = false; }
    [RelayCommand] private async Task OpenOrderAsync(OrderData? order)
    {
        if (!CanRead || IsBusy || order is null) return;
        Selected = order;
        await DetailCommand.ExecuteAsync(null);
    }
    public override bool CanRead => orders.CanRead;
    public bool CanManage => CanEdit && orders.CanManage;
    public bool CanCreate => CanManage && customers.CanRead && products.CanRead;
    public bool CanTransition => CanManage && Detail is not null && States.Any(s => OrderRules.CanTransition(Detail.Order.State, s));
    public bool CanLink => CanManage && Detail is not null && Detail.Order.SaleId is null;
    partial void OnSelectedChanged(OrderData? value) { Detail = null; Events.Clear(); EventsPage = 1; }
    partial void OnDetailChanged(OrderSnapshot? value) { IsDetailOpen = value is not null; PermissionsChanged(); }
    protected override void PermissionsChanged() { foreach (var n in new[] { nameof(CanManage), nameof(CanCreate), nameof(CanTransition), nameof(CanLink) }) OnPropertyChanged(n); }
    public OrdersViewModel(OrderService orders, CustomerService customers, ProductService products, IMvpDialogService dialogs, ISessionController session, MvpOperationService operations, ILogger<OrdersViewModel> logger) : base(session, operations, logger) { this.orders = orders; this.customers = customers; this.products = products; this.dialogs = dialogs; }
    protected override void Reset() { IsDetailOpen = false; Items.Clear(); Events.Clear(); Selected = null; Detail = null; Page = EventsPage = 1; Total = EventsTotal = 0; }
    [RelayCommand] private Task ReloadAsync() { Page = 1; return ReadAsync(LoadCoreAsync); }
    private async Task LoadCoreAsync(long r)
    {
        var result = await orders.SearchAsync(State == "TODOS" ? null : State, new(Page)); if (!Current(r)) return;
        var id = Selected?.Id; Items.Clear(); foreach (var o in result.Items) Items.Add(o); Selected = Items.FirstOrDefault(o => o.Id == id); Total = result.Total;
        StatusMessage = Total == 0 ? "No hay pedidos para este estado." : $"{Total} pedidos · Página {Page}";
    }
    [RelayCommand] private async Task NextAsync() { if (IsBusy || Page * 30 >= Total) return; Page++; await ReadAsync(LoadCoreAsync); }
    [RelayCommand] private async Task PreviousAsync() { if (IsBusy || Page <= 1) return; Page--; await ReadAsync(LoadCoreAsync); }
    [RelayCommand] private Task DetailAsync() => ReadAsync(async r => { if (Selected is not null) await DetailCoreAsync(Selected.Id, r); });
    private async Task DetailCoreAsync(long id, long r)
    {
        var result = await orders.ReadAsync(id); if (!Current(r) || Selected?.Id != id) return;
        Detail = result; IsDetailOpen = result is not null; Events.Clear(); EventsPage = 1; EventsTotal = result?.TotalEvents ?? 0;
        foreach (var e in result?.Events ?? []) Events.Add(e);
        StatusMessage = result is null ? "El pedido no está disponible." : $"Pedido {id} · {EventsTotal} eventos · Página 1";
    }
    [RelayCommand] private async Task NextEventsAsync() { if (IsBusy || EventsPage * 30 >= EventsTotal) return; EventsPage++; await ReadAsync(EventsCoreAsync); }
    [RelayCommand] private async Task PreviousEventsAsync() { if (IsBusy || EventsPage <= 1) return; EventsPage--; await ReadAsync(EventsCoreAsync); }
    private async Task EventsCoreAsync(long r)
    {
        if (Detail is null) return; var id = Detail.Order.Id;
        var result = await orders.EventsAsync(id, new(EventsPage)); if (!Current(r) || Detail?.Order.Id != id) return;
        Events.Clear(); foreach (var e in result.Items) Events.Add(e); EventsTotal = result.Total;
        StatusMessage = $"{EventsTotal} eventos · Página {EventsPage}";
    }
    private async Task ShowAsync(MvpFormModel form) { using (form) { IsEditing = true; try { await dialogs.ShowAsync(form); } finally { IsEditing = false; } } }
    [RelayCommand] private async Task CreateAsync()
    {
        if (!CanCreate) return; var draft = new OrderDraftModel(customers, products, () => Session.Revision);
        await ShowAsync(new MvpFormModel("Nuevo pedido", "Registrar pedido", Operation, async f =>
        {
            if (draft.IsBusy) throw new ArgumentException("Espera a que terminen de cargar los datos del pedido.");
            var customer = draft.Customer.Selected ?? throw new ArgumentException("Selecciona un cliente.");
            var id = Guid.NewGuid(); var request = new PlaceOrder(id, customer.Id, draft.ContactName.Trim(), draft.Phone.Trim(), draft.DeliveryAddress.Trim(), draft.Department.Trim(), draft.Municipality.Trim(), draft.Reference.Trim(), f.Date("scheduled"), f.Text("responsible"), null, f.Number("delivery"), draft.Items.Select(i => new OrderItem(i.ProductId, i.Quantity)).ToArray(), f.Text("reason"), draft.Address?.Id);
            await Operation.StartAsync(request, id, q => orders.PlaceAsync(q), receipt => ReadAsync(async r => { await LoadCoreAsync(r); Selected = Items.FirstOrDefault(o => o.Id == receipt.Id); if (Selected is not null) await DetailCoreAsync(receipt.Id, r); }));
        }) { Body = draft, Description = "Selecciona un cliente para precargar sus datos; revisa Datos de entrega y agrega los productos. El pedido no reserva stock ni representa un cobro." }
            .Input("responsible", "Responsable de entrega")
            .Calendar("scheduled", "Fecha prevista").Input("delivery", "Cargo de entrega", "0").Input("reason", "Motivo"));
    }
    private async Task RefreshOrderAsync(long id, long revision)
    {
        await LoadCoreAsync(revision); if (!Current(revision)) return;
        Selected = Items.FirstOrDefault(o => o.Id == id);
        if (Selected is not null) await DetailCoreAsync(id, revision);
        else StatusMessage = "Pedido actualizado. No está en la página o filtro actual; cambia el filtro para consultarlo.";
    }
    [RelayCommand] private async Task TransitionAsync()
    {
        if (!CanTransition || Detail is null) return; var order = Detail.Order;
        var states = States.Where(s => OrderRules.CanTransition(order.State, s)).ToArray();
        await ShowAsync(new MvpFormModel($"Estado del pedido {order.Id}", "Actualizar estado", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new OrderTransition(id, order.Id, order.RowVersion.ToArray(), f.Choice<string>("state"), f.Text("reason"));
            await Operation.StartAsync(request, id, q => orders.TransitionAsync(q), _ => ReadAsync(r => RefreshOrderAsync(order.Id, r)));
        }) { Description = $"Estado actual: {order.State}. En ruta y entregado requieren venta vinculada. Cancelar el pedido no revierte ventas ni pagos." }.Select("state", "Nuevo estado", states, states.FirstOrDefault(), "").Input("reason", "Motivo / observación de entrega"));
    }
    [RelayCommand] private async Task LinkAsync()
    {
        if (!CanLink || Detail is null) return; var order = Detail.Order;
        await ShowAsync(new MvpFormModel($"Vincular venta · Pedido {order.Id}", "Vincular venta", Operation, async f =>
        {
            if (!long.TryParse(f.Text("sale"), out var sale) || sale <= 0) throw new ArgumentException("Escribe el ID de la venta confirmada; puedes consultarlo en Ventas y PDF.");
            var id = Guid.NewGuid(); var request = new LinkOrderSale(id, order.Id, order.RowVersion.ToArray(), sale, f.Text("reason"));
            await Operation.StartAsync(request, id, q => orders.LinkSaleAsync(q), _ => ReadAsync(r => RefreshOrderAsync(order.Id, r)));
        }) { Description = "El servicio verifica cliente, productos, cantidades y cargo. El estado de pago procede de la venta y su cuenta por cobrar." }.Input("sale", "ID de venta confirmada").Input("reason", "Motivo"));
    }
}
