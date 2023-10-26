namespace MicroEthos.Workflow.Abstraction.Models.Response;

public class WorkflowVersionResponseModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Version { get; set; }
    public Guid VersionId { get; set; }
}