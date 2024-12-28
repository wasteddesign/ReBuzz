// ReBuzzEngine32.cpp : Defines the entry point for the application.
//

#include "stdafx.h"
#include <thread>
#include <ObjBase.h>
#include "ReBuzzEngine.h"
#include "Machine.h"
#include "../dsplib/dsplib.h"

#pragma comment(linker, "/manifestdependency:\"type='win32' name='Microsoft.Windows.Common-Controls' version='6.0.0.0' processorArchitecture='*' publicKeyToken='6595b64144ccf1df' language='*'\"")

//char const *BuildCount =
//#include "../buildcount"
//;

typedef std::map<string, MachineDLL *> MapStrToMacDLL;
MapStrToMacDLL dlls;

CMasterInfo g_MasterInfo = { 0 };

IPC::BuzzGlobalState gstate;

__declspec(align(64)) __declspec(thread) float MonoBuffer[MAX_BUFFER_LENGTH*4 + 16];
__declspec(align(64)) __declspec(thread) float StereoBuffer[MAX_BUFFER_LENGTH*4 + 16];

#define MAX_MULTI_IO_BUFFER_COUNT 64
float** MultiIOInputsBuffer = NULL;
float** MultiIOOutputsBuffer = NULL;
float** MultiIOInputsBufferWork = NULL;
float** MultiIOOutputsBufferWork = NULL;

HWND g_HostMainWnd = NULL;

void ReadMasterInfo(IPC::MessageReader &r)
{
	r.Read(&g_MasterInfo, 4 * 6);
}

extern void ReadWavetable(IPC::MessageReader &r);

string GetLastErrorMessage() 
{ 
    // Retrieve the system error message for the last-error code

    LPVOID lpMsgBuf;

    FormatMessage(
        FORMAT_MESSAGE_ALLOCATE_BUFFER | 
        FORMAT_MESSAGE_FROM_SYSTEM |
        FORMAT_MESSAGE_IGNORE_INSERTS,
        NULL,
        GetLastError(),
        MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
       (LPTSTR) &lpMsgBuf,
        0, NULL );

	string s = (char const *)lpMsgBuf;
    LocalFree(lpMsgBuf);

	return s;
}

void AfxMessageBox(string msg)
{
	::MessageBox(NULL, msg.c_str(), "Buzz", MB_OK);
}

void CMachineDataOutput::Write(void *pbuf, int const numbytes) {}

class CMsgDataOutput : public CMachineDataOutput
{
public:
	CMsgDataOutput(IPC::Message &_m) : m(_m) { }

	virtual void Write(void *pbuf, int const numbytes)
	{
		m.Write(pbuf, numbytes);
	}

	IPC::Message &m;
};

void CMachineDataInput::Read(void *pbuf, int const numbytes) {}

class CMRDataInput : public CMachineDataInput
{
public:
	CMRDataInput(IPC::MessageReader &_r) : r(_r) { }

	virtual void Read(void *pbuf, int const numbytes)
	{
		r.Read(pbuf, numbytes);
	}

	IPC::MessageReader &r;
};

IPC::Channel hostChannel;
IPC::Channel uiChannel;
IPC::Channel midiChannel;
IPC::Channel audioChannel;

void DCWrite(char const *fmt, ...)
{
	char buf[1024];

	va_list ap;
	va_start(ap, fmt);
	vsnprintf_s<1024>(buf, sizeof(buf)-1, fmt, ap);
	va_end(ap);

	IPC::Message m(IPC::HostDCWriteLine);
	m.Write(buf);
	IPC::Message reply;
	hostChannel.Send(m, reply);
}

