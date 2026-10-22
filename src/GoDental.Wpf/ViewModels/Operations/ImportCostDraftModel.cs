using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Validators;
using GoDental.Domain.Rules;
namespace GoDental.Wpf.ViewModels.Operations;
public sealed record ImportCostPreview(string Product,decimal NetValue,decimal PurchaseUnitUsd,decimal ExpenseUsd,decimal LandedUnitUsd);
public sealed partial class ImportCostDraftModel(PurchaseOrderSnapshot order,ImportationSnapshot import) : ObservableObject
{
    [ObservableProperty] private string rate=order.Order.EstimatedRate.ToString("0.########");
    [ObservableProperty] private string statusMessage="Calcula y revisa el reparto antes de cerrar.";
    public decimal? ReviewedRate {get;private set;}
    public decimal TotalExpensesUsd => import.ActiveExpensesUsd;
    public ObservableCollection<ImportCostPreview> Items {get;}=[];
    partial void OnRateChanged(string value){ReviewedRate=null;Items.Clear();StatusMessage="Actualiza el reparto con el cambio indicado.";}
    [RelayCommand] private void Calculate()
    {
        try
        {
            ReviewedRate=null;Items.Clear();var fx=CheckoutModel.Parse(Rate,"cambio real");
            if(!PurchaseValidator.Rate(fx)||order.Order.Currency=="USD"&&fx!=1)throw new ArgumentException("Usa cambio positivo de hasta ocho decimales; USD requiere 1.");
            var budgets=ImportRules.Allocate(TotalExpensesUsd,order.Lines.Select(l=>(l.Id,l.Total)).ToArray());
            foreach(var line in order.Lines)
            {
                var cost=PurchaseRules.PurchaseCostUsd(line.UnitCost,line.Discount,line.Quantity,fx);var landed=cost+PurchaseRules.Cost(budgets[line.Id]/line.Quantity);
                if(!PurchaseValidator.Cost(cost)||!PurchaseValidator.Cost(landed))throw new ArgumentException("El costo excede el rango unitario.");
                Items.Add(new($"{line.Sku} · {line.Name}",line.Total,cost,budgets[line.Id],landed));
            }
            ReviewedRate=fx;StatusMessage="Reparto calculado por valor neto. Los centavos residuales se distribuyen por resto mayor; el servidor volverá a validar gastos y versión.";
        }
        catch(ArgumentException ex){Items.Clear();StatusMessage=ex.Message;}
    }
}
