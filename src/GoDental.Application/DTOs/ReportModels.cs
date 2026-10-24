namespace GoDental.Application.DTOs;
public enum OperationalReport { Sales, Inventory, Expiry, Purchases }
public enum ReportGrouping { Document, Day, Product, Category, Customer }
public sealed record ReportFilter(OperationalReport Kind,DateTime From,DateTime To,ReportGrouping Group=ReportGrouping.Document);
public sealed class ReportRow
{
    public int ProductId { get; set; }
    public int CategoryId { get; set; }
    public long CustomerId { get; set; }
    public DateTimeOffset Date { get; set; }
    public DateTime LocalDate=>Date.ToOffset(TimeSpan.FromHours(-6)).Date;
    public string Document { get; set; }="";
    public string Product { get; set; }="";
    public string Category { get; set; }="";
    public string Party { get; set; }="";
    public string Warehouse { get; set; }="";
    public string Lot { get; set; }="";
    public DateTime? Expires { get; set; }
    public string Note { get; set; }="";
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal Tax { get; set; }
    public decimal Delivery { get; set; }
    public decimal Net=>Amount-Tax-Delivery;
    public decimal? Cost { get; set; }
    public decimal? Margin=>Cost is {} c?Net-c:null;
    public decimal Stock { get; set; }
    public decimal Reserved { get; set; }
    public decimal Sellable { get; set; }
    public decimal Minimum { get; set; }
    public decimal InRoute { get; set; }
    public decimal? Value { get; set; }
    public decimal? RouteValue { get; set; }
    public decimal? Freight { get; set; }
    public string GroupLabel { get; set; }="";
}
public sealed record OperationalReportData(ReportFilter Filter,DateTimeOffset AsOf,bool CostsVisible,IReadOnlyList<ReportRow> Rows)
{
    public decimal Total=>Rows.Sum(x=>x.Amount);
    public decimal Tax=>Rows.Sum(x=>x.Tax);
    public decimal Delivery=>Rows.Sum(x=>x.Delivery);
    public decimal Net=>Rows.Sum(x=>x.Net);
    public decimal? Cost=>CostsVisible?Rows.Sum(x=>x.Cost??0):null;
    public decimal? Margin=>CostsVisible?Net-Cost:null;
    public decimal? Value=>CostsVisible?Rows.Sum(x=>(x.Value??0)+(x.RouteValue??0)):null;
}
