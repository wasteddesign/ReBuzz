
#include "MachineInterface.h"

/*
* These are stub methods to resolve linker issues, which occur because we say we inherit from
* 'CMICallbacks', and this causes the linker to want to find ALL the methods from CMICallbacks.
*
* For actual implementation, please override and put that stuff into MessageCallbackWrapper.cpp
* 
* ALSO: This file MUST be complied with Common Runtime Language support TURNED OFF
*/

CWaveInfo const* CMICallbacks::GetWave(int const i)
{
	return NULL;
}

CWaveLevel const* CMICallbacks::GetWaveLevel(int const i, int const level)
{
	return NULL;
}


void CMICallbacks::MessageBox(char const* txt)
{}

void CMICallbacks::Lock()
{}

void CMICallbacks::Unlock()
{}

int CMICallbacks::GetWritePos()
{
	return -1;
}

int CMICallbacks::GetPlayPos()
{
	return -1;
}

float* CMICallbacks::GetAuxBuffer()
{
	return NULL;
}

void CMICallbacks::ClearAuxBuffer()
{}

int CMICallbacks::GetFreeWave()
{
	return -1;
}

bool CMICallbacks::AllocateWave(int const i, int const size, char const* name)
{
	return false;
}

void CMICallbacks::ScheduleEvent(int const time, dword const data)
{}

void CMICallbacks::MidiOut(int const dev, dword const data)
{}

short const* CMICallbacks::GetOscillatorTable(int const waveform)
{
	return NULL;
}

int CMICallbacks::GetEnvSize(int const wave, int const env)
{
	return -1;
}

bool CMICallbacks::GetEnvPoint(int const wave, int const env, int const i, word& x, word& y, int& flags)
{
	return false;
}

CWaveLevel const* CMICallbacks::GetNearestWaveLevel(int const i, int const note)
{
	return NULL;
}

void CMICallbacks::SetNumberOfTracks(int const n)
{}

CPattern* CMICallbacks::CreatePattern(char const* name, int const length)
{
	return NULL;
}

CPattern* CMICallbacks::GetPattern(int const index)
{
	return NULL;
}

char const* CMICallbacks::GetPatternName(CPattern* ppat)
{
	return NULL;
}

void CMICallbacks::RenamePattern(char const* oldname, char const* newname)
{}

void CMICallbacks::DeletePattern(CPattern* ppat)
{}

int CMICallbacks::GetPatternData(CPattern* ppat, int const row, int const group, int const track, int const field)
{
	return -1;
}

void CMICallbacks::SetPatternData(CPattern* ppat, int const row, int const group, int const track, int const field, int const value)
{}

CSequence* CMICallbacks::CreateSequence()
{
	return NULL;
}

void CMICallbacks::DeleteSequence(CSequence* pseq)
{}

CPattern* CMICallbacks::GetSequenceData(int const row)
{
	return NULL;
}

void  CMICallbacks::SetSequenceData(int const row, CPattern* ppat)
{}

void CMICallbacks::SetMachineInterfaceEx(CMachineInterfaceEx* pex)
{}

void CMICallbacks::ControlChange__obsolete__(int group, int track, int param, int value)
{}

int CMICallbacks::ADGetnumChannels(bool input)
{
	return -1;
}

void CMICallbacks::ADWrite(int channel, float* psamples, int numsamples)
{}

void CMICallbacks::ADRead(int channel, float* psamples, int numsamples)
{}

CMachine* CMICallbacks::GetThisMachine()
{
	return NULL;
}

void CMICallbacks::ControlChange(CMachine* pmac, int group, int track, int param, int value)
{}

CSequence* CMICallbacks::GetPlayingSequence(CMachine* pmac)
{
	return NULL;
}

void* CMICallbacks::GetPlayingRow(CSequence* pseq, int group, int track)
{
	return NULL;
}

int CMICallbacks::GetStateFlags()
{
	return -1;
}

void CMICallbacks::SetnumOutputChannels(CMachine* pmac, int n)
{}

void CMICallbacks::SetEventHandler(CMachine* pmac, BEventType et, EVENT_HANDLER_PTR p, void* param)
{}

char const* CMICallbacks::GetWaveName(int const i)
{
	return NULL;
}

