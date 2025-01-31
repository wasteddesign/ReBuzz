#pragma once

#include <mutex>
#include <map>
#include <MachineInterface.h>
#include "RebuzzBuzzLookup.h"

using BuzzGUI::Interfaces::IWave;
using BuzzGUI::Interfaces::IWaveLayer;
using System::Collections::Generic::List;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        public ref class WaveManager : System::IDisposable
        {
        public:
            delegate void OnSelectedWaveChange(IWave^ oldSelected, IWave^ newSelected);

            WaveManager();
            ~WaveManager();
            !WaveManager();

            void SetSelectedWave(IWave^ wave);
            IWave^ GetSelectedWave();

            void AddSelectedWaveChangeCallback(OnSelectedWaveChange^ callback);
            void RemoveSelectedWaveChangeCallback(OnSelectedWaveChange^ callback);

            IWave^ FindWaveByOneIndex(int oneIndex);

            IWave^ FindWaveByZeroIndex(int zeroIndex);

            CWaveLevel* GetWaveLevelFromLayer(IWaveLayer^ layer);

            IWaveLayer^ GetLayerFromWaveLevel(CWaveLevel* level);

            CWaveInfo* GetWaveInfo(IWave^ wave);

        private:
            void Free();

            std::mutex* m_lock;
            std::map<int, CWaveInfo>* m_waveInfosZeroIndex;
            RebuzzBuzzLookup<IWaveLayer, CWaveLevel, CWaveLevel>* m_waveLevelMap;
            RebuzzBuzzLookup<IWave, CWaveInfo, CWaveInfo>* m_waveInfoMap;
            void* m_waveLevelMapCallbackData;

            IWave^ m_selectedWave;
            List< OnSelectedWaveChange^>^ m_onSelectedWaveChangeCallbacks;
        };
    }
}