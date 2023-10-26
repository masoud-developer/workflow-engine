using MicroEthos.Common.Models;
using MongoDB.Bson.Serialization.Attributes;

namespace MicroEthos.Workflow.Abstraction.Models.Database;

public class Modules : MongoDbEntity
{
    [BsonElement] [BsonRequired] public string Name { get; set; }
    [BsonElement] [BsonRequired] public string Version { get; set; }
    [BsonElement] [BsonRequired] public string AssemblyName { get; set; }
    [BsonElement] public string Hash { get; set; }
    [BsonElement] [BsonRequired] public QueueNamesDto Queues { get; set; }
    [BsonElement] public byte[] AssemblyFile { get; set; }
    [BsonElement] [BsonRequired] public bool Deprecated { get; set; }
}