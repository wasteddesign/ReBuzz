using BuzzGUI.Interfaces;
using Microsoft.Windows.Devices.Midi2;
using Microsoft.Windows.Devices.Midi2.Endpoints.Virtual;
using Microsoft.Windows.Devices.Midi2.Initialization;
using Microsoft.Windows.Devices.Midi2.Messages;
using System;
using System.Collections.Generic;
using System.Text;


namespace ReBuzz.Midi
{

    internal class Midi2
    {
        private IBuzz buzz;

        MidiDesktopAppSdkInitializer _initializer;
        MidiSession _session;
        MidiEndpointConnection _connection;
        MidiVirtualDevice _virtualDevice;
        MidiEndpointDeviceWatcher _watcher;

        List<MidiEndpointDeviceInformation> Endpoints = new();

        internal Midi2(IBuzz buzz)
        {
            this.buzz = buzz;
        }

        internal bool CreateMidi2Endpoint()
        {
            _initializer = MidiDesktopAppSdkInitializer.Create();

            if (!_initializer.InitializeSdkRuntime())
            {
                buzz.DCWriteLine("Failed to initialize SDK Runtime");
                return false;
            }

            // start the service
            if (!_initializer.EnsureServiceAvailable())
            {
                buzz.DCWriteLine("Failed to get service running");
                return false;
            }

            _watcher = MidiEndpointDeviceWatcher.Create();

            if (_watcher != null)
            {
                _watcher.Added += Watcher_Added;
                _watcher.Removed += Watcher_Removed;
                _watcher.Updated += Watcher_Updated;

                _watcher.Start();
            }

            StartVirtualDevice();

            return true;
        }

        private void Watcher_Updated(MidiEndpointDeviceWatcher sender, MidiEndpointDeviceInformationUpdatedEventArgs args)
        {
            if (!args.IsNameUpdated) return;

            var newEp = MidiEndpointDeviceInformation.CreateFromEndpointDeviceId(args.EndpointDeviceId);

            if (newEp != null)
            {
                foreach (var ep in Endpoints)
                {
                    if (ep.EndpointDeviceId == args.EndpointDeviceId)
                    {
                        Endpoints.Remove(ep);
                        Endpoints.Add(newEp);
                        return;
                    }
                }
            }
        }

        private void Watcher_Removed(MidiEndpointDeviceWatcher sender, MidiEndpointDeviceInformationRemovedEventArgs args)
        {
            foreach (var ep in Endpoints)
            {
                if (ep.EndpointDeviceId == args.EndpointDeviceId)
                {
                    Endpoints.Remove(ep);
                    return;
                }
            }
        }

        private void Watcher_Added(MidiEndpointDeviceWatcher sender, MidiEndpointDeviceInformationAddedEventArgs args)
        {
            var props = args.AddedDevice.Properties;

            Endpoints.Add(args.AddedDevice);
        }

        private bool StartVirtualDevice()
        {
            try
            {
                buzz.DCWriteLine("StartVirtualDevice Connection enter");

                // define our virtual device
                var creationConfig = DefineDevice();

                // create the session. The name here is just convenience.
                buzz.DCWriteLine("StartVirtualDevice Creating session");
                _session = MidiSession.Create(creationConfig.Name);

                // return if unable to create session
                if (_session == null)
                {
                    buzz.DCWriteLine("StartVirtualDevice Unable to create session", DCLogLevel.Error);
                    return false;
                }

                // create the virtual device, so we can get the endpoint device id to connect to
                buzz.DCWriteLine("StartVirtualDevice Creating virtual device");
                _virtualDevice = MidiVirtualDeviceManager.CreateVirtualDevice(creationConfig);


                // return if unable to create virtual device
                if (_virtualDevice == null)
                {
                    buzz.DCWriteLine("StartVirtualDevice Unable to create virtual device");
                    return false;
                }

                // this is for debugging in the sample. Normally you'd have this set to true
                // you want to set this before you open the "device" side connection or else you may
                // miss the initial discovery messages
                _virtualDevice.SuppressHandledMessages = false;


                // create our device-side connection
                buzz.DCWriteLine("StartVirtualDevice Creating endpoint connection");
                _connection = _session.CreateEndpointConnection(_virtualDevice.DeviceEndpointDeviceId);

                if (_connection == null)
                {
                    buzz.DCWriteLine("StartVirtualDevice failed to create connection", DCLogLevel.Error);
                    return false;
                }

                // necessary for the virtual device to participate in MIDI communication
                _connection.AddMessageProcessingPlugin(_virtualDevice);

                // wire up the stream configuration request received handler
                _virtualDevice.StreamConfigRequestReceived += OnStreamConfigurationRequestReceived;

                // wire up the message received handler on the connection itself
                _connection.MessageReceived += OnMidiMessageReceived;

                buzz.DCWriteLine("StartVirtualDevice Opening connection");
                if (_connection.Open())
                {
                    buzz.DCWriteLine("Connection Opened");

                    return true;
                }
                else
                {
                    buzz.DCWriteLine("Connection Open Failed", DCLogLevel.Error);

                    return false;
                }
            }
            catch (Exception ex)
            {
                buzz.DCWriteLine("Exception: " + ex.ToString(), DCLogLevel.Fatal);

                return false;
            }
        }

