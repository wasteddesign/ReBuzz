

//For some calls, we need access to the raw mi class members.
//In order to do that, because mi is self contained within a single source file, we have to 
//include PatternXp.cpp
//This compiles PatternXp.cpp with our source file, allowing access to the public members of mi,
//without needing to change PatternXp.cpp
#include "PatternXPRE/patternxp/PatternXp.cpp"


#include <RefClassWrapper.h>
#include <NativeMachineReader.h>
#include <NativeMachineWriter.h>
#include <Utils.h>
#include <WindowUtils.h>


#include "ReBuzzPatternXP.h"
#include <memory>
#include "stdafx.h"

enum  ContextMenuId
{
    CreatePattern,
    DeletePattern,
    PatternProperties
};


using namespace ReBuzz::NativeMachineFramework;

using BuzzGUI::Interfaces::PatternEvent;
using BuzzGUI::Interfaces::IParameterGroup;
using BuzzGUI::Common::Global;

using System::IntPtr;
using System::Collections::Generic::IEnumerable;
using System::Collections::Generic::List;

using ReBuzz::NativeMachineFramework::SampleListControl;

static void PositionSampleListControl(CMachineInterface * machInterface, SampleListControl^ sampleListCtrl)
{
    const mi* pmi = reinterpret_cast<const mi*>(machInterface);
    if (pmi->patEd != NULL)
    {
        RECT rt1 = { 0 }, rt2 = { 0 };

        CButton* pc = (CButton*)pmi->patEd->dlgBar.GetDlgItem(IDC_FOLLOW_PLAY_POS);
        if (pc != NULL)
        {
            WindowUtils::GetWindowRectToParent(pc->m_hWnd, pmi->patEd->dlgBar.m_hWnd, &rt1);
        }

        pc = (CButton*)pmi->patEd->dlgBar.GetDlgItem(IDC_FOLLOW_PLAYING_PATTERN);
        if (pc != NULL)
        {
            WindowUtils::GetWindowRectToParent(pc->m_hWnd, pmi->patEd->dlgBar.m_hWnd, &rt2);
        }

        int preferredWidth = sampleListCtrl->GetPreferredWidth();

        RECT crect = { 0 };
        GetClientRect(pmi->patEd->dlgBar.m_hWnd, &crect);
        int listPosLeft = (rt2.right > rt1.right) ? rt2.right : rt1.right;
        int listPosTop = (rt2.top > rt1.top) ? rt2.top : rt1.top;
        int listPosBottom = (rt2.bottom > rt1.bottom) ? rt2.bottom : rt1.bottom;

        sampleListCtrl->GetControl()->Left = listPosLeft;
        sampleListCtrl->GetControl()->Top = listPosTop;
        sampleListCtrl->GetControl()->Width = preferredWidth;
        sampleListCtrl->GetControl()->Height = listPosBottom - listPosTop;
        sampleListCtrl->GetControl()->Visible = true;
    }
}



static LRESULT OnMouseRightClick(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam, void* callbackParam, bool* pbBlock)
{
    RefClassWrapper<ReBuzzPatternXpMachine> * reBuzzPatternXp = reinterpret_cast<RefClassWrapper<ReBuzzPatternXpMachine> *>(callbackParam);
   
    reBuzzPatternXp->GetRef()->ShowContextMenu();
    
    //Prevent context menu message being processed further
    *pbBlock = true;
    return 0;
}

static LRESULT OnSizeChanged(HWND hwnd, UINT msg, WPARAM wparam, LPARAM lparam, void* callbackParam, bool* pbBlock)
{
    RefClassWrapper<ReBuzzPatternXpMachine>* reBuzzPatternXp = reinterpret_cast<RefClassWrapper<ReBuzzPatternXpMachine> *>(callbackParam);
    PositionSampleListControl(reBuzzPatternXp->GetRef()->GetInterface(),
                              reBuzzPatternXp->GetRef()->GetSampleListControl());

    *pbBlock = false;
    return 0;
}

