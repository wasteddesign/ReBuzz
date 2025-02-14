#include "NativeMFCMachineControl.h"


using namespace System::Windows::Forms;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        NativeMFCMachineControl::NativeMFCMachineControl(AttachCallback^ onAttach,
                                                         DetatchCallback^ onDetatch, 
                                                         SizeChangedCallback^ onSzChanged,
                                                         void* callbackParam) : m_onAttach(onAttach),
                                                                                m_onDetatch(onDetatch),
                                                                                m_onSizeChanged(onSzChanged),
                                                                                m_callbackParam(callbackParam)
        {
           
        }

        NativeMFCMachineControl::!NativeMFCMachineControl()
        {
            Destroy();
        }

        NativeMFCMachineControl::~NativeMFCMachineControl()
        {
            Destroy();
        }

        void NativeMFCMachineControl::Destroy()
        {
            if (m_onDetatch != nullptr)
            {
                m_onDetatch(m_cwnd, m_callbackParam);
                m_cwnd = IntPtr::Zero;
                m_onDetatch = nullptr;
            }
        }

        void NativeMFCMachineControl::OnHandleCreated(EventArgs^ args) 
        {
            //Tell caller about the window handle
            m_cwnd = m_onAttach(Handle, m_callbackParam);

            SetStyle(System::Windows::Forms::ControlStyles::UserPaint, false);
        }

        void NativeMFCMachineControl::OnHandleDestroyed(EventArgs^ args)
        {
            Destroy();
        }

        void NativeMFCMachineControl::OnSizeChanged(EventArgs^ args)
        {
            if (m_onSizeChanged != nullptr)
            {
                m_onSizeChanged(m_cwnd, m_callbackParam, Left, Top, ClientSize.Width, ClientSize.Height);
            }
        }

        

      
    }
}