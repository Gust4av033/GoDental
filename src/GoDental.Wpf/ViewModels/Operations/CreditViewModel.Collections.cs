using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using GoDental.Application.Validators;
using GoDental.Wpf.Dialogs;
namespace GoDental.Wpf.ViewModels.Operations;

public sealed partial class CreditViewModel
{
    private readonly CollectionService? collections;
    private readonly IPdfFileService? files;
    private readonly IPdfPrintService? printer;
    public ObservableCollection<CollectionNoticeData> Notices { get; } = [];
    public ObservableCollection<CreditMovementData> StatementMovements { get; } = [];
    [ObservableProperty] private int selectedDetailTab;
    [ObservableProperty] private CustomerPaymentData? selectedPayment;
    [ObservableProperty] private CustomerStatement? statement;
    [ObservableProperty] private bool noticesDueOnly;
    [ObservableProperty] private int noticePage = 1;
    [ObservableProperty] private int noticeTotal;
    [ObservableProperty] private int statementPage = 1;
    public string NoticePageLabel => $"{NoticeTotal} avisos de la cuenta · Página {NoticePage}";
    public string StatementPageLabel => $"{Statement?.Movements.Count ?? 0} movimientos del cliente · Página {StatementPage}";
    public bool CanRegisterNotice => CanEdit && collections?.CanManage == true && Selected is { NoticeDue: true, Balance: > 0 };
    public bool CanPrintCredit => CanEdit && collections?.CanPrint == true && Selected is not null && files is not null && printer is not null;
    public bool CanPrintPayment => CanPrintCredit && SelectedPayment is not null;
    private void CollectionPermissionsChanged() { OnPropertyChanged(nameof(CanRegisterNotice)); OnPropertyChanged(nameof(CanPrintCredit)); OnPropertyChanged(nameof(CanPrintPayment)); OnPropertyChanged(nameof(CanUsePreview)); }
    partial void OnSelectedPaymentChanged(CustomerPaymentData? value) { ClearCreditPreview(); OnPropertyChanged(nameof(CanPrintPayment)); }
    partial void OnNoticePageChanged(int value) => OnPropertyChanged(nameof(NoticePageLabel));
    partial void OnNoticeTotalChanged(int value) => OnPropertyChanged(nameof(NoticePageLabel));
    partial void OnStatementChanged(CustomerStatement? value) => OnPropertyChanged(nameof(StatementPageLabel));
    partial void OnStatementPageChanged(int value) => OnPropertyChanged(nameof(StatementPageLabel));
    private void ResetCollectionDetail()
    {
        Notices.Clear(); StatementMovements.Clear(); Statement = null; SelectedPayment = null; NoticePage = StatementPage = 1; NoticeTotal = 0; SelectedDetailTab = 0; ClearCreditPreview();
    }
    [RelayCommand] private Task LoadNoticesAsync() { NoticePage = 1; return ReadAsync(LoadNoticesCoreAsync); }
    private async Task LoadNoticesCoreAsync(long r)
    {
        if (collections is null || Selected is not { } account) return;
        var result = await collections.NoticesAsync(account.Id, new(NoticePage));
        if (!Current(r) || Selected?.Id != account.Id) return;
        Notices.Clear(); foreach (var row in result.Items) Notices.Add(row); NoticeTotal = result.Total;
        StatusMessage = Notices.Count == 0 ? "No hay avisos registrados para esta cuenta." : "Historial de gestiones consultado; la aplicación no envía mensajes.";
    }
    [RelayCommand] private async Task NextNoticeAsync() { if (IsBusy || NoticePage * 30 >= NoticeTotal) return; NoticePage++; await ReadAsync(LoadNoticesCoreAsync); }
    [RelayCommand] private async Task PreviousNoticeAsync() { if (IsBusy || NoticePage == 1) return; NoticePage--; await ReadAsync(LoadNoticesCoreAsync); }
    [RelayCommand] private Task RegisterNoticeAsync() => ReadAsync(async r =>
    {
        if (collections?.CanManage != true || Selected is not { NoticeDue: true } account) return;
        using var form = new MvpFormModel("Registrar aviso de cobranza", "Registrar aviso", Operation, async f =>
        {
            var id = Guid.NewGuid(); var request = new RegisterCollectionNotice(id, account.Id, account.RowVersion.ToArray(), f.Choice<string>("medium"), f.Choice<string>("outcome"), f.Text("reason"));
            await Operation.StartAsync(request, id, q => collections.RegisterAsync(q), async _ =>
            {
                var revision = Session.Revision; await LoadCoreAsync(revision); if (!Current(revision)) return; NoticePage = 1; await LoadNoticesCoreAsync(revision);
            });
        }) { Description = $"Cuenta {account.Id} · {account.Sale} · Saldo consultado {account.Balance:N2}. Registra sólo la gestión que realizaste; no se enviará ningún mensaje." };
        form.Select("medium", "Medio de la gestión", CollectionValidator.Media, "INTERNO").Select("outcome", "Resultado", CollectionValidator.Outcomes, "GESTION_INTERNA").Input("reason", "Observación / motivo");
        IsEditing = true; try { await dialogs.ShowAsync(form); } finally { IsEditing = false; }
    });
    [RelayCommand] private Task LoadStatementAsync() => ReadAsync(async r =>
    {
        if (collections is null || Selected is not { } account) return;
        var result = await collections.StatementAsync(account.CustomerId);
        if (!Current(r) || Selected?.CustomerId != account.CustomerId) return;
        Statement = result; StatementPage = 1; FillStatementPage();
        StatusMessage = result is null ? "El cliente no está disponible." : result.Difference != 0 ? "El saldo registrado y el historial disponible difieren; revisa antes de entregar el estado." : "Estado de cuenta consultado con abonos y posventa aplicada.";
    });
    private void FillStatementPage() { StatementMovements.Clear(); foreach (var m in Statement?.Movements.Skip((StatementPage - 1) * 30).Take(30) ?? []) StatementMovements.Add(m); }
    [RelayCommand] private void NextStatement() { if (IsBusy || StatementPage * 30 >= Statement?.Movements.Count) return; StatementPage++; FillStatementPage(); }
    [RelayCommand] private void PreviousStatement() { if (IsBusy || StatementPage == 1) return; StatementPage--; FillStatementPage(); }

