using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

public class InMemorySink : ILogEventSink //bug
{
    public ConcurrentBag<LogEvent> Entries { get; } = [];
    
    public void Emit(LogEvent logEvent)
    {
        Entries.Add(logEvent);
    }
}