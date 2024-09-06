using System.Diagnostics;
using System.Threading;

namespace ReBuzz.Core
{
    internal static class ProcessAndThreadProfile
    {
        // Rebuzz
        internal static ProcessPriorityClass ProcessPriorityClassMainProcess;

        // Native plugin process
        internal static ProcessPriorityClass ProcessPriorityClassNativeHostProcess;

        // DedicatedThreadPool thread
        internal static ThreadPriority DedicatedThreadPoolThread;

        // AudioProvide thread
        internal static ThreadPriority AudioProviderThread;

        // WorkThreadEngine Thread
        internal static ThreadPriority WorkThreadEngineThread;

        // "Default"
        internal static void Profile1()
        {
            ProcessPriorityClassMainProcess = ProcessPriorityClass.Normal;
            ProcessPriorityClassNativeHostProcess = ProcessPriorityClass.Normal;
            DedicatedThreadPoolThread = ThreadPriority.Normal;
            AudioProviderThread = ThreadPriority.Highest;
            WorkThreadEngineThread = ThreadPriority.Highest;
        }

        // Don't touch! This is good!
        internal static void Profile2()
        {
            ProcessPriorityClassMainProcess = ProcessPriorityClass.High;
            ProcessPriorityClassNativeHostProcess = ProcessPriorityClass.AboveNormal;
            DedicatedThreadPoolThread = ThreadPriority.AboveNormal;
            AudioProviderThread = ThreadPriority.Highest;
            WorkThreadEngineThread = ThreadPriority.AboveNormal;
        }
    }
}
