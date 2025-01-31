#pragma once

#include "RefClassWrapper.h"
#include "RebuzzBuzzLookup.h"
#include "BuzzDataTypes.h"
#include <MachineInterface.h>
#include "MachineCallbackWrapper.h"
#include "MachineManager.h"
#include "PatternManager.h"
#include "WaveManager.h"
#include "SampleListControl.h"

#include <unordered_map>

using System::Windows::Forms::UserControl;
using System::Windows::Forms::KeyEventArgs;
using System::Windows::Forms::KeyEventHandler;
using System::String;

using System::Collections::Generic::List;

using BuzzGUI::Interfaces::IPattern;
using BuzzGUI::Interfaces::IParameter;
using BuzzGUI::Interfaces::BuzzCommand;
using BuzzGUI::Interfaces::ISequence;
using Buzz::MachineInterface::IBuzzMachine;


namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        
        typedef LRESULT(*OnWindowsMessage)(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam, void * callbackParam, bool* pbBlock);

        

        #pragma make_public(OnWindowsMessage) ;

        public ref class MachineWrapper : System::IDisposable
        {
        public:
           MachineWrapper( void * machine, IBuzzMachineHost^ host, IBuzzMachine^ buzzmachine);

            //Destructor - called when Dispose() is called (and we need to be IDisposable)
            ~MachineWrapper();
            !MachineWrapper(); //Called when finalised

            void Init();

            void Release();

            CMachineInterfaceEx* GetExInterface();

            void UpdateMasterInfo();

            void Tick();

            void SetModifiedFlag();

            cli::array<byte>^ Save();

            void MidiNote(int channel, int value, int velocity);

            void MidiControlChange(int ctrl, int channel, int value);

            void ControlChange(IMachine^ machine, int group, int track, int param, int value);

            void RecordControlChange(IParameter^ parameter, int track, int value);

            bool CanExecuteCommand(BuzzCommand cmd);

            void ExecuteCommand(BuzzCommand cmd);


            //==========================================================
            //Machine API
            //==========================================================

            IMachine^ GetThisReBuzzMachine();

            void SetTargetMachine(IMachine^ machine);

            void* GetCMachine(IMachine^ m);

            CMachine* GetCMachineByName(const char* name);

            IMachine^ GetReBuzzMachine(void* mach);

            CMachineData* GetBuzzMachineData(void* mach);

            //=====================================================================
            //Pattern API
            //=====================================================================

             delegate void OnNewPatternDelegate(IMachine^ rebuzzMachine, void * buzzMachine, 
                                               IPattern^ rebuzzPattern, void * buzzPattern,String^ patternName);

            delegate bool OnPatternPlayDelegate(IMachine^ rebuzzMachine, void * buzzMachine,
                                                IPattern^ rebuzzPattern, void * buzzPattern,String^ patternName);

            delegate void OnPatternEditorRedrawDelegate();

            delegate void OnPatternEditorCreatedDelegate();

            delegate IntPtr KeyboardFocusWindowHandleDelegate();

            void* GetCPattern(IPattern^ p);

            IPattern^ GetReBuzzPattern(void* pat);

            CPatternData* GetBuzzPatternData(void* pat);

            void* GetCPatternByName(IMachine^ rebuzzmac, const char* name);

            void UpdatePattern(CPattern* pat, int newLen, const char* newName);

            UserControl^ PatternEditorControl();

            void ActivatePatternEditor();

            void AddPatternEditorRedrawCallback(OnPatternEditorRedrawDelegate^ callback);

            void RemovePatternEditorRedrawCallback(OnPatternEditorRedrawDelegate^ callback);
            
            void AddNewPatternCallback(OnNewPatternDelegate^ callback);

            void RemoveNewPatternCallback(OnNewPatternDelegate^ callback);

            void AddPatternEditorCreaetdCallback(OnPatternEditorCreatedDelegate^ callback);

            void RemovePatternEditorCreaetdCallback(OnPatternEditorCreatedDelegate^ callback);

            void AddPatternPlayCallback(OnPatternPlayDelegate^ callback);

            void RemovePatternPlayCallback(OnPatternPlayDelegate^ callback);

            void SetEditorPattern(IPattern^ pattern);

            void* CreatePattern(IMachine^ machine, const char* name, int len);

            void CreatePatternCopy(IPattern^ pnew, IPattern^ p);

            void NotifyOfPlayingPattern();

            void OverridePatternEditorWindowsMessage(UINT msg, IntPtr callback, void* param);

            void AddPatternEditorKeyboardFocusCallback(KeyboardFocusWindowHandleDelegate^ callback);

            void RemovePatternEditorKeyboardFocusCallback(KeyboardFocusWindowHandleDelegate^ callback);

            OnWindowsMessage GetEditorOverrideCallback(UINT msg, void** param);

            cli::array<int>^ GetPatternEditorMachineMIDIEvents(IPattern^ pattern);

            void SetPatternEditorMachineMIDIEvents(IPattern^ pattern, cli::array<int>^ data);


            //======================================================================================
            //Wave API
            //======================================================================================

            delegate void OnSelectedWaveChange(int newWav);

            CWaveLevel* GetWaveLevel(IWaveLayer^ wavelayer);

            IWaveLayer^ GetReBuzzWaveLevel(CWaveLevel* wavelevel);

            IWave^ GetSelectedWave();

            void SetSelectedWave(IWave^ wave);

            IWave^ FindWaveByOneIndex(int oneIndex);

            CWaveInfo* GetWaveInfo(IWave^ wave);

            void AddSelectedWaveChangeCallback(OnSelectedWaveChange^ callback);
            void RemoveSelectedWaveChangeCallback(OnSelectedWaveChange^ callback);
            
            SampleListControl^ CreateSampleListControl();

            //=================================================================


            CSequence* GetSequence(ISequence^ seq);

            ISequence^ GetReBuzzSequence(CSequence* seq);


        private:

            void OnMachineAdded(int64_t id, IMachine^ rebuzzMach, CMachine* buzzMach);
            void OnMachineRemoved(int64_t id, IMachine^ rebuzzMach, CMachine* buzzMach);

            void OnPatternAdded(int64_t id, IPattern^ pat, CPattern* buzzPat, PatternEventFlags flags);
            void OnPatternRemoved(int64_t id, IPattern^ pat, CPattern* buzzPat, PatternEventFlags flags);
            void OnPatternChanged(int64_t id, IPattern^ pat, CPattern* buzzPat, PatternEventFlags flags);

            void SendMessageToKeyboardWindow(UINT msg, WPARAM wparam, LPARAM lparam);

            void OnKeyDown(Object^ sender, KeyEventArgs^ args);
            void OnKeyUp(Object^ sender, KeyEventArgs^ args);


            static IntPtr RebuzzWindowAttachCallback(IntPtr hwnd, void* callbackParam);
            static void RebuzzWindowDettachCallback(IntPtr cwnd, void* callbackParam);
            static void RebuzzWindowSizeCallback(IntPtr patternEditorHwnd, void* callbackParam, int left, int top, int width, int height);
            
            void Free();

            void OnSequenceCreatedByReBuzz(int seq);
            void OnSequecneRemovedByReBuzz(int seq);

            void BuzzSong_PropertyChanged(System::Object^ sender, PropertyChangedEventArgs^ args);
            

            
            RebuzzBuzzLookup<ISequence, int, CSequence>* m_sequenceMap;
            
            MachineManager^ m_machineMgr;
            MachineManager::OnMachineEventDelegate^ m_onMachineAddedCallback;
            MachineManager::OnMachineEventDelegate^ m_onMachineRemovedCallback;


            PatternManager^ m_patternMgr;
            System::Collections::Generic::List< OnPatternEditorRedrawDelegate^>^ m_onPatEditorRedrawCallbacks;
            System::Collections::Generic::List< OnPatternEditorCreatedDelegate^>^ m_onPatternEditorCreatedCallbacks;
            System::Collections::Generic::List< OnNewPatternDelegate^>^ m_onNewPatternCallbacks;
            System::Collections::Generic::List< OnPatternPlayDelegate^>^ m_onPlayPatternCallbacks;
            System::Collections::Generic::List < KeyboardFocusWindowHandleDelegate^>^ m_kbFocusWindowHandleCallbacks;

            WaveManager^ m_waveManager;
            System::Collections::Generic::List< OnSelectedWaveChange^>^ m_onSelectedWaveChange;


            PatternManager::OnPatternEventDelegate^ m_onPatternAddedCallback;
            PatternManager::OnPatternEventDelegate^ m_onPatternRemovedCallback;
            PatternManager::OnPatternEventDelegate^ m_onPatternChangedCallback;





            RefClassWrapper<MachineWrapper> * m_thisref;
            MachineCallbackWrapper * m_callbackWrapper;
            CMachineInterface* m_machine;
            CMachine* m_thisCMachine;
            IBuzzMachineHost^ m_host;
            HWND m_hwndEditor;
            bool m_initialised;
            IBuzzMachine^ m_buzzmachine;
            CMasterInfo* m_masterInfo;
            void* m_mapCallbackData;
            IMachine^ m_rebuzzMachine;
            
            CPattern * m_patternEditorPattern;
            CMachine* m_patternEditorMachine;

           
            System::Action<int>^ m_seqAddedAction;
            System::Action<int>^ m_seqRemovedAction;
            UserControl^ m_control;

            KeyEventHandler^ m_onKeyDownHandler;
            KeyEventHandler^ m_onKeyupHandler;
            std::unordered_map<UINT, OnWindowsMessage> * m_editorMessageMap;
            std::unordered_map<UINT, void *> * m_editorMessageParamMap;
            PropertyChangedEventHandler^ m_buzzSongPropChangeHandler;
            
        };
    }
}