void CMICallbacks::SetInternalWaveName(CMachine* pmac, int const i, char const* name)
{}

void CMICallbacks::GetMachineNames(CMachineDataOutput* pout)
{}

CMachine* CMICallbacks::GetMachine(char const* name)
{
	return NULL;
}

CMachineInfo const* CMICallbacks::GetMachineInfo(CMachine* pmac)
{
	return NULL;
}

char const* CMICallbacks::GetMachineName(CMachine* pmac)
{
	return NULL;
}

bool CMICallbacks::GetInput(int index, float* psamples, int numsamples, bool stereo, float* extrabuffer)
{
	return false;
}

int CMICallbacks::GetHostVersion()
{
	return 0;
}

int CMICallbacks::GetSongPosition()
{
	return -1;
}

void CMICallbacks::SetSongPosition(int pos)
{}

int CMICallbacks::GetTempo()
{
	return -1;
}

void CMICallbacks::SetTempo(int bpm)
{}

int CMICallbacks::GetTPB()
{
	return -1;
}

void CMICallbacks::SetTPB(int tpb)
{}

int CMICallbacks::GetLoopStart()
{
	return -1;
}

int CMICallbacks::GetLoopEnd()
{
	return -1;
}

int CMICallbacks::GetSongEnd()
{
	return -1;
}

void CMICallbacks::Play()
{}

void CMICallbacks::Stop()
{}

bool CMICallbacks::RenameMachine(CMachine* pmac, char const* name)
{
	return false;
}

void CMICallbacks::SetModifiedFlag()
{}

int CMICallbacks::GetAudioFrame()
{
	return -1;
}

bool CMICallbacks::HostMIDIFiltering()
{
	return false;
}

dword CMICallbacks::GetThemeColor(char const* name)
{
	return 0;
}

void CMICallbacks::WriteProfileInt(char const* entry, int value)
{}

void CMICallbacks::WriteProfileString(char const* entry, char const* value)
{}

void CMICallbacks::WriteProfileBinary(char const* entry, byte* data, int nbytes)
{}

int CMICallbacks::GetProfileInt(char const* entry, int defvalue)
{
	return defvalue;
}

void CMICallbacks::GetProfileString(char const* entry, char const* value, char const* defvalue)
{
}

void CMICallbacks::GetProfileBinary(char const* entry, byte** data, int* nbytes)
{}

void CMICallbacks::FreeProfileBinary(byte* data)
{}

int CMICallbacks::GetNumTracks(CMachine* pmac)
{
	return 0;
}

void CMICallbacks::SetNumTracks(CMachine* pmac, int n)
{}

void CMICallbacks::SetPatternEditorStatusText(int pane, char const* text)
{}

char const* CMICallbacks::DescribeValue(CMachine* pmac, int const param, int const value)
{
	return NULL;
}

int CMICallbacks::GetBaseOctave()
{
	return -1;
}

int CMICallbacks::GetSelectedWave()
{
	return 0;
}

void CMICallbacks::SelectWave(int i)
{}

void CMICallbacks::SetPatternLength(CPattern* p, int length)
{}

int CMICallbacks::GetParameterState(CMachine* pmac, int group, int track, int param)
{
	return -1;
}

void CMICallbacks::ShowMachineWindow(CMachine* pmac, bool show)
{}

void CMICallbacks::SetPatternEditorMachine(CMachine* pmac, bool gotoeditor)
{}

CSubTickInfo const* CMICallbacks::GetSubTickInfo()
{
	return NULL;
}

int CMICallbacks::GetSequenceColumn(CSequence* s)
{
	return -1;
}

void CMICallbacks::SetGroovePattern(float* data, int size)
{}

void CMICallbacks::ControlChangeImmediate(CMachine* pmac, int group, int track, int param, int value)
{}

void CMICallbacks::SendControlChanges(CMachine* pmac)
{}

int CMICallbacks::GetAttribute(CMachine* pmac, int index)
{
	return -1;
}

void CMICallbacks::SetAttribute(CMachine* pmac, int index, int value)
{}

void CMICallbacks::AttributesChanged(CMachine* pmac)
{}

void CMICallbacks::GetMachinePosition(CMachine* pmac, float& x, float& y)
{}

void CMICallbacks::SetMachinePosition(CMachine* pmac, float x, float y)
{}

