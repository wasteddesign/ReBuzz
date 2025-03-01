
#include "MachineManager.h"
#include "Utils.h"

#include <map>
#include <sstream>

using BuzzGUI::Interfaces::MachineType;
using BuzzGUI::Interfaces::MachineInfoFlags;
using BuzzGUI::Interfaces::IAttribute;
using BuzzGUI::Interfaces::IMenuItem;
using BuzzGUI::Interfaces::IParameter;
using BuzzGUI::Interfaces::ParameterType;
using BuzzGUI::Interfaces::ParameterFlags;
using BuzzGUI::Interfaces::IParameterGroup;

using BuzzGUI::Common::Global;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        struct MachineCreateCallbackData
        {
            std::map<std::string, uint64_t> machineNameMap;
            RebuzzBuzzLookup< IMachine, CMachineData, CMachine>* machineMap;
        };

        //This callback is called when a new CMachine * map entry is created.
        //The purpose of this is to populate the machine info, so that the native buzz machine can query
        //information about CMachine * without the need to convert from .NET types to c++ types each
        //and every time.
        static void CreateMachineCallback(void* mach, void* param)
        {
            //Don't lock - this is callback is only triggered by adding to m_machineMap, 
            //which should already be done under the lock
            ///
            //ALSO: Don't call back into MachineManager methods that themselves (attempt to) take the lock.

            MachineCreateCallbackData* machCallbackData = reinterpret_cast<MachineCreateCallbackData*>(param);

            //Use machine wrapper to convert from CMachine * to ReBuzz machine
            CMachine* buzzMach = reinterpret_cast<CMachine*>(mach);
            IMachine^ rebuzzMach = machCallbackData->machineMap->GetReBuzzTypeByBuzzType(buzzMach);
            if (rebuzzMach == nullptr)
                return;

            //Get the emulation type, as this contains info about the machine
            CMachineData* machdata = machCallbackData->machineMap->GetBuzzEmulationType(buzzMach);
            if (machdata == NULL)
                return;

            //Use ReBuzz to get info about the machine, and populate the CMachineInfo
            Utils::CLRStringToStdString(rebuzzMach->DLL->Info->Author, machdata->author);
            machdata->m_info.Author = machdata->author.c_str();
            Utils::CLRStringToStdString(rebuzzMach->Name, machdata->name);
            machdata->m_info.Name = machdata->name.c_str();
            Utils::CLRStringToStdString(rebuzzMach->DLL->Info->ShortName, machdata->shortname);
            machdata->m_info.ShortName = machdata->shortname.c_str();

            machdata->m_info.Version = MI_VERSION;
            machdata->m_info.minTracks = rebuzzMach->DLL->Info->MinTracks;
            machdata->m_info.maxTracks = rebuzzMach->DLL->Info->MaxTracks;
            switch (rebuzzMach->DLL->Info->Type)
            {
            case MachineType::Master:
                machdata->m_info.Type = MT_MASTER;
                break;
            case MachineType::Effect:
                machdata->m_info.Type = MT_EFFECT;
                break;
            case MachineType::Generator:
                machdata->m_info.Type = MT_GENERATOR;
                break;
            }

            //Flags
            machdata->m_info.Flags = 0;
            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::ALWAYS_SHOW_PLUGS) == MachineInfoFlags::ALWAYS_SHOW_PLUGS)
                machdata->m_info.Flags |= MIF_ALWAYS_SHOW_PLUGS;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::MONO_TO_STEREO) == MachineInfoFlags::MONO_TO_STEREO)
                machdata->m_info.Flags |= MIF_MONO_TO_STEREO;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::PLAYS_WAVES) == MachineInfoFlags::PLAYS_WAVES)
                machdata->m_info.Flags |= MIF_PLAYS_WAVES;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::USES_LIB_INTERFACE) == MachineInfoFlags::USES_LIB_INTERFACE)
                machdata->m_info.Flags |= MIF_USES_LIB_INTERFACE;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::USES_INSTRUMENTS) == MachineInfoFlags::USES_INSTRUMENTS)
                machdata->m_info.Flags |= MIF_USES_INSTRUMENTS;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::DOES_INPUT_MIXING) == MachineInfoFlags::DOES_INPUT_MIXING)
                machdata->m_info.Flags |= MIF_DOES_INPUT_MIXING;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::NO_OUTPUT) == MachineInfoFlags::NO_OUTPUT)
                machdata->m_info.Flags |= MIF_NO_OUTPUT;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::CONTROL_MACHINE) == MachineInfoFlags::CONTROL_MACHINE)
                machdata->m_info.Flags |= MIF_CONTROL_MACHINE;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::INTERNAL_AUX) == MachineInfoFlags::INTERNAL_AUX)
                machdata->m_info.Flags |= MIF_INTERNAL_AUX;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::EXTENDED_MENUS) == MachineInfoFlags::EXTENDED_MENUS)
                machdata->m_info.Flags |= MIF_EXTENDED_MENUS;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::PATTERN_EDITOR) == MachineInfoFlags::PATTERN_EDITOR)
                machdata->m_info.Flags |= MIF_PATTERN_EDITOR;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::PE_NO_CLIENT_EDGE) == MachineInfoFlags::PE_NO_CLIENT_EDGE)
                machdata->m_info.Flags |= MIF_PE_NO_CLIENT_EDGE;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::GROOVE_CONTROL) == MachineInfoFlags::GROOVE_CONTROL)
                machdata->m_info.Flags |= MIF_GROOVE_CONTROL;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::DRAW_PATTERN_BOX) == MachineInfoFlags::DRAW_PATTERN_BOX)
                machdata->m_info.Flags |= MIF_DRAW_PATTERN_BOX;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::STEREO_EFFECT) == MachineInfoFlags::STEREO_EFFECT)
                machdata->m_info.Flags |= MIF_STEREO_EFFECT;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::MULTI_IO) == MachineInfoFlags::MULTI_IO)
                machdata->m_info.Flags |= MIF_MULTI_IO;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::PREFER_MIDI_NOTES) == MachineInfoFlags::PREFER_MIDI_NOTES)
                machdata->m_info.Flags |= MIF_PREFER_MIDI_NOTES;

            if ((rebuzzMach->DLL->Info->Flags & MachineInfoFlags::LOAD_DATA_RUNTIME) == MachineInfoFlags::LOAD_DATA_RUNTIME)
                machdata->m_info.Flags |= MIF_LOAD_DATA_RUNTIME;

            //Get and convert attributes
            machdata->m_info.numAttributes = 0;
            for each (IAttribute ^ attr in rebuzzMach->Attributes)
            {
                std::shared_ptr<CMachineAttribute> buzzAttr = std::make_shared<CMachineAttribute>();
                buzzAttr->DefValue = attr->DefValue;
                buzzAttr->MaxValue = attr->MaxValue;
                buzzAttr->MinValue = attr->MinValue;

                std::shared_ptr<std::string> buzzAttrName = std::make_shared<std::string>();
                Utils::CLRStringToStdString(attr->Name, *buzzAttrName);
                buzzAttr->Name = buzzAttrName->c_str();

                //Store attribute
                machdata->attributes.push_back(buzzAttr);
                machdata->attributePointers.push_back(buzzAttr.get());
                machdata->attributeNames.push_back(buzzAttrName);

                //Increase attribute count
                machdata->m_info.numAttributes += 1;
            }

            if (machdata->attributePointers.empty())
                machdata->m_info.Attributes = NULL;
            else
                machdata->m_info.Attributes = machdata->attributePointers.data();

            //Get and convert commands
            for each (IMenuItem ^ cmd in rebuzzMach->Commands)
            {
                //Native Buzz stored commands in a char * array, separated with a \n
                std::string text;
                Utils::CLRStringToStdString(cmd->Text, text);

                if (!machdata->commands.empty())
                    machdata->commands.append("\n");

                machdata->commands.append(text);
            }

            if (machdata->commands.empty())
                machdata->m_info.Commands = NULL;
            else
                machdata->m_info.Commands = machdata->commands.c_str();


            //Okay, now do parameters
            //From what I can gather - Parameter group 1 are global parameters
            //and Parameter group 2 are track parameters
            //I've no idea what parameter group 0 is for.
            //What could have been useful here , is a 'type' enum on each group, so I 
            //don't have to hard-code group numbers here, and can just go by the 'Type' enum value.
            machdata->m_info.numGlobalParameters = 0;
            machdata->m_info.numTrackParameters = 0;
            int grpNum = 0;
            for each (IParameterGroup ^ grp in rebuzzMach->ParameterGroups)
            {
                if ((grpNum != 1) && (grpNum != 2))
                {
                    ++grpNum;
                    continue;
                }

                for each (IParameter ^ param in grp->Parameters)
                {
                    std::shared_ptr<CMachineParameter> buzzParam = std::make_shared<CMachineParameter>();
                    buzzParam->DefValue = param->DefValue;
                    buzzParam->MaxValue = param->MaxValue;
                    buzzParam->MinValue = param->MinValue;
                    buzzParam->NoValue = param->NoValue;
                    switch (param->Type)
                    {
                    case ParameterType::Note:
                        buzzParam->Type = pt_note;
                        break;
                    case ParameterType::Byte:
                        buzzParam->Type = pt_byte;
                        break;
                    case ParameterType::Internal:
                        buzzParam->Type = pt_internal;
                        break;
                    case ParameterType::Switch:
                        buzzParam->Type = pt_switch;
                        break;
                    case ParameterType::Word:
                        buzzParam->Type = pt_word;
                        break;
                    }

                    buzzParam->Flags = 0;
                    if ((param->Flags & ParameterFlags::Ascii) == ParameterFlags::Ascii)
                        buzzParam->Flags |= MPF_ASCII;

                    if ((param->Flags & ParameterFlags::State) == ParameterFlags::State)
                        buzzParam->Flags |= MPF_STATE;

                    if ((param->Flags & ParameterFlags::TickOnEdit) == ParameterFlags::TickOnEdit)
                        buzzParam->Flags |= MPF_TICK_ON_EDIT;

                    if ((param->Flags & ParameterFlags::TiedToNext) == ParameterFlags::TiedToNext)
                        buzzParam->Flags |= MPF_TIE_TO_NEXT;

                    if ((param->Flags & ParameterFlags::Wave) == ParameterFlags::Wave)
                        buzzParam->Flags |= MPF_WAVE;

                    std::shared_ptr<std::string> desc = std::make_shared<std::string>();
                    Utils::CLRStringToStdString(param->Description, *desc);
                    buzzParam->Description = desc->c_str();

                    std::shared_ptr<std::string> name = std::make_shared<std::string>();
                    Utils::CLRStringToStdString(param->Name, *name);
                    buzzParam->Name = name->c_str();


                    machdata->parameters.push_back(buzzParam);
                    machdata->parameterPtrs.push_back(buzzParam.get());
                    machdata->paramDescriptions.push_back(desc);
                    machdata->paramDescriptions.push_back(name);

                    if (grpNum == 1)
                        machdata->m_info.numGlobalParameters += 1;
                    else if (grpNum == 2)
                        machdata->m_info.numTrackParameters += 1;
                }

                ++grpNum;
            }

            if (machdata->parameterPtrs.empty())
                machdata->m_info.Parameters = NULL;
            else
                machdata->m_info.Parameters = machdata->parameterPtrs.data();

            //Store the internal id against the machine name
            int64_t id = rebuzzMach->CMachinePtr.ToInt64();
            machCallbackData->machineNameMap[machdata->name] = id;
        }

        //===============================================================================================

        MachineManager::MachineManager(OnMachineEventDelegate^ onMachineAddedCallback,
                                        OnMachineEventDelegate^ onMachineRemovedCallback)
        {
            m_lock = new std::mutex();
            MachineCreateCallbackData* machCallbackData = new MachineCreateCallbackData();
            m_machineCallbackData = machCallbackData;
            m_machineMap = new RebuzzBuzzLookup< IMachine, CMachineData, CMachine>(CreateMachineCallback, m_machineCallbackData);
            
            //Store callback info for MachineWrapper
            m_onMachineAddedCallback = onMachineAddedCallback;
            m_onMachineRemovedCallback = onMachineRemovedCallback;

            //Set up callback data
            machCallbackData->machineMap = m_machineMap;
          
            //Ask ReBuzz to tell us when a machine has been added
            m_machineAddedAction = gcnew System::Action<IMachine^>(this, &MachineManager::OnMachineCreatedByReBuzz);
            Global::Buzz->Song->MachineAdded += m_machineAddedAction;

            //Ask ReBuzz to tell us when a machine has been deleted
            m_machineRemovedAction = gcnew System::Action<IMachine^>(this, &MachineManager::OnMachineRemovedByReBuzz);
            Global::Buzz->Song->MachineRemoved += m_machineRemovedAction;
        }

        MachineManager::!MachineManager()
        {
            Free();
        }

        MachineManager::~MachineManager()
        {
            Free();
        }

        void MachineManager::Free()
        {
            Release();

            if (m_machineCallbackData != NULL)
            {
                MachineCreateCallbackData* machCallbackData = reinterpret_cast<MachineCreateCallbackData*>(m_machineCallbackData);
                delete machCallbackData;
                m_machineCallbackData = NULL;
            }

            if (m_lock != NULL)
            {
                delete m_lock;
                m_lock = NULL;
            }
        }

        
        void MachineManager::Release()
        {
            if (m_lock == NULL)
                return;

            std::lock_guard<std::mutex> lg(*m_lock);

            //Unregister events
            if (m_machineAddedAction != nullptr)
            {
                Global::Buzz->Song->MachineAdded -= m_machineAddedAction;
                delete m_machineAddedAction;
                m_machineAddedAction = nullptr;
            }

            if (m_machineRemovedAction != nullptr)
            {
                Global::Buzz->Song->MachineRemoved -= m_machineRemovedAction;
                delete m_machineRemovedAction;
                m_machineRemovedAction = nullptr;
            }

            if (m_machineMap != NULL)
            {  
                m_machineMap->Release();
                delete m_machineMap;
                m_machineMap = NULL;
            }
        }

        IMachine^ MachineManager::GetReBuzzMachine(CMachine * mach)
        {
            if (mach == NULL)
                return nullptr;

            std::lock_guard<std::mutex> lg(*m_lock);

            return m_machineMap->GetReBuzzTypeByBuzzType(mach);
        }

        CMachineData* MachineManager::GetBuzzMachineData(CMachine * mach)
        {
            if (mach == NULL)
                return NULL;

            std::lock_guard<std::mutex> lg(*m_lock);

            return m_machineMap->GetBuzzEmulationType(reinterpret_cast<CMachine*>(mach));
        }

        CMachine * MachineManager::GetOrStoreMachine(IMachine^ m)
        {
            if (m == nullptr)
                return NULL;

            int64_t machid = 0;
            CMachine* pmach = NULL;
            bool itemCreated = false;
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                machid = m->CMachinePtr.ToInt64();
                pmach = m_machineMap->GetOrStoreReBuzzTypeById(machid, m, &itemCreated);
            }

            //If an item was created in the map, then call the machine added callback
            if (itemCreated && (m_onMachineAddedCallback != nullptr))
            {
                m_onMachineAddedCallback(machid, m, pmach);
            }

            return pmach;
        }

        CMachine* MachineManager::GetBuzzMachine(IMachine^ m)
        {
            std::lock_guard<std::mutex> lg(*m_lock);

            int64_t machid = m->CMachinePtr.ToInt64();
            return m_machineMap->GetBuzzTypeById(machid);
        }

        CMachine* MachineManager::GetCMachineByName(const char* name)
        {
            bool itemCreated = false;
            CMachine* ret = NULL;
            IMachine^ rebuzzMachine = nullptr;
            int64_t machid;
            {

                std::lock_guard<std::mutex> lg(*m_lock);

                //Names are stored in callback data
                MachineCreateCallbackData* machCallbackData = reinterpret_cast<MachineCreateCallbackData*>(m_machineCallbackData);
                const auto& found = machCallbackData->machineNameMap.find(name);
                if (found == machCallbackData->machineNameMap.end())
                {
                    //We don't have this machine, but does ReBuzz?
                    //Convert the char * to a CLR string
                    String^ clrName = Utils::stdStringToCLRString(name);

                    try
                    {
                        for each (IMachine ^ mach in Global::Buzz->Song->Machines)
                        {
                            if (mach->Name == clrName)
                            {
                                //Found - create entry in our map
                                machid = mach->CMachinePtr.ToInt64();
                                rebuzzMachine = mach;
                                ret = machCallbackData->machineMap->GetOrStoreReBuzzTypeById(machid, mach, &itemCreated);
                                machCallbackData->machineNameMap[name] = machid;
                                break;
                            }
                        }
                    }
                    finally
                    {
                        delete clrName;
                    }
                }

                if (ret == NULL)
                {   
                    machid = (*found).second;
                    ret = m_machineMap->GetBuzzTypeById((*found).second);
                    rebuzzMachine = m_machineMap->GetReBuzzTypeByBuzzType(ret);
                }
            }

            if (itemCreated && (m_onMachineAddedCallback != nullptr))
            {
                m_onMachineAddedCallback(machid, rebuzzMachine, ret);
            }

            return ret;
        }

        void MachineManager::OnMachineCreatedByReBuzz(IMachine^ machine)
        {
            bool itemCreated = false;
            int64_t id;
            CMachine* pmach;
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                //Do we have this machine in our map?
                id = machine->CMachinePtr.ToInt64();
                pmach = m_machineMap->GetBuzzTypeById(id);
                if (pmach == NULL)
                {
                    //Create machine. This will also trigger the above 'CreateMachineCallback'
                    m_machineMap->GetOrStoreReBuzzTypeById(id, machine, &itemCreated);
                }
            }

            //If an item was created in the map, then call the machine added callback
            if (itemCreated && (m_onMachineAddedCallback != nullptr))
            {
                m_onMachineAddedCallback(id, machine, pmach);
            }
        }

        void MachineManager::OnMachineRemovedByReBuzz(IMachine^ machine)
        {
            //Call the removed callback first
            if (m_onMachineRemovedCallback != nullptr)
            {
                int64_t id = machine->CMachinePtr.ToInt64();
                CMachine* buzzMachine = NULL;
                {
                    std::lock_guard<std::mutex> lg(*m_lock);
                    buzzMachine = m_machineMap->GetBuzzTypeById(id);
                }

                if(buzzMachine != NULL)
                    m_onMachineRemovedCallback(id, machine, buzzMachine);
            }

            std::lock_guard<std::mutex> lg(*m_lock);

            //Remove from machine manager
            int64_t id = machine->CMachinePtr.ToInt64();
            if (m_machineMap != NULL)
                m_machineMap->RemoveById(id);
            
            //Remove from name map
            MachineCreateCallbackData* cbdata = reinterpret_cast<MachineCreateCallbackData*>(m_machineCallbackData);
            std::vector<std::string> removeNames;
            for (const auto& itr : cbdata->machineNameMap)
            {
                if (itr.second == id)
                {
                    removeNames.push_back(itr.first);
                }
            }

            for (const auto& nameitr : removeNames)
            {
                cbdata->machineNameMap.erase(nameitr);
            }

            //Remove object from object id utility dictionary
            Utils::RemoveObjectInt64(machine);
        }
    }
}