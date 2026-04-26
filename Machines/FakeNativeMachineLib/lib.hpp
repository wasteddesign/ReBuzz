#pragma once
#include <charconv>
#include <optional>
#include <string_view>
#include <unordered_map>
#include <cmath>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <string>
#include <sstream>
#include <string>
#include <unordered_map>

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

std::string ReadFileContent(std::filesystem::path filePath)
{
  std::string content;
  std::ifstream file(filePath.string());
  file >> content;
  file.close();
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

// Reads a key=value config file into a map.
static std::unordered_map<std::string, std::string> ReadConfigFile(const std::filesystem::path& path)
{
  std::unordered_map<std::string, std::string> config;
  std::ifstream file(path);
  std::string line;
  while (std::getline(file, line))
  {
    std::string_view sv = line;
    const auto eq = sv.find('=');
    if (eq != std::string_view::npos && eq != 0)
    {
      config[std::string(sv.substr(0, eq))] = std::string(sv.substr(eq + 1));
    }
  }
  return config;
}

// Parses a typed value from a config map entry. Returns std::nullopt if the key
// is missing or the value cannot be converted to T.
template<typename T>
static std::optional<T> GetConfigValue(
  const std::unordered_map<std::string, std::string>& config, const std::string& key)
{
  const auto it = config.find(key);
  if (it == config.end()) return std::nullopt;
  T value;
  auto [ptr, ec] = std::from_chars(it->second.data(), it->second.data() + it->second.size(), value);
  if (ec == std::errc()) return value;
  return std::nullopt;
}

// Reads all per-instance startup configuration from <dll>.init and deletes the
// file so the next instance starts with a clean slate.
static std::unordered_map<std::string, std::string> ReadAndDeleteInstanceInitConfig()
{
  auto path = GetDllFilePath().string() + ".init";
  auto config = ReadConfigFile(path);
  std::remove(path.c_str());
  return config;
}

// Extracts the machine instance name from an init config map.
static std::string GetMachineNameFromConfig(
  const std::unordered_map<std::string, std::string>& config)
{
  const auto it = config.find("MachineName");
  return it != config.end() ? it->second : std::string{};
}


static void DebugShow(const std::string& machineName, const std::string& message, byte enabled = 1)
{
  if (1 == enabled)
  {
    MessageBoxA(nullptr, message.c_str(), machineName.c_str(), 0);
  }
}

static void AbortIfRequested(const std::string& machineName, const std::string& when)
{
  _set_abort_behavior(0, _WRITE_ABORT_MSG);
  auto path = GetDllFilePath().parent_path() / (std::string("crash_fake_machine_") + machineName);
  if (std::filesystem::exists(path))
  {
    auto content = ReadFileContent(path);
    if (content == when)
    {
      std::abort();
    }
  }
}

#define FAKE_MACHINE_INT_SLIDER(const_name, param_name) \
constexpr CMachineParameter const_name = \
{ \
  .Type = pt_word, \
  .Name = #param_name, \
  .Description = #param_name, \
  .MinValue = -100, \
  .MaxValue = 100, \
  .NoValue = 101, \
  .Flags = 0, \
  .DefValue = 0 \
}

#define FAKE_MACHINE_SWITCH_SLIDER(const_name, param_name) \
constexpr CMachineParameter const_name = \
{ \
  .Type = pt_switch, \
  .Name = #param_name, \
  .Description = #param_name, \
  .MinValue = SWITCH_OFF, \
  .MaxValue = SWITCH_ON, \
  .NoValue = SWITCH_NO, \
  .Flags = MPF_STATE, \
  .DefValue = SWITCH_OFF \
};
