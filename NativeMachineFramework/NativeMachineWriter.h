#pragma once

#include <vector>
#include <MachineInterface.h>


namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        class NativeMachineWriter : public CMachineDataOutput
        {
        public:
            NativeMachineWriter()
            {}

            void Write(void* pbuf, int const numbytes) override
            {
                const size_t pos = m_data.size();
                m_data.resize(pos + numbytes);
                memcpy(&m_data[pos], pbuf, numbytes);
            }

            const unsigned char* dataPtr() const
            {
                if (m_data.size() == 0)
                    return NULL;

                return &m_data[0];
            }

            const size_t size() const
            {
                return m_data.size();
            }

        private:
            std::vector<unsigned char> m_data;
        };        
    }
}
