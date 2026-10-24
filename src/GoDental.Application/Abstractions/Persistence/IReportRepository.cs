using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IReportRepository { Task<OperationalReportData> ReportAsync(ReportFilter filter,int userId,CancellationToken ct); }
public interface IReportRenderer { ReceiptDocument Render(OperationalReportData report,PrintLayout layout); }
