

#include "MachineEventWrapper.h"
#include "Utils.h"

using ReBuzz::NativeMachineFramework::MachineManager;

namespace ReBuzz
{

    namespace NativeMachineFramework
    {
        MachineEventWrapper::MachineEventWrapper(MachineManager^ mgr,
                                                 IMachine^ self,
                                                 CMachineInterface* machineIface) : m_machineInterface(machineIface),
                                                                                    m_machmgr(mgr)
        {
            m_callbacks = new std::vector<EVENT_HANDLER_PTR>();
            m_callbackParams = new std::vector<void*>();
            m_selfId = self->CMachinePtr.ToInt64();
        }

        MachineEventWrapper::!MachineEventWrapper()
        {
            if (m_callbacks != NULL)
            {
                delete m_callbacks;
                m_callbacks = NULL;
            }

            if (m_callbackParams != NULL)
            {
                delete m_callbackParams;
                m_callbackParams = NULL;
            }
        }

        MachineEventWrapper::~MachineEventWrapper()
        {
            if (m_callbacks != NULL)
            {
                delete m_callbacks;
                m_callbacks = NULL;
            }

            if (m_callbackParams != NULL)
            {
                delete m_callbackParams;
                m_callbackParams = NULL;
            }
        }

        void MachineEventWrapper::OnEvent(IMachine^ machine)
        {
            if ((m_callbacks == NULL) || (m_callbackParams == NULL))
                return;

            //Is this us?
            int64_t machineId = machine->CMachinePtr.ToInt64();
            if (machineId == m_selfId)
                return;

            //Get the native machine. 
            //Don't call the 'OrStore' method here, since this may be a delete event
            //and we don't want to re-add the machine if it has already been deleted.
            CMachine* nativeMachine = m_machmgr->GetBuzzMachine(machine);
            if (nativeMachine == NULL)
                return;

            //Call all the callbacks
            for (size_t x = 0; x < m_callbacks->size(); ++x)
            {
                EVENT_HANDLER_PTR callback = (*m_callbacks)[x];
                void* param = (*m_callbackParams)[x];
                (*m_machineInterface.*callback)(nativeMachine);
            }
        }

        void MachineEventWrapper::AddEvent(EVENT_HANDLER_PTR p, void* param)
        {
            m_callbacks->push_back(p);
            m_callbackParams->push_back(param);
        }
    }
}
