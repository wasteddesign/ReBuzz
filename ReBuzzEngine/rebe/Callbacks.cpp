#include "stdafx.h"
#include "mdkimp.h"
#include "Machine.h"
#include <cstdint>
#include <atlbase.h>

// Do we need to make things thread safe (using machine/global locks) so that the message passing system don't get messed up if
// callback are called from a thread?

extern void DoCallback(IPC::Message const &msg, IPC::Message &reply);
extern IPC::BuzzGlobalState gstate;

// Max row data = 1028 * 4. Enough?
#define ROW_DATA_LENGHT 1028
int rowData[ROW_DATA_LENGHT];

static int BMPRandStat = 1235135;

inline void BMPrand()
{
	BMPRandStat = (BMPRandStat * 1103515245 + 12347) & 0x7fffffff;
}

__declspec(align(64)) __declspec(thread) float AuxBuffer[MAX_BUFFER_LENGTH*4 * 2 + 16];


extern void AfxMessageBox(string txt);

extern CMasterInfo g_MasterInfo;

extern void DCWrite(char const *fmt, ...);

static void MICB(CMachine *pmac, char const *loc, char const *msg)
{
	/*
	double const time = GetPerfCount() / GetPerfFreq();

	if (pmac->CBDebugInfos.find(loc) == pmac->CBDebugInfos.end())
	{
		pmac->CBDebugInfos[loc] = CMICBDebugInfo();
		CMICBDebugInfo &di = pmac->CBDebugInfos[loc];
		di.Count = 0;
		di.Suppressed = false;
		di.FirstTime = time;
	}


	CMICBDebugInfo &di = pmac->CBDebugInfos[loc];

	if (!di.Suppressed)
	{
		CDebugConsole::Write("[CB] '%s' %s", pmac->GetName(), msg);
	}
	else
	{
		if ((time - di.FirstTime) > 5.0)
		{
			CDebugConsole::Write("[CB] '%s' %s (%d times in last 5 seconds)", pmac->GetName(), msg, di.Count);
			di.Count = 0;
			di.FirstTime = time;
		}
	}

	di.Count++;
	if (!di.Suppressed && di.Count > 10 && (time - di.FirstTime) < 2.0)
	{
		di.Suppressed = true;
		di.Count = 0;
		di.FirstTime = time;
//		CDebugConsole::Write("(suppressed, showing once every 5 seconds)");
	}
	*/
}

static void _MICB0(CMachine *pmac, char const *loc)
{
	//CSpinCSLock(pmac->CBDebugCS);
	char txt[256];
	sprintf_s(txt, 256, "%s", loc);
	MICB(pmac, loc, txt);
}

static void _MICB1(CMachine *pmac, char const *loc, int a)
{
	//CSpinCSLock(pmac->CBDebugCS);
	char txt[256];
	sprintf_s(txt, 256, "%s(%d)", loc, a);
	MICB(pmac, loc, txt);
}

static void _MICB2(CMachine *pmac, char const *loc, int a, int b)
{
	//CSpinCSLock(pmac->CBDebugCS);
	char txt[256];
	sprintf_s(txt, 256, "%s(%d,%d)", loc, a, b);
	MICB(pmac, loc, txt);
}

static void _MICB3(CMachine *pmac, char const *loc, int a, int b, int c)
{
	//CSpinCSLock(pmac->CBDebugCS);
	char txt[256];
	sprintf_s(txt, 256, "%s(%d,%d,%d)", loc, a, b, c);
	MICB(pmac, loc, txt);
}

static void _MICB4(CMachine *pmac, char const *loc, int a, int b, int c, int d)
{
	//CSpinCSLock(pmac->CBDebugCS);
	char txt[256];
	sprintf_s(txt, 256, "%s(%d,%d,%d,%d)", loc, a, b, c, d);
	MICB(pmac, loc, txt);
}

static void _MICB5(CMachine *pmac, char const *loc, int a, int b, int c, int d, int e)
{
	//CSpinCSLock(pmac->CBDebugCS);
	char txt[256];
	sprintf_s(txt, 256, "%s(%d,%d,%d,%d,%d)", loc, a, b, c, d, e);
	MICB(pmac, loc, txt);
}

static void _MICB6(CMachine *pmac, char const *loc, int a, int b, int c, int d, int e, int f)
{
	//CSpinCSLock(pmac->CBDebugCS);
	char txt[256];
	sprintf_s(txt, 256, "%s(%d,%d,%d,%d,%d,%d)", loc, a, b, c, d, e, f);
	MICB(pmac, loc, txt);
}

