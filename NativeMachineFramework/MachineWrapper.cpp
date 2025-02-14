#include <Windows.h>
#include <commctrl.h>
#include <map>

#include <MachineInterface.h>

#include "MachineWrapper.h"
#include "NativeMFCMachineControl.h"
#include "Utils.h"
#include "NativeMachineWriter.h"
#include "NativeMachineReader.h"

#include <sstream>

using System::Collections::Generic::List;
using BuzzGUI::Common::Global;

using BuzzGUI::Interfaces::IParameterGroup;
using BuzzGUI::Interfaces::IAttribute;
using BuzzGUI::Interfaces::IMenuItem;
using BuzzGUI::Interfaces::MachineType;
using BuzzGUI::Interfaces::MachineInfoFlags;
using BuzzGUI::Interfaces::ParameterType;
using BuzzGUI::Interfaces::ParameterFlags;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        //static LRESULT CALLBACK OverriddenWindowProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
        static const UINT_PTR s_uiSubClassId = 0x07eb0220; //ID I made up for use with WndProc sub classing routines

        static LRESULT CALLBACK OverriddenWindowProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData)
        {
            //Get class instance
            RefClassWrapper<MachineWrapper>* classRef = reinterpret_cast<RefClassWrapper<MachineWrapper> *>(dwRefData);
            if (classRef == NULL)
            {
                return 0;
            }

            /*std::ostringstream msg;
            msg << "uMsg=" << std::hex << uMsg << " wparam=" << std::hex << wParam << " lParam=" << std::hex << lParam << "\r\n";
            OutputDebugStringA(msg.str().c_str());
            */

            //Get override
            void* callbackParam = NULL;
            OnWindowsMessage callbackProc = classRef->GetRef()->GetEditorOverrideCallback(uMsg, &callbackParam);
            if (callbackProc != NULL)
            {
                bool block = false;
                LRESULT res = callbackProc(hWnd, uMsg, wParam, lParam, callbackParam, &block);

                //If callback specified to not pass the message onto the actual window, then return now.
                if (block)
                    return res;
            }

            //Call the real window proc
            return DefSubclassProc(hWnd, uMsg, wParam, lParam);
        }


        
        

        void MachineWrapper::OnSequenceCreatedByReBuzz(int seq)
        {

        }

        void MachineWrapper::OnSequecneRemovedByReBuzz(int seq)
        {

        }



        static void OnNewSequence(void* item, void* param)
        {
        }

        void MachineWrapper::BuzzSong_PropertyChanged(System::Object^ sender, PropertyChangedEventArgs^ args)
        {
            if (m_machine == NULL)
                return;

            if (args->PropertyName == "Playing")
            {
                if (!Global::Buzz->Playing)
                {
                    //No longer playing. Let the machine know the song has stopped.
                    m_machine->Stop();
                }
            }
        }


        MachineWrapper::MachineWrapper( void* machine,
                                        IBuzzMachineHost^ host,
                                        IBuzzMachine^ buzzmachine) :
                                                                m_thisref(new RefClassWrapper<MachineWrapper>(this)),
                                                                m_machine((CMachineInterface*)machine),
                                                                m_thisCMachine(NULL),
                                                                m_host(host),
                                                                m_hwndEditor(NULL),
                                                                m_initialised(false),
                                                                m_buzzmachine(buzzmachine),
                                                                m_patternEditorPattern(NULL),
                                                                m_patternEditorMachine(NULL),
                                                                m_control(nullptr),
                                                                m_onKeyDownHandler(nullptr),
                                                                m_onKeyupHandler(nullptr),
                                                                m_editorMessageMap(new std::unordered_map<UINT, OnWindowsMessage>()),
                                                                m_editorMessageParamMap(new std::unordered_map<UINT, void*>()),
                                                                m_onSelectedWaveChange(gcnew System::Collections::Generic::List<OnSelectedWaveChange^>())
        {
            
            //Create machine manager
            m_onMachineAddedCallback = gcnew MachineManager::OnMachineEventDelegate(this, &MachineWrapper::OnMachineAdded);
            m_onMachineRemovedCallback = gcnew MachineManager::OnMachineEventDelegate(this, &MachineWrapper::OnMachineRemoved);
            m_machineMgr = gcnew MachineManager(m_onMachineAddedCallback, m_onMachineRemovedCallback);
            
            //Create pattern manager
            m_onPatEditorRedrawCallbacks = gcnew List<OnPatternEditorRedrawDelegate^>();
            m_onNewPatternCallbacks = gcnew List<OnNewPatternDelegate^>();
            m_onPlayPatternCallbacks = gcnew List< OnPatternPlayDelegate^>();
            m_kbFocusWindowHandleCallbacks = gcnew List<KeyboardFocusWindowHandleDelegate^>();
            m_onPatternEditorCreatedCallbacks = gcnew List<OnPatternEditorCreatedDelegate^>();
            m_onPatternAddedCallback = gcnew PatternManager::OnPatternEventDelegate(this, &MachineWrapper::OnPatternAdded);
            m_onPatternRemovedCallback = gcnew PatternManager::OnPatternEventDelegate(this, &MachineWrapper::OnPatternRemoved);
            m_onPatternChangedCallback = gcnew PatternManager::OnPatternEventDelegate(this, &MachineWrapper::OnPatternChanged);
            m_patternMgr = gcnew PatternManager(m_onPatternAddedCallback, m_onPatternRemovedCallback, m_onPatternChangedCallback, nullptr);
            
            //Create Wave manager
            m_waveManager = gcnew WaveManager();


            m_sequenceMap = new RebuzzBuzzLookup<ISequence, int, CSequence>(OnNewSequence, m_mapCallbackData);

            //Register add this machine to the machine map
            if (host->Machine != nullptr)
            {
                Init();
            }

            //Allocate some master info
            m_masterInfo = new CMasterInfo();

            //Ask ReBuzz to tell us when a sequence has been added
            m_seqAddedAction = gcnew System::Action<int>(this, &MachineWrapper::OnSequenceCreatedByReBuzz);
            Global::Buzz->Song->SequenceAdded += m_seqAddedAction;

            //Ask ReBuzz to tell us when a sequence has been removed
            m_seqRemovedAction = gcnew System::Action<int>(this, &MachineWrapper::OnSequecneRemovedByReBuzz);
            Global::Buzz->Song->SequenceRemoved += m_seqRemovedAction;

            //Set up action for buzz song property changed (allows us to detect song stopped)
            m_buzzSongPropChangeHandler = gcnew PropertyChangedEventHandler(this, &MachineWrapper::BuzzSong_PropertyChanged);
            Global::Buzz->PropertyChanged += m_buzzSongPropChangeHandler;
        }

        MachineWrapper::!MachineWrapper()
        {
            Free();
        }

        MachineWrapper::~MachineWrapper()
        {
            Free();
        }

        void MachineWrapper::Free()
        {
            Release();

            if (m_machine != NULL)
                m_machine->pCB = NULL; //Callbacks no longer available...

            if (m_callbackWrapper != NULL)
            {
                delete m_callbackWrapper;
                m_callbackWrapper = NULL;
            }

         

            if (m_sequenceMap != NULL)
            {
                delete m_sequenceMap;
                m_sequenceMap = NULL;
            }

            if (m_masterInfo != NULL)
            {
                delete m_masterInfo;
                m_masterInfo = NULL;
            }


            if (m_onSelectedWaveChange != nullptr)
            {
                delete m_onSelectedWaveChange;
                m_onSelectedWaveChange = nullptr;   
            }
        }

        void MachineWrapper::Init()
        {
            if (!m_initialised && (m_host->Machine != nullptr))
            {
                //Store this machine
                m_thisCMachine = m_machineMgr->GetOrStoreMachine(m_host->Machine);
                m_rebuzzMachine = m_host->Machine;

                //populate master info
                m_machine->pMasterInfo = m_masterInfo;

                //Create callback wrapper class
                m_callbackWrapper = new MachineCallbackWrapper(this, m_machineMgr, m_buzzmachine, m_host, m_machine, m_thisCMachine, m_masterInfo);
                
                //Set the callback instance on the machine interface 
                m_machine->pCB = (CMICallbacks*)m_callbackWrapper;

                //Collect the patterns
                for each (IPattern ^ p in m_host->Machine->Patterns)
                {
                    m_patternMgr->GetOrStorePattern(p);
                }

                //Finally init the actual machine
                m_machine->Init(NULL);
                m_initialised = true;
            }
        }

        void MachineWrapper::Release()
        {
            if (m_hwndEditor != NULL)
            {
                //Restore the window proc first!
                RemoveWindowSubclass(m_hwndEditor, OverriddenWindowProc, s_uiSubClassId);

                //Destroy the window
                CloseWindow(m_hwndEditor);
                DestroyWindow(m_hwndEditor);
                m_hwndEditor = NULL;
            }

            if (m_callbackWrapper != NULL)
            {
                m_callbackWrapper->Release();
                m_callbackWrapper = NULL;
            }

            if (m_seqAddedAction != nullptr)
            {
                Global::Buzz->Song->SequenceAdded -= m_seqAddedAction;
                delete m_seqAddedAction;
                m_seqAddedAction = nullptr;
            }

            if (m_seqRemovedAction != nullptr)
            {
                Global::Buzz->Song->SequenceRemoved -= m_seqRemovedAction;
                delete m_seqRemovedAction;
                m_seqRemovedAction = nullptr;
            }

            if (m_control != nullptr)
            {
                if (m_onKeyDownHandler != nullptr)
                {
                    m_control->KeyDown -= this->m_onKeyDownHandler;
                    delete m_onKeyDownHandler;
                    m_onKeyDownHandler = nullptr;
                }

                if (m_onKeyupHandler != nullptr)
                {
                    m_control->KeyUp -= this->m_onKeyupHandler;
                    delete m_onKeyupHandler;
                    m_onKeyupHandler = nullptr;
                }

                delete m_control;
                m_control = nullptr;
            }

            //Remove all machines from the machine manager
            if (m_machineMgr != nullptr)
            {
                m_machineMgr->Release();
                delete m_machineMgr;
                m_machineMgr = nullptr;
            }

            if (m_onMachineAddedCallback != nullptr)
            {
                delete m_onMachineAddedCallback;
                m_onMachineAddedCallback = nullptr;
            }

            if (m_onMachineRemovedCallback != nullptr)
            {
                delete m_onMachineRemovedCallback;
                m_onMachineRemovedCallback = nullptr;
            }

            if (m_patternMgr != nullptr)
            {
                m_patternMgr->Release();
                delete m_patternMgr;
                m_patternMgr = nullptr;
            }

            if (m_onPatEditorRedrawCallbacks != nullptr)
            {
                delete m_onPatEditorRedrawCallbacks;
                m_onPatEditorRedrawCallbacks = nullptr;
            }

            if (m_onNewPatternCallbacks != nullptr)
            {
                delete m_onNewPatternCallbacks;
                m_onNewPatternCallbacks = nullptr;
            }

            if (m_onPatternEditorCreatedCallbacks != nullptr)
            {
                delete m_onPatternEditorCreatedCallbacks;
                m_onPatternEditorCreatedCallbacks = nullptr;
            }

            if (m_onPlayPatternCallbacks != nullptr)
            {
                delete  m_onPlayPatternCallbacks;
                m_onPlayPatternCallbacks = nullptr;
            }

            if (m_kbFocusWindowHandleCallbacks != nullptr)
            {
                delete m_kbFocusWindowHandleCallbacks;
                m_kbFocusWindowHandleCallbacks = nullptr;
            }

            if (m_onPatternAddedCallback != nullptr)
            {
                delete m_onPatternAddedCallback;
                m_onPatternAddedCallback = nullptr;
            }

            if (m_onPatternRemovedCallback != nullptr)
            {
                delete m_onPatternRemovedCallback;
                m_onPatternRemovedCallback = nullptr;
            }

            if (m_onPatternChangedCallback != nullptr)
            {
                delete m_onPatternChangedCallback;
                m_onPatternChangedCallback = nullptr;
            }

            if (m_editorMessageMap != NULL)
            {
                delete m_editorMessageMap;
                m_editorMessageMap = NULL;
            }

            if (m_buzzSongPropChangeHandler != nullptr)
            {
                Global::Buzz->Song->PropertyChanged -= m_buzzSongPropChangeHandler;
                delete m_buzzSongPropChangeHandler;
                m_buzzSongPropChangeHandler = nullptr;
            }


            if (m_editorMessageParamMap != NULL)
            {
                delete m_editorMessageParamMap;
                m_editorMessageParamMap = NULL;
            }

            m_machine = NULL;
        }


        CMachineInterfaceEx* MachineWrapper::GetExInterface()
        {
            return m_callbackWrapper->GetExInterface();
        }


        void MachineWrapper::UpdateMasterInfo()
        {
            //populate master info
            m_masterInfo->BeatsPerMin = m_host->MasterInfo->BeatsPerMin;
            //m_mastirInfo->GrooveData = m_host->MasterInfo->GrooveData; //No idea
            m_masterInfo->GrooveSize = m_host->MasterInfo->GrooveSize;
            m_masterInfo->PosInGroove = m_host->MasterInfo->PosInGroove;
            m_masterInfo->PosInTick = m_host->MasterInfo->PosInTick;
            m_masterInfo->SamplesPerSec = m_host->MasterInfo->SamplesPerSec;
            m_masterInfo->SamplesPerTick = m_host->MasterInfo->SamplesPerTick;
            m_masterInfo->TicksPerBeat = m_host->MasterInfo->TicksPerBeat;
            m_masterInfo->TicksPerSec = m_host->MasterInfo->TicksPerSec;
        }

        void MachineWrapper::Tick()
        {
            //Update master info
            //This copies the master info from ReBuzz into the
            //CMasterInfo pointer attached to the native machine
            UpdateMasterInfo();

            //Call tick on machine on the stroke of every tick
            if (m_initialised && (m_machine != NULL) && m_masterInfo->PosInTick == 0)
            {
                //Tell the machine to tick
                m_machine->Tick();
            }
        }

        void MachineWrapper::SetModifiedFlag()
        {
            Global::Buzz->SetModifiedFlag();
        }

        cli::array<byte>^ MachineWrapper::Save()
        {
            if (!m_initialised || (m_machine == NULL))
                return nullptr;


            //Save data 
            NativeMachineWriter output;
            m_machine->Save(&output);

            //Get data 
            const unsigned char* srcdata = output.dataPtr();
            if (srcdata == NULL)
                return nullptr;

            //Convert to .NET array
            cli::array<byte>^ retArray = gcnew cli::array<byte>(output.size());

            //Copy data
            pin_ptr<byte> destPtr = &retArray[0];
            memcpy(destPtr, srcdata, output.size());

            return retArray;
        }


        void MachineWrapper::MidiNote(int channel, int value, int velocity)
        {
            m_machine->MidiNote(channel, value, velocity);
        }

        void MachineWrapper::MidiControlChange(int ctrl, int channel, int value)
        {
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            exInterface->MidiControlChange(ctrl, channel, value);
        }

        void MachineWrapper::ControlChange(IMachine^ machine, int group, int track, int param, int value)
        {
            //Get machine
            CMachine* mach = m_machineMgr->GetOrStoreMachine(machine);

            //Not sure how to do this one?
            //
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            //exInterface->RecordControlChange(mach, group, track, param, value);
        }

        static int FindParameterGroupAndParam(IMachine^ mach, IParameter^ param, int* retParamNum)
        {
            int group = 0;
            for each (IParameterGroup ^ g in mach->ParameterGroups)
            {
                *retParamNum = g->Parameters->IndexOf(param);
                if (*retParamNum >= 0)
                {
                    return group;
                }

                ++group;
            }

            return -1;
        }


        void MachineWrapper::RecordControlChange(IParameter^ parameter, int track, int value)
        {
            //Get Ex Interface for calling the machine
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();

            if (m_thisCMachine != NULL)
            {
                //Get our CMachine * 
                CMachine* mach = m_thisCMachine;

                //Find the parameter group and parameter number values
                int paramNum = -1;
                int groupNum = FindParameterGroupAndParam(m_host->Machine, parameter, &paramNum);
                if (groupNum >= 0)
                {
                    //Call the machine
                    exInterface->RecordControlChange(mach, groupNum, track, paramNum, value);
                }
            }
        }

        static int ConvertBuzzCommandToNative(BuzzCommand cmd)
        {
            int nativeCmd = -1;
            switch (cmd)
            {
            case BuzzCommand::Cut:
                return 0xE123; // ID_EDIT_CUT;
            case BuzzCommand::Copy:
                return 0xE122; // ID_EDIT_COPY;
            case BuzzCommand::Paste:
                return 0xE125; // ID_EDIT_PASTE;
            case BuzzCommand::Undo:
                return 0xE12B; // ID_EDIT_UNDO;
            case BuzzCommand::Redo:
                return 0xE1CB; // ID_EDIT_REDO;
            default:
                return -1;
            }
        }


        bool MachineWrapper::CanExecuteCommand(BuzzCommand cmd)
        {
            //Convert command to native
            int nativeCmd = ConvertBuzzCommandToNative(cmd);
            if (nativeCmd == -1)
                return false; //not supported.

            //Ask buzz machine
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            return exInterface->EnableCommandUI(nativeCmd);
        }

        void MachineWrapper::ExecuteCommand(BuzzCommand cmd)
        {
            //Convert command to native
            int nativeCmd = ConvertBuzzCommandToNative(cmd);
            if (nativeCmd == -1)
                return; //not supported.

            //Send command to editor window
            PostMessage(m_hwndEditor, WM_COMMAND, nativeCmd, 0);
        }



        //==============================================================
        // Machine Handling
        //==============================================================

        IMachine^ MachineWrapper::GetThisReBuzzMachine()
        {
            return m_rebuzzMachine;
        }

        //Called by the MachineManager whenever a machine is added.
        void MachineWrapper::OnMachineAdded(int64_t id, IMachine^ rebuzzMach, CMachine* buzzMach)
        {
            //Register pattern added event handlers to the machine
            m_patternMgr->AddEventHandlersToMachine(rebuzzMach);

            //Fire registered events that were registered by the native machine via the callback wrapper
            if (m_callbackWrapper != NULL)
            {
                m_callbackWrapper->OnMachineAdded(rebuzzMach);
            }
        }

        void MachineWrapper::OnMachineRemoved(int64_t id, IMachine^ rebuzzMach, CMachine* buzzMach)
        {
            //First, fire events that were registered by the native machine via the callback interface
            //This allows the native machine to handle the deleted event, before the machine is 
            //physically removed from memory
            if (m_callbackWrapper != NULL)
            {
                m_callbackWrapper->OnMachineRemoved(rebuzzMach);
            }

            //Remove all patterns for this machine from the pattern manager
            m_patternMgr->RemovePatternsByMachine(rebuzzMach);
        }


        void MachineWrapper::SetTargetMachine(IMachine^ machine)
        {
            //Get Ex Interface for calling the machine
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();

            //MC: I'm guessing here that setting the target machine ALSO sets
            //    the pattern as well
            // As we don't have the pattern, just pick the first

            //Get machine
            CMachine* mach = m_machineMgr->GetOrStoreMachine(machine);

            //Get first pattern (is this the correct thing to do? - we're not told the pattern otherwise)
            IPattern^ pattern = machine->Patterns[0];
            CPattern* pat = m_patternMgr->GetOrStorePattern(pattern);

            exInterface->SetPatternTargetMachine(pat, mach);
        }

        void* MachineWrapper::GetCMachine(IMachine^ m)
        {
            return m_machineMgr->GetOrStoreMachine(m);
        }

        CMachine* MachineWrapper::GetCMachineByName(const char* name)
        {
            return m_machineMgr->GetCMachineByName(name);
        }

        IMachine^ MachineWrapper::GetReBuzzMachine(void* mach)
        {
            return m_machineMgr->GetReBuzzMachine(reinterpret_cast<CMachine*>(mach));
        }

        CMachineData* MachineWrapper::GetBuzzMachineData(void* mach)
        {
            return m_machineMgr->GetBuzzMachineData(reinterpret_cast<CMachine*>(mach));
        }

        //===========================================================================
        //Pattern API
        //===========================================================================

        void* MachineWrapper::GetCPattern(IPattern^ p)
        {
            return m_patternMgr->GetOrStorePattern(p);
        }

        IPattern^ MachineWrapper::GetReBuzzPattern(void* pat)
        {
            return m_patternMgr->GetReBuzzPattern(reinterpret_cast<CPattern*>(pat));
        }

        CPatternData* MachineWrapper::GetBuzzPatternData(void* pat)
        {
            return m_patternMgr->GetBuzzPatternData(reinterpret_cast<CPattern*>(pat));
        }

        void* MachineWrapper::GetCPatternByName(IMachine^ rebuzzmac, const char* name)
        {
            return m_patternMgr->GetPatternByName(rebuzzmac, name);
        }

        void MachineWrapper::UpdatePattern(CPattern* pat, int newLen, const char* newName)
        {
            m_patternMgr->OnNativePatternChange(pat, newLen, newName);
        }

        UserControl^ MachineWrapper::PatternEditorControl()
        {
            if (m_control == nullptr)
            {
                //Create MFC wrapper
                NativeMFCMachineControl::AttachCallback^ onAttach = gcnew NativeMFCMachineControl::AttachCallback(RebuzzWindowAttachCallback);
                NativeMFCMachineControl::DetatchCallback^ onDetatch = gcnew NativeMFCMachineControl::DetatchCallback(RebuzzWindowDettachCallback);
                NativeMFCMachineControl::SizeChangedCallback^ onSzChanged = gcnew NativeMFCMachineControl::SizeChangedCallback(RebuzzWindowSizeCallback);
                NativeMFCMachineControl^ mfccontrol = gcnew NativeMFCMachineControl(onAttach, onDetatch, onSzChanged, m_thisref);
                m_control = mfccontrol;

                //Register for events
                m_control->KeyDown += m_onKeyDownHandler;

                //Get ex interface
                CMachineInterfaceEx* exInterface = (CMachineInterfaceEx*)m_callbackWrapper->GetExInterface();

                //Tell PatterXP to create the pattern editor using the .NET user control as its parent
                //By accessing 'Handle' the window attachment stuff will run
                HWND controlHwnd = (HWND)mfccontrol->Handle.ToPointer();

                //Make sure editor window is set up
                if (m_hwndEditor == NULL)
                {
                    RebuzzWindowAttachCallback(mfccontrol->Handle, m_thisref);
                }

                //Create and register window events
                m_onKeyDownHandler = gcnew KeyEventHandler(this, &MachineWrapper::OnKeyDown);
                m_control->KeyDown += m_onKeyDownHandler;

                m_onKeyupHandler = gcnew KeyEventHandler(this, &MachineWrapper::OnKeyUp);
                m_control->KeyUp += m_onKeyupHandler;

                //If we have a pattern to set, then do that now
                //(it was deferred from earlier)
                if ((m_initialised) && (m_patternEditorMachine != NULL) && (m_patternEditorPattern != NULL))
                {
                    exInterface->SetPatternTargetMachine(m_patternEditorPattern, m_patternEditorMachine);
                    exInterface->SetEditorPattern(m_patternEditorPattern);

                    m_patternEditorPattern = NULL;
                    m_patternEditorMachine = NULL;
                }

                //Tell the caller that the control has now been created and set up
                if (m_onPatternEditorCreatedCallbacks != nullptr)
                {
                    for each (OnPatternEditorCreatedDelegate^ patEditorCreatedCallback  in m_onPatternEditorCreatedCallbacks)
                    {
                        try
                        {
                            patEditorCreatedCallback();
                        }
                        catch(...)
                        {}
                    }
                }

                //Set focus on the keyboard focus window
                SetForegroundWindow(m_hwndEditor);
                SetFocus(m_hwndEditor);
                SetActiveWindow(m_hwndEditor);
            }

            return m_control;
        }

        void MachineWrapper::ActivatePatternEditor()
        {
            if (m_hwndEditor != NULL)
            {
                SetForegroundWindow(m_hwndEditor);
                SetActiveWindow(m_hwndEditor);
                SetFocus(m_hwndEditor);
            }
        }

        void MachineWrapper::AddPatternEditorRedrawCallback(OnPatternEditorRedrawDelegate^ callback)
        {
            if (m_onPatEditorRedrawCallbacks != nullptr)
            {
                m_onPatEditorRedrawCallbacks->Add(callback);
            }
        }

        void MachineWrapper::RemovePatternEditorRedrawCallback(OnPatternEditorRedrawDelegate^ callback)
        {
            if (m_onPatEditorRedrawCallbacks != nullptr)
            {
                m_onPatEditorRedrawCallbacks->Add(callback);
            }
        }

        void MachineWrapper::AddNewPatternCallback(OnNewPatternDelegate^ callback)
        {
            if (m_onNewPatternCallbacks != nullptr)
            {
                m_onNewPatternCallbacks->Add(callback);
            }
        }

        void MachineWrapper::RemoveNewPatternCallback(OnNewPatternDelegate^ callback)
        {
            if (m_onNewPatternCallbacks != nullptr)
            {
                m_onNewPatternCallbacks->Remove(callback);
            }
        }

        void MachineWrapper::AddPatternEditorCreaetdCallback(OnPatternEditorCreatedDelegate^ callback)
        {
            if (m_onPatternEditorCreatedCallbacks != nullptr)
            {
                m_onPatternEditorCreatedCallbacks->Add(callback);
            }
        }
        void MachineWrapper::RemovePatternEditorCreaetdCallback(OnPatternEditorCreatedDelegate^ callback)
        {
            if (m_onPatternEditorCreatedCallbacks != nullptr)
            {
                m_onPatternEditorCreatedCallbacks->Remove(callback);
            }
        }


        void MachineWrapper::AddPatternPlayCallback(OnPatternPlayDelegate^ callback)
        {
            if (m_onPlayPatternCallbacks != nullptr)
            {
                m_onPlayPatternCallbacks->Add(callback);
            }
        }

        void MachineWrapper::RemovePatternPlayCallback(OnPatternPlayDelegate^ callback)
        {
            if (m_onPlayPatternCallbacks != nullptr)
            {
                m_onPlayPatternCallbacks->Remove(callback);
            }
        }


        void MachineWrapper::SetEditorPattern(IPattern^ pattern)
        {
            //Make sure we're initialised
            Init();

            //Store the machine ref (if not already stored)
            CMachine* patMach = m_machineMgr->GetOrStoreMachine(pattern->Machine);

            //Set target machine if needed
            bool haveSetTargetMach = false;
            IMachine^ currentEditorTargetReBuzzMachine = m_patternMgr->GetEditorTargetMachine();
            CMachine* currentEditorTgtMach = (currentEditorTargetReBuzzMachine == nullptr) ? NULL : m_machineMgr->GetBuzzMachine(currentEditorTargetReBuzzMachine);
            if (currentEditorTgtMach != patMach)
            {
                m_patternMgr->SetEditorTargetMachine(pattern->Machine);
                currentEditorTgtMach = patMach;
            }

            //Store pattern ref (if not already stored)
            CPattern* pat = m_patternMgr->GetOrStorePattern(pattern);

            //Get ex interface and callback data
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();

            //Set target machine if needed            
            if ((currentEditorTgtMach != patMach) && (m_onNewPatternCallbacks != nullptr))
            {
                int64_t patid = Utils::ObjectToInt64(pattern);
                for each (OnNewPatternDelegate ^ onNewPatCallback in m_onNewPatternCallbacks)
                {
                    try
                    {
                        onNewPatCallback(pattern->Machine, patMach, pattern, pat, pattern->Name);
                    }
                    catch (...)
                    {
                    }
                }


                if (exInterface != NULL)
                {
                    haveSetTargetMach = true;
                    exInterface->SetPatternTargetMachine(pat, patMach);
                }
            }

            if (!haveSetTargetMach)
            {
                if (exInterface != NULL)
                {   //Tell pattern editor, if the pattern editor is active
                    if ((m_hwndEditor != NULL) && (exInterface != NULL))
                    {
                        exInterface->SetPatternTargetMachine(pat, patMach);
                        exInterface->SetEditorPattern(pat);
                    }
                    else
                    {
                        //Store the pattern and machine for later, when the editor is created
                        m_patternEditorMachine = patMach;
                        m_patternEditorPattern = pat;
                    }
                }
                else
                {
                    //Tell callback to call the mathods when the exInterface has been set
                    m_callbackWrapper->SetDelayedEditorPattern(patMach, pat);
                }
            }
        }

        void* MachineWrapper::CreatePattern(IMachine^ machine, const char* name, int len)
        {
            //Create pattern in rebuzz
            String^ patname = Utils::stdStringToCLRString(name);
            machine->CreatePattern(patname, len);

            //Get the CPattern *
            CPattern* cpat = m_patternMgr->GetPatternByName(machine, name);
            return cpat;
        }

        void MachineWrapper::CreatePatternCopy(IPattern^ pnew, IPattern^ p)
        {
            //Get old pattern
            CPattern* oldPat = m_patternMgr->GetOrStorePattern(p);

            ///Get new pattern
            CPattern* newPat = m_patternMgr->GetOrStorePattern(pnew);

            if ((oldPat == NULL) || (newPat == NULL))
                return;

            //Get old pattern data
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            exInterface->CreatePatternCopy(newPat, oldPat);
        }

        void MachineWrapper::NotifyOfPlayingPattern()
        {
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();

            //Get sequences and tell machine about them
            for each (ISequence ^ s in Global::Buzz->Song->Sequences)
            {
                if (!s->IsDisabled)
                {
                    //Ignore any sequence that does not have a playing sequence
                    IPattern^ playingPat = s->PlayingPattern;
                    if ((playingPat != nullptr) && (playingPat->PlayPosition >= 0))
                    {
                        //Get CPattern * for this pattern
                        CPattern* cpat = m_patternMgr->GetOrStorePattern(playingPat);
                        if (cpat != NULL)
                        {
                            CPatternData* patdata = m_patternMgr->GetBuzzPatternData(cpat);

                            //Get CSequence * for this sequence
                            uint64_t seqid = s->GetHashCode();
                            bool created = false;
                            CSequence* cseq = m_sequenceMap->GetOrStoreReBuzzTypeById(seqid, s, &created);
                            if (cseq != NULL)
                            {
                                CMachine* patMach = m_machineMgr->GetOrStoreMachine(playingPat->Machine);

                                //Ask the Native machine if it's ok to call the exInterface
                                bool callExInterface = true;
                                if (m_onPlayPatternCallbacks != nullptr)
                                {
                                    for each (OnPatternPlayDelegate ^ playPatCallback in m_onPlayPatternCallbacks)
                                    {
                                        if (!playPatCallback(playingPat->Machine, patMach, playingPat, cpat, playingPat->Name))
                                        {
                                            callExInterface = false;
                                        }
                                    }
                                }

                                if (callExInterface && (exInterface != NULL))
                                {
                                    //Tell interface about this pattern and the current play position within
                                    //that pattern.
                                    int playpos = s->PlayingPatternPosition;
                                    exInterface->PlayPattern(cpat, cseq, playpos);
                                }
                            }
                        }
                    }
                }
            }
        }

        void MachineWrapper::OverridePatternEditorWindowsMessage(UINT msg, IntPtr callback, void* param)
        {
            (*m_editorMessageMap)[msg] = reinterpret_cast<OnWindowsMessage>(callback.ToPointer());
            (*m_editorMessageParamMap)[msg] = param;
        }

        void MachineWrapper::AddPatternEditorKeyboardFocusCallback(KeyboardFocusWindowHandleDelegate^ callback)
        {
            if (m_kbFocusWindowHandleCallbacks != nullptr)
            {
                m_kbFocusWindowHandleCallbacks->Add(callback);
            }
        }

        void MachineWrapper::RemovePatternEditorKeyboardFocusCallback(KeyboardFocusWindowHandleDelegate^ callback)
        {
            if (m_kbFocusWindowHandleCallbacks != nullptr)
            {
                m_kbFocusWindowHandleCallbacks->Remove(callback);
            }
        }

        OnWindowsMessage MachineWrapper::GetEditorOverrideCallback(UINT msg, void** param)
        {
            const auto& msgHandler = m_editorMessageMap->find(msg);
            if (msgHandler == m_editorMessageMap->end())
                return NULL;

            const auto& msgHandlerParam = m_editorMessageParamMap->find(msg);
            if (msgHandlerParam == m_editorMessageParamMap->end())
            {
                *param = NULL;
            }
            else
            {
                *param = (*msgHandlerParam).second;
            }

            return (*msgHandler).second;
        }

        cli::array<int>^ MachineWrapper::GetPatternEditorMachineMIDIEvents(IPattern^ pattern)
        {
            //Get pattern
            uint64_t patid = pattern->GetHashCode();
            CPattern* pat = m_patternMgr->GetOrStorePattern(pattern);

            //Save data 
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            NativeMachineWriter output;
            exInterface->ExportMidiEvents(pat, &output);

            //Get data 
            const unsigned char* srcdata = output.dataPtr();
            if (srcdata == NULL)
                return gcnew cli::array<int>(0); //empty array. Returning null crashes sequence editor

            //Convert to .NET array
            cli::array<int>^ retArray = gcnew cli::array<int>(output.size() / sizeof(int));
            pin_ptr<int> destPtr = &retArray[0];
            memcpy(destPtr, srcdata, output.size());
            return retArray;
        }

        void MachineWrapper::SetPatternEditorMachineMIDIEvents(IPattern^ pattern, cli::array<int>^ data)
        {
            if ((data == nullptr) || (data->Length == 0))
                return;

            //Get pattern
            CPattern* pat = m_patternMgr->GetOrStorePattern(pattern);

            //Convert data from .NET to native
            std::vector<unsigned char> nativeData(data->Length * sizeof(int));
            pin_ptr<int> srcPtr = &data[0];
            memcpy(&nativeData[0], srcPtr, data->Length * sizeof(int));
            NativeMachineReader input(&nativeData[0], nativeData.size());

            //Load data
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            exInterface->ImportMidiEvents(pat, &input);
        }


        //Called by PatternManager whenever a pattern is added
        void MachineWrapper::OnPatternAdded(int64_t id, IPattern^ rebuzzPat, CPattern* buzzPat, PatternEventFlags changeflags)
        {
            //Check that the pattern is for this machine
            CMachine* patmach = m_machineMgr ->GetBuzzMachine(rebuzzPat->Machine);
            IMachine^ currentTargetRebuzzMachine = m_patternMgr->GetEditorTargetMachine();
            bool isTargetMachine = false;
            if (currentTargetRebuzzMachine != nullptr)
            {
                CMachine* currentTargetMachine = m_machineMgr->GetBuzzMachine(currentTargetRebuzzMachine);
                isTargetMachine = (currentTargetMachine == patmach);
            }

            //Notify the external callback, only if the target machine is set and is the correct one
            //If this pattern is NOT the target editor machine, then do not notify the machine of this pattern.
            if (isTargetMachine && (m_onNewPatternCallbacks != nullptr))
            {
                CMachine* patmach = m_machineMgr->GetOrStoreMachine(rebuzzPat->Machine);

                for each (MachineWrapper::OnNewPatternDelegate ^ newPatCallback  in m_onNewPatternCallbacks)
                {
                    try
                    {
                        newPatCallback(rebuzzPat->Machine, patmach, rebuzzPat, buzzPat, rebuzzPat->Name);
                    }
                    catch (...)
                    {
                    }
                }
            }

            //Notify the machine EX interface
            if (isTargetMachine)
            {   
                CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface(); 
                if(exInterface != NULL)
                    exInterface->CreatePattern(buzzPat, rebuzzPat->Length);
            }
        }

        //Called by PatternManager whenever a pattern is removed
        void MachineWrapper::OnPatternRemoved(int64_t id, IPattern^ rebuzzPat, CPattern* buzzPat, PatternEventFlags changeflags)
        {
            //Notify the machine EX interface
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            if (exInterface != NULL)
            {
                exInterface->DeletePattern(buzzPat);
            }
        }

        void MachineWrapper::OnPatternChanged(int64_t id, IPattern^ rebuzzPat, CPattern* buzzPat, PatternEventFlags changeflags)
        {
            //Notify the machine EX interface
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            if (exInterface != NULL)
            {
                if (changeflags & PatternEventFlags::PatternEventFlags_Name)
                {
                    std::string newName;
                    Utils::CLRStringToStdString(rebuzzPat->Name, newName);
                    exInterface->RenamePattern(buzzPat, newName.c_str());
                }

                if (changeflags & PatternEventFlags::PatternEventFlags_Length)
                {
                    exInterface->SetPatternLength(buzzPat, rebuzzPat->Length);
                }
            }

            //Call the redraw callbacks
            if (m_onPatEditorRedrawCallbacks != nullptr)
            {
                for each (OnPatternEditorRedrawDelegate ^ redrawCallback in m_onPatEditorRedrawCallbacks)
                {
                    try
                    {
                        redrawCallback();
                    }
                    catch(...)
                    {
                    }
                }
            }
        }


        //====================================================================
        //Wave API
        //====================================================================

        CWaveLevel* MachineWrapper::GetWaveLevel(IWaveLayer^ wavelayer)
        {
            return m_waveManager->GetWaveLevelFromLayer(wavelayer);
        }

        IWaveLayer^ MachineWrapper::GetReBuzzWaveLevel(CWaveLevel* wavelevel)
        {
            return m_waveManager->GetLayerFromWaveLevel(wavelevel);
        }

        IWave^ MachineWrapper::GetSelectedWave()
        {
            return m_waveManager->GetSelectedWave();
        }

        void MachineWrapper::SetSelectedWave(IWave^ wave)
        {
            m_waveManager->SetSelectedWave(wave);
        }


        IWave^ MachineWrapper::FindWaveByOneIndex(int oneIndex)
        {
            return m_waveManager->FindWaveByOneIndex(oneIndex);
        }

        CWaveInfo* MachineWrapper::GetWaveInfo(IWave^ wave)
        {
            return m_waveManager->GetWaveInfo(wave);
        }


        void MachineWrapper::AddSelectedWaveChangeCallback(OnSelectedWaveChange^ callback)
        {
            m_onSelectedWaveChange->Add(callback);
        }

        void MachineWrapper::RemoveSelectedWaveChangeCallback(OnSelectedWaveChange^ callback)
        {
            m_onSelectedWaveChange->Remove(callback);
        }

        SampleListControl^ MachineWrapper::CreateSampleListControl()
        {
            return gcnew SampleListControl(m_waveManager);
        }

        //====================================================================================

        void MachineWrapper::SendMessageToKeyboardWindow(UINT msg, WPARAM wparam, LPARAM lparam)
        {
            //If a keyboard window callback has been specified, then call it
            //to get the window that we should be fowarding the windows message to
            HWND hwndSendMsg = m_hwndEditor;
            if (m_kbFocusWindowHandleCallbacks != nullptr)
            {
                for each (KeyboardFocusWindowHandleDelegate ^ kbFocusCallback  in m_kbFocusWindowHandleCallbacks)
                {
                    try
                    {
                        IntPtr hwndPtr = kbFocusCallback();
                        HWND hwndSendMsg = (HWND)hwndPtr.ToPointer();
                        if (hwndSendMsg != NULL)
                        {
                            //Set focus on the keyboard focus window
                            SetForegroundWindow(m_hwndEditor);
                            SetFocus(hwndSendMsg);
                            SetActiveWindow(hwndSendMsg);

                            //Send the windows message
                            SendMessage(hwndSendMsg, msg, wparam, lparam);
                        }
                    }
                    catch (...)
                    {
                    }
                }
            }
        }


        void MachineWrapper::OnKeyDown(Object^ sender, KeyEventArgs^ args)
        {
            SendMessageToKeyboardWindow(WM_KEYDOWN, (WPARAM)args->KeyValue, 0);
        }

        void MachineWrapper::OnKeyUp(Object^ sender, KeyEventArgs^ args)
        {
            SendMessageToKeyboardWindow(WM_KEYUP, (WPARAM)args->KeyValue, 0);
        }


        IntPtr MachineWrapper::RebuzzWindowAttachCallback(IntPtr hwnd, void* callbackParam)
        {
            RefClassWrapper<MachineWrapper>* classRef = reinterpret_cast<RefClassWrapper<MachineWrapper> *>(callbackParam);
            CMachineInterfaceEx* exInterface = (CMachineInterfaceEx*)classRef->GetRef()->GetExInterface();

            //Create editor
            void* patternEditorHwnd = exInterface->CreatePatternEditor(hwnd.ToPointer());

            //Store the HWND in the class for sending window messages
            classRef->GetRef()->m_hwndEditor = (HWND)patternEditorHwnd;

            //Subclass the editor window to override various low-level window messages
            SetWindowSubclass((HWND)patternEditorHwnd, OverriddenWindowProc, s_uiSubClassId, (DWORD_PTR)classRef);

            //Return editor window to NativeMFCControl, so that the window sizes can be syncronised.
            return IntPtr(patternEditorHwnd);
        }


        void MachineWrapper::RebuzzWindowDettachCallback(IntPtr patternEditorHwnd, void* callbackParam)
        {
            if (patternEditorHwnd != IntPtr::Zero)
            {
                //Get machine wrapper
                RefClassWrapper<MachineWrapper>* classRef = reinterpret_cast<RefClassWrapper<MachineWrapper> *>(callbackParam);

                //Destroy the pattern editor window
                DestroyWindow((HWND)patternEditorHwnd.ToPointer());
                classRef->GetRef()->m_hwndEditor = NULL;
            }
        }

        void MachineWrapper::RebuzzWindowSizeCallback(IntPtr patternEditorHwnd, void* callbackParam, int left, int top, int width, int height)
        {
            if (patternEditorHwnd != IntPtr::Zero)
            {
                SetWindowPos((HWND)patternEditorHwnd.ToPointer(), NULL, 0, 0, width, height, SWP_NOZORDER);
                InvalidateRect((HWND)patternEditorHwnd.ToPointer(), NULL, TRUE);
            }
        }

        
        

        
        
        
        

        CSequence* MachineWrapper::GetSequence(ISequence^ seq)
        {
            uint64_t id = seq->GetHashCode();
            bool created = false;
            return m_sequenceMap->GetOrStoreReBuzzTypeById(id, seq, &created);
        }

        ISequence^ MachineWrapper::GetReBuzzSequence(CSequence* seq)
        {
            return m_sequenceMap->GetReBuzzTypeByBuzzType(seq);
        }

    }
}