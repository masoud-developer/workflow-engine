using MicroEthos.Workflow.Abstraction.Enums;
using MongoDB.Bson.Serialization.Attributes;

namespace MicroEthos.Workflow.Abstraction.Models.Request;

public class WorkflowDefinitionModel
{
    public WorkflowDefinitionModel()
    {
        Steps = new List<WorkflowStepDefinitionModel>();
    }

    public Guid Id { get; set; }
    public string? InstitutionId { get; set; }
    public string? ServiceId { get; set; }
    public string Name { get; set; }
    public int Version { get; set; }
    public List<WorkflowStepDefinitionModel> Steps { get; set; }

    [BsonIgnore] public WorkflowAction? Action { get; set; }

    [BsonIgnore] public string MetaData { get; set; }
}

public class WorkflowStepDefinitionModel
{
    public WorkflowStepDefinitionModel()
    {
        Inputs = new Dictionary<string, string>();
        Outputs = new Dictionary<string, string>();
        SelectNextStep = new Dictionary<string, string>();
        Do = new List<List<WorkflowStepDefinitionModel>>();
    }

    public string Id { get; set; }
    public string? Name { get; set; }
    public string StepType { get; set; }
    public string? NextStepId { get; set; }
    public Dictionary<string, string>? Inputs { get; set; }
    public Dictionary<string, string>? Outputs { get; set; }
    public Dictionary<string, string>? SelectNextStep { get; set; }
    public List<List<WorkflowStepDefinitionModel>>? Do { get; set; }
}