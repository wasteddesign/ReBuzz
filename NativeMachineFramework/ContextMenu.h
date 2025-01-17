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
        typedef void (*OnMenuItemClickCallback)(int id, void* param);
        
        public ref class ContextMenu : System::IDisposable
        {
        public:
            ContextMenu();
            !ContextMenu();
            ~ContextMenu();

            void AddMenuItem(int id, const char * text, OnMenuItemClickCallback clickCallback, void * callbackParam);

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
                MenuItem(int id, const char* txt, OnMenuItemClickCallback cb, void * p) : m_text(gcnew String(txt)),
                                                                                          m_id(id),
                                                                                          m_param(p),
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
                void* m_param;
                OnMenuItemClickCallback m_callback;
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
