using System.Data.Common;

namespace GoDental.Application.Abstractions.Persistence;

public interface IDbConnectionFactory
{
    Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
