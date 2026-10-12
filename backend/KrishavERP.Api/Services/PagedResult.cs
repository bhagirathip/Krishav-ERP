namespace KrishavERP.Api.Services;

public class PagedResult<T>
{
    public List<T> Rows { get; set; } = new();
    public int TotalCount { get; set; }
}
