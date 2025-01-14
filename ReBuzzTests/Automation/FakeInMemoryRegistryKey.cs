using ReBuzz.Core;
using System.Collections.Generic;

namespace ReBuzzTests.Automation
{
    internal class FakeInMemoryRegistryKey(
        Dictionary<(string Path, string Key), object> registryDictionary,
        string path) : IRegistryKey
    {
        public void SetValue(string name, object value)
        {
            registryDictionary[(path, name)] = value;
            TestContext.Out.WriteLine($"Set subkey value: {path}=>{name}=>{value}");
        }
    }
}