#define MICB0 if (((CMachineCallbacks *)this)->pMachine->CallbackDebugging()) _MICB0(((CMachineCallbacks *)this)->pMachine, __FUNCTION__)
#define MICB1(a) if (((CMachineCallbacks *)this)->pMachine->CallbackDebugging()) _MICB1(((CMachineCallbacks *)this)->pMachine, __FUNCTION__, (int)(a))
#define MICB2(a, b) if (((CMachineCallbacks *)this)->pMachine->CallbackDebugging()) _MICB2(((CMachineCallbacks *)this)->pMachine, __FUNCTION__, (int)(a), (int)(b))
#define MICB3(a, b, c) if (((CMachineCallbacks *)this)->pMachine->CallbackDebugging()) _MICB3(((CMachineCallbacks *)this)->pMachine, __FUNCTION__, (int)(a), (int)(b), (int)(c))
#define MICB4(a, b, c, d) if (((CMachineCallbacks *)this)->pMachine->CallbackDebugging()) _MICB4(((CMachineCallbacks *)this)->pMachine, __FUNCTION__, (int)(a), (int)(b), (int)(c), (int)(d))
#define MICB5(a, b, c, d, e) if (((CMachineCallbacks *)this)->pMachine->CallbackDebugging()) _MICB5(((CMachineCallbacks *)this)->pMachine, __FUNCTION__, (int)(a), (int)(b), (int)(c), (int)(d), (int)(e))
#define MICB6(a, b, c, d, e, f) if (((CMachineCallbacks *)this)->pMachine->CallbackDebugging()) _MICB6(((CMachineCallbacks *)this)->pMachine, __FUNCTION__, (int)(a), (int)(b), (int)(c), (int)(d), (int)(e), (int)(f))


extern CWaveInfo const *wavetableGetWave(int i);
CWaveInfo const *CMICallbacks::GetWave(int const i)
{
	MICB1(i);
	return wavetableGetWave(i);
}

extern int wavetableGetEnvSize(int const wave, int const env);
int CMICallbacks::GetEnvSize(int const wave, int const env)
{
	MICB2(wave, env);

	if (wave < 0)
	{
		BMPRandStat = -wave;
		BMPrand();
		return BMPRandStat;
	}

	return wavetableGetEnvSize(wave, env);
}

extern bool wavetableGetEnvPoint(int const wave, int const env, int const i, word &x, word &y, int &flags);
bool CMICallbacks::GetEnvPoint(int const wave, int const env, int const i, word &x, word &y, int &flags)
{
	MICB6(wave, env, i, x, y, flags);
	return wavetableGetEnvPoint(wave, env, i, x, y, flags);
}

extern CWaveLevel const *wavetableGetWaveLevel(int const i, int const level);
CWaveLevel const *CMICallbacks::GetWaveLevel(int const i, int const level)
{
	MICB2(i, level);
	return wavetableGetWaveLevel(i, level);
}

extern CMDKImplementation *NewMDKImp();

extern CWaveLevel const *wavetableGetNearestWaveLevel(int const i, int const note);
CWaveLevel const *CMICallbacks::GetNearestWaveLevel(int const i, int const note)
{
	MICB2(i, note);
	
	if (i == -1 && note == -1)
	{
		CMachineCallbacks *pmcb = (CMachineCallbacks *)this;

		IPC::Message m(IPC::HostInitMDK);
		m.Write(pmcb->pMachine->pHostMac);
		IPC::Message reply;
		DoCallback(m, reply);

		pmcb->pMachine->mdkImpl = NewMDKImp();
		return (CWaveLevel const *)pmcb->pMachine->mdkImpl;
	}
	else if (i == -2 && note == -2)
	{
		return (CWaveLevel const *)-1;
	}
	else if (i == -3 && note == -3)
	{
		return (CWaveLevel const*)-1;
	}
	
	return wavetableGetNearestWaveLevel(i, note);
}

	   
int CMICallbacks::GetFreeWave()
{
	MICB0;
	return 0;
}

bool CMICallbacks::AllocateWave(int const i, int const size, char const *name)
{
	MICB3(i, size, name);
	return false;
}

void CMICallbacks::MessageBox(char const *txt)
{
	MICB1(txt);

	AfxMessageBox(txt);
}

void CMICallbacks::Lock()
{
	MICB0;
}

void CMICallbacks::DebugLock(char const *sourcelocation)
{
	MICB1(sourcelocation);
}

void CMICallbacks::Unlock()
{
	MICB0;
}

int CMICallbacks::GetWritePos()
{
	MICB0;
	return gstate.ADWritePos;
}

int CMICallbacks::GetPlayPos()
{
	MICB0;
	return gstate.ADPlayPos;
}

float *CMICallbacks::GetAuxBuffer()
{
	MICB0;

	return AuxBuffer;
}

extern short const *OscTableOffsets[];

short const *CMICallbacks::GetOscillatorTable(int const waveform)
{
	MICB1(waveform);
	
	if (waveform < 0 || waveform > 5)
		return NULL;

	return OscTableOffsets[waveform];
}

void CMICallbacks::ClearAuxBuffer()
{
	MICB0;

	memset(AuxBuffer, 0, MAX_BUFFER_LENGTH * 4);
	return;
}

extern void ScheduleEvent(int const time, CMachine *pMac, dword data);

void CMICallbacks::ScheduleEvent(int const time, dword const data)
{
	MICB2(time, data);
}

