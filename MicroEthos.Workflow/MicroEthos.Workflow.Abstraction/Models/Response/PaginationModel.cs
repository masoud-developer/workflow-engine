namespace MicroEthos.Workflow.Abstraction.Models.Response;

public class PaginationModel<T>
{
    public List<T> Items { get; set; }
    public long TotalCount { get; set; }
    public long PageSize { get; set; }
    public long PageNumber { get; set; }
}