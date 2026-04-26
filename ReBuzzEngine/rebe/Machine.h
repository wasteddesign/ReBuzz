#pragma once

#include "MachineDLL.h"
#include "mdkimp.h"
#include "pattern.h"
#include <afxmt.h>

struct WriteToData {
	void* machine;
	int group;
	int track;
	int param;
	int value;
};


// Extra stuff for machine reference
/*
extern CMasterInfo g_MasterInfo;

class CMIDummy : public CMachineInterface
{
};

class CMIExDummy : public CMachineInterfaceEx
{
};
*/

// Machine
class CMachine
{
public:
	CMachine(MachineDLL &dll)
	{
		pTemplate = &dll;
		pInterface = dll.CreateMI();
		mdkImpl = NULL;

		for (int c = 0; c < pTemplate->pInfo->numAttributes; c++)
			pInterface->AttrVals[c] = pTemplate->pInfo->Attributes[c]->DefValue;

		pHostMac = 0;
		pTemplateRef = NULL;
		row = 0;
		buzzTickPosition = 0;
	}

	CMachine()
	{
		pTemplate = NULL;
		pInterface = NULL;
		mdkImpl = NULL;
		numTracks = 0;

		pHostMac = 0;
		pTemplateRef = NULL;
		row = 0;
		buzzTickPosition = 0;
	}

	~CMachine()
	{
		// Dummy machines
		if (pTemplateRef != NULL)
		{
			/*
			if (pInterface != NULL && pInterface->pCB != NULL)
			{
				delete pInterface->pCB;
			}
			if (pInterfaceEx != NULL)
			{
				delete pInterfaceEx;
			}
			*/
			delete pTemplateRef;
			pTemplateRef = NULL;
		}

		if (pInterface != NULL)
			delete pInterface;
	}

	void CMachine::TransferDllRef(MachineDLL* dll)
	{
		MachineDLL* pDll = dll;
		if (pTemplateRef != NULL)
		{
			delete pTemplateRef;
			pTemplate = NULL;
		}
		
		pTemplateRef = pDll;
		pTemplate = pTemplateRef;

		// Create some dummy objects if machines try to access them
		/*
		pInterface = new CMIDummy();
		pInterface->pMasterInfo = &g_MasterInfo;
		pInterface->pCB = new CMICallbacks();
		((CMachineCallbacks*)pInterface->pCB)->pMachine = this;
		pInterfaceEx = new CMIExDummy();
		*/
	}

	bool CallbackDebugging() const { return false; }

	void SetnumTracks(int n)
	{
		numTracks = n;
		pInterface->SetNumTracks(n);
	}

	void CMachine::SetInputChannelCount(int count)
	{
		if (count < 0 || count > 256) return;

		// TODO: needs locking if callback is called outside Init
		// pInterface->pCB->Lock();
		//for (auto i = mioInputBuffers.begin(); i != mioInputBuffers.end(); i++) delete[] *i;
		//mioInputBuffers.resize(count);
		//for (auto i = mioInputBuffers.begin(); i != mioInputBuffers.end(); i++) *i = new float[2 * MAX_BUFFER_LENGTH + 16];
		// pInterface->pCB->Unlock();
	}

	void CMachine::SetOutputChannelCount(int count)
	{
		if (count < 0 || count > 256) return;

		// TODO: needs locking if callback is called outside Init
		// pInterface->pCB->Lock();
		//for (auto i = mioOutputBuffers.begin(); i != mioOutputBuffers.end(); i++) delete[] *i;
		//mioOutputBuffers.resize(count);
		//for (auto i = mioOutputBuffers.begin(); i != mioOutputBuffers.end(); i++) *i = new float[2 * MAX_BUFFER_LENGTH + 16];
		// pInterface->pCB->Unlock();
	}

public:
	CMachineInterface *pInterface;
	CMachineInterfaceEx *pInterfaceEx;
	MachineDLL *pTemplate; 
	MachineDLL *pTemplateRef;
	int numTracks;

	CMDKImplementation *mdkImpl;
	__int64 pHostMac;

	vector<CPattern> patterns;

	//vector<float *> mioInputBuffers;
	//vector<float *> mioOutputBuffers;

	vector<WriteToData> g_write_pattern_vector;
	bool writing_to_pattern = false;
	//std::mutex g_machine_mutex; // Slower
	CCriticalSection g_machine_cc;

	int row;
	float buzzTickPosition;
	CMachineInterfaceNext* pInterfaceNext;
};