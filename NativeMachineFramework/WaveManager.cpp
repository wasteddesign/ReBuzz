#include "WaveManager.h"
#include "Utils.h"

using System::Collections::Generic::List;
using BuzzGUI::Common::Global;
using BuzzGUI::Interfaces::IWaveLayer;
using BuzzGUI::Interfaces::WaveFlags;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        struct WaveLevelCallbackData
        {
            RebuzzBuzzLookup<IWaveLayer, CWaveLevel, CWaveLevel>* m_waveLevelMap;
            RebuzzBuzzLookup<IWave, CWaveInfo, CWaveInfo>* m_waveInfoMap;
            RefClassWrapper<WaveManager> m_waveMgr;
        };

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
            WaveLevelCallbackData* cbdata = reinterpret_cast<WaveLevelCallbackData*>(param);
            CWaveLevel* waveLev = reinterpret_cast<CWaveLevel*>(item);
            IWaveLayer^ wavelayer = cbdata->m_waveLevelMap->GetReBuzzTypeByBuzzType(waveLev);
            if (wavelayer != nullptr)
            {
                updateWaveLevel(waveLev, wavelayer);
            }
        }

        static void  OnNewBuzzWaveInfo(void* item, void* param)
        {
            WaveLevelCallbackData* cbdata = reinterpret_cast<WaveLevelCallbackData*>(param);
            CWaveInfo* wavinfo = reinterpret_cast<CWaveInfo *>(item);
            IWave^ rebuzzWave = cbdata->m_waveInfoMap->GetReBuzzTypeByBuzzType(wavinfo);
            if (rebuzzWave != nullptr)
            {
                wavinfo->Flags = ((rebuzzWave->Flags & WaveFlags::Loop) == WaveFlags::Loop) ? WF_LOOP : 0;
                wavinfo->Flags |= ((rebuzzWave->Flags & WaveFlags::BidirectionalLoop) == WaveFlags::BidirectionalLoop) ? WF_BIDIR_LOOP : 0;
                wavinfo->Flags |= ((rebuzzWave->Flags & WaveFlags::Not16Bit) == WaveFlags::Not16Bit) ? WF_NOT16BIT : 0;
                wavinfo->Flags |= ((rebuzzWave->Flags & WaveFlags::Stereo) == WaveFlags::Stereo) ? WF_STEREO : 0;
                wavinfo->Volume = rebuzzWave->Volume;
            }
        }

        

        WaveManager::WaveManager()
        {
            m_lock = new std::mutex();
            m_waveInfosZeroIndex = new std::map<int, CWaveInfo>();
            m_selectedWave = nullptr;
            m_onSelectedWaveChangeCallbacks = gcnew List< OnSelectedWaveChange^>();
            
            WaveLevelCallbackData * callbackdata = new WaveLevelCallbackData();
            m_waveLevelMapCallbackData = callbackdata;
            m_waveLevelMap = new RebuzzBuzzLookup<IWaveLayer, CWaveLevel, CWaveLevel>(OnNewBuzzWaveLevel, m_waveLevelMapCallbackData);            
            m_waveInfoMap = new RebuzzBuzzLookup<IWave, CWaveInfo, CWaveInfo>(OnNewBuzzWaveInfo, m_waveLevelMapCallbackData);
            callbackdata->m_waveLevelMap = m_waveLevelMap;
            callbackdata->m_waveInfoMap = m_waveInfoMap;
            callbackdata->m_waveMgr.Assign(this);
        }

        WaveManager::~WaveManager()
        {
            Free();
        }

        WaveManager::!WaveManager()
        {
            Free();
        }

        void WaveManager::Free()
        {
            if (m_onSelectedWaveChangeCallbacks != nullptr)
            {
                delete m_onSelectedWaveChangeCallbacks;
                m_onSelectedWaveChangeCallbacks = nullptr;
            }

            if (m_waveInfosZeroIndex != NULL)
            {
                delete m_waveInfosZeroIndex;
                m_waveInfosZeroIndex = NULL;
            }

            if (m_waveLevelMap != NULL)
            {
                delete m_waveLevelMap;
                m_waveLevelMap = NULL;
            }

            if (m_waveInfoMap != NULL)
            {
                delete m_waveInfoMap;
                m_waveInfoMap = NULL;
            }

            if (m_waveLevelMapCallbackData != NULL)
            {
                WaveLevelCallbackData* cbdata = reinterpret_cast<WaveLevelCallbackData*>(m_waveLevelMapCallbackData);
                delete cbdata;
                m_waveLevelMapCallbackData = NULL;
            }

            if (m_lock != NULL)
            {
                delete m_lock;
                m_lock = NULL;
            }
        }

        void WaveManager::SetSelectedWave(IWave^ wave)
        {
            cli::array< OnSelectedWaveChange^>^ callbacks = nullptr;
            IWave^ oldWave = nullptr;
            
            {
                std::lock_guard<std::mutex> lg(*m_lock);
             
                oldWave = m_selectedWave;
                m_selectedWave = wave;

                if((oldWave != wave) &&   (m_onSelectedWaveChangeCallbacks != nullptr))
                    callbacks = m_onSelectedWaveChangeCallbacks->ToArray();
            }

            if (callbacks != nullptr)
            {
                for each (OnSelectedWaveChange ^ cb in callbacks)
                {
                    try
                    {
                        cb(oldWave, wave);
                    }
                    catch (...)
                    {}
                }
            }
        }

        IWave^ WaveManager::GetSelectedWave()
        {
            IWave^ ret = nullptr;
            {
                std::lock_guard<std::mutex> lg(*m_lock);
                ret = m_selectedWave;
            }

            return ret;
        }



        void WaveManager::AddSelectedWaveChangeCallback(OnSelectedWaveChange^ callback)
        {
            std::lock_guard<std::mutex> lg(*m_lock);

            if(m_onSelectedWaveChangeCallbacks != nullptr)
                m_onSelectedWaveChangeCallbacks->Add(callback);
        }

        void WaveManager::RemoveSelectedWaveChangeCallback(OnSelectedWaveChange^ callback)
        {
            std::lock_guard<std::mutex> lg(*m_lock);

            if (m_onSelectedWaveChangeCallbacks != nullptr)
                m_onSelectedWaveChangeCallbacks->Remove(callback);
        }

        IWave^ WaveManager::FindWaveByOneIndex(int oneIndex)
        {
            if (oneIndex < 1)
                return nullptr;

            return FindWaveByZeroIndex(oneIndex - 1);
        }

        IWave^ WaveManager::FindWaveByZeroIndex(int zeroIndex)
        {
            if (Global::Buzz->Song == nullptr)
                return nullptr;

            if (Global::Buzz->Song->Wavetable == nullptr)
                return nullptr;

            if (Global::Buzz->Song->Wavetable->Waves == nullptr)
                return nullptr;

            for each (IWave^ w in Global::Buzz->Song->Wavetable->Waves)
            {
                if((w != nullptr) && (w->Index == zeroIndex))
                {
                    return w;
                }
            }

            return nullptr;
        }

        CWaveLevel* WaveManager::GetWaveLevelFromLayer(IWaveLayer^ layer)
        {
            if (layer == nullptr)
                return NULL;

            CWaveLevel* ret = NULL;
            {
                std::lock_guard<std::mutex> lg(*m_lock);

                int64_t id = Utils::ObjectToInt64(layer);
                bool created = false;
                ret = m_waveLevelMap->GetOrStoreReBuzzTypeById(id, layer, &created);
            }

            updateWaveLevel(ret, layer);
            return ret;
        }

        IWaveLayer^ WaveManager::GetLayerFromWaveLevel(CWaveLevel* level)
        {
            IWaveLayer^ ret = nullptr;
            {
                std::lock_guard<std::mutex> lg(*m_lock);
                ret = m_waveLevelMap->GetReBuzzTypeByBuzzType(level);
            }

            return ret;
        }

        CWaveInfo* WaveManager::GetWaveInfo(IWave^ wave)
        {
            CWaveInfo* ret = NULL;
            {
                std::lock_guard<std::mutex> lg(*m_lock);
                int64_t id = Utils::ObjectToInt64(wave);
                bool created = false;
                ret = m_waveInfoMap->GetOrStoreReBuzzTypeById(id, wave, &created);
            }

            return ret;
        }
    }
}