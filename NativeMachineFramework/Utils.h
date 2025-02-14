#pragma once

#include <string>

using System::String;


namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        class Utils
        {
        public:
            
            static void CLRStringToStdString(String^ str, std::string& out);

            static String^ stdStringToCLRString(const std::string& str);

            static int64_t ObjectToInt64(System::Object^ obj);

            static void RemoveObjectInt64(System::Object^ obj);
        };
    }
}
