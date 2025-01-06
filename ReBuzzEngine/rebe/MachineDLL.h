#pragma once

#include "../IPC/ServerProcess.h"

extern void AfxMessageBox(string msg);

class MachineDLL
{
public:
	MachineDLL(string path, string libname, HINSTANCE h)
	{
		pInfoRef = NULL;
		LibName = libname;
		LibHandle = h;

		CMachineInfo const *(__cdecl *pGetInfo)();

		CreateMachineFunc = (CMachineInterface *(__cdecl *)())GetProcAddress(LibHandle, "CreateMachine");
		pGetInfo = (CMachineInfo const *(__cdecl *)())GetProcAddress(LibHandle, "GetInfo");

		if (CreateMachineFunc == NULL)
		{
			int le = GetLastError();

			string msg = "GetProcAddress(" + path + ") failed.";
			//AfxMessageBox(msg);
			FreeLibrary(LibHandle);
			LibHandle = NULL;
			return;
		} 

		if (pGetInfo == NULL)
		{
			string msg = "GetProcAddress(" + path + ") failed.";
			//AfxMessageBox(msg);
			FreeLibrary(LibHandle);
			LibHandle = NULL;
			return;
		}

		pInfo = pGetInfo();
		assert(pInfo != NULL);

	}

	MachineDLL(IPC::MessageReader& m)
	{
		pInfo = NULL;
		pInfoRef = new CMachineInfo();
		ReadMachineInfo(m);
	}

	~MachineDLL()
	{
		if (pInfo == NULL)
		{
			for (int i = 0; i < pInfoRef->numGlobalParameters + pInfoRef->numTrackParameters; i++)
			{
				delete pInfoRef->Parameters[i]->Name;
				delete pInfoRef->Parameters[i]->Description;
				delete pInfoRef->Parameters[i];
			}

			for (int i = 0; i < pInfoRef->numAttributes; i++)
			{
				delete pInfoRef->Attributes[i]->Name;
				delete pInfoRef->Attributes[i];
			}

			delete pInfoRef->Name;
			delete pInfoRef->ShortName;
			delete pInfoRef->Author;
			delete pInfoRef->Commands;
			delete pInfoRef;
		}
	}

	bool ImplementsFunction(const char* str)
	{
		bool found = (CMachineInterface * (__cdecl*)())GetProcAddress(LibHandle, str);
		return found;
	}

	void WriteMachineInfo(IPC::Message &m)
	{
		m.Write(pInfo->Type);
		m.Write(pInfo->Version);
		m.Write(pInfo->Flags);
		m.Write(pInfo->minTracks);
		m.Write(pInfo->maxTracks);
		m.Write(pInfo->numGlobalParameters);
		m.Write(pInfo->numTrackParameters);
		for (int i = 0; i < pInfo->numGlobalParameters + pInfo->numTrackParameters; i++)
			WriteParameter(m, pInfo->Parameters[i]);
		m.Write(pInfo->numAttributes);
		for (int i = 0; i < pInfo->numAttributes; i++)
			WriteAttribute(m, pInfo->Attributes[i]);
		m.Write(pInfo->Name);
		m.Write(pInfo->ShortName);
		m.Write(pInfo->Author);
		m.Write(pInfo->Commands);
		m.Write(pInfo->pLI != NULL);
	}

	void WriteParameter(IPC::Message &m, CMachineParameter const *p)
	{
		m.Write(p->Type);
		m.Write(p->Name);
		m.Write(p->Description);
		m.Write(p->MinValue);
		m.Write(p->MaxValue);
		m.Write(p->NoValue);
		m.Write(p->Flags);
		m.Write(p->DefValue);
	}

	void WriteAttribute(IPC::Message &m, CMachineAttribute const *p)
	{
		m.Write(p->Name);
		m.Write(p->MinValue);
		m.Write(p->MaxValue);
		m.Write(p->DefValue);
	}

	void ReadMachineInfo(IPC::MessageReader& m)
	{
		pInfoRef->Type = m.ReadDWORD();
		pInfoRef->Version = m.ReadDWORD();
		pInfoRef->Flags = m.ReadDWORD();
		pInfoRef->minTracks = m.ReadDWORD();
		pInfoRef->maxTracks = m.ReadDWORD();
		pInfoRef->numGlobalParameters = m.ReadDWORD();
		pInfoRef->numTrackParameters = m.ReadDWORD();
		int parametersCount = pInfoRef->numGlobalParameters + pInfoRef->numTrackParameters;
		pInfoRef->Parameters = const_cast<const CMachineParameter**>(new CMachineParameter*[parametersCount]);
		for (int i = 0; i < parametersCount; i++)
		{
			pInfoRef->Parameters[i] = new CMachineParameter();
			ReadParameter(m, (CMachineParameter*)pInfoRef->Parameters[i]);
		}
		pInfoRef->numAttributes = m.ReadDWORD();
		pInfoRef->Attributes = const_cast<const CMachineAttribute**>(new CMachineAttribute*[pInfoRef->numAttributes]);
		for (int i = 0; i < pInfoRef->numAttributes; i++)
		{
			pInfoRef->Attributes[i] = new CMachineAttribute();
			ReadAttribute(m, (CMachineAttribute*)pInfoRef->Attributes[i]);
		}
		m.AllocAndRead(&pInfoRef->Name);
		m.AllocAndRead(&pInfoRef->ShortName);
		m.AllocAndRead(&pInfoRef->Author);
		m.AllocAndRead(&pInfoRef->Commands);
		pInfoRef->pLI != NULL;
	}

	void ReadParameter(IPC::MessageReader& m, CMachineParameter* p)
	{
		p->Type = (CMPType)m.ReadDWORD();
		m.AllocAndRead(&p->Name);
		m.AllocAndRead(&p->Description);
		p->MinValue = m.ReadDWORD();
		p->MaxValue = m.ReadDWORD();
		p->NoValue = m.ReadDWORD();
		p->Flags = m.ReadDWORD();
		p->DefValue = m.ReadDWORD();
	}

	void ReadAttribute(IPC::MessageReader& m, CMachineAttribute* p)
	{
		m.AllocAndRead(&p->Name);
		p->MinValue = m.ReadDWORD();
		p->MaxValue = m.ReadDWORD();
		p->DefValue = m.ReadDWORD();
	}

	CMachineInterface *CreateMI() { return CreateMachineFunc(); }

public:
	string LibName;
	HINSTANCE LibHandle;

	CMachineInfo const *pInfo;
	CMachineInfo *pInfoRef;
	CMachineInterface *(__cdecl *CreateMachineFunc)();

};