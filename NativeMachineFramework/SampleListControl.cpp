

#include <Windows.h>
#include <CommCtrl.h>

#include <MachineInterface.h>
#include "BuzzDataTypes.h"
#include "MachineWrapper.h"
#include "NativeMFCMachineControl.h"
#include "SampleListControl.h"

using BuzzGUI::Common::Global;
using BuzzGUI::Interfaces::IWave;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {

        SampleListControl::SampleListControl(MachineWrapper^ machWrapper, 
                                             OnSampleListChange^ onListChangeCallback)
        {
            m_machineWrapper = machWrapper;
            m_waves = gcnew Dictionary<int, IWave^>();
            m_onAttachCallback = gcnew AttachCallback(this, &SampleListControl::OnAttach);
            m_onDetachCallback = gcnew DetatchCallback(this, &SampleListControl::OnDetach);
            m_onSizeChangedCallback = gcnew SizeChangedCallback(this, &SampleListControl::OnSizeChanged);
            m_parentControl = gcnew NativeMFCMachineControl(m_onAttachCallback, m_onDetachCallback, m_onSizeChangedCallback, NULL);
            
            m_onSelectChangeCallback = gcnew System::EventHandler(this, &SampleListControl::OnSelectChanged);

            m_onSelectedWaveChangeCallback = nullptr;

            m_labelControl = nullptr;
            m_comboControl = nullptr;
            m_newFont = nullptr;
            m_onListChangeCallback = onListChangeCallback;
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
                m_machineWrapper->RemoveSelectedWaveChangeCallback(m_onSelectedWaveChangeCallback);
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
            m_onSelectedWaveChangeCallback = gcnew NativeMachineFramework::OnSelectedWaveChange(this, &SampleListControl::OnSelectedWaveChange);
            m_machineWrapper->AddSelectedWaveChangeCallback(m_onSelectedWaveChangeCallback);

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
                m_machineWrapper->RemoveSelectedWaveChangeCallback(m_onSelectedWaveChangeCallback);
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
            if ((m_machineWrapper == nullptr) || (m_comboControl == nullptr))
            {
                return;
            }

            int currentWav = m_machineWrapper->GetSelectedWaveIndex();
            int selectedWavIndex = m_comboControl->SelectedIndex;
            if((m_waves != nullptr) && (m_waves->ContainsKey(selectedWavIndex)))
            {
                //Native machines use 1 based wave index value
                IWave^ selectedWav = m_waves[selectedWavIndex];
                if ((selectedWav->Index + 1) != currentWav)
                {   
                    m_machineWrapper->SetSelectedWaveIndex(selectedWav->Index + 1); //+1 because the first wave is expected to be at index 1
                    if (m_onListChangeCallback != nullptr)
                    {
                        m_onListChangeCallback(selectedWav->Index + 1);
                    }
                }
            }
        }

        void SampleListControl::OnSelectedWaveChange(int x)
        {
            if (m_waves == nullptr)
            {
                return;
            }

            IWave^ foundWav = nullptr;
            int foundIndex = -1;
            for each (auto w in m_waves)
            {
                if ((w.Value->Index + 1) == x) //We pass index + 1 to native machines
                {
                    foundIndex = w.Key;
                    foundWav = w.Value;
                    break;
                }
            }

            if (foundWav == nullptr)
            {
                return;
            }

            if (m_comboControl->SelectedIndex != foundIndex)
            {
                m_comboControl->SelectedIndex = foundIndex;
            }
        }

        void SampleListControl::PositionControls()
        {
            if ((m_parentControl != nullptr) && (m_labelControl != nullptr) && (m_comboControl != nullptr))
            {
                System::Drawing::Size labelTextSize = System::Windows::Forms::TextRenderer::MeasureText(m_labelControl->Text, m_labelControl->Font);

                int parentwidth = m_parentControl->Width;
                int parentheight = m_parentControl->Height;
                int labelheight = labelTextSize.Height;

                m_labelControl->Left = 0;
                m_labelControl->Height = labelheight;
                if (labelheight < parentheight)
                {
                    m_labelControl->Top = (parentheight - labelheight) / 2;
                }
                else
                {
                    m_labelControl->Top = 0;
                }

                int comboHieght = m_comboControl->Height;
                if (comboHieght < parentheight)
                {
                    m_comboControl->Top = (parentheight - comboHieght) / 2;
                }
                else
                {
                    m_comboControl->Top = 0;
                }

                m_comboControl->Left = m_labelControl->Right;
                m_comboControl->Top = m_labelControl->Top;
                m_comboControl->Width = parentwidth - m_comboControl->Left;
            }
        }

        void SampleListControl::PopulateControl()
        {
            if (m_comboControl == nullptr)
            {
                return;
            }

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

                    int index = m_comboControl->Items->Add(text);
                    m_waves->Add(index, wave);
                }
            } 
        }
    }
}