void UIReceive(IPC::Message const &msg, IPC::Message &reply)
{
	IPC::MessageReader r(msg);
	int id;
	r.Read(id);

	switch (id)
	{
	case IPC::UIBuzzInit:
	{
		DCWrite("[Engine32] BuzzInit");
		g_HostMainWnd = (HWND)r.ReadPtr();
	}
	break;

	case IPC::UIDSPInit:
	{
		DCWrite("[Engine32] DSP_Init");
		int sr = (int)r.ReadDWORD();
		DSP_Init(sr);
	}
	break;

	case IPC::UILoadLibrary:
	{
		string libname = r.ReadString();
		string path = r.ReadString();

		auto h = ::LoadLibrary(path.c_str());

		if (h == NULL)
		{
			DCWrite("[MM32] LoadLibrary failed '%s'", path.c_str());

			string msg = "LoadLibrary(\"" + path + "\") failed.\n\n";
			msg += GetLastErrorMessage();
			// ::MessageBox(NULL, msg.c_str(), "Buzz", MB_OK);

			reply.WritePtr(0);
			reply.Write(msg.c_str());
		}
		else
		{
			DCWrite("[MM32] LoadLibrary '%s'", path.c_str());

			auto dll = new MachineDLL(path, libname, h);
			if (dll->CreateMachineFunc == NULL || dll->pInfo == NULL)
			{
				string msg = "LoadLibrary(\"" + path + "\") failed.\n\n";

				reply.WritePtr(0);
				reply.Write(msg.c_str());
			}
			else
			{
				dlls[libname] = dll;

				reply.WritePtr(h);
				dll->WriteMachineInfo(reply);
			}
		}
	}
	break;

	case IPC::UINewMI:
	{
		string libname = r.ReadString();
		DCWrite("[MM32] New Machine '%s'", libname.c_str());

		auto dll = dlls[libname];
		CMachine* pmac = new CMachine(*dll);
		pmac->pInterface->pMasterInfo = &g_MasterInfo;
		pmac->pInterface->pCB = new CMachineCallbacks();
		((CMachineCallbacks*)pmac->pInterface->pCB)->pMachine = pmac;
		reply.WritePtr(pmac);
	}
	break;

	case IPC::UIDeleteMI:
	{
		CMachine* pmac = (CMachine*)r.ReadPtr();
		DCWrite("[MM32] Delete Machine");
		// Deleted elsewhere?
		if (pmac != NULL)
			delete pmac;
	}
	break;

	case IPC::UIInit:
	{
		DCWrite("[Engine32] Init");

		ReadMasterInfo(r);

		CMachine* pmac = (CMachine*)r.ReadPtr();
		r.Read(pmac->pHostMac);

		DWORD datasize = r.ReadDWORD();
		if (datasize > 0)
		{
			CMRDataInput din(r);
			pmac->pInterface->Init(&din);
		}
		else
		{
			pmac->pInterface->Init(NULL);
		}
	}
	break;

	case IPC::UISave:
	{
		DCWrite("[Engine32] Save");

		CMachine* pmac = (CMachine*)r.ReadPtr();

		CMsgDataOutput mdo(reply);
		pmac->pInterface->Save(&mdo);
	}
	break;

	case IPC::UIAttributesChanged:
	{
		DCWrite("[Engine32] AttributesChanged");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		pmac->pInterface->AttributesChanged();
	}
	break;

	case IPC::UIStop:
	{
		DCWrite("[Engine32] Stop");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		pmac->pInterface->Stop();
	}
	break;

	case IPC::UICommand:
	{
		DCWrite("[Engine32] Command");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		int cmd = (int)r.ReadDWORD();
		pmac->pInterface->Command(cmd);
	}
	break;

	case IPC::UIAddInput:
	{
		DCWrite("[Engine32] AddInput");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		string name = r.ReadString();
		bool stereo;
		r.Read(stereo);
		pmac->pInterfaceEx->AddInput(name.length() > 0 ? name.c_str() : NULL, stereo);
	}
	break;

	case IPC::UIDeleteInput:
	{
		DCWrite("[Engine32] DeleteInput");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		string name = r.ReadString();
		if (pmac && pmac->pInterfaceEx)
			pmac->pInterfaceEx->DeleteInput(name.c_str());
	}
	break;

	case IPC::UIRenameInput:
	{
		DCWrite("[Engine32] RenameInput");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		string oldname = r.ReadString();
		string newname = r.ReadString();
		pmac->pInterfaceEx->RenameInput(oldname.c_str(), newname.c_str());
	}
	break;

	case IPC::UISetInputChannels:
	{
		//DCWrite("[Engine32] SetInputChannels");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		string name = r.ReadString();
		bool stereo;
		r.Read(stereo);
		pmac->pInterfaceEx->SetInputChannels(name.c_str(), stereo);
	}
	break;

	case IPC::UIDescribeValue:
	{
		DCWrite("[Engine32] DescribeValue");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		int param = (int)r.ReadDWORD();
		int value = (int)r.ReadDWORD();
		char const* str = pmac->pInterface->DescribeValue(param, value);
		reply.Write(str != NULL);
		if (str != NULL) reply.Write(str);
	}
	break;

	case IPC::UIHandleGUIMessage:
	{
		//DCWrite("[Engine32] HandleGUIMessage");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CMRDataInput din(r);
		CMsgDataOutput mdo(reply);
		bool ret = pmac->pInterfaceEx->HandleGUIMessage(&mdo, &din);
		reply.Write(ret);
	}
	break;

	case IPC::UIGetEnvelopeInfos:
	{
		DCWrite("[Engine32] GetEnvelopeInfos");
		CMachine* pmac = (CMachine*)r.ReadPtr();

		auto pei = pmac->pInterface->GetEnvelopeInfos();
		reply.Write(pei != NULL);

		if (pei != NULL)
		{
			int count = 0;
			for (CEnvelopeInfo const** p = pei; *p != NULL; p++) count++;
			reply.Write(count);
			for (int i = 0; i < count; i++)
			{
				reply.Write(pei[i]->Name);
				reply.Write(pei[i]->Flags);
			}
		}
	}
	break;

	case IPC::UIGetDLLPtr:
	{
		string libname = r.ReadString();
		DCWrite("[MM32] GetDLLPtr '%s'", libname.c_str());

		auto i = dlls.find(libname);
		if (i != dlls.end())
			reply.WritePtr((*i).second);
		else
			reply.WritePtr((DWORD)0);
	}
	break;

	case IPC::UIGetInstrumentList:
	{
		//DCWrite("[Engine32] GetInstrumentList");
		MachineDLL* pdll = (MachineDLL*)r.ReadPtr();

		CMsgDataOutput mdo(reply);
		if (pdll->pInfo->pLI != NULL) pdll->pInfo->pLI->GetInstrumentList(&mdo);
	}
	break;
	case IPC::UIGetInstrumentPath:
	{
		MachineDLL* pdll = (MachineDLL*)r.ReadPtr();
		string instrumentName = r.ReadString();

		char path[256] = { 0 };
		bool ret;
		if (pdll->pInfo->pLI != NULL) ret = pdll->pInfo->pLI->GetInstrumentPath(instrumentName.c_str(), path, 255);
		reply.Write(ret);
		if (ret)
			reply.Write(path);
	}
	break;
	case IPC::UISetInstrument:
	{
		DCWrite("[Engine32] SetInstrument");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		string instrumentName = r.ReadString();
		bool ret = pmac->pInterfaceEx->SetInstrument(instrumentName.c_str());
		reply.Write(ret);
	}
	break;
	case IPC::UIEvent:
	{
		DCWrite("[Engine32] SendEvent");
		CMachine* pmac = (CMachine*)r.ReadPtr();

		void* ptr = (void*)r.ReadPtr();
		EVENT_HANDLER_PTR eh1 = 0;
		reinterpret_cast<void*&>(eh1) = ptr;
		void* param = (void*)r.ReadPtr();
		//EVENT_HANDLER_PTR eh = *(EVENT_HANDLER_PTR*)ptr; // cast it back
		bool ret = (pmac->pInterface->*eh1)(param); // And call

		reply.Write(ret);
	}
	break;
	case IPC::UIGetResources:
	{
		DCWrite("[Engine32] GetResourceImage");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		HINSTANCE pdll = pmac->pTemplate->LibHandle;

		HBITMAP bmp = ::LoadBitmap(pdll, TEXT("SKIN"));
		if (bmp != NULL)
		{
			BITMAP bm;
			GetObject(bmp, (int)sizeof bm, &bm);

			if (bm.bmBitsPixel == 32)
			{
				// byte == 1 if "ICON" found
				reply.Write(true);

				// Width
				reply.Write(bm.bmWidth);
				// Height
				reply.Write(bm.bmHeight);

				int size = bm.bmWidth * bm.bmHeight * 4;

				LPVOID buffer = malloc(size);

				GetBitmapBits(bmp, size, buffer);

				byte* pPixels = reinterpret_cast<byte*>(buffer);
				for (int i = 0; i < size; i++)
				{
					reply.Write(pPixels[i]);
				}

				free(buffer);
			}

			DeleteObject(bmp);
		}
		else
		{
			reply.Write(false);
		}

		bmp = LoadBitmap(pdll, TEXT("SKINLED"));
		if (bmp != NULL)
		{
			BITMAP bm;
			GetObject(bmp, (int)sizeof bm, &bm);

			if (bm.bmBitsPixel == 32)
			{
				// byte == 1 if "ICON" found
				reply.Write(true);

				// Width
				reply.Write(bm.bmWidth);
				// Height
				reply.Write(bm.bmHeight);

				int size = bm.bmWidth * bm.bmHeight * 4;

				LPVOID buffer = malloc(size);

				GetBitmapBits(bmp, size, buffer);

				byte* pPixels = reinterpret_cast<byte*>(buffer);
				for (int i = 0; i < size; i++)
				{
					reply.Write(pPixels[i]);
				}

				free(buffer);
			}
			DeleteObject(bmp);
		}
		else
		{
			reply.Write(false);
		}

		// LED position
		HRSRC myResource = ::FindResource(pdll, TEXT("SKINLEDPOS"), RT_RCDATA);
		if (myResource != NULL)
		{
			DWORD myResourceSize = ::SizeofResource(pdll, myResource);
			HGLOBAL hResourceLoaded = LoadResource(pdll, myResource);
			char* lpResLock = (char*)LockResource(hResourceLoaded);
			reply.Write(lpResLock[0]);
			reply.Write(lpResLock[1]);
		}
		else
		{
			reply.Write((WORD)0);
		}
	}
	break;
	case IPC::UICreatePattern:
	{
		DCWrite("[Engine32] CreatePattern");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* pat = (CPattern*)r.ReadPtr();
		int numRows = r.ReadDWORD();
		pmac->pInterfaceEx->CreatePattern(pat, numRows);
	}
	break;
	case IPC::UICreatePatternCopy:
	{
		DCWrite("[Engine32] CreatePatternCopy");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* patNew = (CPattern*)r.ReadPtr();
		CPattern* patOld = (CPattern*)r.ReadPtr();
		pmac->pInterfaceEx->CreatePatternCopy(patNew, patOld);
	}
	break;
	case IPC::UIDeletePattern:
	{
		DCWrite("[Engine32] DeletePattern");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* pat = (CPattern*)r.ReadPtr();
		pmac->pInterfaceEx->DeletePattern(pat);
	}
	break;
	case IPC::UIRenamePattern:
	{
		DCWrite("[Engine32] RenamePattern");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* pat = (CPattern*)r.ReadPtr();
		string name = r.ReadString();
		pmac->pInterfaceEx->RenamePattern(pat, name.c_str());
	}
	break;
	case IPC::UISetPatternLength:
	{
		DCWrite("[Engine32] SetPatternLength");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* pat = (CPattern*)r.ReadPtr();
		int len = r.ReadDWORD();
		pmac->pInterfaceEx->SetPatternLength(pat, len);
	}
	break;
	case IPC::UIPlayPattern:
	{
		DCWrite("[Engine32] SetPatternLength");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* pat = (CPattern*)r.ReadPtr();
		CSequence* seq = (CSequence*)r.ReadPtr();
		int ofs = r.ReadDWORD();
		pmac->pInterfaceEx->PlayPattern(pat, seq, ofs);
	}
	break;
	case IPC::UICreatePatternEditor:
	{
		DCWrite("[Engine32] CreatePatternEditor");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		HWND hwnd = (HWND)r.ReadPtr();
		void* editorHwnd = pmac->pInterfaceEx->CreatePatternEditor(hwnd);
		reply.Write(editorHwnd);
	}
	break;
	case IPC::UISetEditorPattern:
	{
		DCWrite("[Engine32] SetEditorPattern");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* pat = (CPattern*)r.ReadPtr();
		pmac->pInterfaceEx->SetEditorPattern(pat);
	}
	break;
	case IPC::UIAddTrack:
	{
		DCWrite("[Engine32] AddTrack");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		pmac->pInterfaceEx->AddTrack();
	}
	break;
	case IPC::UIDeleteLastTrack:
	{
		DCWrite("[Engine32] DeleteLastTrack");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		pmac->pInterfaceEx->DeleteLastTrack();
	}
	break;
	case IPC::UIEnableCommandUI:
	{
		DCWrite("[Engine32] EnableCommandUI");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		int id = r.ReadDWORD();
		bool ret = pmac->pInterfaceEx->EnableCommandUI(id);
		reply.Write(ret);
	}
	break;
	case IPC::UIDrawPatternBox:
	{
		DCWrite("[Engine32] DrawPatternBox");
	}
	break;
	case IPC::UISetPatternTargetMachine:
	{
		DCWrite("[Engine32] SetPatternTargetMachine");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		CPattern* pat = (CPattern*)r.ReadPtr();
		CMachine* tmac = (CMachine*)r.ReadPtr();
		pmac->pInterfaceEx->SetPatternTargetMachine(pat, tmac);
	}
	break;
	case IPC::UIGetChannelName:
	{
		DCWrite("[Engine32] GetChannelName");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		bool input;
		r.Read(input);
		int index = r.ReadDWORD();
		char const* str = pmac->pInterfaceEx->GetChannelName(input, index);
		reply.Write(str != NULL);
		if (str != NULL) reply.Write(str);
	}
	break;
	case IPC::UIGetSubMenu:
	{
		DCWrite("[Engine32] GetSubMenu");
		CMachine* pmac = (CMachine*)r.ReadPtr();
		int index = r.ReadDWORD();

		CMsgDataOutput mdo(reply);
		pmac->pInterfaceEx->GetSubMenu(index, &mdo);
	}
	break;
	case IPC::UILoad:
	{
		DCWrite("[Engine32] Load");

		CMachine* pmac = (CMachine*)r.ReadPtr();
		DWORD datasize = r.ReadDWORD();
		if (datasize > 0)
		{
			CMRDataInput din(r);
			pmac->pInterfaceEx->Load(&din);
		}
		else
		{
			pmac->pInterfaceEx->Load(NULL);
		}
	}
	break;
	case IPC::UIGotMidiFocus:
	{
		//DCWrite("[Engine32] UIGotMidiFocus");

		CMachine* pmac = (CMachine*)r.ReadPtr();
		pmac->pInterfaceEx->GotMidiFocus();
	}
	break;
	case IPC::UILostMidiFocus:
	{
		//DCWrite("[Engine32] UILostMidiFocus");

		CMachine* pmac = (CMachine*)r.ReadPtr();
		pmac->pInterfaceEx->LostMidiFocus();
	}
	break;
	case IPC::UIImportFinished:
	{
		CMachine* pmac = (CMachine*)r.ReadPtr();
		pmac->pInterfaceEx->ImportFinished();
	}
	break;
	case IPC::UIImplementsFunction:
	{
		CMachine* pmac = (CMachine*)r.ReadPtr();
		string str = r.ReadString();
		bool found = pmac->pTemplate->ImplementsFunction(str.c_str());
		reply.Write(found);
	}
	break;
	case IPC::UIGetCommands:
	{
		CMachine* pmac = (CMachine*)r.ReadPtr();
		reply.Write(pmac->pTemplate->pInfo->Commands);
	}
	break;
	case IPC::UIRemapMachineNames:
	{
		CMachine* pmac = (CMachine*)r.ReadPtr();
		auto cb = ((CMachineCallbacks*)pmac->pInterface->pCB);
		int count = r.ReadDWORD();
		cb->remappedMachineNames.clear();

		for (int i = 0; i < count; i++)
		{
			string oldname = r.ReadString();
			string newname = r.ReadString();
			cb->remappedMachineNames[oldname] = newname;
		}
	}
	break;
	}
}