//=====================================================
ReBuzzPatternXpMachine::ReBuzzPatternXpMachine(IBuzzMachineHost^ host) : m_host(host),
                                                                         m_dummyParam(false),
                                                                         m_initialised(false),
                                                                         m_patternEditor(nullptr),
                                                                         m_contextmenu(nullptr),
                                                                         m_sampleListControl(nullptr),
                                                                         m_onCreatePatternMenuCallback(nullptr),
                                                                         m_onDeletePatternMenuCallback(nullptr),
                                                                         m_PatternPropertiesMenuCallback(nullptr)
{
    m_interface = CreateMachine();
    
    
    mi* pmi = reinterpret_cast<mi*>(m_interface);
    
    m_self = new RefClassWrapper< ReBuzzPatternXpMachine>(this);

    //Create machine wrapper
    m_machineWrapper = gcnew MachineWrapper(m_interface, host, (IBuzzMachine^)this);

    //Add callbacks
    m_onPatternEditorCreatedCallback = gcnew MachineWrapper::OnPatternEditorCreatedDelegate(this, &ReBuzzPatternXpMachine::OnEditorCreated);
    m_machineWrapper->AddPatternEditorCreaetdCallback(m_onPatternEditorCreatedCallback);

    m_onKbFocusCallback = gcnew MachineWrapper::KeyboardFocusWindowHandleDelegate(this, &ReBuzzPatternXpMachine::OnKeyboardFocusWindow);
    m_machineWrapper->AddPatternEditorKeyboardFocusCallback(m_onKbFocusCallback);

    m_onEditorRedrawCallback = gcnew MachineWrapper::OnPatternEditorRedrawDelegate(this, &ReBuzzPatternXpMachine::OnRedrawEditorWindow);
    m_machineWrapper->AddPatternEditorRedrawCallback(m_onEditorRedrawCallback);

    m_onNewPatternCallback = gcnew MachineWrapper::OnNewPatternDelegate(this, &ReBuzzPatternXpMachine::OnPatternCreated);
    m_machineWrapper->AddNewPatternCallback(m_onNewPatternCallback);
}

ReBuzzPatternXpMachine::~ReBuzzPatternXpMachine()
{   
    Release();

    delete m_contextmenu;
    delete m_interface;
}

void ReBuzzPatternXpMachine::Release()
{
    if (m_patternEditor != nullptr)
    {
        delete m_patternEditor;
        m_patternEditor = nullptr;
    }

    if (m_machineWrapper != nullptr)
    {
        m_machineWrapper->Release();
        delete m_machineWrapper;
        m_machineWrapper = nullptr;
    }

    if (m_onPatternEditorCreatedCallback != nullptr)
    {
        delete m_onPatternEditorCreatedCallback;
        m_onPatternEditorCreatedCallback = nullptr;
    }

    if (m_onEditorRedrawCallback != nullptr)
    {
        delete m_onEditorRedrawCallback;
        m_onEditorRedrawCallback = nullptr;
    }

    if (m_onKbFocusCallback != nullptr)
    {
        delete m_onKbFocusCallback;
        m_onKbFocusCallback = nullptr;
    }

    if (m_onNewPatternCallback != nullptr)
    {
        delete m_onNewPatternCallback;
        m_onNewPatternCallback = nullptr;
    }

    if (m_sampleListControl != nullptr)
    {
        m_sampleListControl->Release();
        delete m_sampleListControl;
        m_sampleListControl = nullptr;
    }

    if (m_onCreatePatternMenuCallback != nullptr)
    {
        delete m_onCreatePatternMenuCallback;
        m_onCreatePatternMenuCallback = nullptr;
    }

    if (m_onDeletePatternMenuCallback != nullptr)
    {
        delete m_onDeletePatternMenuCallback;
        m_onDeletePatternMenuCallback = nullptr;
    }

    if (m_PatternPropertiesMenuCallback != nullptr)
    {
        delete m_PatternPropertiesMenuCallback;
        m_PatternPropertiesMenuCallback = nullptr;
    }

    if (m_self != NULL)
    {
        m_self->Free();
        delete m_self;
        m_self = NULL;
    }

    m_initialised = false;
}



void ReBuzzPatternXpMachine::OnEditorCreated()
{
    //Editor has been created, so we can allow Tick() to run as normal again
    m_busy = false;
}

