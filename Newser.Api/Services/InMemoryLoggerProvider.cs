using System.Collections.Concurrent;

namespace Newser.Api.Services;

public class InMemoryLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Logs { get; } = new();

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, this);

    public void Dispose() { }
}