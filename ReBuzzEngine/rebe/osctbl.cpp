#include "stdafx.h"
#include "resource.h"

#define MAX_LEVEL	11		// 1 << 11 == 2048

#define TBL_SIZE (2048+1024+512+256+128+64+32+16+8+4+2)

short SineTable[TBL_SIZE];
short SawtoothTable[TBL_SIZE];
short PulseTable[TBL_SIZE];
short TriangleTable[TBL_SIZE];
short NoiseTable[TBL_SIZE];
short Saw303Table[TBL_SIZE];

short const *OscTableOffsets[] = { SineTable, SawtoothTable, PulseTable, TriangleTable, NoiseTable, Saw303Table };


void InitOscTables()
{
	HRSRC hr = FindResource(NULL, (char const *)IDR_OSCTBL, "osctbl");
	assert(hr != NULL);
	
	HGLOBAL hg = LoadResource(NULL, hr);
	assert(hg != NULL);

	short *pdata = (short *)LockResource(hg);
	assert(pdata != NULL);

	memcpy(SineTable, pdata, 2*TBL_SIZE);
	memcpy(SawtoothTable, pdata+TBL_SIZE, 2*TBL_SIZE);
	memcpy(PulseTable, pdata+2*TBL_SIZE, 2*TBL_SIZE);
	memcpy(TriangleTable, pdata+3*TBL_SIZE, 2*TBL_SIZE);	
	memcpy(Saw303Table, pdata+4*TBL_SIZE, 2*TBL_SIZE);	
	
	srand(0);

	for (int c = 0; c < TBL_SIZE; c++)
	{
		NoiseTable[c] = (rand() | (rand() & 1) * 32768) - 32768;
	}

}