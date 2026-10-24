namespace GoDental.Application.DTOs;

public sealed record RegisterCollectionNotice(Guid OperationId, long AccountId, byte[] ExpectedVersion, string Medium, string Outcome, string Reason);
public sealed class CollectionNoticeData
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public DateTimeOffset Date { get; set; }
    public DateTimeOffset LocalDate => Date.ToOffset(TimeSpan.FromHours(-6));
    public string User { get; set; } = "";
    public decimal Balance { get; set; }
    public DateTime Due { get; set; }
    public DateTime NextNotice { get; set; }
    public string Medium { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string Reason { get; set; } = "";
}
public sealed class CreditMovementData
{
    public long Id { get; set; }
    public long AccountId { get; set; }
    public DateTimeOffset Date { get; set; }
    public DateTimeOffset LocalDate => Date.ToOffset(TimeSpan.FromHours(-6));
    public string Type { get; set; } = "";
    public string Document { get; set; } = "";
    public string Sale { get; set; } = "";
    public decimal Delta { get; set; }
    public decimal CashRefund { get; set; }
    public decimal RunningBalance { get; set; }
    public decimal Charge => Math.Max(0, Delta);
    public decimal Reduction => Math.Max(0, -Delta);
}
public sealed record CustomerStatement(long CustomerId, string Code, string Customer, DateTimeOffset AsOf,
    IReadOnlyList<CreditAccountData> Accounts, IReadOnlyList<CreditMovementData> Movements)
{
    public decimal RecordedBalance => Accounts.Sum(a => a.Balance);
    public decimal CollectibleBalance => Accounts.Where(a => a.State is "PENDIENTE" or "PARCIAL" or "VENCIDA").Sum(a => a.Balance);
    public decimal CalculatedBalance => Movements.Sum(m => m.Delta);
    public decimal Difference => RecordedBalance - CalculatedBalance;
}
public sealed class PaymentReceiptData
{
    public long Id { get; set; }
    public long CustomerId { get; set; }
    public long AccountId { get; set; }
    public string Number { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Sale { get; set; } = "";
    public string Method { get; set; } = "";
    public string User { get; set; } = "";
    public string? Reference { get; set; }
    public DateTimeOffset Date { get; set; }
    public DateTimeOffset LocalDate => Date.ToOffset(TimeSpan.FromHours(-6));
    public decimal Amount { get; set; }
    public decimal? BalanceBefore { get; set; }
    public decimal? BalanceAfter { get; set; }
    public decimal CurrentBalance { get; set; }
    public bool Voided { get; set; }
    public DateTimeOffset AsOf { get; set; }
    public bool HistoricalDetailsMissing { get; set; }
}