void CMICallbacks::MidiOut(int const dev, dword const data)
{
	MICB2(dev, data);
	IPC::Message m(IPC::HostMidiOut);
	m.Write(dev);
	m.Write(data);
	IPC::Message reply;
	DoCallback(m, reply);
}

// pattern editing

void CMICallbacks::SetNumberOfTracks(int const n)
{
	MICB1(n);
}

CPattern *CMICallbacks::CreatePattern(char const *name, int const length)
{
	MICB2(name, length);
	return NULL;
}

CPattern *CMICallbacks::GetPattern(int const index)
{
	MICB1(index);
	return NULL;
}

char const *CMICallbacks::GetPatternName(CPattern *ppat)
{		
	MICB1(ppat);
	return "00";
}

void CMICallbacks::SetPatternName(CPattern *ppat, char const *name)
{
	MICB2(ppat, name);
}



void CMICallbacks::RenamePattern(char const *oldname, char const *newname)
{
	MICB2(oldname, newname);

}

void CMICallbacks::DeletePattern(CPattern *ppat)
{
	MICB1(ppat);

}

int CMICallbacks::GetPatternData(CPattern *ppat, int const row, int const group, int const track, int const field)
{
	MICB5(ppat, row, group, track, field);
	return 0;
}

void CMICallbacks::SetPatternData(CPattern *ppat, int const row, int const group, int const track, int const field, int const value)
{
	MICB6(ppat, row, group, track, field, value);
}

CSequence *CMICallbacks::CreateSequence()
{
	MICB0;
	return NULL;
}

void CMICallbacks::DeleteSequence(CSequence *pseq)
{
	MICB1(pseq);
}

CPattern *CMICallbacks::GetSequenceData(int const row)
{
	MICB1(row);
	return NULL;
}

void CMICallbacks::SetSequenceData(int const row, CPattern *ppat)
{
	MICB2(row, ppat);
}

extern bool g_Recording;

void CMICallbacks::ControlChange__obsolete__(int group, int track, int param, int value)
{
	MICB4(group, track, param, value);
}

CMachine *CMICallbacks::GetThisMachine()
{
	MICB0;

	CMachineCallbacks *pmcb = (CMachineCallbacks *)this;
	return pmcb->pMachine;
}

void CMICallbacks::ControlChange(CMachine *pmac, int group, int track, int param, int value)
{
	MICB5(pmac, group, track, param, value);

	if (pmac == NULL)
		return;

	IPC::Message m(IPC::HostControlChange);
	m.Write(pmac->pHostMac);
	m.Write(group);
	m.Write(track);
	m.Write(param);
	m.Write(value);
	IPC::Message reply;
	DoCallback(m, reply);
}

void CMICallbacks::ControlChangeImmediate(CMachine *pmac, int group, int track, int param, int value)
{
	MICB5(pmac, group, track, param, value);

	if (pmac == NULL)
		return;

	IPC::Message m(IPC::HostControlChangeImmediate);
	m.Write(pmac->pHostMac);
	m.Write(group);
	m.Write(track);
	m.Write(param);
	m.Write(value);
	IPC::Message reply;
	DoCallback(m, reply);
}

void CMICallbacks::SendControlChanges(CMachine *pmac)
{
	MICB1(pmac);

	if (pmac == NULL)
		return;

	IPC::Message m(IPC::HostSendControlChanges);
	m.Write(pmac->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);
}


CSequence *CMICallbacks::GetPlayingSequence(CMachine *pmac)
{
	MICB1(pmac);
	// return NULL;
	if (pmac == NULL)
		return NULL;

	IPC::Message m(IPC::HostGetPlayingSequence);
	m.Write(pmac->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);

	IPC::MessageReader r(reply);
	DWORD seq = r.ReadDWORD();
	return (CSequence*)seq;
}

void *CMICallbacks::GetPlayingRow(CSequence *pseq, int group, int track)
{
	MICB3(pseq, group, track);
	return rowData;
}


void CMICallbacks::SetMachineInterfaceEx(CMachineInterfaceEx *pex)
{
	MICB1(pex);

	CMachineCallbacks *pmcb = (CMachineCallbacks *)this;
	pmcb->pMachine->pInterfaceEx = pex;

	IPC::Message m(IPC::HostInitMIEx);
	m.Write(pmcb->pMachine->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);

}

int CMICallbacks::GetStateFlags()
{
	MICB0;
	return gstate.StateFlags;
}


CMICallbacks g_MICallbacks;


int CMICallbacks::ADGetnumChannels(bool input)
{
	MICB1(input);
	return 0;
}

void CMICallbacks::ADWrite(int channel, float *psamples, int numsamples)
{
	MICB3(channel, psamples, numsamples);
}

void CMICallbacks::ADRead(int channel, float *psamples, int numsamples)
{
	MICB3(channel, psamples, numsamples);
}


