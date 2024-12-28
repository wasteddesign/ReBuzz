#include "stdafx.h"
#include "mdk.h"
#include "mdkimp.h"
#include "../buzz/MachineInterface.h"

#define UNROLL		4			// how many times to unroll inner loops
#define SUNROLL		2			// same for loops with stereo output

static void DSP_Copy(float *pout, float const *pin, dword const n)
{
	memcpy(pout, pin, n*sizeof(float));
}

static void DSP_Copy(float *pout, float const *pin, dword const n, float const a)
{

	double const amp = a;	// copy a to fpu stack 


	if (n >= UNROLL)
	{
		int c = n / UNROLL;
		do
		{
			pout[0] = (float)(pin[0] * amp);
			pout[1] = (float)(pin[1] * amp);
			pout[2] = (float)(pin[2] * amp);
			pout[3] = (float)(pin[3] * amp);
			pin += UNROLL;
			pout += UNROLL; 
		} while(--c);
	}

	int c = n & (UNROLL-1);
	while(c--)
		*pout++ = (float)(*pin++ * amp);

 
}

static void DSP_Add(float *pout, float const *pin, dword const n)
{
	if (n >= UNROLL)
	{
		int c = n / UNROLL;
		do
		{
			pout[0] += pin[0];
			pout[1] += pin[1];
			pout[2] += pin[2];
			pout[3] += pin[3];
			pin += UNROLL;
			pout += UNROLL; 
		} while(--c);
	}

	int c = n & (UNROLL-1);
	while(c--)
		*pout++ += *pin++;
}

static void DSP_Add(float *pout, float const *pin, dword const n, float const a)
{
	double const amp = a;	// copy a to fpu stack 

	if (n >= SUNROLL)
	{
		int c = n / SUNROLL;
		do					// FIXME: generates strange code 
		{
			pout[0] += (float)(pin[0] * amp);
			pout[1] += (float)(pin[1] * amp);
			pin += SUNROLL;
			pout += SUNROLL; 
		} while(--c);
	}

	int c = n & (SUNROLL-1);
	while(c--)
		*pout++ += (float)(*pin++ * amp);
}


static void DSP_AddM2S(float *pout, float const *pin, dword const n)
{
	if (n >= SUNROLL)
	{
		int c = n / SUNROLL;
		do
		{
			pout[0] += pin[0];
			pout[1] += pin[0];
			pout[2] += pin[1];
			pout[3] += pin[1];
			pin += SUNROLL;
			pout += SUNROLL*2; 
		} while(--c);
	}

	int c = n & (SUNROLL-1);
	while(c--)
	{
		pout[0] += *pin;
		pout[1] += *pin;
		pin++;
		pout += 2;
	}
} 

static void DSP_AddM2S(float *pout, float const *pin, dword const n, float const a)
{
	double const amp = a;	// copy a to fpu stack 

	if (n >= SUNROLL)
	{
		int c = n / SUNROLL;
		do
		{
			double s = pin[0] * amp;
			pout[0] += (float)s;
			pout[1] += (float)s;
			
			s = pin[1] * amp;
			pout[2] += (float)s;
			pout[3] += (float)s;
			
			pin += SUNROLL;
			pout += SUNROLL*2; 
		} while(--c);
	}
 
	int c = n & (SUNROLL-1);
	while(c--)
	{
		double const s = *pin++ * amp;
		pout[0] += (float)s;
		pout[1] += (float)s;
		pout += 2;
	}
} 



CMDKImplementation *NewMDKImp()
{
	return new CMDKImplementation;
}

void CopyStereoToMono(float *pout, float *pin, int numsamples, float amp)
{
	do
	{
		*pout++ = (pin[0] + pin[1]) * amp;
		pin += 2;
	} while(--numsamples);
}

void AddStereoToMono(float *pout, float *pin, int numsamples, float amp)
{
	do
	{
		*pout++ += (pin[0] + pin[1]) * amp;
		pin += 2;
	} while(--numsamples);
}

void CopyM2S(float *pout, float *pin, int numsamples, float amp)
{
	do
	{
		double s = *pin++ * amp;
		pout[0] = (float)s;
		pout[1] = (float)s;
		pout += 2;
	} while(--numsamples);

}

void Add(float *pout, float *pin, int numsamples, float amp)
{
	do
	{
		*pout++ += *pin++ * amp;
	} while(--numsamples);
}



void CMDKImplementation::AddInput(char const *macname, bool stereo)
{
	if (macname == NULL)
		return;

	Inputs.push_back(CInput(macname, stereo));

	SetMode();
}

void CMDKImplementation::DeleteInput(char const *macname)
{
	for (InputList::iterator i = Inputs.begin(); i != Inputs.end(); i++)
	{
		if ((*i).Name.compare(macname) == 0)
		{

			Inputs.erase(i);

			SetMode();
			return;
		}
	}
}

