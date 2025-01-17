#pragma once

#include <unordered_map>
#include <memory>
#include <mutex>
#include "RefClassWrapper.h"

#include <mutex>
#include <functional>

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        typedef void (*OnNewBuzzLookupItemCallback)(void* item, void* param);

        template<class RebuzzType, class BuzzTypeEmulation,  typename BuzzType>
        class RebuzzBuzzLookup
        {
        public:

            typedef void (*ForEachCallback)(uint64_t id, RebuzzType^ rebuzz, BuzzType* buzztype, BuzzTypeEmulation* betype, void * callbackparam);

            
            RebuzzBuzzLookup(OnNewBuzzLookupItemCallback onNewItemCallback,
                            void * callbackparam)  : m_onNewItemCallback(onNewItemCallback),
                                                     m_callbackParam(callbackparam)
            {}

            ~RebuzzBuzzLookup()
            {
                Release();
            }

            void Release()
            {
                m_idToRefMap.clear();
                m_idToDataMap.clear();
            }

            void RemoveById(uint64_t id)
            {
                auto& found = m_idToDataMap.find(id);
                if (found != m_idToDataMap.end())
                {
                    BuzzType * data = (BuzzType*)reinterpret_cast<BuzzType*>(m_idToDataMap[id].get());
                    m_buzzToIdMap.erase(data);
                }

                m_idToDataMap.erase(id);
                m_idToRefMap.erase(id);
            }

            BuzzType* GetBuzzTypeById(uint64_t id) const
            {
                BuzzType* ret = NULL;

                ret = GetBuzzTypeById_Internal(id);
                
                return ret;
            }

            RebuzzType^ GetReBuzzTypeByBuzzType(BuzzType* bt)
            {
                //Get the id
                const auto& found = m_buzzToIdMap.find(bt);
                if (found == m_buzzToIdMap.end())
                    return nullptr;

                //Get the ref
                const auto& foundref = m_idToRefMap.find((*found).second);
                if (foundref == m_idToRefMap.end())
                    return nullptr;

                return (*foundref).second.GetRef();
            }

            BuzzTypeEmulation* GetBuzzEmulationType(BuzzType* bt)
            {
                //Get the id
                const auto& found = m_buzzToIdMap.find(bt);
                if (found == m_buzzToIdMap.end())
                    return nullptr;

                //Get the take type
                const auto& founddata = m_idToDataMap.find((*found).second);
                if (founddata == m_idToDataMap.end())
                    return nullptr;

                return (*founddata).second.get();
            }

            BuzzType* GetOrStoreReBuzzTypeById(uint64_t id, RebuzzType^ buzztype, bool * pbCreated)
            {
                bool callCallback = false;
                
                BuzzType* ret = GetBuzzTypeById_Internal(id);
                if (ret == NULL)
                {
                    ret = StoreReBuzzTypeById_Internal(id, buzztype);
                    callCallback = true; //New item saved, call the callback after releasing the mutex
                    *pbCreated = true;
                }
                else
                {
                    *pbCreated = false;
                }
                
                //Call the registered callback to tell the parent that a new item has been created.
                //This allows the parent to set up the item as they wish.
                if (callCallback && (m_onNewItemCallback != NULL))
                {
                    m_onNewItemCallback(ret, m_callbackParam);
                }
                                
                return ret;
            }

            void ForEachItem(ForEachCallback func, void * param)
            {
                for (const auto& itr : m_buzzToIdMap)
                {
                    BuzzTypeEmulation* buzzemutype = NULL;

                    uint64_t id = itr.second;
                    const auto& foundemu = m_idToDataMap.find(id);
                    if (foundemu != m_idToDataMap.end())
                        buzzemutype = (*foundemu).second.get();

                    RebuzzType^ rebuzztype = nullptr;
                    const auto& foundrebuzzType = m_idToRefMap.find(id);
                    if (foundrebuzzType != m_idToRefMap.end())
                        rebuzztype = (*foundrebuzzType).second.GetRef();

                    func(itr.second, rebuzztype, itr.first, buzzemutype, param);
                }
            }

        private:
            BuzzType* GetBuzzTypeById_Internal(uint64_t id) const
            {
                BuzzType* ret = NULL;

                const auto& found = m_idToDataMap.find(id);
                if (found == m_idToDataMap.end())
                {
                    return NULL;
                }

                return reinterpret_cast<BuzzType*>((*found).second.get());
            }

            BuzzType* StoreReBuzzTypeById_Internal (uint64_t id, RebuzzType^ ref)
            {
                BuzzType* ret = NULL;

                m_idToRefMap[id] = ref;
                m_idToDataMap[id].reset(new BuzzTypeEmulation());
                    
                ret = (BuzzType*)reinterpret_cast<BuzzType*>(m_idToDataMap[id].get());
                m_buzzToIdMap[ret] = id;
                
                return ret;
            }

           


            std::unordered_map<uint64_t, RefClassWrapper<RebuzzType>> m_idToRefMap;
            std::unordered_map<uint64_t, std::shared_ptr<BuzzTypeEmulation>> m_idToDataMap;
            std::unordered_map<BuzzType*, uint64_t> m_buzzToIdMap;
            OnNewBuzzLookupItemCallback m_onNewItemCallback;
            void* m_callbackParam;
        };
    }
}