void CMICallbacks::SetnumOutputChannels(CMachine *pmac, int n)
{
	MICB2(pmac, n);

	CMachineCallbacks *pmcb = (CMachineCallbacks *)this;

	IPC::Message m(IPC::HostSetnumOutputChannels);
	m.Write(pmcb->pMachine->pHostMac);
	m.Write(n);
	IPC::Message reply;
	DoCallback(m, reply);
}
#define CALL_MEMBER_FN(object, ptrToMember) ((object).*(ptrToMember));

void CMICallbacks::SetEventHandler(CMachine *pmac, BEventType et, EVENT_HANDLER_PTR p, void *param)
{
	MICB4(pmac, et, 0, param);		// p is missing

	//void *paddr = (void *&)p;								// void pointer to function
	//EVENT_HANDLER_PTR eh = *(EVENT_HANDLER_PTR*)&paddr;		// cast it back
	//bool ret = ((pmac->pInterface)->*(eh))(0);				// And call

	CMachineCallbacks* pmcb = (CMachineCallbacks*)this;

	IPC::Message m(IPC::HostSetEventHandler);
	m.Write(pmcb->pMachine->pHostMac);
	m.Write(et);
	m.WritePtr(p);
	m.WritePtr(param);
	IPC::Message reply;
	DoCallback(m, reply);
}

extern char const *wavetableGetWaveName(int i);

char const *CMICallbacks::GetWaveName(int const i)
{
	MICB1(i);
	return wavetableGetWaveName(i);
}

void CMICallbacks::SetInternalWaveName(CMachine *pmac, int const i, char const *name)
{
	MICB3(pmac, i, name);

}

void CMICallbacks::GetMachineNames(CMachineDataOutput *pout)
{
	MICB1(pout);

	CMachineCallbacks* pmcb = (CMachineCallbacks*)this;

	IPC::Message m(IPC::HostGetMachineNames);
	m.Write(pmcb->pMachine->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);

	IPC::MessageReader r(reply);
	while (true)
	{
		std::string name;
		
		name = r.ReadString();

		if (name.size() == 0)
			break;

		pout->Write(name.c_str());
	}
}

CMachine *CMICallbacks::GetMachine(char const *name)
{
	MICB1(name);

	// Create dummy machine?
	CMachineCallbacks* pmcb = (CMachineCallbacks*)this;

	IPC::Message m(IPC::HostCreateRefMachine);
	m.Write(name);
	IPC::Message reply;
	DoCallback(m, reply);
	IPC::MessageReader r(reply);

	bool found;
	r.Read(found);

	if (!found)
		return NULL;

	CMachine* refmac = (CMachine *)r.ReadPtr();
	if (refmac == NULL)
	{
		if (pmcb->machineReferences[name] == NULL)
		{
			// Request enough data to make a dummy machine that other machines can use as a reference

			refmac = new CMachine();
			r.Read(refmac->pHostMac);
			// Create MachineDLL and read machine info from host
			MachineDLL* mDll = new MachineDLL(r);
			refmac->TransferDllRef(mDll);
			pmcb->machineReferences[name] = refmac;
		}
		else
		{
			refmac = pmcb->machineReferences.find(name)->second;
		}
	}

	// Save the name
	return refmac;
}

CMachineInfo const *CMICallbacks::GetMachineInfo(CMachine *pmac)
{
	MICB1(pmac);
	if (pmac == NULL) return NULL;
	if (pmac->pTemplate == NULL) return NULL;
	if (pmac->pTemplate->pInfo != NULL)
		return pmac->pTemplate->pInfo;
	else if (pmac->pTemplateRef != NULL)
		return pmac->pTemplateRef->pInfoRef;
	return NULL;
}

char const *CMICallbacks::GetMachineName(CMachine *pmac)
{
	MICB1(pmac);

	if (pmac == NULL) return NULL;

	CMachineCallbacks* pmcb = (CMachineCallbacks*)this;

	IPC::Message m(IPC::HostGetMachineName);
	m.Write(pmac->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);
	
	std::string name;

	IPC::MessageReader r(reply);
	name = r.ReadString();

	if (pmcb->machineNames.count(name) == 0)
	{
		auto buf = (char*)malloc(256);
		if (buf != NULL)
		{
			buf[0] = 0;
		}
		pmcb->machineNames[name] = buf;
	}

	// Save the name
	char* buf = pmcb->machineNames[name];
	if (buf != NULL)
	{
		int len = min(strlen(name.c_str()), 255);
		strcpy(buf, name.c_str());
		buf[len] = 0;
	}

	return pmcb->machineNames[name];
}

bool CMICallbacks::GetInput(int index, float *psamples, int numsamples, bool stereo, float *extrabuffer)
{
	MICB5(index, psamples, numsamples, stereo, extrabuffer);

	return false;
}

int CMICallbacks::GetHostVersion()
{
	MICB0;

	return MI_VERSION;
}

// if host version >= 2
int CMICallbacks::GetSongPosition()
{
	MICB0;
	return gstate.SongPosition;
}

void CMICallbacks::SetSongPosition(int pos)
{
	MICB1(pos);

	IPC::Message m(IPC::HostSetSongPosition);
	m.Write(pos);
	IPC::Message reply;
	DoCallback(m, reply);
}


