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
        struct MachineWrapperCallbackData
        {
            RefClassWrapper<PatternManager> patternMgr;
            RefClassWrapper<MachineManager> machineMgr;
            MachineCallbackWrapper* callbacks;
            OnPatternEditorRedrawCallback redrawcallback;
            CMachineInterfaceEx* exiface;
            OnNewPatternCallback onNewPatternCallback;
            void* cBParam;
            CMachine* editorTargetMachine;
        };

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


        //Called by the MachineManager whenever a machine is added.
        static void OnMachineAdded(int64_t id, IMachine^ rebuzzMach, CMachine* buzzMach, void* param)
        {
            MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(param);

            //Update pattern manager with patterns from this machine
            //cbdata->patternMgr.GetRef()->ScanMachineForPatterns(rebuzzMach);

            //Register pattern added event handlers to the machine
            cbdata->patternMgr.GetRef()->AddEventHandlersToMachine(rebuzzMach);

            //Fire registered events that were registered by the native machine via the callback wrapper
            if (cbdata->callbacks != NULL)
            {
                cbdata->callbacks->OnMachineAdded(rebuzzMach);
            }
        }

        //Called by the MachineManager whenever a machine is removed.
        static void OnMachineRemoved(int64_t id, IMachine^ rebuzzMach, CMachine* buzzMach, void* param)
        {
            MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(param);

            //First, fire events that were registered by the native machine via the callback interface
            //This allows the native machine to handle the deleted event, before the machine is 
            //physically removed from memory
            if (cbdata->callbacks != NULL)
            {
                cbdata->callbacks->OnMachineRemoved(rebuzzMach);
            }

            //Remove all patterns for this machine from the pattern manager
            cbdata->patternMgr.GetRef()->RemovePatternsByMachine(rebuzzMach);
        }

        //Called by PatternManager whenever a pattern is added
        static void OnPatternAdded(int64_t id, IPattern^ rebuzzPat, CPattern* buzzPat, PatternEventFlags changeflags, void* param)
        {
            MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(param);

            //Check that the pattern is for this machine
            CMachine* patmach = cbdata->machineMgr.GetRef()->GetBuzzMachine(rebuzzPat->Machine);
            bool isTargetMachine = (cbdata->editorTargetMachine != NULL) && (patmach == cbdata->editorTargetMachine);

            //Notify the external callback, only if the target machine is set and is the correct one
            //If this pattern is NOT the target editor machine, then do not notify the machine of this pattern.
            if ((cbdata->onNewPatternCallback != NULL) && isTargetMachine)
            {
                CMachine* patmach = cbdata->machineMgr.GetRef()->GetOrStoreMachine(rebuzzPat->Machine);

                std::string patname;
                Utils::CLRStringToStdString(rebuzzPat->Name, patname);
                cbdata->onNewPatternCallback(patmach, buzzPat, patname.c_str(), cbdata->cBParam);
            }

            //Notify the machine EX interface
            if ((cbdata->exiface != NULL) && isTargetMachine)
            {
                cbdata->exiface->CreatePattern(buzzPat, rebuzzPat->Length);
            }
        }

        //Called by PatternManager whenever a pattern is removed
        static void OnPatternRemoved(int64_t id, IPattern^ rebuzzPat, CPattern* buzzPat, PatternEventFlags changeflags, void* param)
        {
            MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(param);

            //Notify the machine EX interface
            if (cbdata->exiface != NULL)
            {
                cbdata->exiface->DeletePattern(buzzPat);
            }
        }

        static void OnPatternModified(int64_t id, IPattern^ rebuzzPat, CPattern* buzzPat, PatternEventFlags changeflags, void* param)
        {
            MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(param);

            //Notify the machine EX interface
            if (cbdata->exiface != NULL)
            {
                if (changeflags & PatternEventFlags::PatternEventFlags_Name)
                {
                    std::string newName;
                    Utils::CLRStringToStdString(rebuzzPat->Name, newName);
                    cbdata->exiface->RenamePattern(buzzPat, newName.c_str());
                }

                if (changeflags & PatternEventFlags::PatternEventFlags_Length)
                {
                    cbdata->exiface->SetPatternLength(buzzPat, rebuzzPat->Length);
                }
            }

            //Call the redraw callback
            if (cbdata->redrawcallback != NULL)
            {
                cbdata->redrawcallback(cbdata->cBParam);
            }
        }


        void MachineWrapper::OnSequenceCreatedByReBuzz(int seq)
        {

        }

        void MachineWrapper::OnSequecneRemovedByReBuzz(int seq)
        {

        }


        static void updateWaveLevel(CWaveLevel* buzzwavlevel, IWaveLayer^ rebuzzWaveLayer)
        {
            //Populate the buzz wave level
            buzzwavlevel->LoopEnd = rebuzzWaveLayer->LoopEnd;
            buzzwavlevel->LoopStart = rebuzzWaveLayer->LoopStart;
            buzzwavlevel->numSamples = rebuzzWaveLayer->SampleCount;
            buzzwavlevel->SamplesPerSec = rebuzzWaveLayer->SampleRate;
            buzzwavlevel->pSamples = (short*)rebuzzWaveLayer->RawSamples.ToPointer();
        }

        static void  OnNewBuzzWaveLevel(void* item, void* param)
        {
            /*MachineCreateCallbackData* machCallbackData = reinterpret_cast<MachineCreateCallbackData*>(param);

            //Get the rebuzz class
            CWaveLevel* buzzwavlevel = reinterpret_cast<CWaveLevel*>(item);
            IWaveLayer^ rebuzzWaveLayer = machCallbackData->machineWrapper.GetRef()->GetReBuzzWaveLevel(buzzwavlevel);
            if (rebuzzWaveLayer == nullptr)
                return;

            updateWaveLevel(buzzwavlevel, rebuzzWaveLayer);*/
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


        MachineWrapper::MachineWrapper(void* machine,
            IBuzzMachineHost^ host,
            IBuzzMachine^ buzzmachine,
            void* callbackparam,
            OnPatternEditorCreateCallback editorCreateCallback,
            KeyboardFocusWindowHandleCallback kbcallback,
            OnPatternEditorRedrawCallback redrawcallback,
            OnNewPatternCallback newPatternCallback,
            OnPlayingPatternCallback onPlayPatternCallback) :
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
            m_editorCreateCallback(editorCreateCallback),
            m_kbFocusWndcallback(kbcallback),
            m_onPlayPatternCallback(onPlayPatternCallback),
            m_externalCallbackParam(callbackparam),
            m_onKeyDownHandler(nullptr),
            m_onKeyupHandler(nullptr),
            m_editorMessageMap(new std::unordered_map<UINT, OnWindowsMessage>()),
            m_editorMessageParamMap(new std::unordered_map<UINT, void*>()),
            m_targetEditorMachine(NULL)
        {
            //Create callback data
            MachineWrapperCallbackData* internalCallbackData = new MachineWrapperCallbackData();
            m_internalCallbackData = internalCallbackData;
            internalCallbackData->callbacks = NULL;
            internalCallbackData->redrawcallback = redrawcallback;
            internalCallbackData->exiface = NULL;
            internalCallbackData->cBParam = callbackparam;
            internalCallbackData->onNewPatternCallback = newPatternCallback;
            internalCallbackData->editorTargetMachine = NULL;

            //Create machine manager
            m_machineMgr = gcnew MachineManager(OnMachineAdded, OnMachineRemoved, m_internalCallbackData);
            internalCallbackData->machineMgr.Assign(m_machineMgr);

            //Create pattern manager
            //m_patternMgr = gcnew PatternManager(NULL, redrawcallback, m_internalCallbackData);
            m_patternMgr = gcnew PatternManager(OnPatternAdded, OnPatternRemoved, OnPatternModified, NULL, m_internalCallbackData);
            internalCallbackData->patternMgr.Assign(m_patternMgr);

            m_waveLevelsMap = new RebuzzBuzzLookup<IWaveLayer, int, CWaveLevel>(OnNewBuzzWaveLevel, m_mapCallbackData);
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

            if (m_waveLevelsMap != NULL)
            {
                delete m_waveLevelsMap;
                m_waveLevelsMap = NULL;
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

            if (m_internalCallbackData != NULL)
            {
                MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(m_internalCallbackData);
                delete cbdata;
                m_internalCallbackData = NULL;
            }
        }

        void MachineWrapper::Init()
        {
            if (!m_initialised && (m_host->Machine != nullptr))
            {
                MachineWrapperCallbackData* internalCallbackData = reinterpret_cast<MachineWrapperCallbackData*>(m_internalCallbackData);

                //Store this machine
                m_thisCMachine = m_machineMgr->GetOrStoreMachine(m_host->Machine);
                m_rebuzzMachine = m_host->Machine;

                //populate master info
                m_machine->pMasterInfo = m_masterInfo;

                //Create callback wrapper class
                m_callbackWrapper = new MachineCallbackWrapper(this, m_machineMgr, m_buzzmachine, m_host, m_machine, m_thisCMachine, m_masterInfo);
                internalCallbackData->callbacks = m_callbackWrapper;

                //Set the callback instance on the machine interface 
                m_machine->pCB = (CMICallbacks*)m_callbackWrapper;

                //Collect the patterns
                for each (IPattern ^ p in m_host->Machine->Patterns)
                {
                    m_patternMgr->GetOrStorePattern(p);
                }

                //Finally init the actual machine
                m_machine->Init(NULL);

                //We should have an ExInterface at this point, so tell the patten manager
                CMachineInterfaceEx* exiface = m_callbackWrapper->GetExInterface();
                internalCallbackData->exiface = exiface;

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

            if (m_patternMgr != nullptr)
            {
                m_patternMgr->Release();
                delete m_patternMgr;
                m_patternMgr = nullptr;
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

        IMachine^ MachineWrapper::GetThisReBuzzMachine()
        {
            return m_rebuzzMachine;
        }

        void MachineWrapper::SetEditorPattern(IPattern^ pattern)
        {
            //Make sure we're initialised
            Init();

            //Store the machine ref (if not already stored)
            CMachine* patMach = m_machineMgr->GetOrStoreMachine(pattern->Machine);

            //Store pattern ref (if not already stored)
            CPattern* pat = m_patternMgr->GetOrStorePattern(pattern);

            //Get ex interface and callback data
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(m_internalCallbackData);

            //Set target machine if needed
            bool haveSetTargetMach = false;
            if ((m_targetEditorMachine != patMach) || (cbdata->editorTargetMachine != patMach))
            {
                cbdata->editorTargetMachine = patMach;
                m_targetEditorMachine = patMach;

                //Call the patten added callback again, this time with the target machine
                //set up correctly
                int64_t patid = Utils::ObjectToInt64(pattern);
                OnPatternAdded(patid, pattern, pat, PatternEventFlags_None, cbdata);

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
                    if (m_hwndEditor != NULL)
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

        void MachineWrapper::SendMessageToKeyboardWindow(UINT msg, WPARAM wparam, LPARAM lparam)
        {
            //If a keyboard window callback has been specified, then call it
            //to get the window that we should be fowarding the windows message to
            HWND hwndSendMsg = m_hwndEditor;
            if (m_kbFocusWndcallback != NULL)
            {
                HWND hwnd = (HWND)m_kbFocusWndcallback(m_externalCallbackParam);
                if (hwnd != NULL)
                    hwndSendMsg = hwnd;
            }

            //Set focus on the keyboard focus window
            SetForegroundWindow(m_hwndEditor);
            SetFocus(hwndSendMsg);
            SetActiveWindow(hwndSendMsg);

            //Send the windows message
            SendMessage(hwndSendMsg, msg, wparam, lparam);
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


        UserControl^ MachineWrapper::PatternEditorControl()
        {
            if (m_control == nullptr)
            {
                //Create MFC wrapper
                AttachCallback^ onAttach = gcnew AttachCallback(RebuzzWindowAttachCallback);
                DetatchCallback^ onDetatch = gcnew DetatchCallback(RebuzzWindowDettachCallback);
                SizeChangedCallback^ onSzChanged = gcnew SizeChangedCallback(RebuzzWindowSizeCallback);
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
                if (m_editorCreateCallback != NULL)
                    m_editorCreateCallback(m_externalCallbackParam);

                //Set focus on the keyboard focus window
                SetForegroundWindow(m_hwndEditor);
                SetFocus(m_hwndEditor);
                SetActiveWindow(m_hwndEditor);
            }

            return m_control;
        }

        void MachineWrapper::OverridePatternEditorWindowsMessage(UINT msg, IntPtr callback, void* param)
        {
            (*m_editorMessageMap)[msg] = reinterpret_cast<OnWindowsMessage>(callback.ToPointer());
            (*m_editorMessageParamMap)[msg] = param;
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
        void* MachineWrapper::GetCPattern(IPattern^ p)
        {
            return m_patternMgr->GetOrStorePattern(p);
        }

        IPattern^ MachineWrapper::GetReBuzzPattern(void* pat)
        {
            return m_patternMgr->GetReBuzzPattern(reinterpret_cast<CPattern*>(pat));
        }

        IMachine^ MachineWrapper::GetReBuzzMachine(void* mach)
        {
            return m_machineMgr->GetReBuzzMachine(reinterpret_cast<CMachine*>(mach));
        }

        CMachineData* MachineWrapper::GetBuzzMachineData(void* mach)
        {
            return m_machineMgr->GetBuzzMachineData(reinterpret_cast<CMachine*>(mach));
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

        void* MachineWrapper::GetCMachine(IMachine^ m)
        {
            return m_machineMgr->GetOrStoreMachine(m);
        }

        CMachine* MachineWrapper::GetCMachineByName(const char* name)
        {
            return m_machineMgr->GetCMachineByName(name);
        }

        CWaveLevel* MachineWrapper::GetWaveLevel(IWaveLayer^ wavelayer)
        {
            if (wavelayer == nullptr)
                return NULL;

            uint64_t id = wavelayer->GetHashCode();
            bool created = false;
            CWaveLevel* ret = m_waveLevelsMap->GetOrStoreReBuzzTypeById(id, wavelayer, &created);

            //ReBuzz does not notify us of changes to IWaveLayer, so we need to manually update the return data
            updateWaveLevel(ret, wavelayer);
            return ret;
        }

        IWaveLayer^ MachineWrapper::GetReBuzzWaveLevel(CWaveLevel* wavelevel)
        {
            if (wavelevel == NULL)
                return nullptr;

            return m_waveLevelsMap->GetReBuzzTypeByBuzzType(wavelevel);
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


        void MachineWrapper::ControlChange(IMachine^ machine, int group, int track, int param, int value)
        {
            //Get machine
            CMachine* mach = m_machineMgr->GetOrStoreMachine(machine);

            //Not sure how to do this one?
            //
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            //exInterface->RecordControlChange(mach, group, track, param, value);
        }

        void MachineWrapper::SetModifiedFlag()
        {
            Global::Buzz->SetModifiedFlag();
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

        void MachineWrapper::MidiNote(int channel, int value, int velocity)
        {
            m_machine->MidiNote(channel, value, velocity);
        }

        void MachineWrapper::MidiControlChange(int ctrl, int channel, int value)
        {
            CMachineInterfaceEx* exInterface = m_callbackWrapper->GetExInterface();
            exInterface->MidiControlChange(ctrl, channel, value);
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

        void MachineWrapper::Activate()
        {
            if (m_hwndEditor != NULL)
            {
                SetForegroundWindow(m_hwndEditor);
                SetActiveWindow(m_hwndEditor);
                SetFocus(m_hwndEditor);
            }
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

        void* MachineWrapper::CreatePattern(IMachine^ machine, const char* name, int len)
        {
            //Create pattern in rebuzz
            String^ patname = Utils::stdStringToCLRString(name);
            machine->CreatePattern(patname, len);

            //Get the CPattern *
            CPattern* cpat = m_patternMgr->GetPatternByName(machine, name);
            return cpat;
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
                                MachineWrapperCallbackData* cbdata = reinterpret_cast<MachineWrapperCallbackData*>(m_internalCallbackData);
                                if ((m_onPlayPatternCallback == NULL) ||
                                    (m_onPlayPatternCallback(patMach, cpat, patdata->name.c_str(), cbdata->cBParam)))
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
    }
}