void CMDKImplementation::RenameInput(char const *macoldname, char const *macnewname)
{
	for (InputList::iterator i = Inputs.begin(); i != Inputs.end(); i++)
	{
		if ((*i).Name.compare(macoldname) == 0)
		{
			(*i).Name = macnewname;
			return;
		}
	}
}

void CMDKImplementation::SetInputChannels(char const *macname, bool stereo)
{
	for (InputList::iterator i = Inputs.begin(); i != Inputs.end(); i++)
	{
		if ((*i).Name.compare(macname) == 0)
		{
			(*i).Stereo = stereo;
			SetMode();
			return;
		}
	}
}

void CMDKImplementation::Input(float *psamples, int numsamples, float amp)
{
	if (InputIterator == Inputs.end())
	{
		InputIterator = Inputs.begin();
		HaveInput = 0;
		if (InputIterator == Inputs.end()) return;
	}

	if (psamples == NULL)
	{ 
		InputIterator++;
		return;
	}


	if (numChannels == 1)
	{
		if (HaveInput == 0)
		{
			if ((*InputIterator).Stereo)
				CopyStereoToMono(Buffer, psamples, numsamples, 1.0f);
			else
				DSP_Copy(Buffer, psamples, numsamples, 1.0f);
		}
		else
		{
			if ((*InputIterator).Stereo)
				AddStereoToMono(Buffer, psamples, numsamples, 1.0f);
			else
				DSP_Add(Buffer, psamples, numsamples, 1.0f);
		}
	}
	else
	{
		if (HaveInput == 0)
		{
			if ((*InputIterator).Stereo)
				DSP_Copy(Buffer, psamples, numsamples*2, 1.0f);
			else
				CopyM2S(Buffer, psamples, numsamples, 1.0f);
		}
		else
		{
			if ((*InputIterator).Stereo) 
				DSP_Add(Buffer, psamples, numsamples*2, 1.0f);
			else
				DSP_AddM2S(Buffer, psamples, numsamples, 1.0f);
		}
	}

	HaveInput++;
	InputIterator++;

}

void CMDKImplementation::ResetIterator()
{
	InputIterator = Inputs.end();
	HaveInput = 0;
}


bool CMDKImplementation::Work(float *psamples, int numsamples, int const mode)
{
	if ((mode & WM_READ) && HaveInput)
		DSP_Copy(psamples, Buffer, numsamples);

	ResetIterator();

	bool ret = pmi->MDKWork(psamples, numsamples, mode);

	return ret;
}

bool CMDKImplementation::WorkMonoToStereo(float *pin, float *pout, int numsamples, int const mode)
{
	if ((mode & WM_READ) && HaveInput)
		DSP_Copy(pout, Buffer, 2*numsamples);

	ResetIterator();
	
	bool ret = pmi->MDKWorkStereo(pout, numsamples, mode);

	return ret;
}

	
void CMDKImplementation::Init(CMachineDataInput * const pi)
{
	ThisMachine = pmi->pCB->GetThisMachine();
	
	numChannels = 1;

	InputIterator = Inputs.end();
	HaveInput = 0;
	MachineWantsChannels = 1;

	if (pi != NULL)
	{
		byte ver;
		pi->Read(ver);
	}
	

	pmi->MDKInit(pi);
}

void CMDKImplementation::Save(CMachineDataOutput * const po)
{
	po->Write((byte)MDK_VERSION);

	pmi->MDKSave(po);
}

void CMDKImplementation::SetOutputMode(bool stereo)
{
	numChannels = stereo ? 2 : 1;
	MachineWantsChannels = numChannels;
	
	pmi->OutputModeChanged(stereo);
}

void CMDKImplementation::SetMode()
{	
	InputIterator = Inputs.end();
	HaveInput = 0;
	
	if (MachineWantsChannels > 1)
	{
		numChannels = MachineWantsChannels;
		pmi->pCB->SetnumOutputChannels(ThisMachine, numChannels);
		pmi->OutputModeChanged(numChannels > 1 ? true : false);
		return;
	}


	for (InputList::iterator i = Inputs.begin(); i != Inputs.end(); i++)
	{
		if ((*i).Stereo)
		{
			numChannels = 2;
			pmi->pCB->SetnumOutputChannels(ThisMachine, numChannels);
			pmi->OutputModeChanged(numChannels > 1 ? true : false);
			return;
		}
	}

	numChannels = 1;
	pmi->pCB->SetnumOutputChannels(ThisMachine, numChannels);
	pmi->OutputModeChanged(numChannels > 1 ? true : false);

}

CMDKImplementation::CMDKImplementation()
{
	Buffer = (float *)_aligned_malloc((MAX_BUFFER_LENGTH * 2 + 16) * sizeof(float), 64);
}


CMDKImplementation::~CMDKImplementation()
{
	_aligned_free(Buffer);
}

