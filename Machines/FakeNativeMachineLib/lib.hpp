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

std::string ReadMachineName()
{
  return ReadShortFileContentAndRemoveFile(GetDllFilePath().string() + ".txt");
}


static void DebugShow(const std::string& message)
{
  MessageBoxA(nullptr, message.c_str(), "Debug msg", 0);
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

#define FAKE_MACHINE_SLIDER(const_name, param_name) \
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