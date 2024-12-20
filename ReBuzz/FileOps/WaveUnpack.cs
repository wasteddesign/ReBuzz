using System;
using System.IO;
using System.Security.AccessControl;

namespace ReBuzz.FileOps
{
    internal class WaveUnpack
    {
        public WaveUnpack(FileStream fs)
        {
            _filestream = fs;

        }

        //Decompress the wave at the current filestream position
        public byte[] DecompressWave(int numSamples, bool stereo)
        {
            //This is the output
            MemoryStream retData = new MemoryStream();

            //Count zero bits
            int zeroCount = CountZeroBits();
            if (zeroCount != 0)
                throw new Exception("Unknown compressed wave data format");

            //Get size shifter
            int shift = (int)UnPackBits(4);

            //Get size of compressed blocks
            int blockSize = 1 << shift;

            //Get number of compressed blocks
            int blockCount = numSamples >> shift;

            //get size of last compressed block
            int lastBlockSize = (blockSize - 1) & numSamples;

            //get result shifter value (used to shift data after decompression)
            int resultShift = (int)UnPackBits(4);
            if (!stereo)
            {
                //MONO HANDLING

                //Init compression values
                CompressStateData cv1 = new CompressStateData();

                //If there's a remainder... then handle number of blocks + 1
                int count = (lastBlockSize == 0) ? blockCount : (blockCount + 1);
                while (count > 0)
                {
                    byte[] data = DecompressSwitch(cv1, blockSize);

                    //Shift output and write to output
                    for (int x = 0; x < data.Length; x += 2)
                    {
                        //Read 16 bit value and shift
                        ushort wVal = data[x + 1];
                        wVal = (ushort)((wVal << 8) | data[x]);
                        wVal = (ushort)(wVal << resultShift);

                        //Write shifted value back
                        retData.WriteByte((byte)(wVal & 0xFF));
                        retData.WriteByte((byte)((wVal >> 8) & 0xFF));
                    }

                    --count;

                    //check to see if we are handling the last block
                    if ((count == 1) && (lastBlockSize != 0))
                    {   //we are... set block size to size of last block
                        blockSize = lastBlockSize;
                    }
                }
            }
            else
            {
                //Read channel sum flag
                bool channelSumFlag = UnPackBits(1) == 1;

                //zero internal compression values and alloc some temporary space
                CompressStateData cv1 = new CompressStateData();
                CompressStateData cv2 = new CompressStateData();

                //If there's a remainder... then handle number of blocks + 1
                int count = (lastBlockSize == 0) ? blockCount : (blockCount + 1);
                while (count > 0)
                {
                    //decompress both channels into temporary area
                    byte[] data1 = DecompressSwitch(cv1, blockSize);
                    byte[] data2 = DecompressSwitch(cv2, blockSize);
                    if (data1.Length != data2.Length)
                        throw new Exception("Wave compression area - stereo blocks are not same length");

                    for (int x = 0; x < data1.Length; x += 2)
                    {
                        //Channel 1
                        ushort wVal1 = data1[x + 1];
                        wVal1 = (ushort)((wVal1 << 8) | data1[x]);
                        ushort origVal1 = wVal1;
                        wVal1 = (ushort)(wVal1 << resultShift);

                        //Write shifted channel 1
                        retData.WriteByte((byte)(wVal1 & 0xFF));
                        retData.WriteByte((byte)((wVal1 >> 8) & 0xFF));

                        //Channel 2
                        ushort wVal2 = data2[x + 1];
                        wVal2 = (ushort)((wVal2 << 8) | data2[x]);

                        //if btSumChannels flag is set then the second channel is
                        //the sum of both channels
                        if (channelSumFlag)
                            wVal2 += origVal1;

                        //apply result shift to channel 2
                        wVal2 = (ushort)(wVal2 << resultShift);

                        //Write shifted channel 2
                        retData.WriteByte((byte)(wVal2 & 0xFF));
                        retData.WriteByte((byte)((wVal2 >> 8) & 0xFF));
                    }

                    //Next block
                    --count;

                    //check to see if we are handling the last block
                    if ((count == 1) && (lastBlockSize != 0))
                    {   //we are... set block size to size of last block
                        blockSize = lastBlockSize;
                    }
                }
            }

            //Convert the output memory stream to a byte array and return it
            return retData.ToArray();
        }


        //After decompressing all the levels for a wave, call this to reposition/rewind
        //the file pointer back, based on the number of bytes that have not been consumed.
        //(Since this class may over-read into its bit-cache for speedy bit-reading)
        public void Rewind()
        {
            if (_endOfData)
                return;

            int rewind = _currentByteIndex - _bytesInBuffer;
            ++rewind;
            _filestream.Position = _filestream.Position + rewind;
        }

