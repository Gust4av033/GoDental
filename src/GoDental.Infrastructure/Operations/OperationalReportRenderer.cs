using System.Globalization;
using GoDental.Application.Abstractions.Persistence;
using GoDental.Application.DTOs;
using GoDental.Application.Services;
using Microsoft.Extensions.Options;
using QuestPDF.Fluent;
namespace GoDental.Infrastructure.Operations;
public sealed class OperationalReportRenderer(IOptions<ReceiptPdfOptions> options):IReportRenderer
{
    private static string Money(decimal? n)=>n?.ToString("$ #,##0.00",CultureInfo.GetCultureInfo("en-US"))??"Restringido";
    public ReceiptDocument Render(OperationalReportData r,PrintLayout layout)
    {
        if(r.Rows.Count>5000)throw new ArgumentException("El reporte supera 5000 filas.");
        var title=r.Filter.Kind switch{OperationalReport.Sales=>"VENTAS Y MARGEN BRUTO",OperationalReport.Inventory=>"INVENTARIO ACTUAL",OperationalReport.Expiry=>"LOTES Y VENCIMIENTOS",_=>"COMPRAS RECIBIDAS"};
        return new CreditDocumentRenderer(options).Render(layout,title,$"{r.Filter.From:dd/MM/yyyy} — {r.Filter.To:dd/MM/yyyy}",r.AsOf.ToOffset(TimeSpan.FromHours(-6)).ToString("dd/MM/yyyy HH:mm"),c=>
        {
            if(r.Filter.Kind==OperationalReport.Sales)
            {
                c.Item().Text($"Total con impuesto y entrega: {Money(r.Total)} · Impuesto: {Money(r.Tax)} · Entrega: {Money(r.Delivery)}").Bold();
                c.Item().Text($"Mercadería sin impuesto: {Money(r.Net)}");
                if(r.CostsVisible)c.Item().Text($"Costo histórico neto: {Money(r.Cost)} · Margen bruto: {Money(r.Margin)}").Bold();
                c.Item().Text("Ventas y compensaciones según su fecha local. Margen bruto de mercadería; no es utilidad neta. Categoría de referencia actual.").FontSize(8);
            }
            else if(r.Filter.Kind==OperationalReport.Purchases)c.Item().Text("Valor recibido puesto en bodega: "+Money(r.Total)).Bold();
            else c.Item().Text("Fotografía actual; las fechas seleccionadas no reconstruyen inventario histórico. Valor bodega + custodia: "+Money(r.Value)).FontSize(8);
            if(r.Rows.Count==0)c.Item().Text("No hay datos para esta consulta.");
            foreach(var row in ReportService.Group(r))c.Item().BorderBottom(.5f).PaddingVertical(4).Column(b=>
            {
                if(r.Filter.Kind==OperationalReport.Sales)
                {
                    b.Item().Text(row.GroupLabel).SemiBold();b.Item().Text($"Unidades netas {row.Quantity:0.####} · Total {Money(row.Amount)} · Mercadería {Money(row.Net)}");
                    if(r.CostsVisible)b.Item().Text($"Costo {Money(row.Cost)} · Margen {Money(row.Margin)}");
                }
                else
                {
                    b.Item().Text(row.Document+" · "+row.Product).SemiBold();b.Item().Text(row.Warehouse+" · "+row.Party);
                    if(r.Filter.Kind==OperationalReport.Inventory)
                    {
                        b.Item().Text($"Físico {row.Stock:0.####} · Reservado {row.Reserved:0.####} · Vendible {row.Sellable:0.####} · Mínimo {row.Minimum:0.####} · Custodia {row.InRoute:0.####}");
                        if(r.CostsVisible)b.Item().Text($"Valor bodega {Money(row.Value)} · Valor custodia {Money(row.RouteValue)}");
                    }
                    else if(r.Filter.Kind==OperationalReport.Expiry)b.Item().Text($"Lote {row.Lot} · Vence {row.Expires:dd/MM/yyyy} · Unidades {row.Quantity:0.####}");
                    else b.Item().Text($"{row.LocalDate:dd/MM/yyyy} · Unidades {row.Quantity:0.####} · Mercadería {Money(row.Cost)} · Gastos {Money(row.Freight)} · Puesto bodega {Money(row.Amount)}");
                    if(!string.IsNullOrEmpty(row.Note))b.Item().Text(row.Note).FontSize(8);
                }
            });
        });
    }
}
