#include "stdafx.h"
#include "../IPC/ServerProcess.h"
#include "Machine.h"

class Level : public CWaveLevel
{
public:
	Level()
	{
		hShared = NULL;
	}

	~Level()
	{
		if (hShared != NULL && pSamples != NULL) ::UnmapViewOfFile(pSamples);
	}

public:
	HANDLE hShared;
	string mappedFileId;
};

class Envelope
{
public:
	vector<int> Points;
	bool Enabled;
};

class Wave
{
public:
	Wave()
	{
		Allocated = false;
	}

public:
	bool Allocated;
	string Name;
	CWaveInfo Info;
	vector<Level> Levels;
	vector<Envelope> Envelopes;
};

Wave wavetable[WAVE_MAX];

#ifdef _WIN64
#define SIZE_OF_CWAVELEVEL 28
#else
#define SIZE_OF_CWAVELEVEL 24
#endif


void ReadWavetable(IPC::MessageReader &r)
{
	for (int wi = 0; wi < WAVE_MAX; wi++)
	{
		Wave& w = wavetable[wi];

		r.Read(w.Allocated);

		if (w.Allocated != NULL)
		{
			w.Name = r.ReadString();
			r.Read(w.Info);
			int levelcount = (int)r.ReadDWORD();
			if (levelcount != w.Levels.size())
			{
				w.Levels.resize(levelcount);
			}

			for (int i = 0; i < levelcount; i++)
			{
				Level& l = w.Levels[i];
				short* poldsamples = l.pSamples;

				string idNew = r.ReadString();
				r.Read(&w.Levels[i], SIZE_OF_CWAVELEVEL);

				if (idNew != l.mappedFileId)
				{
					if (l.hShared != NULL) ::UnmapViewOfFile(poldsamples);
					HANDLE hShared = ::OpenFileMapping(FILE_MAP_READ | FILE_MAP_WRITE, FALSE, idNew.c_str());
					l.hShared = hShared;
					if (hShared != NULL) l.pSamples = (short*)::MapViewOfFile(hShared, FILE_MAP_WRITE | FILE_MAP_READ, 0, 0, 0);
					l.mappedFileId = idNew;
				}
				else
				{
					l.pSamples = poldsamples;
				}
			}

			int envcount = (int)r.ReadDWORD();
			if (envcount != w.Envelopes.size())
				w.Envelopes.resize(envcount);

			for (int i = 0; i < envcount; i++)
			{
				Envelope& e = w.Envelopes[i];
				int pointcount = (int)r.ReadDWORD();
				if (pointcount != e.Points.size()) e.Points.resize(pointcount);
				if (pointcount > 0) r.Read(&e.Points[0], pointcount * sizeof(int));
				r.Read(e.Enabled);
			}
		}
		else
		{
			w.Levels.resize(0);
		}
	}
}

char const *wavetableGetWaveName(int i)
{
	if (i < 1 || i > WAVE_MAX) return NULL;
	Wave &w = wavetable[i-1];
	if (!w.Allocated) return NULL;
	return w.Name.c_str();
}

CWaveInfo const *wavetableGetWave(int i)
{
	if (i < 1 || i > WAVE_MAX) return NULL;
	Wave &w = wavetable[i-1];
	if (!w.Allocated || w.Levels.size() == 0) return NULL;
	return &w.Info;
}

CWaveLevel const *wavetableGetWaveLevel(int const i, int const level)
{
	if (i < 1 || i > WAVE_MAX) return NULL;

	Wave &w = wavetable[i-1];
	if (!w.Allocated || level < 0 || level >= (int)w.Levels.size()) return NULL;
	return &w.Levels[level];
}

CWaveLevel const *wavetableGetNearestWaveLevel(int const i, int const note)
{
	if (i < 1 || i > WAVE_MAX) return NULL;
	
	Wave &w = wavetable[i-1];
	if (!w.Allocated || w.Levels.size() == 0) return NULL;

	int l = 0;

	for (int j = 0; j < (int)w.Levels.size(); j++)
	{
		if (w.Levels[j].RootNote > note)
			break;

		l = j;
	} 

	return &w.Levels[l];

}

int wavetableGetEnvSize(int const wave, int const env)
{
	if (wave < 1 || wave > WAVE_MAX) return 0;
	Wave &w = wavetable[wave-1];
	if (!w.Allocated || w.Levels.size() == 0) return NULL;
	if (env >= (int)w.Envelopes.size()) return 0;
	if (!w.Envelopes[env].Enabled)	return 0;
	return w.Envelopes[env].Points.size() / 2;

}

bool wavetableGetEnvPoint(int const wave, int const env, int const i, word &x, word &y, int &flags)
{
	if (wave < 1 || wave > WAVE_MAX) return false;
	if (wave < 1 || wave > WAVE_MAX) return 0;
	Wave &w = wavetable[wave-1];
	if (env >= (int)w.Envelopes.size()) return false;

	Envelope const &e = w.Envelopes[env];
	if (!e.Enabled)	return false;
	if (i < 0 || i >= (int)e.Points.size() / 2) return false;

	x = e.Points[i*2] & 65535;
	y = e.Points[i*2+1] & 65535;
	flags = (e.Points[i*2+1] & 65536) ? 1 : 0;

	return true;
}
