# ReBuzz
ReBuzz is a modular digital audio workstation (DAW) built upon the foundation of [Jeskola Buzz](https://jeskola.net/buzz/) software. Written in C#, ReBuzz combines modern features with the beloved workflow of its predecessor. While it’s still in development, users should exercise some caution regarding stability and other potential uncertainties. The primary focus is on providing a stable experience and robust VST support.

## Features
* 32 and 64 bit VST2/3 support
* 32 and 64 bit buzz machine support
* Modern Pattern Editor, Modern Sequence Editor, AudioBlock, EnvelopeBlock, CMC, TrackScript...
* Multi-process architecture
* Multi-io for native and managed machines
* Includes [NWaves](https://github.com/ar1st0crat/NWaves) .NET DSP library for audio processing
* bmx and bmxml file support
* ...

## Download
[ReBuzz Installer](https://github.com/wasteddesign/ReBuzz/releases/latest)

Requires:
1. [.NET 9.0 Desktop Runtime - Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/thank-you/sdk-9.0.100-windows-x64-installer)
2. [Latest Microsoft Visual C++ Redistributable](https://learn.microsoft.com/en-us/cpp/windows/latest-supported-vc-redist?view=msvc-170)

## How to build?
1. Get all the solution parts:
2. 
 a. ReBuzz (this repo)
 b. ReBuzzGUI
 c. ReBuzzEngine
 d. ModernPatternEditor
 e. ReBuzzRunTime (https://github.com/themarcnet/ReBuzzRunTime)
 f. ReBuzz3rdParty (https://github.com/themarcnet/ReBuzz3rdParty) - **IMPORTANT: Get with --recurse-submodules**

3. Your directory layout should be:
 - root\
  - root\ModernPatternEditor
  - root\ReBuzz
  - root\ReBuzz3rdParty
  - root\ReBuzzEngine
  - root\ReBuzzGUI
  - root\ReBuzzRunTime

4. Load the ReBuzz.sln (located at root\ReBuzz\ReBuzz.sln)

5. Ensure ReBuzz is the Startup project. It should be highlighted bold in the Solution Explorer.
 a. If it is not, then right click the ReBuzz project in Solution Explorer, and select "Set as Startup Project"

6. Build.

7. The result should be output to (depending on if Debug or Release is selected):
 - root\ReBuzz\bin\Debug\net9.0-windows\
 - root\ReBuzz\bin\Release\net9.0-windows\

8. You should be able to run ReBuzz directly from here.

## How can I help?
All the basic functionality is implemented but there many areas to improve. In general, contributions are needed in every part of the software, but here are few items to look into:

- [ ] Pick an [issue](https://github.com/wasteddesign/ReBuzz/issues) and start contributing to Rebuzz development today!
- [ ] Improve stability, fix bugs and issues
- [ ] Cleanup code and architecture
- [ ] Add comments and documentation
- [ ] Improve Audio wave handling (Wavetable)
- [ ] Improve file handling to support older songs
- [ ] Reduce latency, optimize code

You might want to improve also
- [ReBuzz GUI Components](https://github.com/wasteddesign/ReBuzzGUI)
- [ReBuzzEngine](https://github.com/wasteddesign/ReBuzzEngine)
- [ModernPatternEditor](https://github.com/wasteddesign/ModernPatternEditor)

Let's make this a good one.
