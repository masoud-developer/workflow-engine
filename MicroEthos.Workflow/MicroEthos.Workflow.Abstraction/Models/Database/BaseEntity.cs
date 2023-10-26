using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MicroEthos.Workflow.Abstraction.Models.Database;

public abstract class MongoDbEntity
{
    // [BsonRepresentation(BsonType.ObjectId)]
    [BsonId] [BsonElement(Order = 0)] public Guid Id { get; set; } = Guid.NewGuid();

    [BsonRepresentation(BsonType.DateTime)]
    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    [BsonElement(Order = 101)]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonRepresentation(BsonType.DateTime)]
    [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
    [BsonElement(Order = 101)]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public abstract class MongoDbGeneralEntity
{
    [BsonRepresentation(BsonType.ObjectId)]
    // [BsonId] [BsonElement(Order = 0)] 
    public string Id { get; set; }
}