int CMICallbacks::GetTempo()
{
	MICB0;

	return g_MasterInfo.BeatsPerMin;
}

void CMICallbacks::SetTempo(int bpm)
{
	MICB1(bpm);
	IPC::Message m(IPC::HostSetTempo);
	m.Write(bpm);
	IPC::Message reply;
	DoCallback(m, reply);
}

int CMICallbacks::GetTPB()
{
	MICB0;

	return g_MasterInfo.TicksPerBeat;
}

void CMICallbacks::SetTPB(int tpb)
{
	MICB1(tpb);
	IPC::Message m(IPC::HostSetTPB);
	m.Write(tpb);
	IPC::Message reply;
	DoCallback(m, reply);
}

int CMICallbacks::GetLoopStart()
{
	MICB0;
	return gstate.LoopStart;
}

int CMICallbacks::GetLoopEnd()
{
	MICB0;
	return gstate.LoopEnd;
}

int CMICallbacks::GetSongEnd()
{
	MICB0;
	return gstate.SongEnd;
}

void CMICallbacks::Play()
{
	MICB0;
	IPC::Message m(IPC::HostPlay);
	IPC::Message reply;
	DoCallback(m, reply);
}

void CMICallbacks::Stop()
{
	MICB0;
}

bool CMICallbacks::RenameMachine(CMachine *pmac, char const *name)
{
	MICB2(pmac, name);

	if (pmac == NULL) return NULL;

	IPC::Message m(IPC::HostRenameMachine);
	m.Write(pmac->pHostMac);
	m.Write(name);
	IPC::Message reply;
	DoCallback(m, reply);
	
	IPC::MessageReader r(reply);
	bool ret;
	r.Read(ret);

	return ret;
}

	
void CMICallbacks::SetModifiedFlag()
{
	MICB0;
	CMachineCallbacks* pmcb = (CMachineCallbacks*)this;

	IPC::Message m(IPC::HostSetModifyFlag);
	m.Write(pmcb->pMachine->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);
}

extern int g_MasterFrame;

int CMICallbacks::GetAudioFrame()
{
	MICB0;
	return gstate.AudioFrame;
}

bool CMICallbacks::HostMIDIFiltering()
{
	MICB0;
	return gstate.MIDIFiltering;
}

extern COLORREF GetGUIColor(char const *name);

dword CMICallbacks::GetThemeColor(char const *name)
{
	MICB1(name);
	return 0;
}

void CMICallbacks::WriteProfileInt(char const *entry, int value)
{
	MICB2(entry, value);
}

void CMICallbacks::WriteProfileString(char const *entry, char const *value)
{
	MICB2(entry, value);
}

void CMICallbacks::WriteProfileBinary(char const *entry, byte *data, int nbytes)
{
	MICB3(entry, data, nbytes);
}


int CMICallbacks::GetProfileInt(char const *entry, int defvalue)
{
	MICB2(entry, defvalue);
	return 0;
}

void CMICallbacks::GetProfileString(char const *entry, char const *value, char const *defvalue)
{
	MICB3(entry, value, defvalue);
}


void CMICallbacks::GetProfileBinary(char const *entry, byte **data, int *nbytes)
{
	MICB3(entry, data, nbytes);
	*nbytes = 0; // FIXME: Read from registry
}

void CMICallbacks::FreeProfileBinary(byte *data)
{
	MICB1(data);

	delete[] data;
}

int CMICallbacks::GetNumTracks(CMachine *pmac)
{
	MICB1(pmac);
	if (pmac->pInterface != NULL)
		return pmac->numTracks;
	else
	{
		IPC::Message m(IPC::HostGetNumTracks);
		m.Write(pmac->pHostMac);
		IPC::Message reply;
		DoCallback(m, reply);
		IPC::MessageReader r(reply);
		return r.ReadDWORD();
	}
}

void CMICallbacks::SetNumTracks(CMachine *pmac, int n)
{
	MICB2(pmac, n);

	pmac->SetnumTracks(n);
}

void CMICallbacks::SetPatternEditorStatusText(int pane, char const *text)
{
	MICB2(pane, text);
}

char const *CMICallbacks::DescribeValue(CMachine *pmac, int const param, int const value)
{
	MICB3(pmac, param, value);
	if (pmac->pInterface != NULL)
	{
		return pmac->pInterface->DescribeValue(param, value);
	}
	return NULL;
}

int CMICallbacks::GetBaseOctave()
{
	MICB0;
	return 4;
}

int CMICallbacks::GetSelectedWave()
{
	MICB0;
	return 0;
}

void CMICallbacks::SelectWave(int i)
{
	MICB1(i);
}

void CMICallbacks::SetPatternLength(CPattern *p, int length)
{
	MICB2(p, length);
}

int CMICallbacks::GetPatternLength(CPattern *p)
{
	MICB1(p);
	return 0;
}

