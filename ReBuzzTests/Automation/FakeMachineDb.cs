using System;
using System.Collections.Generic;
using ReBuzz.Core;
using ReBuzz.FileOps;

namespace ReBuzzTests.Automation;

internal class FakeMachineDb : IMachineDatabase
{
    public event Action<string>? DatabaseEvent;

    public Dictionary<int, MachineDatabase.InstrumentInfo> DictLibRef { get; set; } = new();
    public void CreateDB()
    {

    }

    public string GetLibName(int id)
    {
        Assert.Fail("Not called anywhere yet in the current tests");
        return null!;
    }

    public MenuItemCore IndexMenu { get; } = new MenuItemCore();
}