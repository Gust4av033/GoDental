using GoDental.Application.DTOs;
namespace GoDental.Application.Abstractions.Persistence;
public interface IBarcodeLabelRenderer
{
    ReceiptDocument RenderPreview(LabelContent content, LabelPrintOptions options);
}
