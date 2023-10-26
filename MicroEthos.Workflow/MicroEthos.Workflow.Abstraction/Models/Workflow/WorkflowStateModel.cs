using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MicroEthos.Workflow.Abstraction.Models.Workflow;

public class WorkflowStateModel
{
    public StateDict State { get; set; }
    public Guid TraceId { get; set; }
    public Guid InstitutionId { get; set; }
    public Guid ServiceId { get; set; }
    public Guid? UserId { get; set; }
}

public class StateDict
{
    // [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    // [BsonSerializer(typeof(JTokenStringSerializer))]
    // public Dictionary<string, object> Items { get; set; }
    // [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    public BsonDocument Items { get; set; }

    [BsonIgnore]
    public object? this[string key]
    {
        set
        {
            if (value is int i)
                Items[key] = i;
            else if (value is long l)
                Items[key] = l;
            else if (value is string s)
                Items[key] = s;
            else if (value is DateTime d)
                Items[key] = d;
            else if (value is short sh)
                Items[key] = sh;
            else if (value is bool b)
                Items[key] = b;
            else if (value is double dob)
                Items[key] = dob;
            else if (value is ushort ush)
                Items[key] = ush;
            // else if (value is ulong ul)
            //     Items[key] = ul;
            else if (value is float fl)
                Items[key] = fl;
            else if (value is uint ui)
                Items[key] = ui;
            // else if (value != null && (value.GetType().IsArray || value is IList or ICollection))
            // {
            //     var arr = new BsonArray();
            //     foreach (var item in (ICollection)value)
            //         arr.Add(Bson.Create()).Create(item));
            // }
            else if (value != null && value.GetType().IsPublic && value.GetType().IsClass)
                Items[key] = BsonDocument.Create(value);
            else
                Items[key] = new BsonDocument();
        }
    }
}