#pragma once


#include <vector>
#include <MachineInterface.h>


namespace ReBuzz
{
    namespace NativeMachineFramework
    {
        class NativeMachineReader : public CMachineDataInput
        {
        public:
            NativeMachineReader(const unsigned char* ptr, size_t sz) : m_pos(0)
            {
                m_data.resize(sz);

                if (sz > 0)
                {
                    memcpy(&m_data[0], ptr, sz);
                }
            }

            void Read(void* pbuf, int const numbytes) override
            {
                const size_t sz = m_data.size();
                const size_t copySz = (m_pos + numbytes) > sz ? (sz - m_pos) : numbytes;
                unsigned char* destPtr = reinterpret_cast<unsigned char*>(pbuf);

                if(copySz > 0)
                    memcpy(destPtr, &m_data[m_pos], copySz);

                if (copySz < numbytes)
                    memset(destPtr + copySz, 0, numbytes - copySz);

                m_pos += copySz;
            }

        private:
            std::vector<unsigned char> m_data;
            size_t m_pos;
        };

    }
}