using Microsoft.Win32;

namespace ReBuzz.Core;

public interface IRegistryKey //bug
{
    void SetValue(string name, object value);
}


public class WindowsRegistryKey(RegistryKey registryKey) : IRegistryKey
{
    public void SetValue(string name, object value)
    {
        registryKey.SetValue(name, value);
    }
}