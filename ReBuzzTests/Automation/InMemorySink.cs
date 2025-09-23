using Serilog.Core;
using Serilog.Events;
using System.Collections.Concurrent;

namespace ReBuzzTests.Automation;

public class InMemorySink : ILogEventSink
{
    public ConcurrentBag<LogEvent> Entries { get; } = [];
    
    public void Emit(LogEvent logEvent)
    {
        Entries.Add(logEvent);
    }
}