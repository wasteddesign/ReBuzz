#include "ContextMenu.h"

using System::String;

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        ContextMenu::MenuItem::~MenuItem()
        {
            CleanUp();
            m_menu = nullptr;
            delete m_text;
            m_text = nullptr;
        }

        void ContextMenu::MenuItem::Build(ContextMenuStrip^ menu)
        {
            if (m_item == nullptr)
            {
                
                m_item = gcnew ToolStripMenuItem(m_text);
                m_item->Tag = m_id;

                if (menu != nullptr)
                {
                    m_menu = menu;

                    //Add to menu
                    menu->Items->Add(m_item);
                }
            }
        }

        void ContextMenu::MenuItem::CleanUp()
        {  
            if (m_item != nullptr)
            {
                if (m_menu != nullptr)
                {
                    m_menu->Items->Remove(m_item);
                }

                delete m_item;
                m_item = nullptr;
            }

            m_menu = nullptr;
        }

        int ContextMenu::MenuItem::GetId()
        {
            return m_id;
        }

        void  ContextMenu::MenuItem::OnMenuItemClick()
        {
            m_callback(m_id, m_param);
        }

        //====================================================================
        //====================================================================

        ContextMenu::ContextMenu()
        {
            m_menu = gcnew ContextMenuStrip();
            m_menuItems = new std::vector<RefClassWrapper<MenuItem>>();
            m_closeHandler = gcnew ToolStripDropDownClosedEventHandler(this, &ContextMenu::OnMenuClose);
            m_menu->Closed += m_closeHandler;

            m_clickHandler = gcnew ToolStripItemClickedEventHandler(this, &ContextMenu::OnMenuClicked);
            m_menu->ItemClicked += m_clickHandler;
        }

        ContextMenu::!ContextMenu()
        {
            Free();
        }

        ContextMenu::~ContextMenu()
        {
            Free();
        }

        void ContextMenu::Free()
        {
            CleanUpItems();

            if (m_menuItems != nullptr)
            {
                for (auto& itr : *m_menuItems)
                {
                    delete itr.GetRef();
                    itr.Free();
                }
            }

            if (m_clickHandler != nullptr)
            {
                m_menu->ItemClicked -= m_clickHandler;
                delete m_clickHandler;
                m_clickHandler = nullptr;
            }

            if (m_closeHandler != nullptr)
            {
                m_menu->Closed -= m_closeHandler;
                delete m_closeHandler;
                m_closeHandler = nullptr;
            }

            
            if (m_menu != nullptr)
            {
                delete m_menu;
                m_menu = nullptr;
            }
            
            if (m_menuItems != NULL)
            {
                delete m_menuItems;
                m_menuItems = NULL;
            }
        }

        void ContextMenu::CleanUpItems()
        {
            if (m_menuItems == NULL)
                return;

            for (const auto& itr : *m_menuItems)
            {
                itr.GetRef()->CleanUp();
            }
        }

        void ContextMenu::OnMenuClose(Object^ sender, ToolStripDropDownClosedEventArgs^ args)
        {
            CleanUpItems();
        }

        void ContextMenu::OnMenuClicked(Object^ sender, ToolStripItemClickedEventArgs^ args)
        {
            for (const auto& itr : *m_menuItems)
            {
                if (itr.GetRef()->GetId() == (int)args->ClickedItem->Tag)
                {
                    itr.GetRef()->OnMenuItemClick();
                }
            }
        }

        void ContextMenu::AddMenuItem(int id, const char * text, OnMenuItemClickCallback clickCallback, void* callbackParam)
        {
            m_menuItems->push_back(gcnew MenuItem(id, text, clickCallback, callbackParam));
        }

        void ContextMenu::ShowAtCursor()
        {   
            //Build menu items and add to the menu items list
            for (const auto& itr : *m_menuItems)
            {
                itr.GetRef()->Build(m_menu); //This adds to m_menu.Items
            }

            m_menu->Show(System::Windows::Forms::Cursor::Position.X, System::Windows::Forms::Cursor::Position.Y);
        }
    }
}