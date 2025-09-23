// Interface for ReBuzz native machines.
// Add new callbacks or machine interface methods here.

#ifndef __MACHINE_INTERFACE_NEXT_H
#define __MACHINE_INTERFACE_NEXT_H

#include "MachineInterface.h"

#define MI_NEXT_VERSION				1

class CMachineInterfaceNext;

class CMachineInfoNext
{
public:
	int Version;							// MI_NEXT_VERSION
};


class CMICallbacksNext : public CMICallbacks
{
	// ReBuzz
	virtual int GetExtendedHostVersion();
	virtual void SetMachineInterfaceNext(CMachineInterfaceNext* pex, CMachineInfoNext* info);
	virtual int GetMachineBaseOctave(CMachine* pmac);
	virtual void SetMachineBaseOctave(CMachine* pmac, int octave);
};

class CMachineInterfaceNext
{
public:
	virtual void Dummy1() {}
	virtual void Dummy2() {}
	virtual void Dummy3() {}
	virtual void Dummy4() {}
	virtual void Dummy5() {}
	virtual void Dummy6() {}
	virtual void Dummy7() {}
	virtual void Dummy8() {}
	virtual void Dummy9() {}
	virtual void Dummy10() {}
	virtual void Dummy11() {}
	virtual void Dummy12() {}
	virtual void Dummy13() {}
	virtual void Dummy14() {}
	virtual void Dummy15() {}
	virtual void Dummy16() {}
	virtual void Dummy17() {}
	virtual void Dummy18() {}
	virtual void Dummy19() {}
	virtual void Dummy20() {}
	virtual void Dummy21() {}
	virtual void Dummy22() {}
	virtual void Dummy23() {}
	virtual void Dummy24() {}
	virtual void Dummy25() {}
	virtual void Dummy26() {}
	virtual void Dummy27() {}
	virtual void Dummy28() {}
	virtual void Dummy29() {}
	virtual void Dummy30() {}
	virtual void Dummy31() {}
	virtual void Dummy32() {}

};

#endif 