using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Newser.Models;

public class Source
{
    [JsonConverter(typeof(ObjectIdJsonConverter))]
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("link")] public string Link { get; set; }
    [BsonElement("error")] public string? Error { get; set; }
    [BsonElement("error_count")] public int ErrorCount { get; set; }

    [BsonElement("reason")] public string Reason { get; set; }
}