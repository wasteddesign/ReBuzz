#pragma once

#include "MachineManager.h"
#include <MachineInterface.h>
#include <vector>

using BuzzGUI::Interfaces::IMachine;
using BuzzGUI::Common::Global;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {

        public ref class MachineEventWrapper : System::IDisposable
        {
        public:
            MachineEventWrapper(MachineManager^ machmgr, IMachine^ self, CMachineInterface* machineIface);

            !MachineEventWrapper();
            ~MachineEventWrapper();

            void OnEvent(IMachine^ machine);

            void AddEvent(EVENT_HANDLER_PTR p, void* param);

        private:

            std::vector<EVENT_HANDLER_PTR>* m_callbacks;
            std::vector<void*>* m_callbackParams;
            CMachineInterface* m_machineInterface;
            int64_t m_selfId;
            MachineManager^ m_machmgr;
        };
    }
}