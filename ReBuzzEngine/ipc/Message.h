
#pragma once

#include "ServerSharedMem.h"
#include "../buzz/MachineInterface.h"

namespace IPC
{

enum UIMessages
{
	UIBuzzInit,
	UIDSPInit,
	UILoadLibrary,
	UINewMI,
	UIDeleteMI,
	UIInit,
	UISave,
	UIAttributesChanged,
	UIStop,
	UICommand,
	UIAddInput,
	UIDeleteInput,
	UIRenameInput,
	UISetInputChannels,
	UIDescribeValue,
	UIHandleGUIMessage,
	UIGetEnvelopeInfos,
	UIGetDLLPtr,
	UIGetInstrumentList,
	UIGetInstrumentPath,
	UISetInstrument,
	UIEvent,
	UIGetResources,
	UICreatePattern,
	UICreatePatternCopy,
	UIDeletePattern,
	UIRenamePattern,
	UISetPatternLength,
	UIPlayPattern,
	UICreatePatternEditor,
	UISetEditorPattern,
	UIAddTrack,
	UIDeleteLastTrack,
	UIEnableCommandUI,
	UIDrawPatternBox,
	UISetPatternTargetMachine,
	UIGetChannelName,
	UIGetSubMenu,
	UILoad,
	UIGotMidiFocus,
	UILostMidiFocus,
	UIImportFinished,
	UIImplementsFunction,
	UIGetCommands,
	UIRemapMachineNames
};

enum AudioMessages
{
	AudioBeginBlock,
	AudioBeginFrame,
	AudioSetNumTracks,
	AudioTick,
	AudioWork,
	AudioWorkMonoToStereo,
	AudioInput,
	AudioMultiWork,
};

enum HostMessages
{
	HostDCWriteLine,
	HostInitMIEx,
	HostInitMDK,
	HostSetnumOutputChannels,
	HostSetInputChannelCount,
	HostSetOutputChannelCount,
	HostGetMachineName,
	HostRenameMachine,
	HostSetEventHandler,
	HostSetModifyFlag,
	HostControlChange,
	HostControlChangeImmediate,
	HostSendControlChanges,
	HostSetMidiFocus,
	HostGetMachineNames,
	HostCreateRefMachine,
	HostSendMidiNote,
	HostSendMidiControlChange,
	HostGetOption,
	HostGetNumTracks,
	HostGetParameterState,
	HostDescribeValue,
	HostGetAttribute,
	HostSetAttribute,
	HostAttributesChanged,
	HostGetPlayingSequence,
	HostGetPlayingRow,
	HostBeginWriteToPlayingPattern,
	HostEndWriteToPlayingPattern,
	HostWriteToPlayingPattern,
	HostMidiOut
};

enum MIDIMessages
{	
	MIDINote,
	MIDIControlChange
};

#pragma pack(8)
struct BuzzGlobalState
{
	int AudioFrame;
	int ADWritePos;
	int ADPlayPos;
	int SongPosition;
	int LoopStart;
	int LoopEnd;
	int SongEnd;
	int StateFlags;
	bool MIDIFiltering;
	bool SongClosing;
};
#pragma pack()

class Message
{
private:
	Message(Message const &x) { }

public:
	friend class MessageReader;

	Message()
	{
	}

	Message(int id)
	{
		Write(id);
	}

	void Reset()
	{
		data.clear();
	}

	void Write(void *pdata, int nbytes)
	{
		int oldsize = (int)data.size();
		data.resize(oldsize + nbytes);
		if (nbytes > 0) memcpy(&data[oldsize], pdata, nbytes);
	}

	void Write(char const *pstr)
	{
		if (pstr == NULL)
			Write((byte)0);
		else
			Write((void *)pstr, (int)strlen(pstr) + 1);
	}

#ifdef _AFX
	void Write(CString &str)
	{
		Write((char const *)str);
	}
#endif

	template<typename T>
	void Write(T &&data)
	{
		Write((void *)&data, sizeof(T));
	}

	template<typename T>
	void WritePtr(T&& data)
	{
#ifdef _WIN64
		Write((void*)&data, sizeof(LONGLONG));
#else
		Write((void*)&data, sizeof(DWORD));
#endif
	}

	void Format(char const *fmt, ...)
	{
		char buf[1024];

		va_list ap;
		va_start(ap, fmt);
		vsnprintf_s(buf, sizeof(buf), sizeof(buf)-1, fmt, ap);
		va_end(ap);

		Write((char const *)buf);
	}

	byte const *GetData() const { return data.size() > 0 ? &data[0] : NULL; }
	int GetSize() const { return (int)data.size(); }

private:
	vector<byte> data;

};

class MessageReader
{
public:
	MessageReader(Message const &m)
		: msg(m), readPos(0)
	{
	}

	int Read(void *pdata, int nbytes)
	{
		int n = min(nbytes, (int)msg.data.size() - readPos);
		if (n <= 0) return 0;
		memcpy(pdata, &msg.data[readPos], n);
		readPos += n;
		return n;
	}

	template<typename T>
	void Read(T &&data)
	{
		Read(&data, sizeof(T));
	}

	DWORD ReadDWORD()
	{
		DWORD r;
		Read(r);
		return r;
	}

	LONGLONG ReadLONGLONG()
	{
		LONGLONG r;
		Read(r);
		return r;
	}

	LONGLONG ReadPtr()
	{
#ifdef _WIN64
		LONGLONG r;
		Read(r);
		return r;
#else
		DWORD r;
		Read(r);
		return r;
#endif
	}

#ifdef _AFX
	CString ReadString()
	{
		CString s;

		while(true)
		{
			char ch;
			Read(ch);
			if (ch == 0) break;
			s += ch;
		}

		return s;
	}

	void AllocAndRead(char const **p)
	{
		auto s = ReadString();
		*p = new char [s.GetLength() + 1];
		strcpy((char *)*p, s);
	}

#else
	string ReadString()
	{
		string s;

		while(true)
		{
			char ch;
			Read(ch);
			if (ch == 0) break;
			s += ch;
		}

		return s;
	}

	void AllocAndRead(char const** p)
	{
		auto s = ReadString();
		*p = new char[s.length() + 1];
		strcpy((char*)*p, s.c_str());
	}

#endif

	int ReadTo(CMachineDataOutput *po, int nbytes)
	{
		int n = min(nbytes, (int)msg.data.size() - readPos);
		if (n <= 0) return 0;
		po->Write((void *)&msg.data[readPos], n);		
		readPos += n;
		return n;
	}

	int GetBytesLeft() const { return msg.GetSize() - readPos; }

private:
	Message const &msg;
	int readPos;

};

}