int CMICallbacks::GetParameterState(CMachine *pmac, int group, int track, int param)
{
	MICB4(pmac, group, track, param);
	if (pmac == NULL)
		return 0;

	IPC::Message m(IPC::HostGetParameterState);
	m.Write(pmac->pHostMac);
	m.Write(group);
	m.Write(track);
	m.Write(param);
	IPC::Message reply;
	DoCallback(m, reply);
	IPC::MessageReader r(reply);
	return r.ReadDWORD();
}

void CMICallbacks::ShowMachineWindow(CMachine *pmac, bool show)
{
	MICB2(pmac, show);
}

void CMICallbacks::SetPatternEditorMachine(CMachine *pmac, bool gotoeditor)
{
	MICB2(pmac, gotoeditor);
}

extern CSubTickInfo g_SubTickInfo;
extern bool g_SubTickInfoAvailable;

extern CSubTickInfo g_osSubTickInfo;

CSubTickInfo const *CMICallbacks::GetSubTickInfo()
{
	MICB0;
	return NULL;
	/*
	if (!g_SubTickInfoAvailable)
		return NULL;

	CMachineCallbacks *pmcb = (CMachineCallbacks *)this;

	if (pmcb->pMachine->oversampler != NULL)
		return &g_osSubTickInfo;
	else
		return &g_SubTickInfo;
	*/
}

int CMICallbacks::GetSequenceColumn(CSequence *s)
{
	MICB1(s);
	return NULL;
}

void CMICallbacks::SetGroovePattern(float *data, int size)
{
	MICB2(data, size);
}

int CMICallbacks::GetAttribute(CMachine *pmac, int index)
{
	MICB2(pmac, index);
	if (pmac->pInterface != NULL)
	{
		if (pmac == NULL || index < 0 || index >= pmac->pTemplate->pInfo->numAttributes)
			return 0;

		return pmac->pInterface->AttrVals[index];
	}
	else
	{
		IPC::Message m(IPC::HostGetAttribute);
		m.Write(pmac->pHostMac);
		m.Write(index);
		IPC::Message reply;
		DoCallback(m, reply);
		IPC::MessageReader r(reply);
		return r.ReadDWORD();
	}
}

void CMICallbacks::SetAttribute(CMachine *pmac, int index, int value)
{
	MICB3(pmac, index, value);

	if (pmac->pInterface != NULL)
	{
		if (pmac == NULL || index < 0 || index >= pmac->pTemplate->pInfo->numAttributes)
			return;

		CMachineAttribute const** pat = pmac->pTemplate->pInfo->Attributes;

		if (pat == NULL)
			return;

		pat += index;

		if (value < (*pat)->MinValue || value >(*pat)->MaxValue)
			value = (*pat)->DefValue;

		pmac->pInterface->AttrVals[index] = value;
	}
	else
	{
		IPC::Message m(IPC::HostSetAttribute);
		m.Write(pmac->pHostMac);
		m.Write(index);
		m.Write(value);
		IPC::Message reply;
		DoCallback(m, reply);
	}
}

void CMICallbacks::AttributesChanged(CMachine *pmac)
{
	MICB1(pmac);

	if (pmac == NULL)
		return;

	if (pmac->pInterface != NULL)
	{
		//	MLOCK;
		pmac->pInterface->AttributesChanged();
	}
	else
	{
		// Call host
		IPC::Message m(IPC::HostAttributesChanged);
		m.Write(pmac->pHostMac);
		IPC::Message reply;
		DoCallback(m, reply);
	}

}

void CMICallbacks::GetMachinePosition(CMachine *pmac, float &x, float &y)
{
	MICB3(pmac, x, y);
	x = y = 0;
}

void CMICallbacks::SetMachinePosition(CMachine *pmac, float x, float y)
{
	MICB3(pmac, x, y);
}

void CMICallbacks::MuteMachine(CMachine *pmac, bool mute)
{
	MICB2(pmac, mute);
}

void CMICallbacks::SoloMachine(CMachine *pmac)
{
	MICB1(pmac);
}

void CMICallbacks::UpdateParameterDisplays(CMachine *pmac)
{
	MICB1(pmac);

}

void CMICallbacks::WriteLine(char const *text)
{
	MICB1(text);

	return;

	// Don't use DCWrite. Use callback.
	IPC::Message m(IPC::HostDCWriteLine);
	m.Write(text);
	IPC::Message reply;
	DoCallback(m, reply);
}

bool CMICallbacks::GetPlayNotesState()
{
	MICB0;
	return false;
}

void CMICallbacks::EnableMultithreading(bool enable)
{
	MICB1(enable);
}

CPattern *CMICallbacks::GetPatternByName(CMachine *pmac, char const *patname)
{
	MICB2(pmac, patname);
	return NULL;
}

CMachine *CMICallbacks::GetPatternOwner(CPattern *p)
{
	MICB1(p);
	return NULL;
}


bool CMICallbacks::MachineImplementsFunction(CMachine *pmac, int vtblindex, bool miex)
{
	MICB3(pmac, vtblindex, miex);
	return false;
}

