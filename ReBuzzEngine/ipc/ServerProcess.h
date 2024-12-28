#pragma once

#include "ServerSharedMem.h"
#include "Message.h"

namespace IPC
{

class Job
{
public:
	Job()
	{
		hJob = ::CreateJobObject(NULL, NULL);
		JOBOBJECT_EXTENDED_LIMIT_INFORMATION jeli = { 0 };
		jeli.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
		SetInformationJobObject(hJob, JobObjectExtendedLimitInformation, &jeli, sizeof(jeli));
	}

	HANDLE GetHandle() const { return hJob; }

private:
	HANDLE hJob;

};

inline void TerminateTarget(PROCESS_INFORMATION* pi)
{
  ::CloseHandle(pi->hThread);
  ::TerminateProcess(pi->hProcess, 0);
  ::CloseHandle(pi->hProcess);
}

class Channel
{
public:
	typedef function<void (Message const &, Message &)> ReceiveCallback;

	void InitParent(SharedMem *pSharedMem, HANDLE hProcess, int index, ReceiveCallback callbackReceiveCallback)
	{
		child = false;
		this->pSharedMem = pSharedMem;
		channel = index;
		this->callbackReceiveCallback = callbackReceiveCallback;
		inReceiveCallback = false;

		const DWORD kDesiredAccess = SYNCHRONIZE | EVENT_MODIFY_STATE;

		HANDLE server_ping;
		ping = ::CreateEvent(NULL, FALSE, FALSE, NULL);
		::DuplicateHandle(::GetCurrentProcess(), ping, hProcess, &server_ping, kDesiredAccess, FALSE, 0);
		pSharedMem->channels[index].serverPing = (DWORD)server_ping;

		HANDLE server_pong;
		pong = ::CreateEvent(NULL, FALSE, FALSE, NULL);
		::DuplicateHandle(::GetCurrentProcess(), pong, hProcess, &server_pong, kDesiredAccess, FALSE, 0); 
		pSharedMem->channels[index].serverPong = (DWORD)server_pong;

		pSharedMem->channels[index].callbackMode = false;
	}

	const WCHAR* CreateIdL(char* a, const char* b)
	{
		string s = "";
		s.append(a);
		s.append(b);

		wstring ws;
		for (int i = 0; i < s.length(); ++i)
			ws += wchar_t(s[i]);

		const WCHAR* ret = ws.c_str();
		return ret;
	}

	void InitChild(SharedMem *pSharedMem, int index, const char *eventId)
	{
		child = true;
		this->pSharedMem = pSharedMem;
		channel = index;
		this->callbackReceiveCallback = NULL;
		inReceiveCallback = false;
		//ping = (HANDLE)pSharedMem->channels[index].serverPing;
		//pong = (HANDLE)pSharedMem->channels[index].serverPong;
		
		string id;
		id.append("Global\\Ping").append(eventId);
		wstring ws;
		for (int i = 0; i < id.length(); ++i)
			ws += wchar_t(id[i]);

		const WCHAR* initializedEventNamePing = ws.c_str();
		int attempt = 0;
		
		ping = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, initializedEventNamePing);

		id = "";
		id.append("Global\\Pong").append(eventId);
		ws = L"";
		for (int i = 0; i < id.length(); ++i)
			ws += wchar_t(id[i]);

		const WCHAR* initializedEventNamePong = ws.c_str();
		attempt = 0;

		pong = OpenEventW(SYNCHRONIZE | EVENT_MODIFY_STATE, FALSE, initializedEventNamePong);
	}

	const wchar_t* GetWC(const char* c)
	{
		const size_t cSize = strlen(c) + 1;
		wchar_t* wc = new wchar_t[cSize];
		mbstowcs(wc, c, cSize);

		return wc;
	}

	void DoCall()
	{
		DWORD timeout = INFINITE;

		DWORD wait = ::SignalObjectAndWait(ping, pong, timeout, FALSE);
		if (wait == WAIT_TIMEOUT)
		{
		}

	}

	void Listen(ReceiveCallback receiveCb)
	{
		this->receiveCallback = receiveCb;

		while(true)
		{
			::WaitForSingleObject(ping, INFINITE);
			HandleMessage();
		}
	}

