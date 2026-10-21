using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IReceiptRenderer
{
    byte[] Render(SaleSnapshot sale, PrintLayout? layout = null, ReceiptKind kind = ReceiptKind.Sale);
    ReceiptDocument RenderPreview(SaleSnapshot sale, PrintLayout? layout = null, ReceiptKind kind = ReceiptKind.Sale);
}