void AudioReceive(IPC::Message const &msg, IPC::Message &reply)
{
	IPC::MessageReader r(msg);
	int id;
	r.Read(id);
	switch(id)
	{
	case IPC::AudioSetNumTracks:
		{
			CMachine *pmac = (CMachine *)r.ReadPtr();
			//DCWrite("[Engine32] SetNumTracks");

			int n;
			r.Read(n);

			pmac->SetnumTracks(n);

		}
		break;

	case IPC::AudioTick:
		{
			ReadMasterInfo(r);
			
			CMachine *pmac = (CMachine *)r.ReadPtr();
			//DCWrite("[Engine32] Tick");

			DWORD gsize = r.ReadDWORD();
			r.Read(pmac->pInterface->GlobalVals, gsize);

			DWORD tsize = r.ReadDWORD();
			r.Read(pmac->pInterface->TrackVals, tsize);

			pmac->row = r.ReadDWORD();
			r.Read(pmac->buzzTickPosition);

			pmac->pInterface->Tick();
		}
		break;

	case IPC::AudioWork:
		{
			ReadMasterInfo(r);

			CMachine *pmac = (CMachine *)r.ReadPtr();
			//DCWrite("[Engine32] Work");

			DWORD nch = r.ReadDWORD();
			DWORD numsamples = r.ReadDWORD();
			DWORD mode = r.ReadDWORD();

			r.Read(MonoBuffer, numsamples * nch * sizeof(float));

			bool ret = pmac->pInterface->Work(MonoBuffer, numsamples, mode);
			reply.Write(ret);
			if (ret) reply.Write(MonoBuffer, numsamples * nch * sizeof(float));
		}
		break;

	case IPC::AudioWorkMonoToStereo:
		{
			ReadMasterInfo(r);

			CMachine *pmac = (CMachine *)r.ReadPtr();
			//DCWrite("[Engine32] WorkMonoToStereo");

			DWORD numsamples = r.ReadDWORD();
			DWORD mode = r.ReadDWORD();

			bool gotpin;
			r.Read(gotpin);
			if (gotpin)	r.Read(MonoBuffer, numsamples * sizeof(float));

			bool ret = pmac->pInterface->WorkMonoToStereo(gotpin ? MonoBuffer : NULL, StereoBuffer, numsamples, mode);
			reply.Write(ret);
			if (ret) reply.Write(StereoBuffer, numsamples * 2 * sizeof(float));
		}
		break;

	case IPC::AudioInput:
		{
			//DCWrite("[Engine32] Input");
			CMachine *pmac = (CMachine *)r.ReadPtr();
			int numsamples = (int)r.ReadDWORD();
			bool hasinput;
			r.Read(hasinput);
			if (hasinput) r.Read(StereoBuffer, numsamples * 2 * sizeof(float));
			float amp;
			r.Read(amp);
			pmac->pInterfaceEx->Input(hasinput ? StereoBuffer : NULL, numsamples, amp);
		}
		break;

	case IPC::AudioBeginBlock:
		{
			//DCWrite("[Engine32] AudioBeginBlock");
			ReadWavetable(r);
		}
		break;

	case IPC::AudioBeginFrame:
		{
			//DCWrite("[Engine32] AudioBeginFrame");
			r.Read(gstate);
		}
		break;
	case IPC::AudioMultiWork:
		{
			ReadMasterInfo(r);

			CMachine* pmac = (CMachine*)r.ReadPtr();
			//DCWrite("[Engine32] MultiWork");

			DWORD numsamples = r.ReadDWORD();
			int nin = (int)r.ReadDWORD();
			int nout = (int)r.ReadDWORD();

			bool gotinputs;
			r.Read(gotinputs);

			if (gotinputs)
			{
			
				for (int i = 0; i < nin; i++)
				{
					bool gotptr;
					r.Read(gotptr);

					if (gotptr)
					{
						MultiIOInputsBufferWork[i] = MultiIOInputsBuffer[i];
						if (gotptr)	r.Read(MultiIOInputsBufferWork[i], numsamples * 2 * sizeof(float));
					}
					else
					{
						MultiIOInputsBufferWork[i] = NULL;
					}
				}
			}

			bool gotoutputs;
			r.Read(gotoutputs);

			if (gotoutputs)
			{
				for (int i = 0; i < nout; i++)
				{
					bool gotptr;
					r.Read(gotptr);

					if (gotptr)
					{
						MultiIOOutputsBufferWork[i] = MultiIOOutputsBuffer[i];
					}
					else
						MultiIOOutputsBufferWork[i] = NULL;
				}
			}

			pmac->pInterfaceEx->MultiWork(MultiIOInputsBufferWork, MultiIOOutputsBufferWork, numsamples);

			for (int i = 0; i < nout; i++)
			{
				reply.Write(MultiIOOutputsBufferWork[i] != NULL);
				if (MultiIOOutputsBufferWork[i] != NULL) reply.Write(MultiIOOutputsBufferWork[i], numsamples * 2 * sizeof(float));
			}
		}
		break;
	}
}

