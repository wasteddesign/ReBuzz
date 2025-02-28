// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <cmath>

#define OUTPUT_COUNT	1

CMachineParameter const paraLevel = { pt_byte, "Level", "Level", 0, 127, 255, MPF_STATE, 0 };

static CMachineParameter const *pParameters[] = { 
	// track
	&paraLevel,
};

#pragma pack(1)

class gvals
{
public:
	byte level;
};

#pragma pack()

CMachineInfo const MacInfo = 
{
	MT_GENERATOR,							// type
	MI_VERSION,
	MIF_MULTI_IO,							// flags
	0,											// min tracks
	0,								// max tracks
	1,										// numGlobalParameters
	0,										// numTrackParameters
	pParameters,
	0, 
	NULL,
	"MultiIOTest",
	"MultiIOTest",								// short name
	"Oskari Tammelin", 						// author
	NULL
};

class mi;

class miex : public CMachineInterfaceEx
{
public:
	virtual void MultiWork(float const * const *inputs, float **outputs, int numsamples);
	virtual char const *GetChannelName(bool input, int index)
	{
		if (input)
		{
			return "<invalid>";
		}
		else
		{
			switch(index)
			{
			case 0: return "A"; break;
			case 1: return "Bb"; break;
			case 2: return "B"; break;
			default: return "<invalid>"; break;
			}
		}
	}

public:
        mi *pmi;

};
class mi : public CMachineInterface
{
public:
	mi();
	virtual ~mi();

	virtual void Init(CMachineDataInput * const pi);
	virtual void Tick();
	virtual void Save(CMachineDataOutput * const po);

	void MultiWork(float const * const *inputs, float **outputs, int numsamples);

public:
	double phase[OUTPUT_COUNT];
	double freq[OUTPUT_COUNT];

	miex ex;
	gvals gval;

};

void miex::MultiWork(float const * const *inputs, float **outputs, int numsamples) { pmi->MultiWork(inputs, outputs, numsamples); }


mi::mi()
{
	ex.pmi = this;
	GlobalVals = &gval;
	TrackVals = NULL;
	AttrVals = NULL;
}

mi::~mi()
{
}

void mi::Init(CMachineDataInput * const pi)
{
	pCB->SetMachineInterfaceEx(&ex);
	pCB->SetOutputChannelCount(OUTPUT_COUNT);

	for (int i = 0; i < OUTPUT_COUNT; i++)
	{
		phase[i] = 0;
		freq[i] = pow(2.0, i / 12.0) * 440 * 2 * PI / pMasterInfo->SamplesPerSec;
	}
}

void mi::Save(CMachineDataOutput * const po)
{
}

void mi::Tick()
{

}

void mi::MultiWork(float const * const *inputs, float **outputs, int numsamples)
{
	for (int o = 0; o < OUTPUT_COUNT; o++)
	{
		float * __restrict pout = (float *)outputs[o];
		if (pout != NULL)
		{
			for (int i = 0; i < numsamples*2; i++)
			{
				pout[i] = 30000;
			}
		}
	}
}

DLL_EXPORTS;