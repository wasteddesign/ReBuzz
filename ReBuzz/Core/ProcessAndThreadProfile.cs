using BuzzGUI.Common.Settings;
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
            ProcessPriorityClassNativeHostProcess = ProcessPriorityClass.High;
            DedicatedThreadPoolThread = ThreadPriority.Highest;
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

        // All normal
        internal static void Profile3()
        {
            ProcessPriorityClassMainProcess = ProcessPriorityClass.Normal;
            ProcessPriorityClassNativeHostProcess = ProcessPriorityClass.Normal;
            DedicatedThreadPoolThread = ThreadPriority.Normal;
            AudioProviderThread = ThreadPriority.Normal;
            WorkThreadEngineThread = ThreadPriority.Normal;
        }

        internal static void SetProfile(PriorityProfileType priorityProfileType)
        {
            switch (priorityProfileType)
            {
                case PriorityProfileType.NormalAppPriority:
                    Profile1();
                    break;
                case PriorityProfileType.AllFocusOnAudio:
                    Profile2();
                    break;
                case PriorityProfileType.AllDefaults:
                    Profile3();
                    break;
            }
        }
    }
}
