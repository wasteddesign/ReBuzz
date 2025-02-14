#include <sstream>
#include "MachineCallbackWrapper.h"

//<Windows.h> must be after MachineCallbackWrapper.h, to avoide <Windows.h> redefining some of the 
//callback methods by suffixing a W
#include <Windows.h>



#undef GetProfileInt
#undef GetProfileString
#undef WriteProfileString

#include "MachineEventWrapper.h"
#include "MachineWrapper.h"
#include "Utils.h"



using BuzzGUI::Common::Global;
using BuzzGUI::Interfaces::IIndex;
using BuzzGUI::Interfaces::IWave;
using BuzzGUI::Interfaces::IWaveLayer;
using BuzzGUI::Interfaces::ISequence;
using BuzzGUI::Interfaces::IParameterGroup;

using Buzz::MachineInterface::IBuzzMachineHost;

using Buzz::MachineInterface::SubTickInfo;

using System::Xml::Linq::XElement;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
      

        MachineCallbackWrapper::MachineCallbackWrapper(MachineWrapper^ mw, 
                                                       MachineManager^ mm,
                                                        IBuzzMachine^  netmach,
                                                        IBuzzMachineHost^ host,
                                                        CMachineInterface* iface, 
                                                        CMachine * machine,
                                                        CMasterInfo * masterinfo) :  
                                                                                m_netmcahine(netmach),
                                                                                m_machinehost(host),
                                                                                m_machineWrapper(mw),
                                                                                m_machineMgr(mm),
                                                                                m_thisMachine(machine),
                                                                                m_exInterface(NULL),
                                                                                m_interface(iface),
                                                                                m_masterinfo(masterinfo),
                                                                                m_setPatternEditorPattern(NULL),
                                                                                m_setPatternEditorMachine(NULL)
        {
            //Update master info
            m_machineWrapper.GetRef()->UpdateMasterInfo();
        }

        MachineCallbackWrapper::~MachineCallbackWrapper()
        {
            Release();
            m_netmcahine.Free();
        }

        void MachineCallbackWrapper::Release()
        {
            if (!m_addedMachineEventHandler.isNull())
            {
                MachineEventWrapper^ evtWrapper = m_addedMachineEventHandler.GetRef();
                delete evtWrapper;
                m_addedMachineEventHandler.Free();
            }

            if (!m_deleteMachineEventHandler.isNull())
            {
                MachineEventWrapper^ evtWrapper = m_deleteMachineEventHandler.GetRef();
                delete evtWrapper;
                m_deleteMachineEventHandler.Free();
            }
        }

        void MachineCallbackWrapper::OnMachineAdded(IMachine^ mach)
        {
            if (!m_addedMachineEventHandler.isNull())
            {
                m_addedMachineEventHandler.GetRef()->OnEvent(mach);
            }
        }
        
        void MachineCallbackWrapper::OnMachineRemoved(IMachine^ mach)
        {
            if (!m_deleteMachineEventHandler.isNull())
            {
                m_deleteMachineEventHandler.GetRef()->OnEvent(mach);
            }
        }


        int MachineCallbackWrapper::GetHostVersion()
        {
            return 3;
        }

        CMachineInterfaceEx* MachineCallbackWrapper::GetExInterface() const
        {
            return m_exInterface;
        }

        CMachine* MachineCallbackWrapper::GetThisMachine()
        {
            return m_thisMachine;
        }

        void MachineCallbackWrapper::SetMachineInterfaceEx(CMachineInterfaceEx* pex)
        {
            m_exInterface = pex;

            //Anything that we need to do that was delayed becuase the exInterface was not set in time?
            if ((m_setPatternEditorMachine != NULL) && (m_setPatternEditorPattern != NULL))
            {
                pex->SetEditorPattern(m_setPatternEditorPattern);
                pex->SetPatternTargetMachine(m_setPatternEditorPattern, m_setPatternEditorMachine);
            }

            m_setPatternEditorMachine = NULL;
            m_setPatternEditorPattern = NULL;
        }

       
        void MachineCallbackWrapper::SetEventHandler(CMachine* pmac, BEventType et, EVENT_HANDLER_PTR p, void* param)
        {
            //Make sure this is for us
            if (pmac != (CMachine*)m_thisMachine)
                return;


            switch (et)
            {
                case gAddMachine:
                    //Set up event handler
                    //The MachineManager will call MachineWrapper, which will call our OnMachineAdded method, 
                    //which will call the OnEvent of this event handler class
                    if (m_addedMachineEventHandler.isNull())
                    {
                        m_addedMachineEventHandler = gcnew MachineEventWrapper(m_machineMgr.GetRef(), m_machinehost.GetRef()->Machine, m_interface);
                    }

                    //Add callback and param to event handler
                    m_addedMachineEventHandler.GetRef()->AddEvent(p, param);
                    break;
                case gDeleteMachine:
                    //Set up event handler
                    //The MachineManager will call MachineWrapper, which will call our OnMachineRemoved method, 
                    //which will call the OnEvent of this event handler class
                    if (m_deleteMachineEventHandler.isNull())
                    {
                        m_deleteMachineEventHandler = gcnew MachineEventWrapper(m_machineMgr.GetRef(), m_machinehost.GetRef()->Machine, m_interface);
                    }

                    //Add callback and param to event handler
                    m_deleteMachineEventHandler.GetRef()->AddEvent(p, param);
                    break;

                case gUndeleteMachine:
                    //TODO
                    break;
                case gWaveChanged:
                    //TODO
                    break;
                case gRenameMachine:
                    //TODO
                    break;
            }
        }

        void MachineCallbackWrapper::SetModifiedFlag()
        {
            Global::Buzz->SetModifiedFlag();
        }

        CSubTickInfo const* MachineCallbackWrapper::GetSubTickInfo()
        {
            //Update master info
            m_machineWrapper.GetRef()->UpdateMasterInfo();

            //Get subtick info from host
            SubTickInfo^ subtickInfo = m_machinehost.GetRef()->SubTickInfo;

            //Translate into native speak, stored in our class
            m_subtickInfo.CurrentSubTick = subtickInfo->CurrentSubTick;
            m_subtickInfo.PosInSubTick = subtickInfo->PosInSubTick;
            m_subtickInfo.SamplesPerSubTick = subtickInfo->SamplesPerSubTick;
            m_subtickInfo.SubTicksPerTick = subtickInfo->SubTicksPerTick;
            
            //return the pointer inside our class
            return &m_subtickInfo;
        }

        CMachineInfo const* MachineCallbackWrapper::GetMachineInfo(CMachine* pmac)
        {  
            //Get the emulation data. This contains the buzz machine info in native form
            CMachineData* machdata = m_machineWrapper.GetRef()->GetBuzzMachineData(pmac);
            if (machdata == NULL)
                return NULL;
            
            //Info is already 
            return &machdata->m_info;
        }

        char const* MachineCallbackWrapper::GetMachineName(CMachine* pmac)
        {
            //Get the emulation data. This contains the buzz machine info in native form
            CMachineData* machdata = m_machineWrapper.GetRef()->GetBuzzMachineData(pmac);
            if (machdata == NULL)
                return NULL;

            return machdata->name.c_str();
        }

        void MachineCallbackWrapper::GetMachineNames(CMachineDataOutput* pout)
        {
            //Ask ReBuzz for list of machines
            for each (IMachine^ mach in Global::Buzz->Song->Machines)
            {
                //Make sure machine manager has this machine, and get the machine data.
                //The machine data will then contain the c++ version of the name
                void * pbuzzmach = m_machineWrapper.GetRef()->GetCMachine(mach);
                CMachineData* machdata = m_machineWrapper.GetRef()->GetBuzzMachineData(pbuzzmach);
                if (machdata != NULL)
                {
                    pout->Write(machdata->name.c_str());
                }
            } 
        }


        CMachine* MachineCallbackWrapper::GetMachine(char const* name)
        {
            return m_machineWrapper.GetRef()->GetCMachineByName(name);
        }

        dword MachineCallbackWrapper::GetThemeColor(char const* name)
        {
            //Convert name
            String^ clrname = Utils::stdStringToCLRString(name);

            //Get colour
            System::Drawing::Color^ colour =  Global::Buzz->GetThemeColour(clrname);

            //Convert the colour
            // Native machines (well, PatternXP) set alpha to 0xFF, which won't work since it is transparent.
            // So set alpha to be always zero here.
            dword ret = 0; //Alpha
            ret = (ret << 8) | ((colour->B) & 0xFF);
            ret = (ret << 8) | ((colour->G) & 0xFF);
            ret = (ret << 8) | ((colour->R) & 0xFF);
            return ret;
        }


        int MachineCallbackWrapper::GetNumTracks(CMachine* pmac) 
        {
            //Get the machine
            IMachine^ rebuzzMach =  m_machineWrapper.GetRef()->GetReBuzzMachine(pmac);
            if (rebuzzMach == nullptr)
                return 0;

            //Ask the machine for the number of tracks
            return rebuzzMach->TrackCount;
        }

        char const* MachineCallbackWrapper::GetPatternName(CPattern* ppat)
        {
            //Get the pattern data
            CPatternData* patdata = m_machineWrapper.GetRef()->GetBuzzPatternData(ppat);
            if (patdata == NULL)
                return NULL;

            return patdata->name.c_str();
        }

        CPattern* MachineCallbackWrapper::GetPatternByName(CMachine* pmac, const char* name)
        {
            //Get the rebuzz machine
            IMachine^ rebuzzMach = m_machineWrapper.GetRef()->GetReBuzzMachine(pmac);
            if (rebuzzMach == nullptr)
                return NULL;

            //Query pattern manager
            return reinterpret_cast<CPattern *>( m_machineWrapper.GetRef()->GetCPatternByName(rebuzzMach, name));
        }

        int MachineCallbackWrapper::GetPatternLength(CPattern* p)
        {
            //Get the rebuzz pattern
            IPattern^ pat = m_machineWrapper.GetRef()->GetReBuzzPattern(p);
            if (pat == nullptr)
                return 0;

            return pat->Length;
        }

        void MachineCallbackWrapper::SetPatternLength(CPattern* p, int length)
        {
            //Tell machine wrapper about the change
            m_machineWrapper.GetRef()->UpdatePattern(p, length, NULL);
        }

        void MachineCallbackWrapper::SetPatternName(CPattern* p, const char * name)
        {
            //Tell machine wrapper about the change
            m_machineWrapper.GetRef()->UpdatePattern(p, -1, name);
        }

        void MachineCallbackWrapper::SetPatternEditorStatusText(int pane, char const* text)
        {
            switch (pane)
            {
                case 0:
                    m_statusBarText0 = text;
                    break;
                case 1:
                    m_statusBarText1 = text;
                    break;
            }
            
            std::string outputText = "{statusbar}"; // write to debug console. prefix with "{statusbar}" to also set the status bar text.
            outputText.append(m_statusBarText0); 

            if (!m_statusBarText1.empty())
            {
                outputText.append(" | ");
                outputText.append(m_statusBarText1);
            }

            //Convert the CLR string
            String^ clrText = Utils::stdStringToCLRString(outputText);
            Global::Buzz->DCWriteLine(clrText);
            
        }

        CMachine* MachineCallbackWrapper::GetPatternOwner(CPattern* p)
        {
            //Get pattern
            IPattern^ rebuzzPattern =  m_machineWrapper.GetRef()->GetReBuzzPattern(p);
            if (rebuzzPattern == nullptr)
                return NULL;

            //Get the machine that owns this patter
            IMachine^ rebuzzMachine = rebuzzPattern->Machine;
            
            CMachine* buzzMach = (CMachine *)m_machineWrapper.GetRef()->GetCMachine(rebuzzMachine);
            return buzzMach;
        }

        void MachineCallbackWrapper::RemapLoadedMachineName(char* name, int bufsize)
        {
            //Find machine by name
            CMachine * buzzmach = (CMachine *)m_machineWrapper.GetRef()->GetCMachineByName(name);
            if (buzzmach == NULL)
                return;

            //Get the machine data
            CMachineData* machdata = m_machineWrapper.GetRef()->GetBuzzMachineData(buzzmach);
            if (machdata != NULL)
            {
                //Make sure the name buffer is big enough?
                //(I'm not sure if this is what this callback is supposed to do.
                //I am guessing by what PatternXP is doing)
                if (bufsize > machdata->name.size())
                    machdata->name.resize(bufsize);
            }

        }

        CWaveLevel const* MachineCallbackWrapper::GetNearestWaveLevel(int const i, int const note)
        {
            //These are some versioning hacks (?)
            if (i == -1 && note == -1)
            {

            }
            else if (i == -2 && note == -2)
            {
                return (CWaveLevel const*)-1;
            }
            else if (i == -3 && note == -3)
            {
                return (CWaveLevel const*)-1;
            }
            
            
            int index = i - 1;
            if((index < 0) || (index >= Global::Buzz->Song->Wavetable->Waves->Count))
                return NULL;

            IWaveLayer^ foundLevel = nullptr;
            IWave^ wav = Global::Buzz->Song->Wavetable->Waves[i - 1];
            for each (IWaveLayer^ wavlevl in wav->Layers)
            {
                if (wavlevl->RootNote > note)
                    break;

                foundLevel = wavlevl;
            }

            if (foundLevel == nullptr)
                return NULL;

            //Convert from ReBuzz IWaveLayer to Buzz
            CWaveLevel* ret = m_machineWrapper.GetRef()->GetWaveLevel(foundLevel);
            return ret;
        }

        CSequence* MachineCallbackWrapper::GetPlayingSequence(CMachine* pmac)
        {
            //Get the rebuzz machine
            IMachine^ rebuzzMachine = m_machineWrapper.GetRef()->GetReBuzzMachine(pmac);
            if (rebuzzMachine == nullptr)
                return NULL;

            //find the sequence
            for each (ISequence^ seq in Global::Buzz->Song->Sequences)
            {
                if((seq->PlayingPattern != nullptr) &&  (seq->Machine == rebuzzMachine))
                {
                    //Convert sequence to Buzz
                    CSequence* buzzseq = m_machineWrapper.GetRef()->GetSequence(seq);
                    return buzzseq;
                }
            } 

            return NULL;
        }

        CPattern* MachineCallbackWrapper::GetPlayingPattern(CSequence* pseq)
        {
            // Convert sequence to ReBuzz
            ISequence^ buzzseq = m_machineWrapper.GetRef()->GetReBuzzSequence(pseq);
            if (buzzseq == nullptr)
                return NULL;

            //Get the pattern
            IPattern^ playingPattern = buzzseq->PlayingPattern;
            return reinterpret_cast<CPattern*>( m_machineWrapper.GetRef()->GetCPattern(playingPattern));
        }

        int MachineCallbackWrapper::GetSequenceColumn(CSequence* s) 
        {
            // Convert sequence to ReBuzz
            ISequence^ buzzseq = m_machineWrapper.GetRef()->GetReBuzzSequence(s);
            if (buzzseq == nullptr)
                return -1;

            //Go through the sequences 
            int curCol = 0;
            for each (ISequence^ seq in Global::Buzz->Song->Sequences)
            {
                if (seq == buzzseq)
                    return curCol;

                ++curCol;
            }

            return -1;
        }

        int MachineCallbackWrapper::GetStateFlags()
        {
            int ret = 0;

            if (Global::Buzz->Recording)
                ret |= SF_RECORDING;

            if (Global::Buzz->Playing)
                ret |= SF_PLAYING;

            return ret;
        }

        void MachineCallbackWrapper::ControlChange(CMachine* pmac, int group, int track, int param, int value)
        {
            //Get the machine internal data
            CMachineData* machdata = m_machineWrapper.GetRef()->GetBuzzMachineData(pmac);
            if (machdata == NULL)
                return;

            //Store parameter change in machine data.
            //These will get sent to the machine when SendControlChange is called
            int  changePos = machdata->paramChanges.size();
            machdata->paramChanges.resize(changePos + 1);

            ParamChange & change = machdata->paramChanges[changePos];
            change.group = group & 0xF;
            change.track = track;
            change.param = param;
            change.value = value;
            change.noRecord = (group & 0x10) == 0x10; //16 = no record
        }

        void MachineCallbackWrapper::SendControlChanges(CMachine* pmac)
        {
            //Get the machine internal data
            CMachineData* machdata = m_machineWrapper.GetRef()->GetBuzzMachineData(pmac);
            if (machdata == NULL)
                return;

            //Get rebuzz machine
            IMachine^ rebuzzmach = m_machineWrapper.GetRef()->GetReBuzzMachine(pmac);
            if (rebuzzmach == nullptr)
                return;

            for (const auto& changeitr : machdata->paramChanges)
            {
                if (changeitr.group < rebuzzmach->ParameterGroups->Count)
                {
                    IParameterGroup^ pg = rebuzzmach->ParameterGroups[changeitr.group];
                    if (pg != nullptr)
                    {
                        if (changeitr.param < pg->Parameters->Count)
                        {
                            IParameter^ p = pg->Parameters[changeitr.param];
                            p->SetValue(changeitr.track | 1 << 16, changeitr.value);
                        }
                    }
                }
            }

            machdata->paramChanges.clear();

            //Tell machine about the control changes
            rebuzzmach->SendControlChanges();
        }

        bool MachineCallbackWrapper::GetPlayNotesState()
        {
            //Not sure on the condition where we should return 'false'
            return true;
        }

        int MachineCallbackWrapper::GetBaseOctave()
        {
            IMachine^ mach = m_machineWrapper.GetRef()->GetThisReBuzzMachine();
            return mach->BaseOctave;
        }

        int MachineCallbackWrapper::GetSelectedWave()
        {
            IWave^ wave = m_machineWrapper.GetRef()->GetSelectedWave();
            if (wave == nullptr)
                return 0;

            return wave->Index + 1;
        }

        CWaveInfo const* MachineCallbackWrapper::GetWave(int const i)
        {
            IWave^ wave = m_machineWrapper.GetRef()->FindWaveByOneIndex(i);
            if (wave != nullptr)
            {
                return m_machineWrapper.GetRef()->GetWaveInfo(wave);
            }
        }

        void MachineCallbackWrapper::SelectWave(int i)
        {
            IWave^ wave = m_machineWrapper.GetRef()->FindWaveByOneIndex(i);
            if (wave != nullptr)
            {
                m_machineWrapper.GetRef()->SetSelectedWave(wave);
            }
        }

        void MachineCallbackWrapper::SetPatternEditorMachine(CMachine* pmac, bool gotoeditor)
        {
            IMachine^ mach = m_machineWrapper.GetRef()->GetReBuzzMachine(pmac);
            if ((mach == nullptr) || (m_exInterface == NULL))
            {
                m_setPatternEditorMachine = pmac;
            }
            else
            {
                //m_exInterface->SetPatternTargetMachine
            }
        }

        

        int MachineCallbackWrapper::GetProfileInt(char const* entry, int defvalue)
        {
            int ret = defvalue;
            try
            {
                String^ profilename = m_machinehost.GetRef()->Machine->DLL->Info->ShortName;
                XElement^ profile = Global::Buzz->GetModuleProfileInts(profilename);


                String^ entryname = Utils::stdStringToCLRString(entry);
                XElement^ value = profile->Element(entryname);
                if (value == nullptr)
                {
                    profile->Add(gcnew XElement(entryname, ret));
                }
                else
                {
                    ret = System::Int32::Parse(value->Value);
                }
            }
            catch(System::Exception^ ex)
            {}

            return ret;
        }
        
        void MachineCallbackWrapper::GetProfileString(char const* entry, char const* value, char const* defvalue)
        {}
        
        void MachineCallbackWrapper::GetProfileBinary(char const* entry, byte** data, int* nbytes)
        {
            *data = NULL;
            *nbytes = 0;
            try
            {
                String^ profilename = m_machinehost.GetRef()->Machine->DLL->Info->ShortName;
                XElement^ profile = Global::Buzz->GetModuleProfileBinary(profilename);
                String^ entryname = Utils::stdStringToCLRString(entry);
                XElement^ value = profile->Element(entryname);
                cli::array<uint8_t>^ bytearray = nullptr;

                if (value != nullptr)
                {
                    bytearray = System::Convert::FromBase64String(value->Value);
                    pin_ptr<uint8_t> byteArrayPtr(&bytearray[0]);
                    *data = (uint8_t *)LocalAlloc(LPTR, bytearray->Length);
                    memcpy(*data, byteArrayPtr, bytearray->Length);
                    *nbytes = bytearray->Length;
                }
            }
            catch (System::Exception^ ex)
            {
                if (*data != NULL)
                {
                    LocalFree(*data);
                    *data = NULL;
                    nbytes = 0;
                }
            }
        }

        void MachineCallbackWrapper::FreeProfileBinary(byte* data)
        {
            if (data != NULL)
                LocalFree(data);
        }

        void MachineCallbackWrapper::WriteProfileInt(char const* entry, int value)
        {
            try
            {
                String^ profilename = m_machinehost.GetRef()->Machine->DLL->Info->ShortName;
                XElement^ profile = Global::Buzz->GetModuleProfileInts(profilename);
                String^ entryname = Utils::stdStringToCLRString(entry);
                XElement^ profilevalue = profile->Element(entryname);

                if (profilevalue == nullptr)
                {
                    profile->Add(gcnew XElement(entryname, value));
                }
                else
                {
                    std::ostringstream  valuestr;
                    valuestr << value;
                    profilevalue->Value = Utils::stdStringToCLRString(valuestr.str());
                }
            }
            catch (System::Exception^ ex)
            {
            }
        }

        void MachineCallbackWrapper::WriteProfileString(char const* entry, char const* value)
        {}
        
        void MachineCallbackWrapper::WriteProfileBinary(char const* entry, byte* data, int nbytes)
        {
            try
            {
                String^ profilename = m_machinehost.GetRef()->Machine->DLL->Info->ShortName;
                XElement^ profile = Global::Buzz->GetModuleProfileBinary(profilename);
                String^ entryname = Utils::stdStringToCLRString(entry);
                XElement^ profilevalue = profile->Element(entryname);

                cli::array<uint8_t>^ bytearray = gcnew cli::array<uint8_t>(nbytes);
                pin_ptr<uint8_t> byteArrayPtr(&bytearray[0]);
                memcpy(byteArrayPtr, data, nbytes);
                String^ database64 = System::Convert::ToBase64String(bytearray, System::Base64FormattingOptions::None);
                
                if (profilevalue == nullptr)
                {
                    profile->Add(gcnew XElement(entryname, database64));
                }
                else
                {
                    profilevalue->Value = database64;
                }
            }
            catch (System::Exception^ ex)
            {
            }
        }
    }
}
