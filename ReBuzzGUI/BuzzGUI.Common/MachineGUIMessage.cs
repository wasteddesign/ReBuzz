using BuzzGUI.Interfaces;
using System;
using System.IO;

namespace BuzzGUI.Common
{
    public class MachineGUIMessage
    {
        public Action<BinaryWriter> Send;
        public Action<BinaryReader> Receive;
        public Action NoResponse;

        public IMachine Machine
        {
            set
            {
                var request = new MemoryStream();
                var bw = new BinaryWriter(request);
                Send(bw);

                var response = value.SendGUIMessage(request.ToArray());
                if (response != null)
                {
                    var br = new BinaryReader(new MemoryStream(response));
                    Receive(br);
                }
                else
                {
                    if (NoResponse != null) NoResponse();
                }
            }
        }

    }
}