void CMICallbacks::SendMidiNote(CMachine *pmac, int const channel, int const value, int const velocity)
{
	MICB4(pmac, channel, value, velocity);

	IPC::Message m(IPC::HostSendMidiNote);
	m.Write(pmac->pHostMac);
	m.Write(channel);
	m.Write(value);
	m.Write(velocity);
	IPC::Message reply;
	DoCallback(m, reply);
}

void CMICallbacks::SendMidiControlChange(CMachine *pmac, int const ctrl, int const channel, int const value)
{
	MICB4(pmac, ctrl, channel, value);

	IPC::Message m(IPC::HostSendMidiControlChange);
	m.Write(pmac->pHostMac);
	m.Write(ctrl);
	m.Write(channel);
	m.Write(value);
	IPC::Message reply;
	DoCallback(m, reply);
}

//extern char const *BuildCount;

int CMICallbacks::GetBuildNumber()
{
	MICB0;
	//return ::atoi(BuildCount);
	return 1700;
}

void CMICallbacks::SetMidiFocus(CMachine *pmac)
{
	MICB1(pmac);

	IPC::Message m(IPC::HostSetMidiFocus);
	m.Write(pmac->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);
}

void CMICallbacks::BeginWriteToPlayingPattern(CMachine *pmac, int quantization, CPatternWriteInfo &outwi)
{
	// This could be sped up. Get Row and BuzzTickPosition on tick to avoid IPC.
	// If quantization > 0, then call
	MICB3(pmac, quantization, 0);
	if (pmac->writing_to_pattern)
		return;

	pmac->writing_to_pattern = true;

	if (quantization == 0)
	{
		outwi.BuzzTickPosition = pmac->buzzTickPosition;
		outwi.Row = pmac->row;
		return;
	}

	pmac->g_machine_cc.Lock();
;
	IPC::Message m(IPC::HostBeginWriteToPlayingPattern);
	m.Write(pmac->pHostMac);
	m.Write(quantization);
	IPC::Message reply;
	DoCallback(m, reply);

	IPC::MessageReader r(reply);

	outwi.Row = r.ReadDWORD();
	r.Read(outwi.BuzzTickPosition);
	pmac->g_machine_cc.Unlock();
}

void CMICallbacks::WriteToPlayingPattern(CMachine *pmac, int group, int track, int param, int value)
{
	if (!pmac->writing_to_pattern)
		return;

	pmac->g_machine_cc.Lock();
	MICB5(pmac, group, track, param, value);
	IPC::Message m(IPC::HostWriteToPlayingPattern);
	m.Write(pmac->pHostMac);
	m.Write(group);
	m.Write(track);
	m.Write(param);
	m.Write(value);
	IPC::Message reply;
	DoCallback(m, reply);
	pmac->g_machine_cc.Unlock();
}

void CMICallbacks::EndWriteToPlayingPattern(CMachine *pmac)
{
	if (!pmac->writing_to_pattern)
		return;

	pmac->writing_to_pattern = false;
	return; // No need tha call host
	
	pmac->g_machine_cc.Lock();
	IPC::Message m(IPC::HostEndWriteToPlayingPattern);
	m.Write(pmac->pHostMac);
	IPC::Message reply;
	DoCallback(m, reply);

	pmac->g_machine_cc.Unlock();
	pmac->writing_to_pattern = false;
	return;
}

extern HWND g_HostMainWnd;
extern HWND g_ChildMainWnd;

void *CMICallbacks::GetMainWindow()
{
	MICB0;

	//return ::AfxGetMainWnd()->GetSafeHwnd();
	//return g_ChildMainWnd; // Or g_HostMainWnd?
	return g_HostMainWnd; 
}

void CMICallbacks::SetInputChannelCount(int count)
{
	MICB1(count);

	CMachineCallbacks *pmcb = (CMachineCallbacks *)this;
	
	pmcb->pMachine->SetInputChannelCount(count);

	IPC::Message m(IPC::HostSetInputChannelCount);
	m.Write(pmcb->pMachine->pHostMac);
	m.Write(count);
	IPC::Message reply;
	DoCallback(m, reply);

}

void CMICallbacks::SetOutputChannelCount(int count)
{
	MICB1(count);

	CMachineCallbacks *pmcb = (CMachineCallbacks *)this;

	pmcb->pMachine->SetOutputChannelCount(count);

	IPC::Message m(IPC::HostSetOutputChannelCount);
	m.Write(pmcb->pMachine->pHostMac);
	m.Write(count);
	IPC::Message reply;
	DoCallback(m, reply);

}

extern bool g_ClearingSong;

bool CMICallbacks::IsSongClosing()
{
	MICB0;
	return false;
}

void CMICallbacks::SetMidiInputMode(MidiInputMode mode)
{
	MICB1(mode);

}

int CMICallbacks::RemapLoadedMachineParameterIndex(CMachine *pmac, int i)
{
	MICB2(pmac, i);
	return i;
}

char const *CMICallbacks::GetThemePath()
{
	MICB0;
	return NULL;
}

