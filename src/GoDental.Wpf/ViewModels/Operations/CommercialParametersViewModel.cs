using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.Abstractions.Security;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Wpf.Dialogs;
using Microsoft.Extensions.Logging;

namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class CommercialParametersViewModel : MvpWorkspaceViewModel
{
    private readonly CommercialParametersService service;
    private readonly IMvpDialogService dialogs;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanChange))] private CommercialParameters? parameters;
    public override bool CanRead => service.CanManage;
    public bool CanChange => CanEdit && Parameters is not null;
    protected override void Reset() => Parameters = null;
    protected override void PermissionsChanged() => OnPropertyChanged(nameof(CanChange));
    public CommercialParametersViewModel(CommercialParametersService service, IMvpDialogService dialogs, ISessionController session,
        MvpOperationService operations, ILogger<CommercialParametersViewModel> logger) : base(session, operations, logger)
    { this.service = service; this.dialogs = dialogs; }
    [RelayCommand] private Task ReloadAsync() => ReadAsync(async revision =>
    {
        Parameters = null;
        var result = await service.ReadAsync();
        if (!Current(revision)) return;
        Parameters = result; StatusMessage = "Valores vigentes. Los cambios se aplican a los nuevos cálculos de venta.";
    });
    [RelayCommand] private async Task ChangeAsync()
    {
        if (!CanChange || Parameters is not { } original) return;
        using var form = new MvpFormModel("Parámetros de descuentos", "Guardar parámetros", Operation, async f =>
        {
            var id = Guid.NewGuid();
            var request = new ChangeCommercialParameters(id, f.Number("justification"), f.Number("authorization"),
                original.JustificationVersion.ToArray(), original.AuthorizationVersion.ToArray(), f.Text("reason"));
            await Operation.StartAsync(request, id, r => service.SaveAsync(r), _ => ReloadAsync());
        }) { Description = "Por encima del primer umbral se exige motivo; por encima del segundo, autorización administrativa. Usa valores de 0 a 100, con justificación menor o igual a autorización. Afecta a todas las estaciones y exige recalcular carritos abiertos." };
        form.Input("justification", "Exigir justificación por encima de (%)", original.Justification.ToString("0.####", CultureInfo.CurrentCulture))
            .Input("authorization", "Exigir autorización por encima de (%)", original.Authorization.ToString("0.####", CultureInfo.CurrentCulture))
            .Input("reason", "Motivo del cambio");
        IsEditing = true;
        try { await dialogs.ShowAsync(form); }
        finally { IsEditing = false; }
    }
}