        private void OnMidiMessageReceived(IMidiMessageReceivedEventSource sender, MidiMessageReceivedEventArgs args)
        {
            var ump = args.GetMessagePacket();
#if DEBUG
            buzz.DCWriteLine("");
            buzz.DCWriteLine("Received UMP");
            buzz.DCWriteLine("- Current Timestamp: " + MidiClock.Now);
            buzz.DCWriteLine("- UMP Timestamp:     " + ump.Timestamp);
            buzz.DCWriteLine("- UMP Msg Type:      " + ump.MessageType);
            buzz.DCWriteLine("- UMP Packet Type:   " + ump.PacketType);
            buzz.DCWriteLine("- Message:           " + MidiMessageHelper.GetMessageDisplayNameFromFirstWord(args.PeekFirstWord()));
#endif
            if (ump is MidiMessage32)
            {
                var ump32 = ump as MidiMessage32;

                if (ump32 != null)
                {
#if DEBUG
                    buzz.DCWriteLine(string.Format("- Word 0:            0x{0:X}", ump32.Word0));
#endif
                    buzz.SendMIDIInput((int)ump32.Word0);
                }
            }
        }

        private void OnStreamConfigurationRequestReceived(MidiVirtualDevice sender, MidiStreamConfigRequestReceivedEventArgs args)
        {
            buzz.DCWriteLine("Stream configuration request received");
        }

