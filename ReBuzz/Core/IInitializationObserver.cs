using ReBuzz.MachineManagement;

namespace ReBuzz.Core
{
    internal interface IInitializationObserver
    {
        void NotifyMachineManagerCreated(MachineManager machineManager);
    }
}