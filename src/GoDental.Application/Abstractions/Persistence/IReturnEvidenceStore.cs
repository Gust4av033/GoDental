using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IReturnEvidenceStore
{
    Task CheckRootAsync(string root,CancellationToken ct=default);
    Task<ReturnEvidence> SaveAsync(byte[] image,string name,int userId,CancellationToken ct=default);
    Task<bool> VerifyAsync(ReturnEvidence evidence,int userId,CancellationToken ct=default,string? root=null);
    Task<byte[]> ReadAsync(ReturnEvidence evidence,CancellationToken ct=default);
}
