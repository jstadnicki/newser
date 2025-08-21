using System.Net;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Newser.Models;

namespace Newser.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class SourcesController : ControllerBase
{
    [HttpGet]
    public async Task<List<Source>> Get()
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var sources = await GetSources(mongoClient);
        return sources;
    }

    [HttpPost]
    public async Task<HttpStatusCode> Post([FromBody] SourcesPost model)
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<Source>("sources");

        var filter = Builders<Source>.Filter.Eq(f => f.Link, model.Link);
        var existringSource = (await collection.FindAsync(filter)).ToList();

        if (existringSource.Any())
        {
            return HttpStatusCode.Conflict;
        }

        var source = new Source
        {
            Link = model.Link
        };
        await collection.InsertOneAsync(source);
        return HttpStatusCode.Accepted;
    }

    public class SourcesPost
    {
        public string Link { get; set; }
    }

    [HttpPut("{id}/error")]
    public async Task<IActionResult> ResetError(string id)
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<Source>("sources");

        var filter = Builders<Source>.Filter.Eq(f => f.Id, new ObjectId(id));
        var update = Builders<Source>.Update.Set(s => s.ErrorCount, 0).Set(s => s.Error, null);

        var result = await collection.UpdateOneAsync(filter, update);

        if (result.MatchedCount == 0)
            return NotFound();

        return NoContent();
    }


    [HttpPut("error")]
    public async Task<IActionResult> ResetError()
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<Source>("sources");

        var filter = Builders<Source>.Filter.Exists(f => f.ErrorCount);
        var update = Builders<Source>.Update
            .Set(s => s.ErrorCount, 0)
            .Set(s => s.Error, null)
            .Set(s => s.Reason, null);

        var result = await collection.UpdateManyAsync(filter, update);

        if (result.MatchedCount == 0)
            return NotFound();

        return NoContent();
    }

    private static async Task<List<Source>> GetSources(MongoClient client)
    {
        var database = client.GetDatabase("news");
        var collection = database.GetCollection<Source>("sources");

        var feeds = (await collection.FindAsync(_ => true)).ToList();
        return feeds;
    }
}