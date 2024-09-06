namespace ReBuzz.FileOps
{
    internal class WaveUnpack
    {
        /*

		typedef unsigned char		BYTE;
typedef unsigned short      WORD;
typedef unsigned long       DWORD;
typedef unsigned int	 	UINT;
typedef int		 			BOOL;

#define FALSE   0
#define TRUE    1
typedef signed char         INT8, *PINT8;
typedef signed short        INT16, *PINT16;
typedef signed int          INT32, *PINT32;
typedef unsigned char       UINT8, *PUINT8;
typedef unsigned short      UINT16, *PUINT16;
typedef unsigned int        UINT32, *PUINT32;
typedef BOOL				*LPBOOL;
typedef BYTE				*LPBYTE;
typedef int					*LPINT;
typedef WORD				*LPWORD;
typedef long				*LPLONG;
typedef DWORD				*LPDWORD;
typedef void				*LPVOID;

typedef char* LPSTR;
typedef const char* LPCSTR;

		 */


        /*
//=====================================DEFINITIONS================================
#define MAXPACKEDBUFFER 2048

//=====================================STRUCTURES================================
#include "compresstypes.h"

typedef struct _COMPRESSIONVALUES
{
	WORD	wSum1;
	WORD	wSum2;
	WORD	wResult;

	LPWORD	lpwTempData;
}COMPRESSIONVALUES;

typedef struct _WAVEUNPACK
{
	zzub_output_t* pStreamOut;
	zzub_input_t* pStreamIn;
	BYTE abtPackedBuffer[MAXPACKEDBUFFER];
	DWORD dwCurIndex;
	DWORD dwCurBit;

	DWORD dwBytesInBuffer;
	DWORD dwMaxBytes;
	DWORD dwBytesInFileRemain;

}WAVEUNPACK;		 
		 */


        /*
        
        #include <cstring>
#include "library.h"
#include "zzub/zzub.h"
#include "decompress.h"

template <typename T>
int zzub_read(zzub_input_t* f, T &d) { zzub_input_read(f, (char*)&d, sizeof(T)); return sizeof(T); } 

//==================================BIT UNPACKING===================================
BOOL InitWaveUnpack(WAVEUNPACK * waveunpackinfo, zzub_input_t* inf,DWORD dwSectionSize)
{
	waveunpackinfo->dwMaxBytes = MAXPACKEDBUFFER;
	waveunpackinfo->dwBytesInFileRemain = dwSectionSize ;
	waveunpackinfo->pStreamIn = inf;
	waveunpackinfo->pStreamOut = 0;
	waveunpackinfo->dwCurBit = 0;

	//set up so that call to UnpackBits() will force an immediate read from file
	waveunpackinfo->dwCurIndex = MAXPACKEDBUFFER;
	waveunpackinfo->dwBytesInBuffer = 0;

	return TRUE;
}

DWORD UnpackBits(WAVEUNPACK * unpackinfo,DWORD dwAmount)
{	
	DWORD dwRet,dwReadAmount,dwSize,dwMask,dwVal;
	DWORD dwFileReadAmnt,dwReadFile,dwShift;
	DWORD dwMax = 8;

	if((unpackinfo->dwBytesInFileRemain == 0) && (unpackinfo->dwCurIndex == MAXPACKEDBUFFER))
	{
		return 0;
	}
	
	dwReadAmount = dwAmount;
	dwRet = 0;
	dwShift = 0;
	while(dwReadAmount > 0)
	{
		//check to see if we need to update buffer and/or index
		if((unpackinfo->dwCurBit == dwMax) || (unpackinfo->dwBytesInBuffer == 0))
		{	
			unpackinfo->dwCurBit = 0;
			unpackinfo->dwCurIndex++;
			if(unpackinfo->dwCurIndex >= unpackinfo->dwBytesInBuffer )
			{	//run out of buffer... read more file into buffer
				dwFileReadAmnt= (unpackinfo->dwBytesInFileRemain > unpackinfo->dwMaxBytes ) ? unpackinfo->dwMaxBytes : unpackinfo->dwBytesInFileRemain;
				
				dwReadFile = zzub_input_read(unpackinfo->pStreamIn, (char*)unpackinfo->abtPackedBuffer, dwFileReadAmnt);

				unpackinfo->dwBytesInFileRemain -= dwReadFile;	
				unpackinfo->dwBytesInBuffer = dwReadFile;
				unpackinfo->dwCurIndex = 0;

				//if we didnt read anything then exit now
				if(dwReadFile == 0)
				{	//make sure nothing else is read
					unpackinfo->dwBytesInFileRemain = 0;
					unpackinfo->dwCurIndex = MAXPACKEDBUFFER;
					return 0;
				}
			}
		}
		
		//calculate size to read from current dword
		dwSize = ((dwReadAmount + unpackinfo->dwCurBit) > dwMax) ? dwMax - unpackinfo->dwCurBit : dwReadAmount;
		
		//calculate bitmask
		dwMask = (1 << dwSize) - 1;

		//Read value from buffer
		dwVal = unpackinfo->abtPackedBuffer[unpackinfo->dwCurIndex];
		dwVal = dwVal >> unpackinfo->dwCurBit;

		//apply mask to value
		dwVal &= dwMask;

		//shift value to correct position
		dwVal = dwVal << dwShift;
		
		//update return value
		dwRet |= dwVal;

		//update info
		unpackinfo->dwCurBit += dwSize;
		dwShift += dwSize;
		dwReadAmount -= dwSize;
	}

	return dwRet;
}

DWORD CountZeroBits(WAVEUNPACK * unpackinfo)
{
	DWORD dwBit;
	DWORD dwCount = 0;

	dwBit = UnpackBits(unpackinfo,1);

	while(dwBit == 0)
	{
		dwCount++;
		dwBit = UnpackBits(unpackinfo,1);
	}

	return dwCount;
}


//==================================WAVE DECOMPRESSING===================================
void ZeroCompressionValues(COMPRESSIONVALUES * lpcv,DWORD dwBlockSize)
{
	lpcv->wResult = 0;
	lpcv->wSum1 = 0;
	lpcv->wSum2 = 0;

	//If block size is given, then allocate specfied temporary data
	if (dwBlockSize > 0)
	{
		lpcv->lpwTempData = (LPWORD)new WORD[dwBlockSize];
		memset(lpcv->lpwTempData, 0, sizeof(WORD) * dwBlockSize);
	}
	else
	{
		lpcv->lpwTempData=NULL;
	}
}

void TidyCompressionValues(COMPRESSIONVALUES * lpcv)
{
	//if there is temporary data - then free it.
	if (lpcv->lpwTempData != NULL)
	{
		delete[] lpcv->lpwTempData;
		lpcv->lpwTempData = 0;
	}
}


BOOL DecompressSwitch(WAVEUNPACK * unpackinfo,COMPRESSIONVALUES * lpcv,
					  LPWORD lpwOutputBuffer,DWORD dwBlockSize)
{
	DWORD dwSwitchValue,dwBits,dwSize,dwZeroCount;
	DWORD wValue;	// calvin changed the type of wValue from WORD to DWORD, which made 32-bit samples work!
	LPWORD lpwaddress;
	if(dwBlockSize == 0)
	{
		return FALSE;
	}

	//Get compression method
	dwSwitchValue = UnpackBits(unpackinfo,2);

	//read size (in bits) of compressed values
	dwBits = UnpackBits(unpackinfo,4);

	dwSize = dwBlockSize;
	lpwaddress = lpwOutputBuffer;
	while(dwSize > 0)
	{
		//read compressed value
		wValue = (WORD)UnpackBits(unpackinfo,dwBits);
		
		//count zeros
		dwZeroCount = CountZeroBits(unpackinfo);
		
		//Construct
		wValue = (WORD)((dwZeroCount << dwBits) | wValue);

		//is value supposed to be positive or negative?
		if((wValue & 1) == 0)
		{	//its positive
			wValue = wValue >> 1;
		}
		else
		{	//its negative. Convert into a negative value.
			wValue++;
			wValue = wValue >> 1;
			wValue = ~wValue; //invert bits
			wValue++; //add one to make 2's compliment
		}

		//Now do stuff depending on which method we're using....
		switch(dwSwitchValue )
		{
			case 0:
				lpcv->wSum2 = ((wValue - lpcv->wResult) - lpcv->wSum1);
				lpcv->wSum1 = wValue - lpcv->wResult;
				lpcv->wResult = wValue;
				break;
			case 1:
				lpcv->wSum2 = wValue - lpcv->wSum1;
				lpcv->wSum1 = wValue;
				lpcv->wResult += wValue;
				break;
			case 2:
				lpcv->wSum2 = wValue;
				lpcv->wSum1 += wValue;
				lpcv->wResult += lpcv->wSum1;
				break;
			case 3:
				lpcv->wSum2 += wValue;
				lpcv->wSum1 += lpcv->wSum2;
				lpcv->wResult += lpcv->wSum1;
				break;
			default: //error
				return FALSE;
		}

		//store value into output buffer
		*lpwOutputBuffer = lpcv->wResult;
		
		//prepare for next loop...
		lpwOutputBuffer++;
		dwSize--;
	}

	return TRUE;
}


BOOL DecompressWave(WAVEUNPACK * unpackinfo,LPWORD lpwOutputBuffer,
					  DWORD dwNumSamples,BOOL bStereo)
{
	DWORD dwZeroCount,dwShift,dwBlockSize,dwBlockCount,dwLastBlockSize;
	DWORD dwResultShift,dwCount,i,ixx;
	BYTE btSumChannels;
	COMPRESSIONVALUES cv1,cv2;

	if(lpwOutputBuffer == NULL)
	{
		return FALSE;
	}

	dwZeroCount = CountZeroBits(unpackinfo);
	if (dwZeroCount != 0)
	{
		//printf("Unknown compressed wave data format \n");
		return FALSE;
	}

	//get size shifter
	dwShift = UnpackBits(unpackinfo,4);

	//get size of compressed blocks
	dwBlockSize = 1 << dwShift;

	//get number of compressed blocks
	dwBlockCount = dwNumSamples >> dwShift;

	//get size of last compressed block
	dwLastBlockSize = (dwBlockSize - 1) & dwNumSamples;

	//get result shifter value (used to shift data after decompression)
	dwResultShift = UnpackBits(unpackinfo,4);		

	if(!bStereo)
	{	//MONO HANDLING

		//zero internal compression values
		ZeroCompressionValues(&cv1,0);

		//If there's a remainder... then handle number of blocks + 1
		dwCount = (dwLastBlockSize == 0) ? dwBlockCount : dwBlockCount +1;
		while(dwCount > 0)
		{
			// anders: sjekk dette:
			// http://www.marcnetsystem.co.uk/cgi-shl/mn2.pl?ti=1141928801?drs=,V77M0R4,39,2,
			// her har jeg flytttet testen
			//check to see if we are handling the last block
			if((dwCount == 1) && (dwLastBlockSize != 0))
			{	//we are... set block size to size of last block
				dwBlockSize = dwLastBlockSize;
			}

			if (!DecompressSwitch(unpackinfo,&cv1,lpwOutputBuffer,dwBlockSize))
			{
				return FALSE;
			}

			for(i=0;i<dwBlockSize;i++)
			{	//shift end result
				lpwOutputBuffer[i] = lpwOutputBuffer[i] << dwResultShift;
			}
			
			//proceed to next block...
			lpwOutputBuffer += dwBlockSize;
			dwCount--;

		}		
	}
	else
	{	//STEREO HANDLING

		//Read "channel sum" flag
		btSumChannels = (BYTE)UnpackBits(unpackinfo,1);
		
		//zero internal compression values and alloc some temporary space
		ZeroCompressionValues(&cv1,dwBlockSize);
		ZeroCompressionValues(&cv2,dwBlockSize);

		//If there's a remainder... then handle number of blocks + 1
		dwCount = (dwLastBlockSize == 0) ? dwBlockCount : dwBlockCount +1;
		while(dwCount > 0)
		{

			// denne testen ble også flyttet fra bunn til topp av whilen

			//check to see if we are handling the last block
			if((dwCount == 1) && (dwLastBlockSize != 0))
			{	//we are... set block size to size of last block
				dwBlockSize = dwLastBlockSize;
			}


			//decompress both channels into temporary area
			if(!DecompressSwitch(unpackinfo,&cv1,cv1.lpwTempData,dwBlockSize))
			{
				return FALSE;
			}

			if (!DecompressSwitch(unpackinfo,&cv2,cv2.lpwTempData,dwBlockSize))
			{
				return FALSE;
			}
			
			for(i=0;i<dwBlockSize;i++)
			{	
				//store channel 1 and apply result shift
				ixx = i * 2;
				lpwOutputBuffer[ixx] = cv1.lpwTempData[i] << dwResultShift;
				
				//store channel 2
				ixx++;
				lpwOutputBuffer[ixx] = cv2.lpwTempData[i];
				
				//if btSumChannels flag is set then the second channel is
				//the sum of both channels
				// jeg tror nemlig at det går til helvete her...
				// så - enten skal simularity fixes, eller -
				// det kommer litt hakk selv når vi har sumChannels til 0 på 32-bit-floatene...
				if(btSumChannels != 0)
				{
					// KAN DET VÆRE VI HAR EN SIGNED_BUG HER PÅ 32-BIT_TALL!??
					lpwOutputBuffer[ixx] += cv1.lpwTempData[i];
				}
				
				//apply result shift to channel 2
				lpwOutputBuffer[ixx] = lpwOutputBuffer[ixx] << dwResultShift;

			}

			//proceed to next block
			lpwOutputBuffer += dwBlockSize * 2;
			dwCount--;


		}
		
		//tidy
		TidyCompressionValues(&cv1);
		TidyCompressionValues(&cv2);
	}

	return TRUE;
}

         */
    }
}
