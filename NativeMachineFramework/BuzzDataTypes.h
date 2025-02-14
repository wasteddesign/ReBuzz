#pragma once

#include <string>
#include <memory>
#include <vector>

#include <stdio.h>

namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        struct ParamChange
        {
            int group;
            int param;
            int track;
            int value;
            bool noRecord;
        };

        struct CMachineData
        {
            CMachineData()
            {
                sprintf_s(m_sig, "CMachineData");
            }


            //This is dummy data that we return to the machine as CMachine *
            //The machine is not supposed to know what this data is, or what the values represent,
            //just that the address is a unique identifier for a specific machine.
            char m_sig[32];
            unsigned char m_machineBytes[32];
            CMachineInfo m_info;

            //CMachineInfo contains char * pointers, which must point somewhere valid
            //ReBuzz uses .NET String^, which we convert to std::string.
            //The results are stored below, and the char * pointers 
            //in the CMachineInfo points to these strings.
            std::string author; 
            std::vector<std::shared_ptr<CMachineAttribute>> attributes;
            std::vector< const CMachineAttribute*> attributePointers;
            std::vector< std::shared_ptr<std::string>> attributeNames;
            std::string commands;
            std::string name;
            std::string shortname;
            std::vector<std::shared_ptr< CMachineParameter>> parameters;
            std::vector<const CMachineParameter  *> parameterPtrs;
            std::vector< std::shared_ptr<std::string>> paramDescriptions;
            std::vector<ParamChange> paramChanges;
        };

        struct CPatternData
        {
            CPatternData()
            {
                sprintf_s(s_sig, "CPatternData");
            }

            //This is also dummy data.
            //The assumption is that the pattern machine does not care what the content
            //of a CPattern is, only that it is unique for the pattern data.
            char s_sig[32];
            unsigned char m_machineBytes[32];
            std::string name; //Storage for the pattern name
            int length;
        };

        struct CWaveLevelData
        {
            CWaveLevelData()
            {
                sprintf_s(m_sig, "CWaveLevelData");
            }


            char m_sig[32];
            std::vector<short> samples;
        };
    }
}