        MidiVirtualDeviceCreationConfig DefineDevice()
        {
            // some of these values may seem redundant, but for physical devices
            // they are all sourced from different locations, and we want virtual
            // devices to behave like physical devices.

            string userSuppliedName = "ReBuzz";
            string userSuppliedDescription = "ReBuzz DAW";

            string transportSuppliedName = "ReBuzz Controller";
            string transportSuppliedDescription = "MIDI virtual device";
            string transportSuppliedManufacturerName = "ReBuzz";

            string endpointSuppliedName = transportSuppliedName;


            var declaredEndpointInfo = new MidiDeclaredEndpointInfo();
            declaredEndpointInfo.Name = endpointSuppliedName;
            declaredEndpointInfo.ProductInstanceId = "REBUZZ_DAW_007";
            declaredEndpointInfo.SpecificationVersionMajor = 1; // see latest MIDI 2 UMP spec
            declaredEndpointInfo.SpecificationVersionMinor = 1; // see latest MIDI 2 UMP spec
            declaredEndpointInfo.SupportsMidi10Protocol = false;
            declaredEndpointInfo.SupportsMidi20Protocol = true;
            declaredEndpointInfo.SupportsReceivingJitterReductionTimestamps = false;
            declaredEndpointInfo.SupportsSendingJitterReductionTimestamps = false;

            declaredEndpointInfo.HasStaticFunctionBlocks = false;   // this makes it possible for us to update them later

            // todo: set any device identity values if you want. This is optional
            // The SysEx id, if used, needs to be a valid one
            var declaredDeviceIdentity = new MidiDeclaredDeviceIdentity();
            declaredDeviceIdentity.DeviceFamilyMsb = 0x01;
            declaredDeviceIdentity.DeviceFamilyLsb = 0x02;
            declaredDeviceIdentity.DeviceFamilyModelNumberMsb = 0x03;
            declaredDeviceIdentity.DeviceFamilyModelNumberLsb = 0x04;
            declaredDeviceIdentity.SoftwareRevisionLevelByte1 = 0x05;
            declaredDeviceIdentity.SoftwareRevisionLevelByte2 = 0x06;
            declaredDeviceIdentity.SoftwareRevisionLevelByte3 = 0x07;
            declaredDeviceIdentity.SoftwareRevisionLevelByte4 = 0x08;
            declaredDeviceIdentity.SystemExclusiveIdByte1 = 0x09;
            declaredDeviceIdentity.SystemExclusiveIdByte2 = 0x0A;
            declaredDeviceIdentity.SystemExclusiveIdByte3 = 0x0B;


            var userSuppliedInfo = new MidiEndpointUserSuppliedInfo();
            userSuppliedInfo.Name = userSuppliedName;           // for names, this will bubble to the top in priority
            userSuppliedInfo.Description = userSuppliedDescription;


            var config = new MidiVirtualDeviceCreationConfig(
                transportSuppliedName,                          // this could be a different "transport-supplied" name value here
                transportSuppliedDescription,                   // transport-supplied description
                transportSuppliedManufacturerName,              // transport-supplied company name
                declaredEndpointInfo,                           // for endpoint discovery
                declaredDeviceIdentity,                         // for endpoint discovery
                userSuppliedInfo
            );

            // Function blocks. The MIDI 2 UMP specification covers the meanings
            // of these values

            // Note: the number of blocks needs to match the number declared for the endpoint
            // and function blocks must start at 0 and go up from there, without any gaps

            var block1 = new MidiFunctionBlock();
            block1.Number = 0;
            block1.Name = "Pads Output";
            block1.IsActive = true;
            block1.UIHint = MidiFunctionBlockUIHint.Bidirectional;
            block1.FirstGroup = new MidiGroup(0);
            block1.GroupCount = 1;
            block1.Direction = MidiFunctionBlockDirection.Bidirectional;
            block1.RepresentsMidi10Connection = MidiFunctionBlockRepresentsMidi10Connection.Not10;
            block1.MaxSystemExclusive8Streams = 0;
            block1.MidiCIMessageVersionFormat = 0;

            config.FunctionBlocks.Add(block1);

            /*
            var block2 = new MidiFunctionBlock();
            block2.Number = 1;
            block2.Name = "Second Function Block";
            block2.IsActive = true;
            block2.UIHint = MidiFunctionBlockUIHint.Sender;
            block2.FirstGroup = new MidiGroup(1);
            block2.GroupCount = 1;
            block2.Direction = MidiFunctionBlockDirection.BlockOutput;
            block2.RepresentsMidi10Connection = MidiFunctionBlockRepresentsMidi10Connection.Not10;
            block2.MaxSystemExclusive8Streams = 0;
            block2.MidiCIMessageVersionFormat = 0;
            
            config.FunctionBlocks.Add(block2);
            
            var block3 = new MidiFunctionBlock();
            block3.Number = 2;
            block3.Name = "Third Function Block";
            block3.IsActive = false;                // function blocks can be marked as inactive.
            block3.UIHint = MidiFunctionBlockUIHint.Receiver;
            block3.FirstGroup = new MidiGroup(5);
            block3.GroupCount = 1;
            block3.Direction = MidiFunctionBlockDirection.BlockInput;
            block3.RepresentsMidi10Connection = MidiFunctionBlockRepresentsMidi10Connection.Not10;
            block3.MaxSystemExclusive8Streams = 0;
            block3.MidiCIMessageVersionFormat = 0;

            config.FunctionBlocks.Add(block3);
            */
            return config;
        }

        internal void Release()
        {
            if (_watcher != null)
            {
                _watcher.Added -= Watcher_Added;
                _watcher.Removed -= Watcher_Removed;
                _watcher.Updated -= Watcher_Updated;

                _watcher.Stop();
            }

            _connection?.MessageReceived -= OnMidiMessageReceived;
            _virtualDevice?.StreamConfigRequestReceived -= OnStreamConfigurationRequestReceived;

            if (_connection != null)
            {
                _session.DisconnectEndpointConnection(_connection.ConnectionId);
            }

            _session?.Dispose();
            _initializer?.Dispose();
        }
    }
}
