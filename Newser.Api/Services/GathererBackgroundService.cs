using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CodeHollow.FeedReader;
using MongoDB.Driver;
using Newser.Models;

namespace Newser.Api.Services;

public class GathererBackgroundService : BackgroundService
{
    private readonly IWorkerTrigger _trigger;
    private readonly ILogger<GathererBackgroundService> _logger;

    public GathererBackgroundService(
        IWorkerTrigger trigger,
        ILogger<GathererBackgroundService> logger)
    {
        _trigger = trigger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_trigger.ShouldRun)
            {
                await DoNewserGathererWorkAsync();
                _trigger.ShouldRun = false;
            }
            else
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            }
        }
    }

    public async Task DoNewserGathererWorkAsync()
    {
        var settings = MongoClientSettings.FromConnectionString(
            "mongodb://mongoRootName:mongoPassword@localhost:27017/news?authSource=admin"
        );
        var mongoClient = new MongoClient(settings);
        var sources = await GetSources(mongoClient);

        using var httpClient = new HttpClient();

        await DownloadArticlesAsync(sources, mongoClient);
        await CreateSummariesAsync(mongoClient);
    }

    private async Task CreateSummariesAsync(MongoClient mongoClient)
    {
        var database = mongoClient.GetDatabase("news");
        var articles = database.GetCollection<Article>("articles");

        var documentsToProcess = (await articles.FindAsync(f => f.Processed == false)).ToList();
        var documentsToProcessCount = documentsToProcess.Count;
        _logger.LogInformation("Found: {Count} documents to process. Starting now...", documentsToProcessCount);
        var index = 1;
        foreach (var documentToProcess in documentsToProcess)
        {
            _logger.LogInformation("{Index} / {Count} Processing article: {Link}...", index++, documentsToProcessCount,
                documentToProcess.Link);
            var rawInput = documentToProcess.Content + documentToProcess.ContentSnippet;
            var input = CleanText(rawInput);
            var summary = await CreateSummaryAsync(input);
            await StoreSummaryAsync(mongoClient, documentToProcess, summary);
            _logger.LogInformation("\tSummary created");
            await MarkDocumentAsProcessedAsync(mongoClient, documentToProcess);
            _logger.LogInformation("\tDocument marked as processed");
        }
    }

    private async Task MarkDocumentAsProcessedAsync(MongoClient mongoClient, Article documentToProcess)
    {
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<Article>("articles");

        var filter = Builders<Article>.Filter.Eq(f => f.Id, documentToProcess.Id);
        var update = Builders<Article>.Update
            .Set(f => f.Processed, true);

        await collection.UpdateOneAsync(filter, update);
    }

    private async Task StoreSummaryAsync(MongoClient mongoClient, Article documentToProcess, string summary)
    {
        try
        {
            var summaryObject = JsonSerializer.Deserialize<ArticleSummaryWithCategoryGeneratedWithLlama>(summary);

            // if object was unsuccessfully summarised,
            // just use the document title
            if (summaryObject!.Summary == "")
            {
                summaryObject.Summary = documentToProcess.Title;
            }

            // cut off a summary that is too long, add ellipses
            if (summaryObject.Summary.Length > 160)
            {
                summaryObject.Summary = summaryObject.Summary[..157] + "...";
            }

            var newsSummary = new ArticleSummary
            {
                Link = documentToProcess.Link,
                ParentId = documentToProcess.Id,
                Seen = false,
                Summary = summaryObject!.Summary,
                Categories = summaryObject!.Categories,
                Title = documentToProcess.Title,
                Author = documentToProcess.Author,
                PulicationDate = documentToProcess.PubDate
            };

            var database = mongoClient.GetDatabase("news");
            var articles = database.GetCollection<ArticleSummary>("summaries");

            await articles.InsertOneAsync(newsSummary);
        }
        catch (JsonException)
        {
            // in case of failure,
            // CreateSummaryAsync will often return an invalid JSON
            // we can just state that a summary is unavailable

            var newsSummary = new ArticleSummary
            {
                Link = documentToProcess.Link,
                ParentId = documentToProcess.Id,
                Seen = false,
                Summary = "(no summary available)",
                Categories = [],
                Title = documentToProcess.Title,
                Author = documentToProcess.Author,
                PulicationDate = documentToProcess.PubDate
            };

            var database = mongoClient.GetDatabase("news");
            var articles = database.GetCollection<ArticleSummary>("summaries");

            await articles.InsertOneAsync(newsSummary);
        }
    }

    private async Task<string> CreateSummaryAsync(string input)
    {
        using var httpClient = new HttpClient();
        var payload = new
        {
            model = "llama3.1:8b",
            messages = new[]
            {
                new
                {
                    role = "system",
                    content =
                       """
                       You are a JSON-only generator.  
                       Your entire response MUST be a single valid JSON object.  
                       Do not include explanations, comments, or markdown.  
                       Do not add any text before or after the JSON.  
                       
                       Required format:
                       {"summary":"<string>","categories":["<noun1>","<noun2>",...]}
                       
                       Rules:
                       - "summary": exactly ONE sentence.  
                       - "summary" MUST NOT exceed 160 characters. If longer, CUT at 160 chars.  
                       - "categories": up to 5 nouns.  
                       - If no valid data: {"summary":"","categories":[]}  
                       
                       If your output contains anything other than JSON, it is invalid.  
                       
                       """
                },
                new { role = "user", content = input }
            }
        };

        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri("http://localhost:11434/api/chat"),
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        try
        {
            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            _logger.LogInformation("received response...");
            using var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            string? line;
            var sb = new StringBuilder();

            try
            {
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    _logger.LogInformation("line: " + line);

                    var json = JsonSerializer.Deserialize<JsonElement>(line);
                    if (json.TryGetProperty("message", out var msg) &&
                        msg.TryGetProperty("content", out var content))
                    {
                        sb.Append(content.GetString());
                    }
                }
            }
            catch (JsonException)
            {
                // failed to parse as JSON (LLM fault)
                return "";
            }

            // if the port is open, but the LLM isn't, there may be an error message of the type:
            // {"error":"model [model] not found"}
            // such case is already handled upstream

            return sb.ToString();
        }
        catch (TaskCanceledException)
        {
            // cannot connect to LLM
            return "";
        }
        catch (HttpRequestException)
        {
            // network failure
            return "";
        }
    }

    static string CleanText(string input)
    {
        return input;
        // var noHtml = Regex.Replace(input, "<.*?>", string.Empty);
        // var clean = Regex.Replace(noHtml, @"[^a-zA-Z0-9\s]", string.Empty);
        // return clean;
    }

    private async Task DownloadArticlesAsync(
        List<Source> sources,
        MongoClient mongoClient)
    {
        using var httpClient = new HttpClient();
        foreach (var source in sources)
        {
            _logger.LogInformation("Checking feed: {Link}", source.Link);
            try
            {
                var rssItems = await GetFeedItems(httpClient, source);

                foreach (var feedItem in rssItems)
                {
                    _logger.LogInformation("\tChecking article: {Link}... ", feedItem.Link);
                    var findRssDocumentAsync = await FindRssDocumentAsync(feedItem, mongoClient);
                    if (findRssDocumentAsync == false)
                    {
                        _logger.LogInformation(" adding ...");
                        await InsertRssDocumentAsync(feedItem, mongoClient);
                        _logger.LogInformation("done");
                    }
                    else
                    {
                        _logger.LogInformation(" exists!");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, e.Message);
                await UpdateFeedErrorsAsync(source, mongoClient, e.Message);
                _logger.LogInformation("Feed: {Link} error description updated", source.Link);
            }
        }
    }

    private async Task InsertRssDocumentAsync(FeedItem feedItem, MongoClient mongoClient)
    {
        var newsItem = new Article
        {
            Link = feedItem.Link,
            Author = feedItem.Author,
            Content = feedItem.Content,
            ContentSnippet = feedItem.Description,
            PubDate = feedItem.PublishingDate ?? DateTime.UtcNow,
            Title = feedItem.Title,
            Categories = feedItem.Categories,
            Processed = false,
            Seen = false
        };
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<Article>("articles");
        await collection.InsertOneAsync(newsItem);
    }

    private async Task<bool> FindRssDocumentAsync(FeedItem feedItem, MongoClient mongoClient)
    {
        var database = mongoClient.GetDatabase("news");
        var collection = database.GetCollection<Article>("articles");
        var document = await collection.FindAsync(f => f.Link == feedItem.Link);
        return await document.AnyAsync();
    }

    private async Task UpdateFeedErrorsAsync(Source source, MongoClient client, string message)
    {
        var database = client.GetDatabase("news");
        var collection = database.GetCollection<Source>("sources");

        var filter = Builders<Source>.Filter.Eq(f => f.Id, source.Id);
        var update = Builders<Source>.Update
            .Inc(f => f.ErrorCount, 1)
            .Set(f => f.Reason, message);

        await collection.UpdateOneAsync(filter, update);
    }

    private async Task<IList<FeedItem>> GetFeedItems(HttpClient httpClient, Source source)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, source.Link);
        request.Headers.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:131.0) Gecko/20100101 Firefox/131.0");
        var requestResult = await httpClient.SendAsync(request);
        var rssString = await requestResult.Content.ReadAsStringAsync();
        var rssFeed = FeedReader.ReadFromString(rssString);
        var rssFeedItems = rssFeed.Items;
        return rssFeedItems;
    }

    private async Task<List<Source>> GetSources(MongoClient client)
    {
        var database = client.GetDatabase("news");
        var collection = database.GetCollection<Source>("sources");

        var feeds = await collection.Find(f => f.ErrorCount == 0).ToListAsync();
        return feeds;
    }
}