	void ListenInThreadPool(ReceiveCallback receiveCb)
	{
		this->receiveCallback = receiveCb;

		HANDLE pool_object;
		BOOL r = ::RegisterWaitForSingleObject(&pool_object, ping, ThreadPoolCallback, this, INFINITE, WT_EXECUTEDEFAULT);
	}

	void ListenMsgLoop(ReceiveCallback receiveCb)
	{
		this->receiveCallback = receiveCb;

		MSG msg;
		while (true) 
		{
			DWORD res = MsgWaitForMultipleObjects(1, &ping, FALSE, INFINITE, QS_ALLINPUT);
			DWORD error = ::GetLastError();
			switch (res)
			{
				case WAIT_OBJECT_0:
					HandleMessage();
					break;
				
				case WAIT_OBJECT_0 + 1:
				
					if (PeekMessage(&msg, NULL, 0, 0, PM_REMOVE)) 
					{
						TranslateMessage(&msg);
						//if (msg.message != 275)
							DispatchMessage(&msg);
					}
					break;
				
				default:
					return;
			}
		}
	}


	static void CALLBACK ThreadPoolCallback(PVOID lpParameter, BOOLEAN TimerOrWaitFired)
	{
		Channel *pchn = (Channel *)lpParameter;
		pchn->HandleMessage();
	}

	void HandleMessage()
	{
		MessageBuffer &mb = pSharedMem->channels[channel].msgBuffer;

		switch(pSharedMem->channels[channel].state)
		{
		case ChannelSharedMem::sendbuffer:
			recvMsg.Write(mb.data, mb.size);		// Append message from memory mapped file. There is going to be another message after this
			mb.Reset();
			::SetEvent(pong);						// Send response
			break;
		case ChannelSharedMem::sendlastbuffer:
			{
				recvMsg.Write(mb.data, mb.size);	// Append message from memory mapped file.
				mb.Reset();							// Reset memory mapped file 

				Message reply;
				inReceiveCallback = true;
				receiveCallback(recvMsg, reply);	// Process received message and get reply
				inReceiveCallback = false;
				Reply(reply);

				recvMsg.Reset();
			}
			break;
		}

	}


	void Send(Message const &msg, Message &reply)
	{
		assert(!pSharedMem->channels[channel].callbackMode);

		pReply = &reply;

		MessageBuffer &mb = pSharedMem->channels[channel].msgBuffer;

		mb.Reset();
		mb.Write(msg.GetData(), msg.GetSize(), this, &IPC::Channel::SendBufferCallback);
	}

	void Reply(Message const &msg)
	{
		assert(!pSharedMem->channels[channel].callbackMode);

		MessageBuffer &mb = pSharedMem->channels[channel].msgBuffer;

		mb.Reset();
		mb.Write(msg.GetData(), msg.GetSize(), this, &Channel::ReplyBufferCallback);

	}

	void Callback(Message const &msg, Message &reply)
	{
		assert(inReceiveCallback);
		assert(!pSharedMem->channels[channel].callbackMode);

		pCallbackReply = &reply;
		pSharedMem->channels[channel].callbackMode = true;

		MessageBuffer &mb = pSharedMem->channels[channel].msgBuffer;

		mb.Reset();
		mb.Write(msg.GetData(), msg.GetSize(), this, &Channel::CallbackCallback);
	}

	bool IsInReceiveCallback() const { return inReceiveCallback; }

private:
	static void SendBufferCallback(Channel *pchn, bool done)
	{
		pchn->SendBufferCallbackImpl(done);
	}
	
	void SendBufferCallbackImpl(bool done)
	{
		pSharedMem->channels[channel].state = done ? ChannelSharedMem::sendlastbuffer : ChannelSharedMem::sendbuffer;
		DoCall();

		MessageBuffer &mb = pSharedMem->channels[channel].msgBuffer;

		if (done)
		{
			pReply->Reset();

			while(true)
			{
				pReply->Write(mb.data, mb.size);
				mb.Reset();
				
				if (pSharedMem->channels[channel].state == ChannelSharedMem::replylastbuffer)
				{
					if (pSharedMem->channels[channel].callbackMode)
					{
						assert(callbackReceiveCallback != NULL);

						Message callbackReply;
						callbackReceiveCallback(*pReply, callbackReply);
						mb.Write(callbackReply.GetData(), callbackReply.GetSize(), this, &Channel::SendBufferCallback);
					}

					break;
				}

				DoCall();
			}

		}
	}

