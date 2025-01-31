#pragma once

#include <MachineInterface.h>

#include "RebuzzBuzzLookup.h"
#include "BuzzDataTypes.h"

using BuzzGUI::Interfaces::IMachine;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        public ref class MachineManager : System::IDisposable
        {
        public:
            delegate void OnMachineEventDelegate(int64_t id, IMachine^ rebuzzMach, CMachine* buzzMach);

            MachineManager(OnMachineEventDelegate^ onMachineAddedCallback,
                           OnMachineEventDelegate^ onMachineRemovedCallback);

            !MachineManager();
            ~MachineManager();

            void Release();

            IMachine^ GetReBuzzMachine(CMachine * mach);

            CMachineData* GetBuzzMachineData(CMachine* mach);

            CMachine* GetCMachineByName(const char* name);

            CMachine * GetOrStoreMachine(IMachine^ m);

            CMachine* GetBuzzMachine(IMachine^ m);


        private:
            void Free();
            
            void OnMachineCreatedByReBuzz(IMachine^ machine);
            void OnMachineRemovedByReBuzz(IMachine^ machine);

            RebuzzBuzzLookup< IMachine, CMachineData, CMachine>* m_machineMap;
            void* m_machineCallbackData;
            std::mutex* m_lock;
            System::Action<IMachine^>^ m_machineAddedAction;
            System::Action<IMachine^>^ m_machineRemovedAction;


            OnMachineEventDelegate^ m_onMachineRemovedCallback;
            OnMachineEventDelegate^ m_onMachineAddedCallback;
        };
    }
}