void CMICallbacks::MuteMachine(CMachine* pmac, bool mute)
{}

void CMICallbacks::SoloMachine(CMachine* pmac)
{}

void CMICallbacks::UpdateParameterDisplays(CMachine* pmac)
{}

void CMICallbacks::WriteLine(char const* text)
{}

bool CMICallbacks::GetOption(char const* name)
{
	return false;
}

bool CMICallbacks::GetPlayNotesState()
{
	return false;
}

void CMICallbacks::EnableMultithreading(bool enable)
{}

CPattern* CMICallbacks::GetPatternByName(CMachine* pmac, char const* patname)
{
	return NULL;
}

void CMICallbacks::SetPatternName(CPattern* p, char const* name)
{}

int CMICallbacks::GetPatternLength(CPattern* p)
{
	return -1;
}

CMachine* CMICallbacks::GetPatternOwner(CPattern* p)
{
	return NULL;
}

bool CMICallbacks::MachineImplementsFunction(CMachine* pmac, int vtblindex, bool miex)
{
	return false;
}

void CMICallbacks::SendMidiNote(CMachine* pmac, int const channel, int const value, int const velocity)
{}

void CMICallbacks::SendMidiControlChange(CMachine* pmac, int const ctrl, int const channel, int const value)
{}

int CMICallbacks::GetBuildNumber()
{
	return 0;
}

void CMICallbacks::SetMidiFocus(CMachine* pmac)
{}

void CMICallbacks::BeginWriteToPlayingPattern(CMachine* pmac, int quantization, CPatternWriteInfo& outpwi)
{}

void CMICallbacks::WriteToPlayingPattern(CMachine* pmac, int group, int track, int param, int value)
{}

void CMICallbacks::EndWriteToPlayingPattern(CMachine* pmac)
{}

void* CMICallbacks::GetMainWindow()
{
	return NULL;
}

void CMICallbacks::DebugLock(char const* sourcelocation)
{}

void CMICallbacks::SetInputChannelCount(int count)
{}

void CMICallbacks::SetOutputChannelCount(int count)
{}

bool CMICallbacks::IsSongClosing()
{
	return true;
}

void CMICallbacks::SetMidiInputMode(MidiInputMode mode)
{}

int CMICallbacks::RemapLoadedMachineParameterIndex(CMachine* pmac, int i)
{
	return i;
}

char const* CMICallbacks::GetThemePath()
{
	return NULL;
}

void CMICallbacks::InvalidateParameterValueDescription(CMachine* pmac, int index)
{}

void CMICallbacks::RemapLoadedMachineName(char* name, int bufsize)
{}

bool CMICallbacks::IsMachineMuted(CMachine* pmac)
{
	return true;
}

int CMICallbacks::GetInputChannelConnectionCount(CMachine* pmac, int channel)
{
	return -1;
}

int CMICallbacks::GetOutputChannelConnectionCount(CMachine* pmac, int channel)
{
	return -1;
}

void CMICallbacks::ToggleRecordMode()
{}

int CMICallbacks::GetSequenceCount(CMachine* pmac)
{
	return 0;
}

CSequence* CMICallbacks::GetSequence(CMachine* pmac, int index)
{
	return NULL;
}

CPattern* CMICallbacks::GetPlayingPattern(CSequence* pseq)
{
	return NULL;
}

int CMICallbacks::GetPlayingPatternPosition(CSequence* pseq)
{
	return -1;
}

bool CMICallbacks::IsValidAsciiChar(CMachine* pmac, int param, char ch)
{
	return false;
}

int CMICallbacks::GetConnectionCount(CMachine* pmac, bool output)
{
	return 0;
}

CMachineConnection* CMICallbacks::GetConnection(CMachine* pmac, bool output, int index)
{
	return NULL;
}

CMachine* CMICallbacks::GetConnectionSource(CMachineConnection* pmc, int& channel)
{
	return NULL;
}

CMachine* CMICallbacks::GetConnectionDestination(CMachineConnection* pmc, int& channel)
{
	return NULL;
}

int CMICallbacks::GetTotalLatency()
{
	return 0;
}

void* CMICallbacks::GetMachineModuleHandle(CMachine* pmac)
{
	return NULL;
}
