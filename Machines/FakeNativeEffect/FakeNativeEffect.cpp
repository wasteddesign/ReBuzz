// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <windef.h>
#include <cstdlib>
#include <cmath>
#include <iterator>
#include <string>

constexpr CMachineParameter sampleValueLeftMultiplier = 
{ 
  pt_word,                      // type
  "SampleValueLeftMultiplier",  // name
  "SampleValueLeftMultiplier",	// description
  -100,                         // MinValue	
  100,                          // MaxValue
  100+1,                        // NoValue
  0,                            // Flags
  0                             // Default value
};

constexpr CMachineParameter sampleValueRightMultiplier = 
{ 
  pt_word,                      // type
  "SampleValueRightMultiplier", // name
  "SampleValueRightMultiplier",	// description
  -100,                         // MinValue	
  100,                          // MaxValue
  100+1,                        // NoValue
  0,                            // Flags
  0                             // Default value
};

static CMachineParameter const* pParameters[] = { 
  // global
  &sampleValueLeftMultiplier,
  &sampleValueRightMultiplier,
};

#pragma pack(1)

class gvals
{
public:
  word sampleValueLeftMultiplier;
  word sampleValueRightMultiplier;
};

#pragma pack()

CMachineInfo const                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   MacInfo = 
{
  MT_EFFECT,  							// type
  MI_VERSION,               // version
  MIF_STEREO_EFFECT,		    // flags
  0,										    // min tracks
  0,										    // max tracks
  std::size(pParameters),		// numGlobalParameters
  0,										    // numTrackParameters
  pParameters,
  0,
  nullptr,
  "FakeNativeEffect",
  "FakeNativeEffect",					// short name
  "WDE", 						        // author
  nullptr,                   //"Command1\nCommand2\nCommand3"
  nullptr
};

class mi : public CMachineInterface
{
public:
  mi();
  bool Work(float* psamples, int numsamples, const int mode) override;

private:
  gvals gval;
};

DLL_EXPORTS

mi::mi()
{
  GlobalVals = &gval;
}

bool mi:: Work(float* psamples, int numsamples, const int mode)
{
  for (auto i = 0 ; i < numsamples*2 ; i+=2)
  {
    psamples[i] = psamples[i] * gval.sampleValueLeftMultiplier;
    psamples[i + 1] = psamples[i + 1] * gval.sampleValueRightMultiplier;
  }

  return true;
}
