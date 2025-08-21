namespace Newser.Api.Services;

public class InMemoryLogger : ILogger
{
    private readonly string _name;
    private readonly InMemoryLoggerProvider _provider;

    public InMemoryLogger(string name, InMemoryLoggerProvider provider)
    {
        _name = name;
        _provider = provider;
    }

    public IDisposable BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
        Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        _provider.Logs.Enqueue($"{DateTime.UtcNow:u} [{logLevel}] {_name}: {message}");
    }
}