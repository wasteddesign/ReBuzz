using Core.NullableReferenceTypesExtensions;
using ReBuzz.Core;
using System;

namespace ReBuzzTests.Automation
{
    public class FakeUserMessages : IUserMessages
    {
        public void Error(string message, string caption, Exception exception)
        {
            Caption = caption;
            Message = message;
            StackStrace = exception.StackTrace.OrThrow();
            AnyErrorReported = true;
        }

        public string StackStrace { get; private set; } = string.Empty;
        public string Message { get; private set; } = string.Empty;
        public string Caption { get; private set; } = string.Empty;
        public bool AnyErrorReported { get; private set; } = false;
    }
}