IntPtr ReBuzzPatternXpMachine::OnKeyboardFocusWindow()
{
    mi* pmi = reinterpret_cast<mi*>(m_interface);
    return IntPtr(pmi->patEd->pe.GetSafeHwnd());
}


void ReBuzzPatternXpMachine::OnRedrawEditorWindow()
{
    mi* pmi = reinterpret_cast<mi*>(m_interface);
    pmi->patEd->RedrawWindow();
}

void ReBuzzPatternXpMachine::OnPatternCreated(IMachine^ rebuzzMachine, void * buzzMachine,
                                            IPattern^ rebuzzPattern, void * buzzPattern, String^ patternName)
{
    mi* pmi = reinterpret_cast<mi*>(m_interface);

    if (pmi != NULL)
    {
        const char* chars = (const char*)(Marshal::StringToHGlobalAnsi(patternName)).ToPointer();
        std::string patname = chars;
        Marshal::FreeHGlobal(IntPtr((void*)chars));

        //Find the loaded pattern by name - 
        auto& foundLoadedPat = pmi->loadedPatterns.find(patname.c_str());
        if (foundLoadedPat != pmi->loadedPatterns.end())
        {
            //Set the CPattern value
            if ((*foundLoadedPat).second->pPattern == NULL)
                (*foundLoadedPat).second->pPattern = reinterpret_cast<CPattern *>( buzzPattern);
        }

        //Check pattern editor current pattern as well
        if ((pmi->patEd->pPattern != NULL) && (pmi->patEd->pPattern->name == patname.c_str()))
        {
            //Pattern exists in the editor class
            if (pmi->patEd->pPattern->pPattern == NULL)
                pmi->patEd->pPattern->pPattern = reinterpret_cast<CPattern*>(buzzPattern);
        }

        //find the pattern by name
        auto& foundpat = pmi->patterns.find(reinterpret_cast<CPattern*>(buzzPattern));
        if (foundpat != pmi->patterns.end())
        {
            //Make sure the found pattern has a CPattern assigned to it
            if ((*foundpat).second->pPattern == NULL)
                (*foundpat).second->pPattern = reinterpret_cast<CPattern*>(buzzPattern);
        }
    }
}

void ReBuzzPatternXpMachine::OnMenuItem_CreatePattern(int menuid)
{
    mi* pmi = reinterpret_cast<mi*>(m_interface);

    //Get the current machine from PatternXP, and then convert that into a ReBuzz machine
    IMachine^ rebuzzMach = m_machineWrapper ->GetReBuzzMachine(pmi->targetMachine);
    if (rebuzzMach != nullptr)
    {
        //For the pattern name, count the current patters and use that as a 2 digit name
        int patcount = rebuzzMach->Patterns->Count;
        char namebuf[64] = { 0 };
        sprintf_s(namebuf, "%02d", patcount);
        String^ patName = gcnew String(namebuf);

        //Create new pattern. 
        rebuzzMach->CreatePattern(patName, 16);
    }
}

void ReBuzzPatternXpMachine::OnMenuItem_DeletePattern(int menuid)
{
    mi* pmi = reinterpret_cast<mi*>(m_interface);

    CMachinePattern* machpat = pmi->patEd->pPattern;
    if (machpat != NULL)
    {
        CPattern* pat = machpat->pPattern;
        if (pat != NULL)
        {
            IPattern^ rebuzzPat = m_machineWrapper->GetReBuzzPattern(pat);
            if (rebuzzPat != nullptr)
            {
                rebuzzPat->Machine->DeletePattern(rebuzzPat);
            }
        }
    }
}

void ReBuzzPatternXpMachine::OnMenuItem_PatternProperties(int id)
{
    mi* pmi = reinterpret_cast<mi*>(m_interface);
    pmi->patEd->OnColumns();
}

CMachineInterface* ReBuzzPatternXpMachine::GetInterface()
{
    return m_interface;
}

SampleListControl^ ReBuzzPatternXpMachine::GetSampleListControl()
{
    return m_sampleListControl;
}


