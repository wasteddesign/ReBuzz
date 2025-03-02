// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <windef.h>
#include <cstdlib>
#include <cmath>
#include <iostream>

#define MAX_TRACKS	4

CMachineParameter const sampleValueLeft = 
{ 
	pt_word,										// type
	"SampleLeft",
	"SampleLeftValue",	// description
	-10000,												// MinValue	
	100,											// MaxValue
	100+1,											// NoValue
	0,												// Flags
	0
};

CMachineParameter const sampleValueRight = 
{ 
	pt_word,										// type
	"SampleRight",
	"SampleRightValue",	// description
	-10000,												// MinValue	
	100,											// MaxValue
	100+1,											// NoValue
	0,												// Flags
	0
};

CMachineParameter const *pParameters[] = { 
	// global
	&sampleValueLeft,
	&sampleValueRight,
};

#pragma pack(1)

class gvals
{
public:
	word sampleValueLeft;
	word sampleValueRight;
};

class tvals
{
};

#pragma pack()

CMachineInfo const                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   MacInfo = 
{
	MT_GENERATOR,							// type
  MI_VERSION,                // version
	MIF_DOES_INPUT_MIXING,		// flags
	0,										// min tracks
	0,										// max tracks
	2,										// numGlobalParameters
	0,										// numTrackParameters
	pParameters,
	0,
	NULL,
	"FakeNativeGenerator",
	"FakeNativeGen",								// short name
	"WDE", 						// author
	"ApplySampleValues" //"Command1\nCommand2\nCommand3"
};

class mi : public CMachineInterface
{
public:
	mi();
	virtual ~mi();

	virtual void Init(CMachineDataInput * const pi);
	virtual void Tick();
	virtual bool WorkMonoToStereo(float *pin, float *pout, int numsamples, int const mode);
  virtual void Command(const int i) override;

private:
	gvals gval;
	tvals tval[MAX_TRACKS];
};

DLL_EXPORTS

mi::mi()
{
	GlobalVals = &gval;
	TrackVals = tval;
}

mi::~mi()
{

}

void mi::Init(CMachineDataInput * const pi)
{
}

void mi::Tick()
{

}

bool mi::WorkMonoToStereo(float *pin, float *pout, int numsamples, int const mode)
{
	pout[0] = static_cast<float>(gval.sampleValueLeft);
	pout[1] = static_cast<float>(gval.sampleValueRight);

	return true;
}

void mi::Command(const int i)
{

}
