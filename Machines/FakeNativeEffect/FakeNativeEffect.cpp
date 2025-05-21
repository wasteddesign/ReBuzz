// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <windef.h>
#include <cstdlib>
#include <cmath>
#include <iterator>
#include <string>
#include <filesystem>
#include <__msvc_filebuf.hpp>

std::filesystem::path GetDllFilePath()
{
    HMODULE hModule = nullptr;
    char path[MAX_PATH];

    // Use a variable inside the DLL to get its module handle
    if (GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                          reinterpret_cast<LPCSTR>(&GetDllFilePath), &hModule))
    {
        if (GetModuleFileNameA(hModule, path, MAX_PATH) > 0)
        {
            return std::filesystem::path(std::string(path));
        }
    }
    throw std::runtime_error("Could not get DLL Path");
}

static void AbortIfRequested()
{
  auto path = GetDllFilePath().parent_path() / "crash_fake_machine";
  if (std::filesystem::exists(path))
  {
    std::abort();
  }
}

constexpr CMachineParameter sampleValueLeftMultiplier = 
{
  .Type = pt_word,                            // type
  .Name = "SampleValueLeftMultiplier",        // name
  .Description = "SampleValueLeftMultiplier",	// description
  .MinValue = -100,                           // MinValue	
  .MaxValue = 100,                            // MaxValue
  .NoValue = 100+1,                           // NoValue
  .Flags = 0,                                 // Flags
  .DefValue = 0                               // Default value
};

constexpr CMachineParameter sampleValueRightMultiplier = 
{
  .Type = pt_word,                              // type
  .Name = "SampleValueRightMultiplier",         // name
  .Description = "SampleValueRightMultiplier",	// description
  .MinValue = -100,                             // MinValue	
  .MaxValue = 100,                              // MaxValue
  .NoValue = 100+1,                             // NoValue
  .Flags = 0,                                   // Flags
  .DefValue = 0                                 // Default value
};

static CMachineParameter const* pParameters[] = { 
  // global
  &sampleValueLeftMultiplier,
  &sampleValueRightMultiplier,
};

#pragma pack(1)

class gvals
{
public:
  word sampleValueLeftMultiplier;
  word sampleValueRightMultiplier;
};

#pragma pack()

CMachineInfo const                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   MacInfo = 
{
  .Type = MT_EFFECT,  							               // type
  .Version = MI_VERSION,                           // version
  .Flags = MIF_STEREO_EFFECT,		                   // flags
  .minTracks = 0,										               // min tracks
  .maxTracks = 0,										               // max tracks
  .numGlobalParameters = std::size(pParameters),	 // numGlobalParameters
  .numTrackParameters = 0,										     // numTrackParameters
  .Parameters = pParameters,
  .numAttributes = 0,
  .Attributes = nullptr,
  .Name = "FakeNativeEffect",
  .ShortName = "FakeNativeEffect",	               // short name
  .Author = "WDE", 						                     // author
  .Commands = nullptr,                             //"Command1\nCommand2\nCommand3"
  .pLI = nullptr
};

class mi : public CMachineInterface
{
public:
  mi();
  bool Work(float* psamples, int numsamples, const int mode) override;

private:
  gvals gval;
};

DLL_EXPORTS

mi::mi()
{
  _set_abort_behavior(0, _WRITE_ABORT_MSG);
  AbortIfRequested();
  GlobalVals = &gval;
}

bool mi:: Work(float* psamples, int numsamples, const int mode)
{
  AbortIfRequested();
  for (auto i = 0 ; i < numsamples*2 ; i+=2)
  {
    psamples[i] = psamples[i] * gval.sampleValueLeftMultiplier;
    psamples[i + 1] = psamples[i + 1] * gval.sampleValueRightMultiplier;
  }

  return true;
}

