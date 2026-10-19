namespace GoDental.Application.DTOs;
public sealed class CategoryData
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; }
}
public sealed record CategoryChange(int Id, string Name, bool Active, string ExpectedName, bool ExpectedActive);
