using FluentAssertions;
using Serilog.Events;
using Serilog.Parsing;
using System;

namespace ReBuzzTests.Automation
{
    public class ReBuzzLogDriverExtension(InMemorySink sink)
    {
        public void AssertLogContainsInvalidPointerMessage()
        {
            sink.Entries.Should()
                .ContainEquivalentOf(
                    new LogEvent(DateTimeOffset.MaxValue, LogEventLevel.Error, null,
                        new MessageTemplate("Invalid pointer", [new TextToken("Invalid pointer")]), []),
                    options => options.Excluding(e => e.Timestamp));
        }

        public void AssertLogContainsCannotAccessDisposedObjectMessage()
        {
            sink.Entries.Should()
                .ContainEquivalentOf(
                    new LogEvent(DateTimeOffset.MaxValue, LogEventLevel.Error,
                        null, new MessageTemplate(
                            "Cannot access a disposed object.\r\nObject name: 'Microsoft.Win32.SafeHandles.SafeWaitHandle'.",
                            [
                                new TextToken(
                                    "Cannot access a disposed object.\r\nObject name: 'Microsoft.Win32.SafeHandles.SafeWaitHandle'.")
                            ]), []),
                    options => options.Excluding(e => e.Timestamp));
        }

        public void AssertLogContainsIndexOutsideArrayBoundsMessage()
        {
            sink.Entries.Should()
                .ContainEquivalentOf(
                    new LogEvent(DateTimeOffset.MaxValue, LogEventLevel.Error,
                        null, new MessageTemplate(
                            "Index was outside the bounds of the array.",
                            [
                                new TextToken("Index was outside the bounds of the array.")
                            ]), []),
                    options => options.Excluding(e => e.Timestamp));
        }
    }
}