void MIDIReceive(IPC::Message const &msg, IPC::Message &reply)
{
	IPC::MessageReader r(msg);
	int id;
	r.Read(id);
	switch(id)
	{
	case IPC::MIDINote:
		{
			CMachine *pmac = (CMachine *)r.ReadPtr();
			int channel, value, velocity;
			r.Read(channel);
			r.Read(value);
			r.Read(velocity);
			pmac->pInterface->MidiNote(channel, value, velocity);
		}
		break;
	
	case IPC::MIDIControlChange:
		{
			CMachine *pmac = (CMachine *)r.ReadPtr();
			int ctrl, channel, value;
			r.Read(ctrl);
			r.Read(channel);
			r.Read(value);
			pmac->pInterfaceEx->MidiControlChange(ctrl, channel, value);
		}
		break;
	}

}

extern void InitOscTables();

class ListenThread //: public thread
{
public:
	ListenThread(IPC::Channel &chn, IPC::Channel::ReceiveCallback cb, int pr = THREAD_PRIORITY_NORMAL)
		: channel(chn), callback(cb), priority(pr)
	{
	}

	void Main()
	{
		id = ::GetCurrentThreadId();

		::SetThreadPriority(::GetCurrentThread(), priority);
		channel.Listen(callback);
	}

	DWORD id;