void ReBuzzPatternXpMachine::Work()
{
    //Make sure we're initialised or busy before working...
    if(m_initialised && !m_busy &&  (m_patternEditor != nullptr) && (m_interface != NULL))
    { 
        //Tick the machine / native buzz machine wrapper
        m_machineWrapper->Tick();

        //The parameters are not used by 'Work', so just put anything in....
        m_interface->Work(NULL, 0, 0);
    }
}

void ReBuzzPatternXpMachine::ImportFinished(IDictionary<String^, String^>^ machineNameMap)
{
    if (!m_initialised)
    {
        //Initialise the native machine wrapper
        m_machineWrapper->Init();
        m_initialised = true;
    }
}



UserControl^ ReBuzzPatternXpMachine::PatternEditorControl()
{
    //Make sure we're initialised
    if (!m_initialised)
    {
        m_machineWrapper->Init();
        m_initialised = true;
    }

    if (m_patternEditor != nullptr)
    {
        return m_patternEditor;
    }

    //std::lock_guard<std::mutex> lg(callbackData->datalock);

    //Set 'busy' to avoid conflicts with other actions
    m_busy = true;

    //Construct control
    m_patternEditor = m_machineWrapper->PatternEditorControl();

    //Create and build right click menu
    m_contextmenu = gcnew ContextMenu();
    m_onCreatePatternMenuCallback = gcnew ContextMenu::OnMenuItemClickDelegate(this, &ReBuzzPatternXpMachine::OnMenuItem_CreatePattern);
    m_contextmenu->AddMenuItem(ContextMenuId::CreatePattern, "Create Pattern", m_onCreatePatternMenuCallback);

    m_onDeletePatternMenuCallback = gcnew ContextMenu::OnMenuItemClickDelegate(this, &ReBuzzPatternXpMachine::OnMenuItem_DeletePattern);
    m_contextmenu->AddMenuItem(ContextMenuId::DeletePattern, "Delete Pattern", m_onDeletePatternMenuCallback);

    m_PatternPropertiesMenuCallback = gcnew ContextMenu::OnMenuItemClickDelegate(this, &ReBuzzPatternXpMachine::OnMenuItem_PatternProperties);
    m_contextmenu->AddMenuItem(ContextMenuId::PatternProperties, "Pattern Properties", m_PatternPropertiesMenuCallback);
    
    //Override the mouse right-click
    m_machineWrapper->OverridePatternEditorWindowsMessage(WM_CONTEXTMENU, IntPtr(OnMouseRightClick), m_self);

    //Add sample list control
    SampleListControl^ smpcontrol = m_machineWrapper->CreateSampleListControl();
    m_sampleListControl = smpcontrol; 
    
    //Set the font to match the rest of pattern editor
    const mi* pmi = reinterpret_cast<const mi*>(m_interface);
    smpcontrol->SetFont(pmi->patEd->dlgBar.GetFont()->m_hObject);
    
    //Add control to the pattern editor    
    smpcontrol->SetNewParent( pmi->patEd->dlgBar.m_hWnd);
    PositionSampleListControl(m_interface, smpcontrol);

    //Allow sample list to be repositioned if window size changes
    m_machineWrapper->OverridePatternEditorWindowsMessage(WM_SIZE, IntPtr(OnSizeChanged), m_self);

    //Return pattern editor
    return m_patternEditor;
}

void ReBuzzPatternXpMachine::ShowContextMenu()
{
    if (m_contextmenu != nullptr)
        m_contextmenu->ShowAtCursor();
}

void ReBuzzPatternXpMachine::SetEditorPattern(IPattern^ pattern)
{
    m_machineWrapper->SetEditorPattern(pattern);
}

void ReBuzzPatternXpMachine::RecordControlChange(IParameter^ parameter, int track, int value)
{
    m_machineWrapper->RecordControlChange(parameter, track, value);
}

void ReBuzzPatternXpMachine::SetTargetMachine(IMachine^ machine)
{
    m_machineWrapper->SetTargetMachine(machine);
}

String^ ReBuzzPatternXpMachine::GetEditorMachine()
{
    return gcnew String("PatternXP");
}

