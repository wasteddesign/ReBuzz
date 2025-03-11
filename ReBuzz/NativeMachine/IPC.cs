using System;

namespace ReBuzz.NativeMachine
{
    // Message format: int message ID, data

    public enum UIMessages
    {
        UIBuzzInit,
        UIDSPInit,
        UILoadLibrary,
        UINewMI,
        UIDeleteMI,
        UIInit,
        UISave,
        UIAttributesChanged,
        UIStop,
        UICommand,
        UIAddInput,
        UIDeleteInput,
        UIRenameInput,
        UISetInputChannels,
        UIDescribeValue,
        UIHandleGUIMessage,
        UIGetEnvelopeInfos,
        UIGetDLLPtr,
        UIGetInstrumentList,
        UIGetInstrumentPath,
        UISetInstrument,
        UIEvent,
        UIGetResources,
        UICreatePattern,
        UICreatePatternCopy,
        UIDeletePattern,
        UIRenamePattern,
        UISetPatternLength,
        UIPlayPattern,
        UICreatePatternEditor,
        UISetEditorPattern,
        UIAddTrack,
        UIDeleteLastTrack,
        UIEnableCommandUI,
        UIDrawPatternBox,
        UISetPatternTargetMachine,
        UIGetChannelName,
        UIGetSubMenu,
        UILoad,
        UIGotMidiFocus,
        UILostMidiFocus,
        UIImportFinished,
        UIImplementsFunction,
        UIGetCommands,
        UIRemapMachineNames
    }
    public enum AudioMessages
    {
        AudioBeginBlock,
        AudioBeginFrame,
        AudioSetNumTracks,
        AudioTick,
        AudioWork,
        AudioWorkMonoToStereo,
        AudioInput,
        AudioMultiWork,
    };

    public enum HostMessages
    {
        HostDCWriteLine,
        HostInitMIEx,
        HostInitMDK,
        HostSetnumOutputChannels,
        HostSetInputChannelCount,
        HostSetOutputChannelCount,
        HostGetMachineName,
        HostRenameMachine,
        HostSetEventHandler,
        HostSetModifyFlag,
        HostControlChange,
        HostControlChangeImmediate,
        HostSendControlChanges,
        HostSetMidiFocus,
        HostGetMachineNames,
        HostCreateRefMachine,
        HostSendMidiNote,
        HostSendMidiControlChange,
        HostGetOption,
        HostGetNumTracks,
        HostGetParameterState,
        HostDescribeValue,
        HostGetAttribute,
        HostSetAttribute,
        HostAttributesChanged,
        HostGetPlayingSequence,
        HostGetPlayingRow,
        HostBeginWriteToPlayingPattern,
        HostEndWriteToPlayingPattern,
        HostWriteToPlayingPattern,
        HostMidiOut,
        HostSetTempo,
        HostSetTPB,
        HostSetSongPosition
    };

    public enum MIDIMessages
    {
        MIDINote,
        MIDIControlChange
    };
    public enum BEventType
    {
        DoubleClickMachine,                 // return true to ignore default handler (open parameter dialog), no parameters
        gDeleteMachine,                     // data = CMachine *, param = ThisMac
        gAddMachine,                        // data = CMachine *, param = ThisMac
        gRenameMachine,                     // data = CMachine *, param = ThisMac
        gUndeleteMachine,                   // data = CMachine *, param = ThisMac
        gWaveChanged                        // (int)data = wave number
    };

    public struct CMachineEvent
    {
        public BEventType Type;
        public IntPtr Event_Handler;
        public IntPtr Param_Addr;
    }

    public enum ChannelType { AudioChannel, UIChannel, MidiChannel, HostChannel }


    public unsafe struct BuzzGlobalState
    {
        public int AudioFrame;
        public int ADWritePos;
        public int ADPlayPos;
        public int SongPosition;
        public int LoopStart;
        public int LoopEnd;
        public int SongEnd;
        public int StateFlags;
        public bool MIDIFiltering;
        public bool SongClosing;
    };

    // BuzzEngine uses pragma pack(8) so...
    // 4 + 256 * 1024 = 262148
    public unsafe struct MessageBuffer
    {
        public static int MaxSize = 256 * 1024;
        public int size;
        public fixed byte data[256 * 1024];
    }
    public enum ChannelState { listening, call, returning, callback, sendbuffer, sendlastbuffer, replybuffer, replylastbuffer };

    // Size: 4 + 4 + 4 + 4 = 16 (base offset)
    public unsafe struct ChannelSharedMem
    {
        public uint serverPing;
        public uint serverPong;

        public ChannelState state;
        public bool callbackMode;

        public MessageBuffer messageBuffer;
    };

    public class IPC
    {
        public const int MaxChannels = 4;

        public static int ChannelSharedMemDataEnd = 16;
        public static int MessageBufferStructSize = 262148;

        public static int GetMessageBufferOffset(ChannelType channel)
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * (int)channel;
        }

        public static int GetChannelSharedMemPingOffset(ChannelType channel)
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * (int)channel + 0;
        }

        public static int GetChannelSharedMemPongOffset(ChannelType channel)
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * (int)channel + 4;
        }

        public static int GetChannelSharedMemStateOffset(ChannelType channel)
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * (int)channel + 8;
        }

        public static int GetChannelSharedMemCallbackOffset(ChannelType channel)
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * (int)channel + 12;
        }

        public static int GetSharedPageSize()
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * 4 + 4;
        }

        internal static int GetChannelSharedMemSizeOffset(ChannelType channel)
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * (int)channel + 16;
        }

        internal static int GetChannelSharedMemDataOffset(ChannelType channel)
        {
            return (ChannelSharedMemDataEnd + MessageBufferStructSize) * (int)channel + 20;
        }
    }
}
