namespace GoDental.Application.DTOs;
public sealed record SaveImportation(Guid OperationId, long OrderId, byte[] ExpectedOrderVersion, string? Courier, string? Guide, DateTime? ExpectedDate, string Reason);
public sealed record ImportEvent(Guid OperationId, long OrderId, long ImportId, byte[] ExpectedVersion, string State, string? Courier, string? Guide, DateTime? ExpectedDate, string Reason);
public sealed record ImportExpense(Guid OperationId, long OrderId, long ImportId, byte[] ExpectedVersion, string Type, decimal Amount, short CurrencyId, decimal Rate, DateTime Date, string Description, string Reason);
public sealed record VoidImportExpense(Guid OperationId, long OrderId, long ImportId, byte[] ExpectedVersion, long ExpenseId, string Reason);
public sealed record CloseImportCosts(Guid OperationId, long OrderId, long ImportId, byte[] ExpectedVersion, decimal Rate, string Reason);
public sealed record PrepareImportReceipt(Guid OperationId, long ImportId, byte[] ExpectedImportVersion, PreparePurchaseReceipt Receipt);
public sealed class ImportationData
{
    public long Id { get; set; }
    public long OrderId { get; set; }
    public string? Courier { get; set; }
    public string? Guide { get; set; }
    public string State { get; set; } = "";
    public DateTime? ExpectedDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public bool CostsClosed { get; set; }
    public decimal? Rate { get; set; }
    public decimal TotalExpensesUsd { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
public sealed class ImportEventData
{
    public long Id { get; set; }
    public DateTimeOffset Date { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public string User { get; set; } = "";
}
public sealed class ImportExpenseData
{
    public long Id { get; set; }
    public string Type { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "";
    public decimal Rate { get; set; }
    public decimal AmountUsd { get; set; }
    public DateTime? Date { get; set; }
    public bool Voided { get; set; }
}
public sealed record ImportationSnapshot(ImportationData Importation, IReadOnlyList<ImportExpenseData> Expenses, IReadOnlyList<ImportEventData> Events, int EventTotal = 0, int EventPage = 1)
{
    public decimal ActiveExpensesUsd => Expenses.Where(x => !x.Voided).Sum(x => x.AmountUsd);
}