	void Start()
	{
		std::thread *mythread = new thread(&ListenThread::Main, this);
	}

	~ListenThread()
	{
		if (mythread != NULL)
		{	
			mythread->join();
			delete mythread;
		}
	}

private:
	IPC::Channel &channel;
	IPC::Channel::ReceiveCallback callback;
	int priority;

	std::thread* mythread;
};

DWORD uiThreadId;

//ListenThread uiThread(uiChannel, &UIReceive);
ListenThread midiThread(midiChannel, &MIDIReceive, THREAD_PRIORITY_HIGHEST);
ListenThread audioThread(audioChannel, &AudioReceive, THREAD_PRIORITY_TIME_CRITICAL);

void DoCallback(IPC::Message const &msg, IPC::Message &reply)
{
	//thread::id id = std::this_thread::get_id();
	DWORD id = ::GetCurrentThreadId();

	if (id == uiThreadId && uiChannel.IsInReceiveCallback())
		uiChannel.Callback(msg, reply);
	else if (id == midiThread.id && midiChannel.IsInReceiveCallback())
		midiChannel.Callback(msg, reply);
	else if (id == audioThread.id && audioChannel.IsInReceiveCallback())
		audioChannel.Callback(msg, reply);
	else
		hostChannel.Send(msg, reply);
}


