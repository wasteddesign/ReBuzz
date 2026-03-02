using System.Collections.Immutable;

namespace ReBuzzTests.Automation
{
    public record struct TestMachineConfig()
    {
        public static readonly TestMachineConfig Empty = new();

        public readonly ImmutableDictionary<string, string> Config = ImmutableDictionary<string, string>.Empty;

        public int Latency
        {
            init
            {
                Config = Config.Add("Latency", value.ToString());
            }
        }
    }
}