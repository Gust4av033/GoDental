using System.Data.Common;
using GoDental.Application.DTOs;

namespace GoDental.Application.Abstractions.Configuration;

public interface IWorkstationConnectionOpener
{
    Task<DbConnection> OpenAsync(WorkstationSettings settings, CancellationToken ct = default);
}
