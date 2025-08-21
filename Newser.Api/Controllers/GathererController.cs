using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Newser.Api.Services;

namespace Newser.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class GathererController : ControllerBase
{
    private readonly IWorkerTrigger _workerTrigger;
    private readonly InMemoryLoggerProvider _logProvider;

    public GathererController(
        IWorkerTrigger workerTrigger,
        InMemoryLoggerProvider logProvider)
    {
        _workerTrigger = workerTrigger;
        _logProvider = logProvider;
    }

    [HttpPost("start")]
    public HttpStatusCode StartGatherer()
    {
        _workerTrigger.ShouldRun = true;
        return HttpStatusCode.Accepted;
    }

    [HttpGet("logs")]
    public async Task GetLogs(CancellationToken cancellationToken)
    {
        Response.Headers.Add("Content-Type", "text/event-stream");
        Response.StatusCode = 200;
    
        while (_workerTrigger.ShouldRun)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }
    
            string log;
            while (_logProvider.Logs.TryDequeue(out log))
            {
                var data = $"data: {log}\n\n";
                var bytes = Encoding.UTF8.GetBytes(data);
                await Response.Body.WriteAsync(bytes, 0, bytes.Length);
                await Response.Body.FlushAsync();
            }
        }
    
        var closeData = "event: close\ndata: Stream closed\n\n";
        var closeBytes = Encoding.UTF8.GetBytes(closeData);
        await Response.Body.WriteAsync(closeBytes, 0, closeBytes.Length, cancellationToken);
        await Response.Body.FlushAsync();
    }
}

