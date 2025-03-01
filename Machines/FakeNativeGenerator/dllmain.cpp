// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <windef.h>
#include <cstring>
#include <cstdlib>
#include <cmath>

#define MAX_TRACKS	4

CMachineParameter const paraBDVolume = 
{ 
	pt_byte,										// type
	"BD Volume",
	"Bassdrum Volume (0=0%, 80=100%, FE=~198%)",	// description
	0,												// MinValue	
	254,											// MaxValue
	255,											// NoValue
	0,												// Flags
	0
};

CMachineParameter const *pParameters[] = { 
	// global
	&paraBDVolume,
};

#pragma pack(1)

class gvals
{
public:
	byte bd_volume;
};

class tvals
{
};

#pragma pack()

CMachineInfo const                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   MacInfo = 
{
	MT_GENERATOR,							// typel;;l;;ll;;ll  ';'p;
	MI_VERSION,
	MIF_DOES_INPUT_MIXING,		// flags
	0,										// min tracks
	0,										// max tracks
	1,										// numGlobalParameters
	0,										// numTrackParameters
	pParameters,
	0,
	NULL,
	"FakeNativeGenerator",
	"FakeNativeGen",								// short name
	"WDE", 						// author
	NULL
};

class CTrackState
{

};

class mi : public CMachineInterface
{
public:
	mi();
	virtual ~mi();

	virtual void Init(CMachineDataInput * const pi);
	virtual void Tick();
	virtual bool WorkMonoToStereo(float *pin, float *pout, int numsamples, int const mode);

private:

	void TickBassdrum();
	void GenerateBassdrum(float *psamples, int numsamples);

	void Filter(float *psamples, int numsamples);

private:
	int BDVolume;

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
	pout[0] = 1000000;
	pout[1] = 2000000;

	return true;
}