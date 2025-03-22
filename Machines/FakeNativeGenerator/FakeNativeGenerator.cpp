// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <windef.h>
#include <cstdlib>
#include <cmath>
#include <iterator>

constexpr CMachineParameter sampleValueLeftIntegral = 
{ 
  pt_word,                    // type
  "SampleValueLeftIntegral",  // name
  "SampleValueLeftIntegral",	// description
  -100,                       // MinValue	
  100,                        // MaxValue
  100+1,                      // NoValue
  0,                          // Flags
  0                           // Default value
};

constexpr CMachineParameter sampleValueLeftDivisor = 
{ 
  pt_word,									// type
  "SampleValueLeftDivisor", // name
  "SampleValueLeftDivisor",	// description
  -100,  										// MinValue	
  100,											// MaxValue
  100+1,										// NoValue
  0,												// Flags
  0                         // Default value
};

constexpr CMachineParameter sampleValueRightIntegral = 
{ 
  pt_word,                    // type
  "SampleValueRightIntegral", // name
  "SampleValueRightIntegral",	// description
  -100,                       // MinValue	
  100,                        // MaxValue
  100+1,                      // NoValue
  0,                          // Flags
  0                           // Default value
};

constexpr CMachineParameter sampleValueRightDivisor = 
{ 
  pt_word,                    // type
  "SampleValueRightDivisor",  // name
  "SampleValueRightDivisor",  // description
  -100,                       // MinValue	
  100,                        // MaxValue
  100+1,                      // NoValue
  0,                          // Flags
  0                           // Default value
};

static CMachineParameter const* pParameters[] = { 
  // global
  &sampleValueLeftIntegral,
  &sampleValueLeftDivisor,
  &sampleValueRightIntegral,
  &sampleValueRightDivisor,
};

#pragma pack(1)

class gvals
{
public:
  word sampleValueLeftIntegral;
  word sampleValueLeftDivisor;
  word sampleValueRightIntegral;
  word sampleValueRightDivisor;
};

#pragma pack()

CMachineInfo const                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   MacInfo = 
{
  MT_GENERATOR,							// type
  MI_VERSION,               // version
  MIF_DOES_INPUT_MIXING,		// flags
  0,										    // min tracks
  0,										    // max tracks
  std::size(pParameters),		// numGlobalParameters
  0,										    // numTrackParameters
  pParameters,
  0,
  nullptr,
  "FakeNativeGenerator",
  "FakeNativeGen",					// short name
  "WDE", 						        // author
  nullptr,                  //"Command1\nCommand2\nCommand3"
  nullptr
};

class mi : public CMachineInterface
{
public:
  mi();
  bool WorkMonoToStereo(float* pin, float* pout, int numsamples, int const mode) override;

private:
  gvals gval;
};

DLL_EXPORTS

mi::mi()
{
  GlobalVals = &gval;
}

bool mi::WorkMonoToStereo(float* pin, float* pout, const int numsamples, int const mode)
{
  for (auto i = 0 ; i < numsamples * 2 ; i+=2)
  {
    pout[i] = 
      static_cast<float>(gval.sampleValueLeftIntegral)/static_cast<float>(gval.sampleValueLeftDivisor);
    pout[i+1] = 
      static_cast<float>(gval.sampleValueRightIntegral)/static_cast<float>(gval.sampleValueRightDivisor);
  }

  return true;
}