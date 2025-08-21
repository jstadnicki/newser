using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Driver;
using Newser.Models;

namespace Newser.Api.Controllers;


[ApiController]
[Route("[controller]")]
public class SummariesController : ControllerBase
{
    [HttpGet]
    public async Task<List<ArticleSummary>> Get()
    {
        var summariesToSee = await GetUnseenSummariesAsync();
        return summariesToSee;
    }
    
    private static async Task<List<ArticleSummary>> GetUnseenSummariesAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<ArticleSummary>("summaries");

        var feeds = (await collection.FindAsync(s => s.Seen == false)).ToList();
        return feeds;
    }
    
    [HttpPut("{id}/seen")]
    public async Task<IActionResult> MarkSeen(string id)
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<ArticleSummary>("summaries");

        var filter = Builders<ArticleSummary>.Filter.Eq(f => f.Id, new ObjectId(id));
        var update = Builders<ArticleSummary>.Update.Set(s => s.Seen, true);

        var result = await collection.UpdateOneAsync(filter, update);

        if (result.MatchedCount == 0)
            return NotFound();

        return NoContent();
    }
    
    [HttpPut("seen")]
    public async Task<IActionResult> MarkSeen([FromBody]string[] ids)
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<ArticleSummary>("summaries");

        var filter = Builders<ArticleSummary>.Filter.In(x => x.Id, ids.Select(s => new ObjectId(s)));
        var update = Builders<ArticleSummary>.Update.Set(s => s.Seen, true);

        var result = await collection.UpdateManyAsync(filter, update);

        if (result.MatchedCount == 0)
            return NotFound();

        return NoContent();
    }


}