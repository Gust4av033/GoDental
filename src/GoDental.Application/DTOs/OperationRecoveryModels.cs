namespace GoDental.Application.DTOs;

public sealed record OperationTicket(Guid OperationId, string Type, string RequestHash);
public enum OperationResolutionState { Unconfirmed, InProgress, Confirmed, RejectedDefinitively }
public sealed record OperationResolution(OperationResolutionState State, MvpReceipt? Receipt = null);
public sealed record PendingOperation(string Scope, long UserId, long? WorkstationId, OperationTicket Ticket, DateTimeOffset Created);
public sealed record RecoveredOperation(PendingOperation Pending, OperationResolution Resolution);