    public ReceiptPrintSettingsModel PrintSettings { get; } = new();
    private PrintLayout? previewLayout;
    private long previewCustomer;
    private long? previewPayment;
    private string previewName = "";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsReceiptPreviewOpen)), NotifyPropertyChangedFor(nameof(PreviewPage)), NotifyPropertyChangedFor(nameof(PreviewPageLabel)), NotifyPropertyChangedFor(nameof(PreviewWidth)), NotifyPropertyChangedFor(nameof(PreviewHeight)), NotifyPropertyChangedFor(nameof(CanUsePreview))] private ReceiptDocument? receiptPreview;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreviewPage)), NotifyPropertyChangedFor(nameof(PreviewPageLabel))] private int previewPageNumber = 1;
    public bool IsReceiptPreviewOpen => ReceiptPreview is not null;
    public string PreviewCaption => previewName;
    public bool CanUsePreview { get { try { return !IsBusy && collections?.CanPrint == true && ReceiptPreview is not null && previewLayout == PrintSettings.Layout() && Selected?.CustomerId == previewCustomer && (previewPayment is null || SelectedPayment?.Id == previewPayment); } catch (ArgumentException) { return false; } } }
    public byte[]? PreviewPage => ReceiptPreview?.Pages.ElementAtOrDefault(PreviewPageNumber - 1);
    public string PreviewPageLabel => $"Página {PreviewPageNumber} de {ReceiptPreview?.Pages.Count ?? 0}";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(PreviewWidth)), NotifyPropertyChangedFor(nameof(PreviewHeight)), NotifyPropertyChangedFor(nameof(ZoomLabel))] private decimal previewZoom = .75m;
    public string ZoomLabel => $"{PreviewZoom:P0}";
    [RelayCommand] private void ZoomIn() { if (PreviewZoom < 2) PreviewZoom += .25m; }
    [RelayCommand] private void ZoomOut() { if (PreviewZoom > .5m) PreviewZoom -= .25m; }
    public double PreviewWidth => (double)(ReceiptPreview?.WidthMm ?? 215.9m) * (double)PreviewZoom * 96 / 25.4;
    public double PreviewHeight => (double)(ReceiptPreview?.HeightMm ?? 279.4m) * (double)PreviewZoom * 96 / 25.4;
    private void ClearCreditPreview() { ReceiptPreview = null; previewPayment = null; previewCustomer = 0; PreviewPageNumber = 1; PreviewZoom = .75m; }
    [RelayCommand] private void CloseReceiptPreview() { if (!IsBusy) ClearCreditPreview(); }
    [RelayCommand] private void NextPreviewPage() { if (PreviewPageNumber < ReceiptPreview?.Pages.Count) PreviewPageNumber++; }
    [RelayCommand] private void PreviousPreviewPage() { if (PreviewPageNumber > 1) PreviewPageNumber--; }
    [RelayCommand] private Task PreviewStatementAsync() => GenerateCreditPreviewAsync(false);
    [RelayCommand] private Task PreviewPaymentAsync() => GenerateCreditPreviewAsync(true);
    [RelayCommand] private Task PreviewReceiptAsync() => GenerateCreditPreviewAsync(previewPayment is not null);
    private Task GenerateCreditPreviewAsync(bool payment) => ReadAsync(async r =>
    {
        if (collections?.CanPrint != true || Selected is not { } account || payment && SelectedPayment is null) return;
        var paymentId = payment ? SelectedPayment!.Id : (long?)null; var layout = PrintSettings.Layout();
        var document = paymentId is { } id ? await collections.PreviewPaymentAsync(id, layout) : await collections.PreviewStatementAsync(account.CustomerId, layout);
        if (!Current(r) || Selected?.CustomerId != account.CustomerId || payment && SelectedPayment?.Id != paymentId || PrintSettings.Layout() != layout) return;
        previewCustomer = account.CustomerId; previewPayment = paymentId; previewLayout = layout; previewName = payment ? $"GoDental-abono-{paymentId}" : $"GoDental-estado-cuenta-{account.CustomerId}";
        ReceiptPreview = document; PreviewPageNumber = 1; SelectedDetailTab = 4; OnPropertyChanged(nameof(PreviewCaption));
        StatusMessage = "Vista previa generada. Revisa los datos y guarda/imprime este documento a tamaño real (100%).";
    });
    [RelayCommand] private Task ExportAsync() => ReadAsync(async r =>
    {
        // ReadAsync marca IsBusy; la identidad/layout/permisos se comprueban sin depender del estado del botón.
        if (!CreditPreviewValid() || files is null || ReceiptPreview is null) return;
        var ok = await files.SaveAsync(ReceiptPreview.Pdf, previewName + ".pdf"); if (Current(r)) StatusMessage = ok ? "PDF guardado." : "Guardado cancelado.";
    });
    [RelayCommand] private Task PrintReceiptAsync() => ReadAsync(async r =>
    {
        if (!CreditPreviewValid() || printer is null || ReceiptPreview is null) return;
        var ok = await printer.PrintAsync(ReceiptPreview, previewName); if (Current(r)) StatusMessage = ok ? "Documento enviado a la cola de impresión." : "Impresión cancelada.";
    });
    private bool CreditPreviewValid() { try { return collections?.CanPrint == true && ReceiptPreview is not null && previewLayout == PrintSettings.Layout() && Selected?.CustomerId == previewCustomer && (previewPayment is null || SelectedPayment?.Id == previewPayment); } catch (ArgumentException) { return false; } }
}
