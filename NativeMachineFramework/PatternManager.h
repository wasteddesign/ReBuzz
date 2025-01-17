#pragma once

#include <MachineInterface.h>
#include "RebuzzBuzzLookup.h"
#include "BuzzDataTypes.h"

#include <set>

using BuzzGUI::Interfaces::IPattern;
using BuzzGUI::Interfaces::IMachine;
using BuzzGUI::Interfaces::IPatternColumn;

using System::ComponentModel::PropertyChangedEventHandler;
using System::ComponentModel::PropertyChangedEventArgs;
using System::Action;


namespace ReBuzz
{
    namespace NativeMachineFramework
    {
      
        typedef void(*OnPatternEditorRedrawCallback)(void * param);

        enum PatternEventFlags
        {
            PatternEventFlags_None = 0,
            PatternEventFlags_Name = 1,
            PatternEventFlags_Length = 2,
            PatternEventFlags_All = PatternEventFlags_Name | PatternEventFlags_Length
        };

        typedef void (*OnPatternEventCallback)(int64_t id, IPattern ^ pat, CPattern * buzzPat, PatternEventFlags flags, void* param);
     
        public ref class PatternManager : System::IDisposable
        {
        public:
            PatternManager(OnPatternEventCallback onPatternAddedCallback,
                           OnPatternEventCallback onPatternRemovedCallback,
                           OnPatternEventCallback onPatternChangedCallback,
                           OnPatternEditorRedrawCallback onPatternEditorRedrawCallback,
                           void* callbackData);

            !PatternManager();
            ~PatternManager();

            void Release();

            void RemovePatternsByMachine(IMachine^ rebuzzmac);

            CPattern* GetPatternByName(IMachine^ rebuzzmac, const char* name);

            CPattern* GetOrStorePattern(IPattern^ p);

            IPattern^ GetReBuzzPattern(CPattern * pat);

            CPatternData* GetBuzzPatternData(CPattern * pat);

            void OnNativePatternChange(CPattern* pat, int newLen, const char * newName );

            void ScanMachineForPatterns(IMachine^ mach);
            
            void AddEventHandlersToMachine(IMachine^ mach);

        private:
            void Free();
            void OnReBuzzPatternColumnChange(IPatternColumn^ patcol);
            void OnReBuzzPatternChange(IPattern^ patcol, bool lock);
            void OnPatternCreatedByRebuzz(IPattern^ pattern);
            void OnPatternRemovedByRebuzz(IPattern^ pattern);
            
            static void PatternChangeCheckCallback(uint64_t id, IPattern^ rebuzzpat, CPattern* buzzpat, CPatternData* patdata, void* param);


            void OnPropertyChangedCallback(System::Object^ sender, PropertyChangedEventArgs^ args);

            RebuzzBuzzLookup<IPattern, CPatternData, CPattern>* m_patternMap;
            void* m_patternCallbackData;
            std::mutex* m_lock;

            OnPatternEventCallback m_onPatternAddedCallback;
            OnPatternEventCallback m_onPatternRemovedCallback;
            OnPatternEventCallback m_onPatternChangedCallback;
            OnPatternEditorRedrawCallback m_onPatternEditorRedrawCallback;
            void* m_callbackParam;

            System::Action<IPatternColumn^>^ m_onPatternChangeAction;
            PropertyChangedEventHandler^ m_onPropChangeEventHandler;

            System::Action<IPattern^>^ m_patternAddedAction;
            System::Action<IPattern^>^ m_patternRemovedAction;
            std::set<int64_t>* m_eventHandlersAddedToMachines;
            std::vector<RefClassWrapper<IMachine>> * m_machinesEventHandlersAddedTo;
        };
    }
}