void ReBuzzPatternXpMachine::SetPatternEditorMachine(IMachineDLL^ editorMachine)
{
    //No idea what to do here
}

int ReBuzzPatternXpMachine::GetTicksPerBeatDelegate(IPattern^ pattern, int playPosition)
{
    //Get CPattern
    CPattern* cpat = (CPattern *)m_machineWrapper->GetCPattern(pattern);
    if (cpat == NULL)
        return BUZZ_TICKS_PER_BEAT;

    //We don't have direct access to Pattern XP's data, and there is no way to query 
    //the 'rowsPerBeat' val evia the existing CMachineInterface * or CMachineInterfaceEx * 
    //interfaces.
    //So this is why PatternXp.cpp is included at the top of this source file - 
    //it allows access to the public members of the mi class, where the CPattern * to
    //PatternXP patter map is located.  From that map, we can then turn a CPattern * value
    //to a PatternXP pattern and query the 'rowsPerBeat' value.
    const mi* pmi = reinterpret_cast<const mi*>(m_interface);
    const auto& foundPattern = pmi->patterns.find(cpat);
    if (foundPattern == pmi->patterns.end())
        return BUZZ_TICKS_PER_BEAT;

    return (*foundPattern).second->rowsPerBeat;
}


void ReBuzzPatternXpMachine::SetModifiedFlag()
{
    m_machineWrapper->SetModifiedFlag();
}

bool ReBuzzPatternXpMachine::CanExecuteCommand(BuzzCommand cmd) 
{
    return m_machineWrapper->CanExecuteCommand(cmd);
}

void ReBuzzPatternXpMachine::ExecuteCommand(BuzzCommand cmd)
{
    return m_machineWrapper->ExecuteCommand(cmd);
}

void ReBuzzPatternXpMachine::MidiNote(int channel, int value, int velocity)
{
    m_machineWrapper->MidiNote(channel, value, velocity);
}

void ReBuzzPatternXpMachine::MidiControlChange(int ctrl, int channel, int value)
{
    m_machineWrapper->MidiControlChange(ctrl, channel, value);
}

cli::array<byte>^ ReBuzzPatternXpMachine::GetPatternEditorData()
{
    return m_machineWrapper->Save();
}

