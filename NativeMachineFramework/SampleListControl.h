#pragma once

#include "WaveManager.h"
#include "NativeMFCMachineControl.h"

using namespace System::Windows::Forms;
using namespace System::Collections::Generic;


namespace ReBuzz
{
    namespace NativeMachineFramework
    {   
        public ref class SampleListControl : System::IDisposable
        {
        public:
            delegate void OnSampleListChange(int newWaveIndex);

            SampleListControl(WaveManager^ waveManager);
            !SampleListControl();
            ~SampleListControl();

            void Release();

            UserControl^ GetControl();

            void SetNewParent(void * hwndNewParent);

            int GetPreferredWidth();

            void SetFont(void * font);

        private:
            IntPtr OnAttach(IntPtr hwnd, void* param);
            void OnDetach(IntPtr hwnd, void* param);
            void OnSizeChanged(IntPtr hwnd, void* param, int left, int top, int width, int height);
            void OnSelectChanged(Object^ sender, System::EventArgs^ args);
            void OnSelectedWaveChange(IWave^ oldSelected , IWave^ newSelected);

            void PositionControls();

            void PopulateControl();

            NativeMFCMachineControl::AttachCallback^ m_onAttachCallback;
            NativeMFCMachineControl::DetatchCallback^ m_onDetachCallback;
            NativeMFCMachineControl::SizeChangedCallback^ m_onSizeChangedCallback;
            System::EventHandler^ m_onSelectChangeCallback;
            NativeMFCMachineControl^ m_parentControl;

            WaveManager^ m_waveManager;
            Label^ m_labelControl;
            ComboBox^ m_comboControl;
            System::Drawing::Font^ m_newFont;
            Dictionary<int, BuzzGUI::Interfaces::IWave^>^ m_waves;
            WaveManager::OnSelectedWaveChange^ m_onSelectedWaveChangeCallback;
        };
    }
}