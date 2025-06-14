// dllmain.cpp : Defines the entry point for the DLL application.
#include "pch.h"

#include "MachineInterface.h"
#include <windef.h>
#include <cstdlib>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>

std::string ReadShortFileContentAndRemoveFile(std::string filePath)
{
  std::string content;
  std::ifstream file(filePath);
  file >> content;
  file.close();

  // removing because the next instance of the same machine will recreate the file
  // with different content and we don't want confusion that
  // the machine can reuse this file
  std::remove(filePath.c_str());
  return content;
}

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

static void DebugShow(const std::string& message)
{
  MessageBoxA(nullptr, message.c_str(), "Debug msg", 0);
}

static void AbortIfRequested(const std::string& machineName)
{
  _set_abort_behavior(0, _WRITE_ABORT_MSG);
  auto path = GetDllFilePath().parent_path() / (std::string("crash_fake_machine_") + machineName);
  if (std::filesystem::exists(path))
  {
    std::abort();    
  }
}

constexpr CMachineParameter sampleValueLeftIntegral = 
{
  .Type = pt_word,                          // type
  .Name = "SampleValueLeftIntegral",        // name
  .Description = "SampleValueLeftIntegral", // description
  .MinValue = -100,                         // MinValue 
  .MaxValue = 100,                          // MaxValue
  .NoValue = 100+1,                         // NoValue
  .Flags = 0,                               // Flags
  .DefValue = 0                             // Default value
};

constexpr CMachineParameter sampleValueLeftDivisor = 
{
  .Type = pt_word,                          // type
  .Name = "SampleValueLeftDivisor",         // name
  .Description = "SampleValueLeftDivisor",  // description
  .MinValue = -100,                         // MinValue 
  .MaxValue = 100,                          // MaxValue
  .NoValue = 100+1,                         // NoValue
  .Flags = 0,                               // Flags
  .DefValue = 0                             // Default value
};

constexpr CMachineParameter sampleValueRightIntegral = 
{
  .Type = pt_word,                            // type
  .Name = "SampleValueRightIntegral",         // name
  .Description = "SampleValueRightIntegral",  // description
  .MinValue = -100,                           // MinValue 
  .MaxValue = 100,                            // MaxValue
  .NoValue = 100+1,                           // NoValue
  .Flags = 0,                                 // Flags
  .DefValue = 0                               // Default value
};

constexpr CMachineParameter sampleValueRightDivisor = 
{
  .Type = pt_word,                           // type
  .Name = "SampleValueRightDivisor",         // name
  .Description = "SampleValueRightDivisor",  // description
  .MinValue = -100,                          // MinValue 
  .MaxValue = 100,                           // MaxValue
  .NoValue = 100+1,                          // NoValue
  .Flags = 0,                                // Flags
  .DefValue = 0                              // Default value
};

static CMachineParameter const* pParameters[] = { 
  // global
  &sampleValueLeftIntegral,
  &sampleValueLeftDivisor,
  &sampleValueRightIntegral,
  &sampleValueRightDivisor,
};

#pragma pack(1)

class gvals
{
public:
  word sampleValueLeftIntegral;
  word sampleValueLeftDivisor;
  word sampleValueRightIntegral;
  word sampleValueRightDivisor;
};

#pragma pack()

CMachineInfo const                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                   MacInfo = 
{
  .Type = MT_GENERATOR,                          // type
  .Version = MI_VERSION,                         // version
  .Flags = MIF_DOES_INPUT_MIXING,                // flags
  .minTracks = 0,                                // min tracks
  .maxTracks = 0,                                // max tracks
  .numGlobalParameters = std::size(pParameters), // numGlobalParameters
  .numTrackParameters = 0,                       // numTrackParameters
  .Parameters = pParameters,
  .numAttributes = 0,
  .Attributes = nullptr,
  .Name = "FakeNativeGenerator",
  .ShortName = "FakeNativeGen",                  // short name
  .Author = "WDE",                               // author
  .Commands = nullptr,                           //"Command1\nCommand2\nCommand3"
  .pLI = nullptr
};

class mi : public CMachineInterface
{
public:
  mi();
  bool WorkMonoToStereo(float* pin, float* pout, int numsamples, int const mode) override;

private:
  gvals gval;
  std::string machineName;
};

DLL_EXPORTS

mi::mi() : machineName(ReadShortFileContentAndRemoveFile(GetDllFilePath().string() + ".txt"))
{
  AbortIfRequested(machineName);
  GlobalVals = &gval;
}

bool mi::WorkMonoToStereo(float* pin, float* pout, const int numsamples, int const mode)
{
  AbortIfRequested(machineName);
  for (auto i = 0 ; i < numsamples * 2 ; i+=2)
  {
    pout[i] = 
      static_cast<float>(gval.sampleValueLeftIntegral)/static_cast<float>(gval.sampleValueLeftDivisor);
    pout[i+1] = 
      static_cast<float>(gval.sampleValueRightIntegral)/static_cast<float>(gval.sampleValueRightDivisor);
  }

  return true;
}