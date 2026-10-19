using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IReceiptRenderer
{
    byte[] Render(SaleSnapshot sale);
}
