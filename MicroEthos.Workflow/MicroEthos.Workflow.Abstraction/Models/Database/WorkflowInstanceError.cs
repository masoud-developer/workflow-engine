using MicroEthos.Workflow.Abstraction.Models.Database.Attributes;
using MongoDB.Bson.Serialization.Attributes;

namespace MicroEthos.Workflow.Abstraction.Models.Database;

[BsonCollection("wfc.execution_errors")]
[BsonIgnoreExtraElements]
public class WorkflowInstanceError : MongoDbGeneralEntity
{
    public DateTime ErrorTime { get; set; }

    public string Message { get; set; }
    public string WorkflowId { get; set; }
    public string ExecutionPointerId { get; set; }
}