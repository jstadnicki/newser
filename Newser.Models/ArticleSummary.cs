using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Newser.Models;

public class ArticleSummary
{
    [JsonConverter(typeof(ObjectIdJsonConverter))]
    [BsonId] public ObjectId Id { get; set; }
    [BsonElement("title")] public string Title { get; set; }
    [BsonElement("link")] public string Link { get; set; }
    [BsonElement("summary")] public string Summary { get; set; }
    [BsonElement("categories")] public string[] Categories { get; set; }
    [BsonElement("seen")] public bool Seen { get; set; }
    [BsonElement("parent_id")] public ObjectId ParentId { get; set; }
    [BsonElement("author")] public string Author { get; set; }
    [BsonElement("publication_date")] public DateTime PulicationDate { get; set; }
}

public class ArticleSummaryWithCategoryGeneratedWithLlama
{
    [JsonPropertyName("summary")]
    public required string Summary { get; set; }
    
    [JsonPropertyName("categories")]
    public required string[] Categories { get; set; }
}