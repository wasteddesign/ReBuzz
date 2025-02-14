

#include <Windows.h>
#include <CommCtrl.h>

#include <MachineInterface.h>
#include "BuzzDataTypes.h"
#include "MachineWrapper.h"
#include "NativeMFCMachineControl.h"
#include "SampleListControl.h"
#include "WaveManager.h"

using BuzzGUI::Common::Global;
using BuzzGUI::Interfaces::IWave;
using System::Collections::Generic::KeyValuePair;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {

        SampleListControl::SampleListControl(WaveManager ^ wavMgr)
        {
            m_waveManager = wavMgr;
            m_waves = gcnew Dictionary<int, BuzzGUI::Interfaces::IWave^>();
            m_onAttachCallback = gcnew NativeMFCMachineControl::AttachCallback(this, &SampleListControl::OnAttach);
            m_onDetachCallback = gcnew NativeMFCMachineControl::DetatchCallback(this, &SampleListControl::OnDetach);
            m_onSizeChangedCallback = gcnew NativeMFCMachineControl::SizeChangedCallback(this, &SampleListControl::OnSizeChanged);
            m_parentControl = gcnew NativeMFCMachineControl(m_onAttachCallback, m_onDetachCallback, m_onSizeChangedCallback, NULL);
            
            m_onSelectChangeCallback = gcnew System::EventHandler(this, &SampleListControl::OnSelectChanged);

            m_onSelectedWaveChangeCallback = nullptr;

            m_onWaveChangedCallback = gcnew System::Action<int>(this, &SampleListControl::OnWaveTableChange);
            Global::Buzz->Song->Wavetable->WaveChanged += m_onWaveChangedCallback;

            m_labelControl = nullptr;
            m_comboControl = nullptr;
            m_newFont = nullptr;
        }

        SampleListControl::~SampleListControl()
        {
            Release();
        }

        SampleListControl::!SampleListControl()
        {
            Release();
        }

        void SampleListControl::Release()
        {
            if (m_onWaveChangedCallback != nullptr)
            {
                Global::Buzz->Song->Wavetable->WaveChanged -= m_onWaveChangedCallback;
                delete m_onWaveChangedCallback;
                m_onWaveChangedCallback = nullptr;
            }

            if (m_labelControl != nullptr)
            {
                if (m_parentControl != nullptr)
                {
                    m_parentControl->Controls->Remove(m_labelControl);
                }

                delete m_labelControl;
                m_labelControl = nullptr;
            }

            if (m_comboControl != nullptr)
            {
                if (m_parentControl != nullptr)
                {
                    m_parentControl->Controls->Remove(m_comboControl);
                }

                if (m_onSelectChangeCallback != nullptr)
                {
                    m_comboControl->SelectedIndexChanged -= m_onSelectChangeCallback;
                }

                delete m_comboControl;
                m_comboControl = nullptr;
            }
            
            if (m_onSelectedWaveChangeCallback != nullptr)
            {
                m_waveManager->RemoveSelectedWaveChangeCallback(m_onSelectedWaveChangeCallback);
                delete m_onSelectedWaveChangeCallback;
                m_onSelectedWaveChangeCallback = nullptr;
            }

            if (m_onSelectChangeCallback != nullptr)
            {
                delete m_onSelectChangeCallback;
                m_onSelectChangeCallback = nullptr;
            }

            if (m_parentControl != nullptr)
            {
                delete m_parentControl;
                m_parentControl = nullptr;
            }

            if (m_onAttachCallback != nullptr)
            {
                delete m_onAttachCallback;
                m_onAttachCallback = nullptr;
            }

            if (m_onDetachCallback != nullptr)
            {
                delete m_onDetachCallback;
                m_onDetachCallback = nullptr;
            }

            if (m_onSizeChangedCallback != nullptr)
            {
                delete m_onSizeChangedCallback;
                m_onSizeChangedCallback = nullptr;
            }

            if (m_waves != nullptr)
            {
                delete m_waves;
                m_waves = nullptr;
            }
        }

        UserControl^ SampleListControl::GetControl()
        {
            return m_parentControl;
        }

        void SampleListControl::SetNewParent(void* hwndNewParent)
        {
            SetParent((HWND)m_parentControl->Handle.ToPointer(), (HWND)hwndNewParent);
            PositionControls();
        }

        void SampleListControl::SetFont(void * font)
        {
            IntPtr hfontPtr(font);

            if ((m_comboControl == nullptr) || (m_labelControl == nullptr))
            {
                //Save for later, when the controls are created
                m_newFont = System::Drawing::Font::FromHfont(hfontPtr);
                return;
            }

            
            System::Drawing::Font^ newFont = System::Drawing::Font::FromHfont(hfontPtr);
            m_labelControl->Font = newFont;
            m_comboControl->Font = newFont;

            PositionControls();
        }

        int SampleListControl::GetPreferredWidth()
        {
            if ((m_labelControl == nullptr) || (m_comboControl == nullptr))
            {
                return 0;
            }

            System::Drawing::Size labelTextSize = System::Windows::Forms::TextRenderer::MeasureText(m_labelControl->Text, m_labelControl->Font);

            int comboWidth = 0;
            for each (System::String^ x in m_comboControl->Items)
            {
                System::Drawing::Size itemSize = System::Windows::Forms::TextRenderer::MeasureText(x, m_comboControl->Font);
                if (itemSize.Width > comboWidth) 
                {
                    comboWidth = itemSize.Width;
                }
            }

            //Add 10 pixels as extra width
            return labelTextSize.Width + comboWidth + SystemInformation::VerticalScrollBarWidth + 10;
        }

        IntPtr SampleListControl::OnAttach(IntPtr hwnd, void* param)
        {
            
            //Create label
            m_labelControl = gcnew Label();
            m_labelControl->Text = gcnew System::String("Wave:");
            m_parentControl->Controls->Add(m_labelControl);

            if (m_newFont != nullptr)
            {
                m_labelControl->Font = m_newFont;
            }

            //Create combo
            m_comboControl = gcnew ComboBox();
            m_comboControl->DropDownStyle = System::Windows::Forms::ComboBoxStyle::DropDownList;
            m_comboControl->SelectedIndexChanged += m_onSelectChangeCallback;
            m_parentControl->Controls->Add(m_comboControl);
            
            if (m_newFont != nullptr)
            {
                m_comboControl->Font = m_newFont;
            }

            PopulateControl();
            PositionControls();

            //Get notified of wave selection changes
            m_onSelectedWaveChangeCallback = gcnew WaveManager::OnSelectedWaveChange(this, &SampleListControl::OnSelectedWaveChange);
            m_waveManager->AddSelectedWaveChangeCallback(m_onSelectedWaveChangeCallback);

            return m_comboControl->Handle;
        }

        void SampleListControl::OnDetach(IntPtr hwnd, void* param)
        {
            if (m_labelControl != nullptr)
            {
                if (m_parentControl != nullptr)
                {
                    m_parentControl->Controls->Remove(m_labelControl);
                }

                delete m_labelControl;
                m_labelControl = nullptr;
            }

            if (m_comboControl != nullptr)
            {
                if (m_parentControl != nullptr)
                {
                    m_parentControl->Controls->Remove(m_comboControl);
                }

                delete m_comboControl;
                m_comboControl = nullptr;
            }

            if (m_onSelectedWaveChangeCallback != nullptr)
            {
                m_waveManager->RemoveSelectedWaveChangeCallback(m_onSelectedWaveChangeCallback);
                delete m_onSelectedWaveChangeCallback;
                m_onSelectedWaveChangeCallback = nullptr;
            }
        }

        void SampleListControl::OnSizeChanged(IntPtr hwnd, void* param, int left, int top, int width, int height)
        {
            PositionControls();
        }

        void SampleListControl::OnSelectChanged(Object^ sender, System::EventArgs^ args)
        {
            if ((m_waveManager == nullptr) || (m_comboControl == nullptr) || (m_waves == nullptr))
            {
                return;
            }

            IWave^ currentWav = m_waveManager->GetSelectedWave();
            int selectedWavIndex = m_comboControl->SelectedIndex;
            if (!m_waves->ContainsKey(selectedWavIndex))
            {
                return;
            }
            
            IWave^ newSelectedWav = m_waves[selectedWavIndex];
            if(currentWav != newSelectedWav)
            {
                m_waveManager->SetSelectedWave(newSelectedWav);
            }
        }

        void SampleListControl::OnSelectedWaveChange(IWave^ oldSelected, IWave^ newSelected)
        {
            if((newSelected != nullptr) && (m_waves != nullptr))
            {
                for each (KeyValuePair<int, IWave^>^ x in m_waves)
                {
                    if (x->Value == newSelected)
                    {
                        m_comboControl->SelectedIndex = x->Key;
                        return;
                    }
                }
            }
        }

        void SampleListControl::PositionControls()
        {
            if ((m_parentControl != nullptr) && (m_labelControl != nullptr) && (m_comboControl != nullptr))
            {
                System::Drawing::Size labelTextSize = System::Windows::Forms::TextRenderer::MeasureText(m_labelControl->Text, m_labelControl->Font);

                int parentwidth = m_parentControl->Width;
                int parentheight = m_parentControl->Height;
                
                m_labelControl->Left = 0;
                m_labelControl->Height = labelTextSize.Height;
                m_labelControl->Width = labelTextSize.Width;
                m_labelControl->Top = 0;
                
                
                m_comboControl->Left = m_labelControl->Right;
                m_comboControl->Top = 0;
                m_comboControl->Height = parentheight;
                m_comboControl->Width = parentwidth - m_comboControl->Left;
            }
        }

        void SampleListControl::PopulateControl()
        {
            if((m_comboControl == nullptr) || (m_waves == nullptr))
            {
                return;
            }

            IWave^ currentWav = m_waveManager->GetSelectedWave();
            int selectedWavIdx = m_comboControl->SelectedIndex;

            m_comboControl->Items->Clear();
            m_waves->Clear();

            if ((Global::Buzz->Song == nullptr) || (Global::Buzz->Song->Wavetable == nullptr))
            {
                return;
            }

            for each (IWave^ wave in Global::Buzz->Song->Wavetable->Waves)
            {
                if (wave != nullptr)
                {
                    System::String^ text = (wave->Index + 1).ToString();
                    text += " : " + wave->Name;

                    if ((currentWav != nullptr) && (wave == currentWav))
                    {
                        selectedWavIdx = m_comboControl->Items->Count;
                    }

                    int index = m_comboControl->Items->Add(text);
                    m_waves->Add(index, wave);
                }
            }

            if (selectedWavIdx < 0)
            {
                //Just pick the first one
                selectedWavIdx = 0;
            }

            if ((selectedWavIdx >= 0) && (selectedWavIdx < m_comboControl->Items->Count))
            {
                m_comboControl->SelectedIndex = selectedWavIdx;
            }
        }

        void SampleListControl::OnWaveTableChange(int x)
        {
            //Repopulate control
            PopulateControl();
        }

        
    }
}