HINSTANCE g_hInstance = NULL;
HWND g_ChildMainWnd = NULL;
ATOM				MyRegisterClass(HINSTANCE hInstance);
BOOL				InitInstance(HINSTANCE, int);

int APIENTRY _tWinMain(_In_ HINSTANCE hInstance,
                     _In_opt_ HINSTANCE hPrevInstance,
                     _In_ LPTSTR    lpCmdLine,
                     _In_ int       nCmdShow)
{
    //CoInitializeEx(NULL, COINIT_APARTMENTTHREADED);
	CoInitialize(NULL);

	g_hInstance = hInstance;
	
	/*
	MessageBox(
		NULL,
		"Attach degugger.",
		"Attach degugger",
		MB_ICONWARNING | MB_OK | MB_DEFBUTTON1
	);
	*/
	
	char* sharename;
	if (lpCmdLine == NULL || lpCmdLine == "")
		sharename = ::GetCommandLine(); // Buzz
	else
		sharename = lpCmdLine;
	
/*	
	HANDLE hSharedSection  = ::CreateFileMapping(INVALID_HANDLE_VALUE, NULL,
							PAGE_READWRITE | SEC_COMMIT,
							0, sizeof(SharedMem), sharename);
	*/

	HANDLE hSharedSection = ::OpenFileMapping(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, sharename);
	if (hSharedSection == NULL) return -1;

	IPC::SharedMem *pSharedMem = (IPC::SharedMem *)::MapViewOfFile(hSharedSection,
                                        FILE_MAP_WRITE|FILE_MAP_READ,
                                        0, 0, 0);

	InitOscTables();
	
	auto h = ::LoadLibrary("dsplib.dll");
	if (h == NULL)
	{
		DCWrite("[Engine32] Can't load dsplib.dll");
	}
	
	uiThreadId = ::GetCurrentThreadId();

	string str = "";
	str.append("Host").append(sharename);
	hostChannel.InitChild(pSharedMem, IPC::HostChannel, str.c_str());

	str = "";
	str.append("UI").append(sharename);
	uiChannel.InitChild(pSharedMem, IPC::UIChannel, str.c_str());
	//uiThread.Start();

	str = "";
	str.append("MIDI").append(sharename);
	midiChannel.InitChild(pSharedMem, IPC::MIDIChannel, str.c_str());
	midiThread.Start();
	
	str = "";
	str.append("Audio").append(sharename);
	audioChannel.InitChild(pSharedMem, IPC::AudioChannel, str.c_str());
	audioThread.Start();

	// Create MultiIOBuffers
	MultiIOInputsBuffer = new float* [MAX_MULTI_IO_BUFFER_COUNT];
	MultiIOOutputsBuffer = new float* [MAX_MULTI_IO_BUFFER_COUNT];
	MultiIOInputsBufferWork = new float* [MAX_MULTI_IO_BUFFER_COUNT];
	MultiIOOutputsBufferWork = new float* [MAX_MULTI_IO_BUFFER_COUNT];

	for (int i = 0; i < MAX_MULTI_IO_BUFFER_COUNT; i++)
	{
		MultiIOOutputsBuffer[i] = (float*)_aligned_malloc((MAX_BUFFER_LENGTH * 2 + 16) * sizeof(float), 64);
		MultiIOInputsBuffer[i] = (float*)_aligned_malloc((MAX_BUFFER_LENGTH * 2 + 16) * sizeof(float), 64);
	}

	MyRegisterClass(hInstance);

	// Perform application initialization:
	if (!InitInstance (hInstance, SW_HIDE))
//	if (!InitInstance (hInstance, nCmdShow))
	{
		return FALSE;
	}

	DCWrite("[Engine32] Init");

	uiChannel.ListenMsgLoop(&UIReceive);
	/*
	MSG msg;
	while (GetMessage(&msg, NULL, 0, 0))
	{
		TranslateMessage(&msg);
		DispatchMessage(&msg);
	}
	*/

	// Cleanup
	for (int i = 0; i < MAX_MULTI_IO_BUFFER_COUNT; i++)
	{
		_aligned_free(MultiIOInputsBuffer[i]);
		_aligned_free(MultiIOOutputsBuffer[i]);
	}

	delete[] MultiIOInputsBuffer;
	delete[] MultiIOOutputsBuffer;
	delete[] MultiIOInputsBufferWork;
	delete[] MultiIOOutputsBufferWork;

	return 0;
}


