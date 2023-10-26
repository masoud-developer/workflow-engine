using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Request;
using MicroEthos.Workflow.Abstraction.Models.Workflow;
using MongoDB.Bson.Serialization.Attributes;

namespace MicroEthos.Workflow.Abstraction.Models.Database;

public class WorkflowDefinitions : MongoDbEntity
{
    [BsonRequired] public WorkflowDefinitionModel? Model { get; set; }
    [BsonRequired] public string Raw { get; set; }
    [BsonRequired] public WorkflowDefinitionStatus Status { get; set; } 
    public string MetaData { get; set; }
    public StateDict RunParameters { get; set; }
}