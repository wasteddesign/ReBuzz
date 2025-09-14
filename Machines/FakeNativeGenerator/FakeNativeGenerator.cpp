// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <windef.h>
#include <cstdlib>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include "../FakeNativeMachineLib/lib.hpp"

FAKE_MACHINE_INT_SLIDER(sampleValueLeftIntegral, SampleValueLeftIntegral);
FAKE_MACHINE_INT_SLIDER(sampleValueLeftDivisor, SampleValueLeftDivisor);
FAKE_MACHINE_INT_SLIDER(sampleValueRightIntegral, SampleValueRightIntegral);
FAKE_MACHINE_INT_SLIDER(sampleValueRightDivisor, SampleValueRightDivisor);
FAKE_MACHINE_SWITCH_SLIDER(debugShowEnabled, DebugShowEnabled);

static CMachineParameter const* pParameters[] = { 
  // global
  &sampleValueLeftIntegral,
  &sampleValueLeftDivisor,
  &sampleValueRightIntegral,
  &sampleValueRightDivisor,
  &debugShowEnabled
};

#pragma pack(1)

class gvals
{
public:
  word sampleValueLeftIntegral;
  word sampleValueLeftDivisor;
  word sampleValueRightIntegral;
  word sampleValueRightDivisor;
  byte debugShowEnabled;
};

#pragma pack()

CMachineInfo const                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   MacInfo = 
{
  .Type = MT_GENERATOR,                          // type
  .Version = MI_VERSION,                         // version
  .Flags = MIF_DOES_INPUT_MIXING,                // flags
  .minTracks = 0,                                // min tracks
  .maxTracks = 0,                                // max tracks
  .numGlobalParameters = std::size(pParameters), // numGlobalParameters
  .numTrackParameters = 0,                       // numTrackParameters
  .Parameters = pParameters,
  .numAttributes = 0,
  .Attributes = nullptr,
  .Name = "FakeNativeGenerator",
  .ShortName = "FakeNativeGen",                  // short name
  .Author = "WDE",                               // author
  .Commands = nullptr,                           //"Command1\nCommand2\nCommand3"
  .pLI = nullptr
};

class mi : public CMachineInterface
{
public:
  mi();
  bool WorkMonoToStereo(float* pin, float* pout, int numsamples, int const mode) override;
  ~mi() override;
  void Init(CMachineDataInput* const pi) override;
  void Tick() override;
  void Stop() override;
  void Save(CMachineDataOutput* const po) override;
  void AttributesChanged() override;
  void Command(const int i) override;
  void SetNumTracks(const int n) override;
  void MuteTrack(const int i) override;
  bool IsTrackMuted(const int i) const override;
  void MidiNote(const int channel, const int value, const int velocity) override;
  void Event(const dword data) override;
  const char* DescribeValue(const int param, const int value) override;
  const CEnvelopeInfo** GetEnvelopeInfos() override;
  bool PlayWave(const int wave, const int note, const float volume) override;
  void StopWave() override;
  int GetWaveEnvPlayPos(const int env) override;

private:
  gvals gval;
  std::string machineName;
};

DLL_EXPORTS

bool mi::WorkMonoToStereo(float* pin, float* pout, const int numsamples, int const mode)
{
  DebugShow(machineName, __func__, gval.debugShowEnabled);
  AbortIfRequested(machineName, __func__);
  for (auto i = 0 ; i < numsamples * 2 ; i+=2)
  {
    pout[i] = 
      static_cast<float>(gval.sampleValueLeftIntegral)/static_cast<float>(gval.sampleValueLeftDivisor);
    pout[i+1] = 
      static_cast<float>(gval.sampleValueRightIntegral)/static_cast<float>(gval.sampleValueRightDivisor);
  }

  DebugShow(machineName, "Wrote " + std::to_string(numsamples) + " samples. First: " + std::to_string(pout[0]) + ", last: " + std::to_string(pout[(numsamples * 2) - 1]), gval.debugShowEnabled);

  return true;
}

mi::mi() : machineName(ReadMachineName())
{
  AbortIfRequested(machineName, "constructor");
  GlobalVals = &gval;
}

void mi::AttributesChanged()
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::AttributesChanged();
}

void mi::Command(const int i)
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::Command(i);
}

void mi::SetNumTracks(const int n)
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::SetNumTracks(n);
}

void mi::MuteTrack(const int i)
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::MuteTrack(i);
}

bool mi::IsTrackMuted(const int i) const
{
  AbortIfRequested(machineName, __func__);
  return CMachineInterface::IsTrackMuted(i);
}

void mi::Event(const dword data)
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::Event(data);
}

const char* mi::DescribeValue(const int param, const int value)
{
  AbortIfRequested(machineName, __func__);
  return CMachineInterface::DescribeValue(param, value);
}

const CEnvelopeInfo** mi::GetEnvelopeInfos()
{
  AbortIfRequested(machineName, __func__);
  return CMachineInterface::GetEnvelopeInfos();
}

bool mi::PlayWave(const int wave, const int note, const float volume)
{
  AbortIfRequested(machineName, __func__);
  return CMachineInterface::PlayWave(wave, note, volume);
}

void mi::StopWave()
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::StopWave();
}

int mi::GetWaveEnvPlayPos(const int env)
{
  AbortIfRequested(machineName, __func__);
  return CMachineInterface::GetWaveEnvPlayPos(env);
}

mi::~mi()
{
  
}

void mi::Init(CMachineDataInput* const input)
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::Init(input);
}

void mi::Tick()
{
  DebugShow(machineName, __func__, gval.debugShowEnabled);
  AbortIfRequested(machineName, __func__);
  CMachineInterface::Tick();
}

void mi::Stop()
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::Stop();
}

void mi::Save(CMachineDataOutput* const output)
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::Save(output);
}

void mi::MidiNote(int const channel, int const value, int const velocity)
{
  AbortIfRequested(machineName, __func__);
  CMachineInterface::MidiNote(channel, value, velocity);
}