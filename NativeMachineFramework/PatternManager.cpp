#include <map>

#include "PatternManager.h"
#include "Utils.h"

using BuzzGUI::Common::Global;
using System::Collections::Generic::Dictionary;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        
        
        struct PatternCreateCallbackData
        {
            RebuzzBuzzLookup<IPattern, CPatternData, CPattern> * patternMap;
            std::vector<CPattern*> callbacksRequired;
            RefClassWrapper < System::Action<IPatternColumn^>> patternChangeAction;
            RefClassWrapper<PropertyChangedEventHandler> propChangeEventHandler;

            RefClassWrapper<System::Action<IPattern^>> patternPlayStartAction;
            RefClassWrapper<System::Action<IPattern^>> patternPlayEndAction;
            RefClassWrapper<System::Action<IPattern^>> patternPlayPosChangeAction;

            std::map<int64_t, std::map<std::string, uint64_t>> patternNameMap;
        };


        static void RemovePatChangeAction(uint64_t id, IPattern^ rebuzzpat, CPattern* buzzpat, CPatternData* patdata, void* param)
        {
            RefClassWrapper<System::Action<IPatternColumn^>>* tmpact = reinterpret_cast<RefClassWrapper<System::Action<IPatternColumn^>> *>(param);
            rebuzzpat->PatternChanged -= tmpact->GetRef();
        }

        static void RemovePropChangeHandler(uint64_t id, IPattern^ rebuzzpat, CPattern* buzzpat, CPatternData* patdata, void* param)
        {
            RefClassWrapper<PropertyChangedEventHandler>* tmpact = reinterpret_cast<RefClassWrapper<PropertyChangedEventHandler> *>(param);
            rebuzzpat->PropertyChanged -= tmpact->GetRef();
        }

        static void RemovePatPlayStartHandler(uint64_t id, IPattern^ rebuzzpat, CPattern* buzzpat, CPatternData* patdata, void* param)
        {
            RefClassWrapper<System::Action<IPattern^>> *  tmpact = reinterpret_cast<RefClassWrapper<System::Action<IPattern^>> *>(param);
            if(!tmpact->isNull())
                rebuzzpat->OnPatternPlayStart -= tmpact->GetRef();
        }

        static void RemovePatPlayEndHandler(uint64_t id, IPattern^ rebuzzpat, CPattern* buzzpat, CPatternData* patdata, void* param)
        {
            RefClassWrapper<System::Action<IPattern^>>* tmpact = reinterpret_cast<RefClassWrapper<System::Action<IPattern^>> *>(param);            
            if(!tmpact->isNull())
            rebuzzpat->OnPatternPlayEnd -= tmpact->GetRef();
        }

        static void RemovePatPlayPosChangeHandler(uint64_t id, IPattern^ rebuzzpat, CPattern* buzzpat, CPatternData* patdata, void* param)
        {
            RefClassWrapper<System::Action<IPattern^>>* tmpact = reinterpret_cast<RefClassWrapper<System::Action<IPattern^>> *>(param);
            if(!tmpact->isNull())
                rebuzzpat->OnPatternPlayPositionChange -= tmpact->GetRef();
        }


        void PatternManager::OnReBuzzPatternChange(IPattern^ rebuzzpat, bool lock)
        {
            bool notifyRename = false;
            bool notifyLength = false;
            const char* newnameptr = NULL;
            CPattern* buzzPat;
            int newLen = 0;
            OnPatternEditorRedrawDelegate^ redrawcallback =nullptr;
            int64_t patid = rebuzzpat->CPattern.ToInt64();
            if (patid == 0)
                return;
           
            {
                std::unique_lock<std::mutex> lg(*m_lock, std::defer_lock);
                if (lock)
                    lg.lock();

                buzzPat = m_patternMap->GetBuzzTypeById(patid);
                if (buzzPat == NULL)
                    return;

                CPatternData* patdata = m_patternMap->GetBuzzEmulationType(buzzPat);
                if (patdata == NULL)
                    return;

                redrawcallback = m_onPatternEditorRedrawCallback;
                
                //Get the patten name, and store into the pattern data
                std::string newname;
                Utils::CLRStringToStdString(rebuzzpat->Name, newname);
                if (newname != patdata->name)
                {
                    patdata->name = newname;
                    notifyRename = true;
                    newnameptr = patdata->name.c_str();
                }

                //Check length
                if (patdata->length != rebuzzpat->Length)
                {
                    notifyLength = true;
                    newLen = rebuzzpat->Length;
                    patdata->length = newLen;
                }
            }

            //Notify the callback of machine of changes
            if (m_onPatternChangedCallback != nullptr)
            {
                PatternEventFlags eventFlags = PatternEventFlags_None;
                if (notifyRename)
                {
                    eventFlags = (PatternEventFlags)(eventFlags | PatternEventFlags_Name);
                }

                if (notifyLength)
                {
                    eventFlags = (PatternEventFlags)(eventFlags | PatternEventFlags_Length);
                }

                m_onPatternChangedCallback(patid, rebuzzpat, buzzPat, eventFlags);
            }
        }

        void PatternManager::PatternChangeCheckCallback(uint64_t id, IPattern^ rebuzzpat, CPattern* buzzpat, CPatternData* patdata, void* param)
        {
            RefClassWrapper<PatternManager>* me = reinterpret_cast<RefClassWrapper<PatternManager> *>(param);
            me->GetRef()->OnReBuzzPatternChange(rebuzzpat, false);
        }

        void PatternManager::OnReBuzzPatternColumnChange(IPatternColumn^ patcol)
        {
            if ((patcol == nullptr) || (patcol->Pattern == nullptr))
            {
                //We don't know what pattern as changed.  Assume any/all
                std::lock_guard<std::mutex> lg(*m_lock);
                RefClassWrapper<PatternManager> me(this);
                m_patternMap->ForEachItem(PatternChangeCheckCallback, &me);
            }
            else
            {
                OnReBuzzPatternChange(patcol->Pattern, true);
            }
        }

        //Property changed handler
        void PatternManager::OnPropertyChangedCallback(System::Object^ sender, PropertyChangedEventArgs^ args)
        {
            OnReBuzzPatternChange((IPattern^)sender, true);
        }

        void PatternManager::OnPatternCreatedByRebuzz(IPattern^ rebuzzpat)
        {
            //Make sure we have the pattern, and call any 'added' callbacks if we don't
            GetOrStorePattern(rebuzzpat);
        }

        void PatternManager::OnPatternRemovedByRebuzz(IPattern^ rebuzzpat)
        {
            int64_t patid = rebuzzpat->CPattern.ToInt64(); 
            int64_t machid = rebuzzpat->Machine->CMachinePtr.ToInt64();
            CPattern* buzzpat = m_patternMap->GetBuzzTypeById(patid);
            if (buzzpat == NULL)
                return;

            //Call the remove callbacks, so that things are notified before the pattern info is removed
            if (m_onPatternRemovedCallback != nullptr)
            {   
                m_onPatternRemovedCallback(patid, rebuzzpat, buzzpat, PatternEventFlags_All);
            }

            //Remove property change handlers from patterns
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                PatternCreateCallbackData* createCallbackData = reinterpret_cast<PatternCreateCallbackData*>(m_patternCallbackData);

                RefClassWrapper<PropertyChangedEventHandler> tmphdlr(m_onPropChangeEventHandler);
                RemovePropChangeHandler(patid, rebuzzpat, buzzpat, (CPatternData*)buzzpat, &tmphdlr);

                RefClassWrapper<System::Action<IPatternColumn^>> tmphdlr2(m_onPatternChangeAction);
                RemovePatChangeAction(patid, rebuzzpat, buzzpat, (CPatternData*)buzzpat, &tmphdlr2);
            
                RefClassWrapper<System::Action<IPattern^>> tmphdlr3(m_patternPlayStartAction);
                RemovePatPlayStartHandler(patid, rebuzzpat, buzzpat, (CPatternData*)buzzpat, &tmphdlr3);

                RefClassWrapper<System::Action<IPattern^>> tmphdlr4(m_patternPlayEndAction);
                RemovePatPlayEndHandler(patid, rebuzzpat, buzzpat, (CPatternData*)buzzpat, &tmphdlr4);

                RefClassWrapper<System::Action<IPattern^>> tmphdlr5(m_patternPlayPosChangeAction);
                RemovePatPlayPosChangeHandler(patid, rebuzzpat, buzzpat, (CPatternData*)buzzpat, &tmphdlr5);

                //Remove pattern from map
                m_patternMap->RemoveById(patid);

                //Remove from name map
                auto foundMach = createCallbackData->patternNameMap.find(machid);
                if (foundMach != createCallbackData->patternNameMap.end())
                {
                    std::string patname;
                    Utils::CLRStringToStdString(rebuzzpat->Name, patname);
                    (*foundMach).second.erase(patname);
                }
            }
        }


        void PatternManager::OnPatternPlayStart(IPattern^ pattern)
        {
            if (m_onPatternPlayStartCallback != nullptr)
            {
                const int64_t patId = pattern->CPattern.ToInt64();
                CPattern* buzzpat = NULL;
                {
                    std::unique_lock<std::mutex> lg(*m_lock, std::defer_lock);
                    buzzpat = m_patternMap->GetBuzzTypeById(patId);
                }
               
                if (buzzpat != NULL)
                {
                    m_onPatternPlayStartCallback(patId, pattern, buzzpat);
                }
            }
        }

        void PatternManager::OnPatternPlayPosChange(IPattern^ pattern)
        {
            if (m_onPatternPlayPosChangeCallback != nullptr)
            {
                const int64_t patId = pattern->CPattern.ToInt64();
                CPattern* buzzpat = NULL;
                {
                    std::unique_lock<std::mutex> lg(*m_lock, std::defer_lock);
                    buzzpat = m_patternMap->GetBuzzTypeById(patId);
                }

                if (buzzpat != NULL)
                {
                    m_onPatternPlayPosChangeCallback(patId, pattern, buzzpat);
                }
            }
        }
                
        void PatternManager::OnPatternPlayEnd(IPattern^ pattern)
        {
            if (m_onPatternPlayEndCallback != nullptr)
            {
                const int64_t patId = pattern->CPattern.ToInt64();
                CPattern* buzzpat = NULL;
                {
                    std::unique_lock<std::mutex> lg(*m_lock, std::defer_lock);
                    buzzpat = m_patternMap->GetBuzzTypeById(patId);
                }

                if (buzzpat != NULL)
                {
                    m_onPatternPlayEndCallback(patId, pattern, buzzpat);
                }
            }
        }

        
        static void CreatePatternCallback(void* pat, void* param)
        {
            PatternCreateCallbackData* callbackData = reinterpret_cast<PatternCreateCallbackData*>(param);

            //Get the emulation type, as this contains info about the pattern
            CPattern* buzzPat = reinterpret_cast<CPattern*>(pat);
            CPatternData* patdata = callbackData->patternMap->GetBuzzEmulationType(buzzPat);
            if (patdata == NULL)
                return;

            //Get the ReBuzz pattern
            IPattern^ rebuzzPattern = callbackData->patternMap->GetReBuzzTypeByBuzzType(buzzPat);

            //Get the patten name, and store into the pattern data
            Utils::CLRStringToStdString(rebuzzPattern->Name, patdata->name);

            //Get length
            patdata->length = rebuzzPattern->Length;

            //Store pattern against name and ReBuzz CMachineDataPtr
            int64_t machineId = rebuzzPattern->Machine->CMachinePtr.ToInt64();
            int64_t patId = rebuzzPattern->CPattern.ToInt64(); 
            callbackData->patternNameMap[machineId][patdata->name] = patId;

            //Mark this pattern needs to have the callback called for it
            callbackData->callbacksRequired.push_back(buzzPat);

            //Register for events
            rebuzzPattern->PatternChanged += callbackData->patternChangeAction.GetRef();
            rebuzzPattern->PropertyChanged += callbackData->propChangeEventHandler.GetRef();

            if(!callbackData->patternPlayStartAction.isNull())
                rebuzzPattern->OnPatternPlayStart += callbackData->patternPlayStartAction.GetRef();
            
            if(!callbackData->patternPlayEndAction.isNull())
                rebuzzPattern->OnPatternPlayEnd += callbackData->patternPlayEndAction.GetRef();
            
            if(!callbackData->patternPlayPosChangeAction.isNull())
                rebuzzPattern->OnPatternPlayPositionChange += callbackData->patternPlayPosChangeAction.GetRef();
        }


        PatternManager::PatternManager(OnPatternEventDelegate^ onPatternAddedCallback,
                                       OnPatternEventDelegate^ onPatternRemovedCallback,
                                       OnPatternEventDelegate^ onPatternChangedCallback,
                                       OnPatternEditorRedrawDelegate^ onPatternEditorRedrawCallback ,
                                       OnPatternPlayDelegate^ onPatternPlayStartCallback,
                                       OnPatternPlayDelegate^ onPatternPlayEndCallback,
                                       OnPatternPlayDelegate^ onPatternPlayPosChangeCallback)
        {
            m_lock = new std::mutex();
            m_editorTargetMachine = nullptr;
            m_eventHandlersAddedToMachines = new std::set<int64_t>();
            m_machinesEventHandlersAddedTo = new std::vector<RefClassWrapper<IMachine>>();

            //Store callbacks to notify changes made by native machine
            m_onPatternAddedCallback = onPatternAddedCallback;
            m_onPatternRemovedCallback = onPatternRemovedCallback;
            m_onPatternChangedCallback = onPatternChangedCallback;
            m_onPatternEditorRedrawCallback = onPatternEditorRedrawCallback;
            m_onPatternPlayEndCallback = onPatternPlayEndCallback;
            m_onPatternPlayStartCallback = onPatternPlayStartCallback;
            m_onPatternPlayPosChangeCallback = onPatternPlayPosChangeCallback;

            PatternCreateCallbackData* patternCallbackData = new PatternCreateCallbackData();
            m_patternCallbackData = patternCallbackData;

            m_patternMap = new RebuzzBuzzLookup< IPattern, CPatternData, CPattern>(CreatePatternCallback, m_patternCallbackData);

            //Set up action for being notified on pattern change
            m_onPatternChangeAction = gcnew System::Action<IPatternColumn^>(this, &PatternManager::OnReBuzzPatternColumnChange);

            //Set up action for property changes
            m_onPropChangeEventHandler = gcnew PropertyChangedEventHandler(this, &PatternManager::OnPropertyChangedCallback);

            //Set up action for pattern added
            m_patternAddedAction = gcnew System::Action<IPattern^>(this, &PatternManager::OnPatternCreatedByRebuzz);

            //set up action for pattern removed
            m_patternRemovedAction = gcnew System::Action<IPattern^>(this, &PatternManager::OnPatternRemovedByRebuzz);

            //Set up for pattern play events
            m_patternPlayStartAction = gcnew System::Action<IPattern^>(this, &PatternManager::OnPatternPlayStart);
            m_patternPlayEndAction = gcnew System::Action<IPattern^>(this, &PatternManager::OnPatternPlayEnd);
            m_patternPlayPosChangeAction= gcnew System::Action<IPattern^>(this, &PatternManager::OnPatternPlayPosChange);

            //Populate callback data
            patternCallbackData->patternMap = m_patternMap;
            patternCallbackData->patternChangeAction.Assign(m_onPatternChangeAction);
            patternCallbackData->propChangeEventHandler.Assign(m_onPropChangeEventHandler);
            patternCallbackData->patternPlayStartAction.Assign(m_patternPlayStartAction);
            patternCallbackData->patternPlayEndAction.Assign(m_patternPlayEndAction);
            patternCallbackData->patternPlayPosChangeAction.Assign(m_patternPlayPosChangeAction);
        }

        PatternManager::!PatternManager()
        {
            Free();
        }

        PatternManager::~PatternManager()
        {
            Free();
        }

        void PatternManager::Free()
        {
            Release();

            if (m_patternCallbackData != NULL)
            {
                PatternCreateCallbackData* patternCallbackData = reinterpret_cast<PatternCreateCallbackData*>(m_patternCallbackData);
                delete patternCallbackData;
                m_patternCallbackData = NULL;
            }
            
            if (m_eventHandlersAddedToMachines != NULL)
            {
                delete m_eventHandlersAddedToMachines;
                m_eventHandlersAddedToMachines = NULL;
            }

            if (m_machinesEventHandlersAddedTo != NULL)
            {
                delete m_machinesEventHandlersAddedTo;
                m_machinesEventHandlersAddedTo = NULL;
            }

            if (m_lock != NULL)
            {
                delete m_lock;
                m_lock = NULL;
            }
        }

        void PatternManager::Release()
        {
            if (m_lock == NULL)
                return;

            std::lock_guard<std::mutex> lg(*m_lock);

            //Unregister the pattern added events from the machines we added it to
            if(m_patternAddedAction != nullptr) 
            {
                if (m_machinesEventHandlersAddedTo != NULL)
                {
                    for (auto& itr : *m_machinesEventHandlersAddedTo)
                    {
                        itr.GetRef()->PatternAdded -= m_patternAddedAction;
                    }
                }

                delete m_patternAddedAction;
                m_patternAddedAction = nullptr;
            }

            if (m_patternRemovedAction != nullptr)
            {
                if (m_machinesEventHandlersAddedTo != NULL)
                {
                    for (auto& itr : *m_machinesEventHandlersAddedTo)
                    {
                        itr.GetRef()->PatternRemoved -= m_patternRemovedAction;
                    }
                }

                delete m_patternRemovedAction;
                m_patternRemovedAction = nullptr;
            }

            if (m_machinesEventHandlersAddedTo != NULL)
            {
                m_machinesEventHandlersAddedTo->clear();
            }

            //Unreigster the action from all patterns
            if (m_onPatternChangeAction != nullptr)
            {
                RefClassWrapper<System::Action<IPatternColumn^>> tmpact(m_onPatternChangeAction);
                if(m_patternMap != NULL)
                    m_patternMap->ForEachItem(RemovePatChangeAction, &tmpact);
                
                delete m_onPatternChangeAction;
                m_onPatternChangeAction = nullptr;
            }

            if (m_onPropChangeEventHandler != nullptr)
            {
                RefClassWrapper<PropertyChangedEventHandler> tmphdlr(m_onPropChangeEventHandler);
                if(m_patternMap != NULL)
                    m_patternMap->ForEachItem(RemovePropChangeHandler, &tmphdlr);
                
                delete m_onPropChangeEventHandler;
                m_onPropChangeEventHandler = nullptr;
            }

            if (m_patternPlayStartAction != nullptr)
            {
                RefClassWrapper<System::Action<IPattern^>> tmphdlr(m_patternPlayStartAction);
                if (m_patternMap != NULL)
                    m_patternMap->ForEachItem(RemovePatPlayStartHandler, &tmphdlr);

                delete m_patternPlayStartAction;
                m_patternPlayStartAction = nullptr;
            }

            if (m_patternPlayEndAction != nullptr)
            {
                RefClassWrapper<System::Action<IPattern^>> tmphdlr(m_patternPlayEndAction);
                if (m_patternMap != NULL)
                    m_patternMap->ForEachItem(RemovePatPlayEndHandler, &tmphdlr);

                delete m_patternPlayEndAction;
                m_patternPlayEndAction = nullptr;
            }

            if (m_patternPlayPosChangeAction != nullptr)
            {
                RefClassWrapper<System::Action<IPattern^>> tmphdlr(m_patternPlayPosChangeAction);
                if (m_patternMap != NULL)
                    m_patternMap->ForEachItem(RemovePatPlayPosChangeHandler, &tmphdlr);

                delete m_patternPlayPosChangeAction;
                m_patternPlayPosChangeAction = nullptr;
            }

            if (m_patternPlayEndAction != nullptr)
            {
                delete m_patternPlayEndAction;
                m_patternPlayEndAction = nullptr;
            }

            if (m_patternPlayPosChangeAction != nullptr)
            {
                delete m_patternPlayPosChangeAction;
                m_patternPlayPosChangeAction = nullptr;
            }

            if (m_patternMap != NULL)
            {
                m_patternMap->Release();
                delete m_patternMap;
                m_patternMap = NULL;
            }
        }

        
        typedef struct EnumPatternsByMachineData
        {
            std::vector<uint64_t> ids;
            std::vector<CPattern*> cpats;
            std::vector<RefClassWrapper<IPattern>> rebuzzPats;
            uint64_t rebuzzMachineId;
        };

        static void RemovePatternsByMachineEnumProc(uint64_t id, IPattern^ pat,  CPattern * buzzPat,  CPatternData* patdata, void* param)
        {
            EnumPatternsByMachineData* cbdata = reinterpret_cast<EnumPatternsByMachineData*>(param);
            
            //Get the machine id
            int64_t machId = pat->Machine->CMachinePtr.ToInt64();
            if (machId == cbdata->rebuzzMachineId)
            {
                cbdata->ids.push_back(id);
                cbdata->rebuzzPats.push_back(pat);
                cbdata->cpats.push_back(buzzPat);
            }
        }

        void PatternManager::RemovePatternsByMachine(IMachine^ rebuzzmac)
        {
            EnumPatternsByMachineData enumData;
            enumData.rebuzzMachineId = rebuzzmac->CMachinePtr.ToInt64();
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                //Get list of patterns to remove, these are patterns associated with the passed in machine            
                m_patternMap->ForEachItem(RemovePatternsByMachineEnumProc, &enumData);
            }

            //Call the remove callbacks, so that things are notified before the pattern info is removed
            if (m_onPatternRemovedCallback != nullptr)
            {
                int idx = 0;
                for (const auto& patid : enumData.ids)
                {
                    m_onPatternRemovedCallback(patid, enumData.rebuzzPats[idx].GetRef(), enumData.cpats[idx], PatternEventFlags_All);
                    ++idx;
                }
            }

            //Unregister the pattern added and removed events from the machine
            int64_t machineId = rebuzzmac->CMachinePtr.ToInt64();
            if (m_eventHandlersAddedToMachines->find(machineId) != m_eventHandlersAddedToMachines->end())
            {
                //Remove pattern added event handler
                rebuzzmac->PatternAdded -= m_patternAddedAction;
                rebuzzmac->PatternRemoved -= m_patternRemovedAction;

                //Remove the entries that tell us that a pattern added event has been added.
                m_eventHandlersAddedToMachines->erase(machineId);
                for (int x = 0; x < m_machinesEventHandlersAddedTo->size(); ++x)
                {
                    if ((*m_machinesEventHandlersAddedTo)[x].GetRef()->CMachinePtr.ToInt64() == machineId)
                    {
                        m_machinesEventHandlersAddedTo->erase(m_machinesEventHandlersAddedTo->begin() + x);
                        break;
                    }
                }
            }

            //Remove patterns
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                PatternCreateCallbackData* createCallbackData = reinterpret_cast<PatternCreateCallbackData*>(m_patternCallbackData);
                for (const auto& patid : enumData.ids)
                {
                    //Unregister event handlers
                    CPattern* buzzpat = m_patternMap->GetBuzzTypeById(patid);
                    if (buzzpat != NULL)
                    {
                        IPattern^ rebuzzPat = m_patternMap->GetReBuzzTypeByBuzzType(buzzpat);
                        if (rebuzzPat != nullptr)
                        {
                            RefClassWrapper<PropertyChangedEventHandler> tmphdlr(m_onPropChangeEventHandler);
                            RemovePropChangeHandler(patid, rebuzzPat, buzzpat, (CPatternData*)buzzpat, &tmphdlr);

                            RefClassWrapper<System::Action<IPatternColumn^>> tmphdlr2(m_onPatternChangeAction);
                            RemovePatChangeAction(patid, rebuzzPat, buzzpat, (CPatternData*)buzzpat, &tmphdlr2);

                            RefClassWrapper<System::Action<IPattern^>> tmphdlr3(m_patternPlayStartAction);
                            RemovePatPlayStartHandler(patid, rebuzzPat, buzzpat, (CPatternData*)buzzpat, &tmphdlr3);

                            RefClassWrapper<System::Action<IPattern^>> tmphdlr4(m_patternPlayEndAction);
                            RemovePatPlayEndHandler(patid, rebuzzPat, buzzpat, (CPatternData*)buzzpat, &tmphdlr4);

                            RefClassWrapper<System::Action<IPattern^>> tmphdlr5(m_patternPlayPosChangeAction);
                            RemovePatPlayPosChangeHandler(patid, rebuzzPat, buzzpat, (CPatternData*)buzzpat, &tmphdlr5);
                        }
                    }

                    m_patternMap->RemoveById(patid);
                }

                //Remove from name map as well
                createCallbackData->patternNameMap.erase(enumData.rebuzzMachineId);
            }
        }

        CPattern* PatternManager::GetPatternByName(IMachine^ rebuzzmac, const char* name)
        {
            std::lock_guard<std::mutex> lg(*m_lock);

            //We need the machine CMachineDataPtr as an int64 to use the name map
            int64_t cmachineid = rebuzzmac->CMachinePtr.ToInt64();
            
            //Get the CPattern * from the name map
            PatternCreateCallbackData* patternCallbackData = reinterpret_cast<PatternCreateCallbackData*>(m_patternCallbackData);
            const auto& foundmach = patternCallbackData->patternNameMap.find(cmachineid);
            bool foundMach = (foundmach != patternCallbackData->patternNameMap.end());
            

            if (foundMach)
            {
                const std::map<std::string, uint64_t>& patnameIdMap = (*foundmach).second;
                const auto& foundname = patnameIdMap.find(name);
                if (foundname != patnameIdMap.end())
                {
                    return m_patternMap->GetBuzzTypeById((*foundname).second);
                }
            }

            String^ clrPatName = Utils::stdStringToCLRString(name);
            bool patternCreated = false;
            CPattern* retPat = NULL;
            int64_t patId = 0;
            IPattern^ patCreated = nullptr;
            try
            {
                //Machine / Pattern do not exist. 
                for each (IPattern ^ pat in rebuzzmac->Patterns)
                {
                    if (clrPatName == pat->Name)
                    {
                        patId = pat->CPattern.ToInt64();
                        patCreated = pat;
                        patternCallbackData->patternNameMap[cmachineid][name] = patId;
                        retPat =  m_patternMap->GetOrStoreReBuzzTypeById(patId, pat, &patternCreated);
                        break;
                    }
                }
            }
            finally
            {
                delete clrPatName;
            }

            //If the pattern was created, then call the added callback
            if ((retPat != NULL) && (m_onPatternAddedCallback != nullptr) && (patCreated != nullptr))
            {
                m_onPatternAddedCallback(patId, patCreated, retPat, PatternEventFlags_All);
            }

            return NULL;
        }

        CPattern* PatternManager::GetOrStorePattern(IPattern^ p)
        {
            if (p == nullptr)
                return NULL;

            OnPatternEventDelegate^ callback = nullptr;
            std::vector<CPattern*> callbackPats;
            std::vector<RefClassWrapper<IPattern>> callbackRebuzzPats;
            CPattern* cRetPat = NULL;
            int64_t id = p->CPattern.ToInt64();
            bool itemCreated = false;
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                //Get/Store pattern
                cRetPat = m_patternMap->GetOrStoreReBuzzTypeById(id, p, &itemCreated);

                //Set up for calling any deferred callbacks outside the lock
                //The callback list gets populated when  GetOrStoreReBuzzTypeById() is called (above)
                PatternCreateCallbackData* patternCallbackData = reinterpret_cast<PatternCreateCallbackData*>(m_patternCallbackData);
                if (patternCallbackData != NULL)
                {   
                    for (const auto& cbp : patternCallbackData->callbacksRequired)
                    {
                        IPattern^ rebuzzP = m_patternMap->GetReBuzzTypeByBuzzType(cbp);
                        if (rebuzzP != nullptr)
                        {
                            callbackRebuzzPats.push_back(rebuzzP);
                            callbackPats.push_back(cbp);
                        }
                    }

                    patternCallbackData->callbacksRequired.clear();

                    callback = m_onPatternAddedCallback;
                }

                //Call the callbacks for this pattern if created
                if (itemCreated)
                {
                    callbackRebuzzPats.push_back(p);
                    callbackPats.push_back(cRetPat);
                }
            }

            //Call the callbacks, if required, outside of the lock
            if (callback != nullptr)
            {
                std::set<CPattern*> donePats;
                int idx = 0;
                for (const auto& cb : callbackPats)
                {
                    //This quickly checks to avoid duplicates callbacks for the same pattern
                    if (donePats.find(cb) == donePats.end())
                    {
                        donePats.insert(cb);
                        IPattern^ cbpat = callbackRebuzzPats[idx].GetRef();
                        callback(id, cbpat, cb, PatternEventFlags_All);
                    }

                    ++idx;
                }
            }

            return cRetPat;
        }

        IPattern^ PatternManager::GetReBuzzPattern(CPattern* pat)
        {
            if (pat == NULL)
                return nullptr;

            std::lock_guard<std::mutex> lg(*m_lock);

            return m_patternMap->GetReBuzzTypeByBuzzType(pat);
        }

        CPatternData* PatternManager::GetBuzzPatternData(CPattern* pat)
        {
            if (pat == NULL)
                return NULL;

            std::lock_guard<std::mutex> lg(*m_lock);

            return m_patternMap->GetBuzzEmulationType(pat);
        }

        IPattern^ PatternManager::GetById(int64_t id, CPattern** retbuzzpat)
        {
            CPattern* buzzpat = NULL;
            IPattern^ ret = nullptr;
            {
                std::lock_guard<std::mutex> lg(*m_lock);
                buzzpat = m_patternMap->GetBuzzTypeById(id);
                ret = m_patternMap->GetReBuzzTypeByBuzzType(buzzpat);
            }

            if (retbuzzpat != NULL)
                *retbuzzpat = buzzpat;

            return ret;
        }

        void PatternManager::OnNativePatternChange(CPattern* pat, int newLen, const char * newName )
        {
            bool notifyNativeLength = false;
            bool hasChanged = false;
            IPattern^ rebuzzPat = nullptr;
           
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                //Get Rebuzz pattern
                rebuzzPat = m_patternMap->GetReBuzzTypeByBuzzType(pat);
            }
            
            //Update info in ReBuzz
            if ((newLen > 0) && (rebuzzPat->Length != newLen))
            {
                rebuzzPat->Length = newLen;
                hasChanged = true;
            }
            
            if (newName != NULL)
            {   
                rebuzzPat->Name =  Utils::stdStringToCLRString(newName);
                hasChanged = true;
            }

            //Notify rebuzz that patten has changed.
            //This should trigger the 'OnReBuzzPatternChange' event to fire via ReBuzz
            if(hasChanged)
                rebuzzPat->NotifyPatternChanged();
        }

        void PatternManager::ScanMachineForPatterns(IMachine^ mach)
        {
            for each (IPattern^ p in mach->Patterns )
            {
                GetOrStorePattern(p); //This uses the lock, and calls the callbacks if required
            }
        }

        void PatternManager::AddEventHandlersToMachine(IMachine^ mach)
        {
            std::lock_guard<std::mutex> lg(*m_lock);
            int64_t machid = mach->CMachinePtr.ToInt64();
            if (m_eventHandlersAddedToMachines->find(machid) == m_eventHandlersAddedToMachines->end())
            {
                mach->PatternAdded += m_patternAddedAction;
                mach->PatternRemoved += m_patternRemovedAction;

                m_eventHandlersAddedToMachines->insert(machid);
                m_machinesEventHandlersAddedTo->push_back(mach);
            }
        }

        IMachine^ PatternManager::GetEditorTargetMachine()
        {
            return m_editorTargetMachine;
        }

        void PatternManager::SetEditorTargetMachine(IMachine^ mach)
        {
            m_editorTargetMachine = mach;
        }
    }

}