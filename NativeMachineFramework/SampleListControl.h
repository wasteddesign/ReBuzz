#pragma once


using namespace System::Windows::Forms;
using namespace System::Collections::Generic;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {   
        public delegate void OnSampleListChange(int newWaveIndex);

        public ref class SampleListControl : System::IDisposable
        {
        public:
            SampleListControl(MachineWrapper^ machWrapper);
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
            void OnSelectedWaveChange(int x);

            void PositionControls();

            void PopulateControl();

            AttachCallback^ m_onAttachCallback;
            DetatchCallback^ m_onDetachCallback;
            SizeChangedCallback^ m_onSizeChangedCallback;
            System::EventHandler^ m_onSelectChangeCallback;
            NativeMFCMachineControl^ m_parentControl;

            MachineWrapper^ m_machineWrapper;
            Label^ m_labelControl;
            ComboBox^ m_comboControl;
            System::Drawing::Font^ m_newFont;
            Dictionary<int, BuzzGUI::Interfaces::IWave^>^ m_waves;
            NativeMachineFramework::OnSelectedWaveChange^ m_onSelectedWaveChangeCallback;
        };
    }
}