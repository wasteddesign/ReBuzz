using System;

namespace BuzzGUI.Interfaces
{
    public enum MachineType { Master, Generator, Effect }

    [Flags]
    public enum MachineInfoFlags
    {
        MONO_TO_STEREO = (1 << 0),
        PLAYS_WAVES = (1 << 1),
        USES_LIB_INTERFACE = (1 << 2),
        USES_INSTRUMENTS = (1 << 3),
        DOES_INPUT_MIXING = (1 << 4),
        NO_OUTPUT = (1 << 5),
        CONTROL_MACHINE = (1 << 6),
        INTERNAL_AUX = (1 << 7),
        EXTENDED_MENUS = (1 << 8),
        PATTERN_EDITOR = (1 << 9),
        PE_NO_CLIENT_EDGE = (1 << 10),
        GROOVE_CONTROL = (1 << 11),
        DRAW_PATTERN_BOX = (1 << 12),
        STEREO_EFFECT = (1 << 13),
        MULTI_IO = (1 << 14),
        PREFER_MIDI_NOTES = (1 << 15),
        LOAD_DATA_RUNTIME = (1 << 16),
        ALWAYS_SHOW_PLUGS = (1 << 17)
    }

    public interface IMachineInfo
    {
        MachineType Type { get; }
        int Version { get; }
        int InternalVersion { get; }
        MachineInfoFlags Flags { get; }
        int MinTracks { get; }
        int MaxTracks { get; }
        string Name { get; }
        string ShortName { get; }
        string Author { get; }

    }
}
