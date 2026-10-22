using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Domain.Rules;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed partial class PurchasesViewModel
{
    [ObservableProperty] private ImportationSnapshot? importation;
    [ObservableProperty] private ImportExpenseData? selectedImportExpense;
    [ObservableProperty] private int importEventPage = 1;
    public string ImportEventsLabel => $"{Importation?.EventTotal ?? 0} eventos · Página {ImportEventPage}";
    public bool HasImportation => Importation is not null;
    public bool CanCreateImport => CanManage && importService is not null && Importation is null && Detail is {} d && PurchaseRules.CanReceive(d.Order.State) && d.Lines.All(l=>l.Received==0) && d.Receipts.All(r=>r.State=="ANULADA");
    public bool CanEditImport => CanManage && importService is not null && Importation is not null;
    public bool CanEditImportCosts => CanEditImport && !Importation!.Importation.CostsClosed;
    public bool CanVoidImportExpense => CanEditImportCosts && SelectedImportExpense is {Voided:false};
    private void ImportPermissionsChanged() { foreach(var n in new[]{nameof(HasImportation),nameof(CanCreateImport),nameof(CanEditImport),nameof(CanEditImportCosts),nameof(CanVoidImportExpense),nameof(ImportEventsLabel)})OnPropertyChanged(n); }
    partial void OnImportationChanged(ImportationSnapshot? value) { SelectedImportExpense=null; PermissionsChanged(); }
    partial void OnSelectedImportExpenseChanged(ImportExpenseData? value) => OnPropertyChanged(nameof(CanVoidImportExpense));
    partial void OnImportEventPageChanged(int value) => OnPropertyChanged(nameof(ImportEventsLabel));
    [RelayCommand] private async Task CreateImportAsync()
    {
        if(!CanCreateImport || Detail is not {} source || importService is null)return;
        using var form=new MvpFormModel("Crear importación","Crear importación",Operation,async f=>
        {
            var id=Guid.NewGuid();var r=new SaveImportation(id,source.Order.Id,source.Order.RowVersion.ToArray(),f.Text("courier"),f.Text("guide"),f.Date("eta"),f.Text("reason"));
            await Operation.StartAsync(r,id,x=>importService.SaveAsync(x),_=>RefreshAsync(source.Order.Id));
        }){Description="Una importación por orden confirmada. Courier y guía son opcionales. Registrar logística no ingresa stock ni reserva unidades."};
        form.Input("courier","Courier (opcional)").Input("guide","Guía (opcional)").Calendar("eta","Fecha estimada (opcional)").Input("reason","Motivo (hasta 300 caracteres)");await DialogAsync(form);
    }
    [RelayCommand] private async Task ImportEventAsync()
    {
        if(!CanEditImport || Detail is not {} order || Importation?.Importation is not {} source || importService is null)return;
        var states=new[]{"CONFIRMADA","EN_PRODUCCION","DESPACHADA","EN_TRANSITO","ADUANA",source.State}.Distinct().Where(s=>ImportRules.CanMove(source.State,s)).ToArray();
        using var form=new MvpFormModel("Registrar evento logístico","Guardar evento",Operation,async f=>
        {
            var id=Guid.NewGuid();var r=new ImportEvent(id,order.Order.Id,source.Id,source.RowVersion.ToArray(),f.Choice<string>("state"),f.Text("courier"),f.Text("guide"),f.Date("eta"),f.Text("reason"));
            await Operation.StartAsync(r,id,x=>importService.SaveAsync(x),_=>RefreshAsync(order.Order.Id));
        }){Description="Agrega un evento al historial, sin sobrescribir eventos anteriores. Conserva el estado para registrar una nota o corregir guía/estimación. Los estados recibidos se calculan al aplicar recepciones."};
        form.Select("state","Estado logístico",states,source.State,"").Input("courier","Courier",source.Courier??"").Input("guide","Guía",source.Guide??"").Calendar("eta","Fecha estimada").Input("reason","Descripción / motivo (hasta 300 caracteres)");
        form.Fields.Single(f=>f.Key=="eta").Date=source.ExpectedDate;await DialogAsync(form);
    }
    [RelayCommand] private async Task AddImportExpenseAsync()
    {
        if(!CanEditImportCosts || Detail is not {} order || Importation?.Importation is not {} source || importService is null || catalogs is null)return;
        using var form=new MvpFormModel("Registrar gasto de importación","Guardar gasto",Operation,async f=>
        {
            var id=Guid.NewGuid();var currency=f.Choice<PurchaseCurrency>("currency");
            var r=new ImportExpense(id,order.Order.Id,source.Id,source.RowVersion.ToArray(),f.Choice<string>("type"),f.Number("amount"),currency.Id,f.Number("rate"),f.Date("date")??throw new ArgumentException("Indica la fecha del gasto."),f.Text("description"),f.Text("reason"));
            await Operation.StartAsync(r,id,x=>importService.SaveAsync(x),_=>RefreshAsync(order.Order.Id));
        }){Description="Importe en moneda origen; cambio histórico = USD por unidad de moneda (USD=1). El importe USD se redondea a centavos en el servidor. Para corregir un gasto, anúlalo y registra otro antes de cerrar costos."};
        var types=new[]{"FLETE","IMPUESTO","ADUANA","MANEJO","SEGURO","OTRO"};
        form.Select("type","Tipo de gasto",types,"FLETE","").Input("amount","Importe origen").Select("currency","Moneda",catalogs.Currencies,catalogs.Currencies.FirstOrDefault(x=>x.Code=="USD")).Input("rate","USD por unidad de moneda","1").Calendar("date","Fecha del gasto").Input("description","Descripción (hasta 250 caracteres)").Input("reason","Motivo (hasta 300 caracteres)");
        form.Fields.Single(f=>f.Key=="date").Date=DateTime.Today;await DialogAsync(form);
    }
    [RelayCommand] private async Task VoidImportExpenseAsync()
    {
        if(!CanVoidImportExpense || Detail is not {} order || Importation?.Importation is not {} source || SelectedImportExpense is not {} expense || importService is null)return;
        using var form=new MvpFormModel("Anular gasto","Anular gasto",Operation,async f=>
        {
            var id=Guid.NewGuid();var r=new VoidImportExpense(id,order.Order.Id,source.Id,source.RowVersion.ToArray(),expense.Id,f.Text("reason"));
            await Operation.StartAsync(r,id,x=>importService.SaveAsync(x),_=>RefreshAsync(order.Order.Id));
        }){Description=$"Gasto #{expense.Id}: {expense.Type}, USD {expense.AmountUsd:0.00}. Se conserva anulado en el historial; deja de participar en el reparto."};
        form.Input("reason","Motivo (hasta 300 caracteres)");await DialogAsync(form);
    }
    [RelayCommand] private async Task CloseImportCostsAsync()
    {
        if(!CanEditImportCosts || Detail is not {} order || Importation is not {} source || importService is null)return;
        var draft=new ImportCostDraftModel(order,source);
        using var form=new MvpFormModel("Cerrar costos de importación","Cerrar costos",Operation,async f=>
        {
            var rate=CheckoutModel.Parse(draft.Rate,"cambio real");
            if(draft.ReviewedRate!=rate)throw new ArgumentException("Calcula y revisa el reparto con el cambio actual antes de confirmar.");
            var id=Guid.NewGuid();var r=new CloseImportCosts(id,order.Order.Id,source.Importation.Id,source.Importation.RowVersion.ToArray(),rate,f.Text("reason"));
            await Operation.StartAsync(r,id,x=>importService.SaveAsync(x),_=>RefreshAsync(order.Order.Id));
        }){Body=draft,Description="Cierre definitivo: confirma que ya registraste todos los gastos y el cambio real de la mercadería. No se reabre ni admite gastos posteriores. Los parciales consumirán este presupuesto; no cambia stock al cerrar."};
        form.Input("reason","Motivo / confirmación de costos completos (hasta 300 caracteres)");await DialogAsync(form);
    }
    [RelayCommand] private Task NextImportEventsAsync() => Importation is {} s && ImportEventPage*30<s.EventTotal ? LoadImportEventsAsync(ImportEventPage+1) : Task.CompletedTask;
    [RelayCommand] private Task PreviousImportEventsAsync() => ImportEventPage>1 ? LoadImportEventsAsync(ImportEventPage-1) : Task.CompletedTask;
    private Task LoadImportEventsAsync(int page) => ReadAsync(async revision=>
    {
        if(importService is null || Detail is not {} order)return;
        var snapshot=await importService.ReadAsync(order.Order.Id,eventPage:page);
        if(!Current(revision)||Detail?.Order.Id!=order.Order.Id)return;
        Importation=snapshot;ImportEventPage=page;
    });
}
