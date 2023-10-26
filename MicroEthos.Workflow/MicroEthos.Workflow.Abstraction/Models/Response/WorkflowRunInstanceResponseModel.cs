using MicroEthos.Workflow.Abstraction.Enums;

namespace MicroEthos.Workflow.Abstraction.Models.Response;

public class WorkflowRunInstanceResponseModel
{
    public string Id { get; set; }

    public string TraceId { get; set; }

    public string DefinitionId { get; set; }
    public string DefinitionName { get; set; }

    public int Version { get; set; }

    public string Description { get; set; }

    public long? NextExecution { get; set; }

    public WorkflowInstanceStatus Status { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime? CompleteTime { get; set; }

    public List<WorkflowInstanceStepDetailResponseModel> Steps { get; set; }

    public string MetaData { get; set; }
}

public class WorkflowInstanceStepDetailResponseModel
{
    public string Id { get; set; }

    public int StepId { get; set; }

    public bool Active { get; set; }

    public DateTime? StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public DateTime? SleepUntil { get; set; }

    public object? EventData { get; set; }

    public string StepName { get; set; }

    public int RetryCount { get; set; }

    public List<string> Children { get; set; } = new();

    public object ContextItem { get; set; }
    public object Data { get; set; }

    // public string PredecessorId { get; set; }
    public WorkflowInstanceStepPointerStatus Status { get; set; }
    public List<WorkflowInstanceLogModel> Logs { get; set; }
}

public class WorkflowInstanceLogModel
{
    public WorkflowInstanceLogType Type { get; set; }
    public string Message { get; set; }
    public DateTime Time { get; set; }
}