#pragma once

#include <functional>

namespace IPC
{

#pragma pack(8)

class Channel;

struct MessageBuffer
{
private:
	MessageBuffer(MessageBuffer const &x) { }
public:

	static int const MaxSize = 256 * 1024;
	typedef std::function<void (Channel *, bool)> BufferFullCallback;

	void Reset()
	{
		size = 0;
	}

	void Write(void const *pdata, int nbytes, Channel *channel, BufferFullCallback cb)
	{
		if (nbytes == 0)
		{
			cb(channel, true);	// Message Size == 0, call ReplyBufferCallback or SendBuffercallback
			return;
		}

		int offset = 0;

		while(offset < nbytes)
		{
			int n = min(nbytes - offset, MaxSize - size);
			memcpy(data + size, (byte *)pdata + offset, n);
			size += n;
			offset += n;

			if (size == MaxSize || offset == nbytes)
			{
				cb(channel, offset == nbytes);
			}
		}

	}


	int size;
	byte data[MaxSize];

};

const int AudioChannel = 0;
const int UIChannel = 1;
const int MIDIChannel = 2;
const int HostChannel = 3;

const int MaxChannels = 4;

struct ChannelSharedMem
{
	enum State { listening, call, returning, callback, sendbuffer, sendlastbuffer, replybuffer, replylastbuffer };

	DWORD serverPing;
	DWORD serverPong;

	State state;
	bool callbackMode;

	MessageBuffer msgBuffer;
};

struct SharedMem
{
	ChannelSharedMem channels[MaxChannels];
	int asdf;
};

#pragma pack()

}