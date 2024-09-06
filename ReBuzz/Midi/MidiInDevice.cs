using BuzzGUI.Common;
using NAudio.Midi;
using ReBuzz.Core;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace ReBuzz.Midi
{
    internal class MidiInDevice
    {
        private MidiIn midiIn;
        private readonly ReBuzzCore buzz;
        ConcurrentQueue<object> midiMessages;
        private Task midiMessagesTask;
        private bool stopped;
        readonly ManualResetEvent midiMessageReceivedEvent = new ManualResetEvent(false);

        public MidiInDevice(ReBuzzCore buzz)
        {
            this.buzz = buzz;
        }

        public void CreateMidiIn(int selectedDeviceIndex)
        {
            DisposeMidiIn();

            try
            {
                midiMessages = new ConcurrentQueue<object>();

                // Create midi message handler task
                stopped = false;
                midiMessagesTask = new Task(() => ProcessMidiMessages(), CancellationToken.None, TaskCreationOptions.LongRunning);
                midiMessagesTask.Start();

                // Start listening Midi messages
                midiIn = new MidiIn(selectedDeviceIndex);
                midiIn.MessageReceived += MidiIn_MessageReceived;
                midiIn.ErrorReceived += MidiIn_ErrorReceived;
                midiIn.SysexMessageReceived += MidiIn_SysexMessageReceived;
                midiIn.Start();
            }
            catch (Exception e)
            {
                buzz.DCWriteLine(e.Message);
            }
        }

        public void DisposeMidiIn()
        {
            stopped = true;
            midiMessageReceivedEvent.Set();
            if (midiIn != null)
            {
                try
                {
                    midiIn.Stop();
                    midiIn.Dispose();
                }
                catch { }
                midiIn = null;
            }
        }

        private void MidiIn_ErrorReceived(object sender, MidiInMessageEventArgs e)
        {
            Global.Buzz.DCWriteLine(String.Format("Time {0} Message 0x{1:X8} Event {2}",
                e.Timestamp, e.RawMessage, e.MidiEvent));
        }

        private void MidiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
        {
            buzz.SendMIDIInput(e.RawMessage);

            //midiMessages.Enqueue(e.RawMessage);
            //midiMessageReceivedEvent.Set();
        }

        private void MidiIn_SysexMessageReceived(object sender, MidiInSysexMessageEventArgs e)
        {
            //buzz.SendMIDIInput(e.SysexBytes);
            //midiMessages.Enqueue(e.SysexBytes);
            //midiMessageReceivedEvent.Set();
        }

        private void ProcessMidiMessages()
        {
            while (!stopped)
            {
                midiMessageReceivedEvent.WaitOne();
                midiMessageReceivedEvent.Reset();
                while (midiMessages.Count > 0)
                {
                    if (midiMessages.TryDequeue(out object msg))
                    {
                        if (msg is int)
                        {
                            buzz.SendMIDIInput((int)msg);
                        }
                        else if (msg is byte[])
                        {
                            //buzz.SendMIDISysexInput((byte[])msg);
                        }
                    }
                }
            }
        }
    }
}
