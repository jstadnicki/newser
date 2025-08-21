using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Newser.Models;

public class Article
{
    [JsonConverter(typeof(ObjectIdJsonConverter))]
    [BsonId] public ObjectId Id { get; set; }

    [BsonElement("title")] public string Title { get; set; }

    [BsonElement("link")] public string Link { get; set; }

    [BsonElement("pubDate")] public DateTime PubDate { get; set; }

    [BsonElement("author")] public string Author { get; set; }

    [BsonElement("content")] public string Content { get; set; }

    [BsonElement("contentSnippet")] public string ContentSnippet { get; set; }

    [BsonElement("categories")] public ICollection<string> Categories { get; set; } = new List<string>();

    [BsonElement("processed")] public bool Processed { get; set; }

    [BsonElement("seen")] public bool Seen { get; set; }
}