char szTitle[] = " - ReBuzz -";

#ifdef _WIN64
char szWindowClass[] = "ReBuzzEngine32Class";
#else
char szWindowClass[] = "ReBuzzEngine64Class";
#endif

LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)
{
	int wmId, wmEvent;
	PAINTSTRUCT ps;
	HDC hdc;

	switch (message)
	{
	case WM_COMMAND:
		wmId    = LOWORD(wParam);
		wmEvent = HIWORD(wParam);
		// Parse the menu selections:
		switch (wmId)
		{
		case IDM_EXIT:
			DestroyWindow(hWnd);
			break;
		default:
			return DefWindowProc(hWnd, message, wParam, lParam);
		}
		break;
	case WM_PAINT:
		hdc = BeginPaint(hWnd, &ps);
		// TODO: Add any drawing code here...
		EndPaint(hWnd, &ps);
		break;
	case WM_DESTROY:
		PostQuitMessage(0);
		break;
	default:
		return DefWindowProc(hWnd, message, wParam, lParam);
	}
	return 0;
}

ATOM MyRegisterClass(HINSTANCE hInstance)
{
	WNDCLASSEX wcex;

	wcex.cbSize = sizeof(WNDCLASSEX);

	wcex.style			= CS_HREDRAW | CS_VREDRAW;
	wcex.lpfnWndProc	= WndProc;
	wcex.cbClsExtra		= 0;
	wcex.cbWndExtra		= 0;
	wcex.hInstance		= hInstance;
	wcex.hIcon			= NULL;
	wcex.hCursor		= LoadCursor(NULL, IDC_ARROW);
	wcex.hbrBackground	= (HBRUSH)(COLOR_WINDOW+1);
	wcex.lpszMenuName	= NULL;
	wcex.lpszClassName	= szWindowClass;
	wcex.hIconSm		= LoadIcon(wcex.hInstance, MAKEINTRESOURCE(IDI_SMALL));

	return RegisterClassEx(&wcex);
}

BOOL InitInstance(HINSTANCE hInstance, int nCmdShow)
{
   HWND hWnd;

   hWnd = ::CreateWindowEx(WS_EX_TOPMOST, szWindowClass, szTitle, WS_OVERLAPPEDWINDOW,
      CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, NULL, NULL, hInstance, NULL);

   if (!hWnd)
   {
      return FALSE;
   }

   g_ChildMainWnd = hWnd;

   ShowWindow(hWnd, nCmdShow);
   UpdateWindow(hWnd);

   return TRUE;
}

