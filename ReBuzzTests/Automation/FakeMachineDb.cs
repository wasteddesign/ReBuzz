using System;
using System.Collections.Generic;
using ReBuzz.Core;
using ReBuzz.FileOps;

namespace ReBuzzTests.Automation
{
    /// <summary>
    /// The real MachineDatabase class starts the ReBuzzEngine processes which are from another solution.
    /// For now, this is too much trouble to set up, so we just use a fake class.
    ///
    /// For the future, we may consider to have fake execs of the ReBuzzEngine or even somehow use the real thing,
    /// but for now, this will do.
    /// </summary>
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

        public MenuItemCore IndexMenu { get; } = new();
    }
}