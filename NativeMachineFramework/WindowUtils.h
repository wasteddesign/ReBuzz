#pragma once

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        class WindowUtils
        {
        public:
            static __declspec(dllexport) double GetScalingFactor(HWND hwnd);

            static __declspec(dllexport) double GetToolbarScalingFactor(HWND hwnd);

            static __declspec(dllexport) void RepositionAndResizeChildControls(HWND parent, double scalingFactor);

            static __declspec(dllexport) void ResizeImageListForCurrentDPI(void * toolbar);
        };
    }
}
