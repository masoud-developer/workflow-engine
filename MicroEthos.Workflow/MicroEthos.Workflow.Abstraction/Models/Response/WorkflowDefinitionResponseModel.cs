using MicroEthos.Workflow.Abstraction.Enums;

namespace MicroEthos.Workflow.Abstraction.Models.Response;

public class WorkflowDefinitionResponseModel
{
    public Guid Id { get; set; }
    public int Version { get; set; }
    public string Name { get; set; }
    public string Author { get; set; }
    public DateTime Created { get; set; }
    public DateTime Updated { get; set; }
    public int StepCount { get; set; }
    public long RunInstanceCount { get; set; }
    public WorkflowDefinitionStatus Status { get; set; }
}