using NAudio.Midi;
using ReBuzz.Midi;
using SysExTests;
using System;
using System.Threading;

class Program
{
    static void Main()
    {
        Console.WriteLine("=== MIDI Synth State Manager ===");

        // List devices so you can pick the right ones
        Console.WriteLine("MIDI Inputs:");
        for (int i = 0; i < MidiIn.NumberOfDevices; i++)
        {
            var caps = MidiIn.DeviceInfo(i);
            Console.WriteLine($"  [{i}] {caps.ProductName}");
        }

        Console.WriteLine("MIDI Outputs:");
        for (int i = 0; i < MidiOut.NumberOfDevices; i++)
        {
            var caps = MidiOut.DeviceInfo(i);
            Console.WriteLine($"  [{i}] {caps.ProductName}");
        }

        Console.Write("Select input device index: ");
        int inIndex = int.Parse(Console.ReadLine() ?? "0");
        Console.Write("Select output device index: ");
        int outIndex = int.Parse(Console.ReadLine() ?? "0");

        var outCaps = MidiOut.DeviceInfo(outIndex);
        string manufacturerGuess = outCaps.ProductName; // crude but useful

        var stateManager = new MidiSynthStateManager();
        var ciManager = new MidiCiStateManager();

        using var device = new MidiDevice(inIndex, outIndex, stateManager, ciManager);

        stateManager.StateReceived += dump =>
        {
            Console.WriteLine("State stored in memory.");
            stateManager.SaveStateToFile("synth_state.syx");
        };

        ciManager.StateReceived += doc =>
        {
            Console.WriteLine("Universal MIDI-CI state received:");
            Console.WriteLine(doc.RootElement.ToString());
        };

        // Choose a “best guess” state request based on manufacturer name
        byte[] requestDumpMessage = SysExRequests.GetDefaultStateRequestForManufacturer(manufacturerGuess);

        Console.WriteLine($"Using manufacturer guess: \"{manufacturerGuess}\"");
        Console.WriteLine("Sending state dump request...");
        device.RequestStateDump(requestDumpMessage);

        Console.WriteLine("Waiting for dump (10 seconds)...");
        Thread.Sleep(10_000);

        // MIDI-CI example: request discovery, profile inquiry, and full state dump

        // 1. Discover device
        device.SendSysEx(MidiCiMessages.Discovery());

        // 2. Ask what profiles it supports
        device.SendSysEx(MidiCiMessages.ProfileInquiry());

        // 3. Request full universal state (Property Exchange)
        device.SendSysEx(MidiCiMessages.PropertyGet("State", "Full"));


        if (stateManager.HasState)
        {
            Console.WriteLine("State received. Press ENTER to restore from memory...");
            Console.ReadLine();
            device.SendStateDump(stateManager.GetStoredState());

            Console.WriteLine("Press ENTER to restore from disk (synth_state.syx)...");
            Console.ReadLine();
            if (stateManager.LoadStateFromFile("synth_state.syx"))
            {
                device.SendStateDump(stateManager.GetStoredState());
            }
        }
        else
        {
            Console.WriteLine("No state received. Check device’s SysEx settings / bulk dump mode.");
        }

        Console.WriteLine("Done.");
    }
}
