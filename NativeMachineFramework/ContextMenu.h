#pragma once

using System::String;

using System::Windows::Forms::ContextMenuStrip;
using System::Windows::Forms::ToolStripMenuItem;
using System::Windows::Forms::ToolStripDropDownClosedEventHandler;
using System::Windows::Forms::ToolStripDropDownClosedEventArgs;
using System::Windows::Forms::ToolStripItemClickedEventArgs;
using System::Windows::Forms::ToolStripItemClickedEventHandler;

#include <string>
#include <vector>
#include <memory>

#include "RefClassWrapper.h"

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        public ref class ContextMenu : System::IDisposable
        {
        public:
            delegate void OnMenuItemClickDelegate(int id);

            ContextMenu();
            !ContextMenu();
            ~ContextMenu();

            void AddMenuItem(int id, const char * text, OnMenuItemClickDelegate^ clickCallback);

            void ShowAtCursor();

        private:
            void Free();
            void CleanUpItems();
            void OnMenuClose(Object^ sender, ToolStripDropDownClosedEventArgs^ args);
            void OnMenuClicked(Object^ sender, ToolStripItemClickedEventArgs^ args);

            //--------------------------------------------------------------------
        public: 
            //Needs to be public to avoid TypeAccessException
            ref class MenuItem
            {
            public:
                MenuItem(int id, const char* txt, OnMenuItemClickDelegate^ cb) : m_text(gcnew String(txt)),
                                                                                m_id(id),
                                                                                m_callback(cb),
                                                                                m_item(nullptr),
                                                                                m_menu(nullptr)
                {}

                ~MenuItem();

                void Build(ContextMenuStrip^ menu);
                void CleanUp();
                
                int GetId();
                void OnMenuItemClick();

                //-------------------------------

            private:
                String^ m_text;
                int m_id;
                OnMenuItemClickDelegate^  m_callback;
                ToolStripMenuItem^ m_item;
                ContextMenuStrip^ m_menu;
            };
        private:

            //--------------------------------------------------------------------

            ContextMenuStrip^ m_menu;
            ToolStripDropDownClosedEventHandler^ m_closeHandler;
            ToolStripItemClickedEventHandler^ m_clickHandler;
            std::vector<RefClassWrapper<MenuItem>> * m_menuItems;
        };
    }
}