void ReBuzzPatternXpMachine::SetPatternEditorData(cli::array<byte>^ data)
{
    //Make sure native machine wrapper is initialised
    m_machineWrapper->Init();

    //Native buzz machines don't directly support 'loading' (only via Init - but that does other stuff)
    //So we'll copy the load implentation from PatterXp here
    mi* pmi = reinterpret_cast<mi*>(m_interface);
    pmi->loadedPatterns.clear();
    
    if ((data == nullptr) || (data->Length == 0))
        return;

    pin_ptr<byte> dataptr = &data[0];
    ReBuzz::NativeMachineFramework::NativeMachineReader input(dataptr, data->Length);
    CMachineDataInput* inputReader = &input;

    byte version;
    inputReader->Read(version);
    
    //This could be Modern Pattern Editor data, or it could be pattern XP data
    if (version == 255)
    {
        //Modern Pattern Editor data
        inputReader->Read(version);
        if (version != 1)
        {
            AfxMessageBox("Modern Pattern Editor data is unknown version ");
            return;
        }

        int dataSize;
        inputReader->Read(dataSize);

        int numpat;
        inputReader->Read(numpat);
        for (int i = 0; i < numpat; i++)
        {
            CString patname = inputReader->ReadString();
            shared_ptr<CMachinePattern> p(new CMachinePattern());
            p->name = patname;

            //Read pattern info
            int mpeBeats, rowsPerBeat, columnCount;
            inputReader->Read(mpeBeats);
            inputReader->Read(rowsPerBeat);
            inputReader->Read(columnCount);

            p->SetRowsPerBeat(rowsPerBeat);
            p->SetLength(mpeBeats * rowsPerBeat, pmi->pCB);

            p->columns.clear();
            for (int c = 0; c < columnCount; ++c)
            {
                std::shared_ptr<CColumn> newColumn = std::make_shared<CColumn>();

                //Modern pattern editor writes the event rows as :
                //  eventTime * PatternEvent.TimeBase * 4 / pat.RowsPerBeat;
                //
                // where PatternEvent.TimeBase = 240
                //       pat.RowsPerBeat = rowsPerBeat
                //
                //So we need to do the reverse to convert to PatternXP event times
                //
                //The members are not directly accessible to us, so we need to 
                //read the column data, perform the conversion, and write the converted data 
                //to a temporary stream, and get the PatternXP column to read from the converted 
                //data
                NativeMachineWriter tempConvertedColumnData;

                //Machine name
                CString machineName = inputReader->ReadString();
                tempConvertedColumnData.Write(machineName.GetBuffer(), machineName.GetLength());
                byte zero = 0;
                tempConvertedColumnData.Write(&zero, 1);

                //Param index and track
                int paramIndex,  paramTrack;
                inputReader->Read(paramIndex);
                inputReader->Read(paramTrack);
                tempConvertedColumnData.Write(&paramIndex, 4);
                tempConvertedColumnData.Write(&paramTrack, 4);

                //Graphical flag
                byte graphical;
                inputReader->Read(graphical);
                tempConvertedColumnData.Write(&graphical, 1);
                
                //Event count
                int eventCount;
                inputReader->Read(eventCount);
                tempConvertedColumnData.Write(&eventCount, 4);

                //Events
                for (int e = 0; e < eventCount; ++e)
                {
                    //Time and value
                    int time, value;
                    inputReader->Read(time);
                    inputReader->Read(value);

                    //Convert the time
                    time *= rowsPerBeat;
                    time /= 960;

                    tempConvertedColumnData.Write(&time, 4);
                    tempConvertedColumnData.Write(&value, 4);
                }

                //Read the converted column data
                NativeMachineReader tempConvertedColDataReader(tempConvertedColumnData.dataPtr(), tempConvertedColumnData.size());
                newColumn->Read(&tempConvertedColDataReader, 3);

                //Read 'beats' (no idea what to do with this)
                for (int b = 0; b < mpeBeats; ++b)
                {
                    int index;
                    inputReader->Read(index);
                }

                p->columns.push_back(newColumn);
            }

            //Cannot do this here - Modern Pattern Editor still has event hooks into ReBuzz
            //and has not yet been fully switched over to Pattern XP, so MPE will get triggered if we create a pattern here.
            /*IMachine^ thisMachine = m_host->Machine;
            p->pPattern = reinterpret_cast<CPattern*>(m_machineWrapper->GetCPatternByName(thisMachine, patname));
            if (p->pPattern == NULL)
            {
                //Create the pattern in ReBuzz
                p->pPattern = reinterpret_cast<CPattern *>(m_machineWrapper->CreatePattern(thisMachine, patname, mpeBeats * rowsPerBeat ));
            }
            */

            pmi->loadedPatterns[patname] = p;
        }
    }
    else if (version >= 1 && version <= PATTERNXP_DATA_VERSION)
    {
        //Pattern XP data
        int numpat;
        inputReader->Read(numpat);

        for (int i = 0; i < numpat; i++)
        {
            CString name = inputReader->ReadString();
            shared_ptr<CMachinePattern> p(new CMachinePattern());
            p->Read(inputReader, version);
            pmi->loadedPatterns[name] = p;
        }
    }
    else
    {
        //Not known
        AfxMessageBox("invalid data");
        return;
    }
}

cli::array<int>^ ReBuzzPatternXpMachine::GetPatternEditorMachineMIDIEvents(IPattern^ pattern)
{
    return m_machineWrapper->GetPatternEditorMachineMIDIEvents(pattern);
}

void ReBuzzPatternXpMachine::SetPatternEditorMachineMIDIEvents(IPattern^ pattern, cli::array<int>^ data)
{
    m_machineWrapper->SetPatternEditorMachineMIDIEvents(pattern, data);
}

void ReBuzzPatternXpMachine::Activate()
{
    m_machineWrapper->ActivatePatternEditor();
}


void ReBuzzPatternXpMachine::CreatePatternCopy(IPattern^ pnew, IPattern^ p)
{
    m_machineWrapper->CreatePatternCopy(pnew, p);
}