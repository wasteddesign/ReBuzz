
#include "Utils.h"

using namespace System;
using  System::Runtime::InteropServices::Marshal;
using System::Collections::Generic::Dictionary;
using System::Threading::Monitor;


namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        private ref class UtilObjStore
        {
        public:
            static Object^ s_lock =  gcnew Object();
            static int64_t s_next_id = System::DateTime::UtcNow.ToFileTimeUtc() & 0x7FFFFFFFFFFFFF00LL;
            static Dictionary<Object^, int64_t>^ s_object_ids = gcnew Dictionary<Object^, int64_t>();
        };

        void Utils::CLRStringToStdString(String^ str, std::string& out)
        {
            using namespace Runtime::InteropServices;
            const char* chars = (const char*)(Marshal::StringToHGlobalAnsi(str)).ToPointer();
            out = chars;
            Marshal::FreeHGlobal(IntPtr((void*)chars));
        }

        String^ Utils::stdStringToCLRString(const std::string& str)
        {
            return gcnew String(str.c_str());
        }

        int64_t Utils::ObjectToInt64(Object^ obj)
        {
            int64_t ret = -1;
            Monitor::Enter(UtilObjStore::s_lock);
            try
            {
                if (UtilObjStore::s_object_ids->ContainsKey(obj))
                {
                    ret = UtilObjStore::s_object_ids[obj];
                }
                else
                {
                    ret = UtilObjStore::s_next_id;
                    UtilObjStore::s_next_id += 16;
                    UtilObjStore::s_object_ids->Add(obj, ret);
                }
            }
            finally
            {
                Monitor::Exit(UtilObjStore::s_lock);
            }

            return ret;
        }

        void Utils::RemoveObjectInt64(Object^ obj)
        {
            Monitor::Enter(UtilObjStore::s_lock);
            try
            {
                if (UtilObjStore::s_object_ids->ContainsKey(obj))
                {
                    UtilObjStore::s_object_ids->Remove(obj);
                }
            }
            finally
            {
                Monitor::Exit(UtilObjStore::s_lock);
            }
        }
    }
}