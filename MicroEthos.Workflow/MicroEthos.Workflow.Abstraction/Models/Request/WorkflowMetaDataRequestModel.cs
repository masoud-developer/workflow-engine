namespace MicroEthos.Workflow.Abstraction.Models.Request;

public class WorkflowMetaDataRequestModel
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string MetaData { get; set; }
}