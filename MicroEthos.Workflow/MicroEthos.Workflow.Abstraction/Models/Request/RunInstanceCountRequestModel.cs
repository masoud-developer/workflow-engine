using MicroEthos.Workflow.Abstraction.Enums;

namespace MicroEthos.Workflow.Abstraction.Models.Request;

public class RunInstanceCountRequestModel
{
    public WorkflowInstanceStatus? Type { get; set; }
    public TimeSpan From { get; set; }
}