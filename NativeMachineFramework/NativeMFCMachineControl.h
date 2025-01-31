#pragma once

using namespace System::Windows::Forms;
using System::IntPtr;
using System::EventArgs;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        
        public ref class NativeMFCMachineControl  : public  UserControl, System::IDisposable
        {
        public:
            delegate IntPtr AttachCallback(IntPtr hwnd, void* param);
            delegate void DetatchCallback(IntPtr hwnd, void* param);
            delegate void SizeChangedCallback(IntPtr hwnd, void* param, int left, int top, int width, int height);

            
            NativeMFCMachineControl(AttachCallback^ onAttach, 
                                    DetatchCallback^ onDetatch, 
                                    SizeChangedCallback^ onSizeChanged,
                                    void * param);
            
            ~NativeMFCMachineControl();
            !NativeMFCMachineControl();


            virtual void OnHandleCreated(EventArgs^ args) override;

            virtual void OnHandleDestroyed(EventArgs^ args) override;

            virtual void OnSizeChanged(EventArgs^ args) override;

        private:
            void Destroy();

            AttachCallback^ m_onAttach;
            DetatchCallback^ m_onDetatch;
            SizeChangedCallback^ m_onSizeChanged;
            IntPtr m_cwnd;
            void* m_callbackParam;
        };
    }
}