        private int CountZeroBits()
        {
            int count = 0;
            UInt32 bit = UnPackBits(1);
            while (bit == 0)
            {
                ++count;
                bit = UnPackBits(1);
            }

            return count;
        }

        private byte[] DecompressSwitch(CompressStateData compressStateData, int blockSize)
        {
            MemoryStream retData = new MemoryStream();
            if (blockSize == 0)
                return null;

            //Get compression method
            int compressMethod = (int)UnPackBits(2);

            //Read size (in bits) of compressed values
            int valSizeBits = (int)UnPackBits(4);

            int size = blockSize;
            while (size > 0)
            {
                //Read compressed value
                ushort cmpValue = (ushort)UnPackBits(valSizeBits);

                //Count zeros
                int zeroCount = CountZeroBits();

                //Construct the true value
                UInt32 val = ((UInt32)zeroCount << valSizeBits) | cmpValue;

                //is value supposed to be positive or negative?
                if ((val & 1) == 0)
                {
                    //Value is positive
                    val = val >> 1;
                }
                else
                {
                    //Value is negative
                    ++val;
                    val = val >> 1;
                    val = ~val; //invert bits
                    ++val;//add one to make 2's compliment
                }

                //Now do stuff depending on which method we're using....
                switch (compressMethod)
                {
                    case 0:
                        compressStateData.wCompressSum2 = (ushort)((val - compressStateData.wCompressResult) - compressStateData.wCompressSum1);
                        compressStateData.wCompressSum1 = (ushort)(val - compressStateData.wCompressResult);
                        compressStateData.wCompressResult = (ushort)val;
                        break;
                    case 1:
                        compressStateData.wCompressSum2 = (ushort)(val - compressStateData.wCompressSum1);
                        compressStateData.wCompressSum1 = (ushort)val;
                        compressStateData.wCompressResult = (ushort)(compressStateData.wCompressResult + val);
                        break;
                    case 2:
                        compressStateData.wCompressSum2 = (ushort)val;
                        compressStateData.wCompressSum1 = (ushort)(compressStateData.wCompressSum1 + val);
                        compressStateData.wCompressResult += compressStateData.wCompressSum1;
                        break;
                    case 3:
                        compressStateData.wCompressSum2 = (ushort)(compressStateData.wCompressSum2 + val);
                        compressStateData.wCompressSum1 += compressStateData.wCompressSum2;
                        compressStateData.wCompressResult += compressStateData.wCompressSum1;
                        break;
                    default:
                        throw new Exception("Invalid wave compression mode");
                }

                //Store result into output buffer
                retData.WriteByte((byte)(compressStateData.wCompressResult & 0xFF));
                retData.WriteByte((byte)((compressStateData.wCompressResult >> 8) & 0xFF));

                --size;
            }

            return retData.ToArray();
        }

        private UInt32 UnPackBits(int amount)
        {
            if (_endOfData)
                return 0;

            UInt32 ret = 0;
            int readAmount = amount;
            int shift = 0;
            while (readAmount > 0)
            {
                if ((_curBit == c_maxbit) || (_bytesInBuffer == 0))
                {
                    _curBit = 0;
                    ++_currentByteIndex;
                    if (_currentByteIndex >= _bytesInBuffer)
                    {
                        //run out of buffer... read more file into buffer
                        int amountRead = _filestream.Read(_buffer, 0, _buffer.Length);
                        _bytesInBuffer = amountRead;
                        _currentByteIndex = 0;

                        //if we didnt read anything then exit now
                        if (amountRead == 0)
                        {
                            _endOfData = true;
                            return 0;
                        }
                    }
                }

                //Calculate number of bits to read from current byte
                int numBits = ((readAmount + _curBit) > c_maxbit) ? (c_maxbit - _curBit) : readAmount;

                //Caclulate bitmask
                byte mask = (byte)((1 << numBits) - 1);

                //Get value from buffer
                UInt32 val = _buffer[_currentByteIndex];
                val = (UInt32)(val >> _curBit);

                //Apply mask
                val &= mask;

                //Shift value to correct position
                val = (UInt32)(val << shift);

                //Update return value
                ret |= val;

                //Update internals
                _curBit += numBits;
                shift += numBits;
                readAmount -= numBits;
            }

            return ret;
        }



        const int c_maxbit = 8;


        FileStream _filestream;
        byte[] _buffer = new byte[2048];
        int _currentByteIndex = -1;
        //int _maxBytes = -1;
        int _bytesInBuffer = 0;
        int _curBit = c_maxbit;
        bool _endOfData = false;

        class CompressStateData
        {
            public ushort wCompressResult = 0;
            public ushort wCompressSum1 = 0;
            public ushort wCompressSum2 = 0;
        }
    }





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