void CMICallbacks::InvalidateParameterValueDescription(CMachine *pmac, int index)
{
	MICB2(pmac, index);
}

void CMICallbacks::RemapLoadedMachineName(char* name, int bufsize)
{
	MICB2(name, bufsize);
	CMachineCallbacks* pmcb = (CMachineCallbacks*)this;
	
	if (pmcb->remappedMachineNames.count(name))
	{
		string newname = pmcb->remappedMachineNames[name];
		int copylen = min(strlen(newname.c_str()), bufsize - 1);
		strncpy(name, newname.c_str(), copylen);
		name[copylen] = 0;
	}
}

bool CMICallbacks::IsMachineMuted(CMachine *pmac)
{
	MICB1(pmac);
	return false;
}

int CMICallbacks::GetInputChannelConnectionCount(CMachine *pmac, int channel)
{
	MICB2(pmac, channel);
	return 1;
}

int CMICallbacks::GetOutputChannelConnectionCount(CMachine *pmac, int channel)
{
	MICB2(pmac, channel);
	return 1;
}

const CString REG_SW_GROUP_REBUZZ_MIDI = _T("Software\\ReBuzz\\BuzzGUI\\MIDISettings");
const CString REG_KEY_MASTER_KEYBOARD_MODE = _T("MasterKeyboardMode");
const CString REG_SW_GROUP_REBUZZ_ENGINE = _T("Software\\ReBuzz\\BuzzGUI\\EngineSettings");
const CString REG_KEY_ACCURATE_BPM = _T("AccurateBPM");
const CString REG_KEY_SUB_TICK_TIMING = _T("SubTickTiming");


bool IsRegKeySet(CString group, CString key)
{
	CRegKey regKey;
	TCHAR pszValue[24];
	ULONG nValueLength = _countof(pszValue);

	if (ERROR_SUCCESS != regKey.Open(HKEY_CURRENT_USER, group, KEY_READ))
	{	
		regKey.Close();
		goto Function_Exit;
	}
	if (ERROR_SUCCESS != regKey.QueryStringValue(key, pszValue, &nValueLength))
	{
		regKey.Close();
		goto Function_Exit;
	}

	return strcmp(pszValue, "True") == 0;

Function_Exit:
	return false;
}

bool CMICallbacks::GetOption(char const *name)
{
	// Avoid IPC calls.
	CMachineCallbacks* pmcb = (CMachineCallbacks*)this;

	if (strcmp(name, "Master Keyboard Mode") == 0) {
		return IsRegKeySet(REG_SW_GROUP_REBUZZ_MIDI, REG_KEY_MASTER_KEYBOARD_MODE);
	}
	else if (strcmp(name, "Accurate BPM") == 0) {
		return IsRegKeySet(REG_SW_GROUP_REBUZZ_ENGINE, REG_KEY_ACCURATE_BPM);
	}
	else if (strcmp(name, "SubTick Timing") == 0) {
		return IsRegKeySet(REG_SW_GROUP_REBUZZ_ENGINE, REG_KEY_SUB_TICK_TIMING);
	}
	else
	{

	}

	return false;
	
	/*
	IPC::Message m(IPC::HostGetOption);
	m.Write(pmcb->pMachine->pHostMac);
	m.Write(name);
	IPC::Message reply;
	DoCallback(m, reply);

	IPC::MessageReader r(reply);
	bool ret;
	r.Read(ret);
	return ret;
	*/
}

void CMICallbacks::ToggleRecordMode()
{
	MICB0;
}

int CMICallbacks::GetSequenceCount(CMachine *pmac)
{
	MICB1(pmac);
	return 0;
}

CSequence *CMICallbacks::GetSequence(CMachine *pmac, int index)
{
	MICB2(pmac, index);
	return NULL;
}

CPattern *CMICallbacks::GetPlayingPattern(CSequence *pseq)
{
	MICB1(pseq);
	return NULL;
}

int CMICallbacks::GetPlayingPatternPosition(CSequence *pseq)
{
	MICB1(pseq);
	return -1;
}

bool CMICallbacks::IsValidAsciiChar(CMachine *pmac, int param, char ch)
{
	MICB3(pmac, param, ch);
	return true;
}

int CMICallbacks::GetConnectionCount(CMachine *pmac, bool output)
{
	MICB2(pmac, output);
	return 0;
}

CMachineConnection *CMICallbacks::GetConnection(CMachine *pmac, bool output, int index)
{
	MICB3(pmac, output, index);
	return NULL;
}

CMachine *CMICallbacks::GetConnectionSource(CMachineConnection *pmc, int &channel)
{
	MICB2(pmc, channel);
	return NULL;
}

CMachine *CMICallbacks::GetConnectionDestination(CMachineConnection *pmc, int &channel)
{
	MICB2(pmc, channel);
	return NULL;
}

int CMICallbacks::GetTotalLatency()
{
	return 0;
}

void *CMICallbacks::GetMachineModuleHandle(CMachine *pmac)
{
	MICB1(pmac);
	return NULL;
}