	static void ReplyBufferCallback(Channel *pchn, bool done)
	{
		pchn->ReplyBufferCallbackImpl(done);
	}

	void ReplyBufferCallbackImpl(bool done)
	{
		pSharedMem->channels[channel].state = done ? ChannelSharedMem::replylastbuffer : ChannelSharedMem::replybuffer;
		if (done)
		{
			::SetEvent(pong);		// Reply sent
		}
		else
		{
			DWORD wait = ::SignalObjectAndWait(pong, ping, INFINITE, FALSE);	// Whole reply is not sent
		}
	}

	static void CallbackCallback(Channel *pchn, bool done)
	{
		pchn->CallbackCallbackImpl(done);
	}

	void CallbackCallbackImpl(bool done)
	{
		assert(pSharedMem->channels[channel].callbackMode);
		pSharedMem->channels[channel].state = done ? ChannelSharedMem::replylastbuffer : ChannelSharedMem::replybuffer;
		::SignalObjectAndWait(pong, ping, INFINITE, FALSE);

		MessageBuffer &mb = pSharedMem->channels[channel].msgBuffer;

		if (done)
		{
			pCallbackReply->Reset();

			while(true)
			{
				pCallbackReply->Write(mb.data, mb.size);
				mb.Reset();
				
				if (pSharedMem->channels[channel].state == ChannelSharedMem::sendlastbuffer)
				{
					pSharedMem->channels[channel].callbackMode = false;
					break;
				}

				::SignalObjectAndWait(pong, ping, INFINITE, FALSE);
			}

		}
	}

	HANDLE ping;
	HANDLE pong;

	SharedMem *pSharedMem;
	int channel;

	Message recvMsg;
	Message *pReply;
	Message *pCallbackReply;

	ReceiveCallback receiveCallback;
	ReceiveCallback callbackReceiveCallback;

	bool child;
	bool inReceiveCallback;


};

class ChildProcess
{
public:
	ChildProcess(HANDLE job)
		: hJob(job)
	{
		assert(hJob != NULL);
	}

	DWORD Create(char const *exepath, char const *sname)
	{
		char sharename[256] = { 0 };
		sprintf_s(sharename, 256, "%s_%d", sname, ::_getpid());

		hSharedSection  = ::CreateFileMapping(INVALID_HANDLE_VALUE, NULL,
							PAGE_READWRITE | SEC_COMMIT,
							0, sizeof(SharedMem), sharename);
		  		  
		pSharedMem = (SharedMem *)::MapViewOfFile(hSharedSection,
                                        FILE_MAP_WRITE|FILE_MAP_READ,
                                        0, 0, 0);


		STARTUPINFO startup_info = {sizeof(STARTUPINFO)};
		PROCESS_INFORMATION process_info = {0};

		if (!::CreateProcess(exepath,
			sharename,
			NULL,
			NULL,
			FALSE,
			CREATE_SUSPENDED | CREATE_BREAKAWAY_FROM_JOB | DETACHED_PROCESS,
			NULL,
			NULL,
			&startup_info,
			&process_info))
			return ::GetLastError();

		if (!::AssignProcessToJobObject(hJob, process_info.hProcess))
		{
			DWORD e = ::GetLastError();
			TerminateTarget(&process_info);
			return e;
		}

		hProcess = process_info.hProcess;
		hThread = process_info.hThread;
		processId = process_info.dwProcessId;

		return 0;
	}

	void Start()
	{
		::ResumeThread(hThread);
	}

	void InitParentChannel(int channel, Channel::ReceiveCallback callbackReceiveCallback)
	{
		channels[channel].InitParent(pSharedMem, hProcess, channel, callbackReceiveCallback);
	}

	void Send(int channel, Message const &msg, Message &reply)
	{
		return channels[channel].Send(msg, reply);
	}

	void Callback(int channel, Message const &msg, Message &reply)
	{
		return channels[channel].Callback(msg, reply);
	}

	void ListenInThreadPool(int channel, Channel::ReceiveCallback cb)
	{
		channels[channel].ListenInThreadPool(cb);
	}

	HANDLE GetProcessHandle() const { return hProcess; }

private:
	HANDLE hJob;
	
	HANDLE hSharedSection;
	SharedMem *pSharedMem;

	HANDLE hProcess;
	HANDLE hThread;
	DWORD processId;
	
	Channel channels[MaxChannels];
};

}