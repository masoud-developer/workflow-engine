using MicroEthos.Workflow.Abstraction.Enums;
using MicroEthos.Workflow.Abstraction.Models.Database.Attributes;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.Options;

namespace MicroEthos.Workflow.Abstraction.Models.Database;

[BsonCollection("wfc.workflows")]
[BsonIgnoreExtraElements]
public class WorkflowInstance : MongoDbGeneralEntity
{
    public string WorkflowDefinitionId { get; set; }

    public int Version { get; set; }

    public string Description { get; set; }

    public string Reference { get; set; }

    public long? NextExecution { get; set; }

    public WorkflowInstanceStatus Status { get; set; }

    [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    public Dictionary<string, object> Data { get; set; }

    public DateTime CreateTime { get; set; }

    public DateTime? CompleteTime { get; set; }
}