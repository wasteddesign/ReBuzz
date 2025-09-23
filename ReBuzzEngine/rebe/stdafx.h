// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
#define WINDOWS_IGNORE_PACKING_MISMATCH
// Windows Header Files:
#include <windows.h>

// C RunTime Header Files
#include <stdlib.h>
#include <malloc.h>
#include <memory.h>
#include <tchar.h>
#include <process.h>
#include <assert.h>

#include <vector>
#include <list>
#include <algorithm>
#include <functional>
#include <string>
#include <map>

#include "../buzz/MachineInterfaceNext.h"

typedef unsigned char byte;

using namespace std;

class CMachine;

class CMachineCallbacks : public CMICallbacksNext
{
private:

public:
	CMachine *pMachine;

	typedef std::map<string, CMachine*> MapMacToMacRef;
	MapMacToMacRef machineReferences;

	typedef std::map<string, char *> MapStringToChars;
	MapStringToChars machineNames;

	typedef std::map<string, string> MapStringToString;
	MapStringToString remappedMachineNames;

	CMachineCallbacks()
	{
		pMachine = NULL;
	}

	~CMachineCallbacks()
	{
		for_each(machineReferences.begin(), machineReferences.end(),
			[](decltype(machineReferences)::value_type const& p) { delete p.second; });

		for_each(machineNames.begin(), machineNames.end(),
			[](decltype(machineNames)::value_type const& p) { free(p.second); });

		machineReferences.clear();
		remappedMachineNames.clear();
		machineNames.clear();
	}

	void AddMachine(string name)
	{
		if (machineReferences[name] != NULL)
		{
			delete machineReferences[name];
			machineReferences.erase(name);
		}
	}
};

// TODO: reference additional headers your program requires here
