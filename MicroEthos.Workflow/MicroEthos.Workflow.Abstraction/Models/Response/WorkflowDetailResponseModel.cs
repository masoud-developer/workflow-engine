using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Request;

namespace MicroEthos.Workflow.Abstraction.Models.Response;

public class WorkflowDetailResponseModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Version { get; set; }
    public List<WorkflowStepDefinitionModel> Steps { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Author { get; set; }
    public string? MetaData { get; set; }
    public long RunInstanceCount { get; set; }
    public WorkflowDefinitionStatus Status { get; set; }
}