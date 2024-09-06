using BuzzGUI.Common.Actions;
using BuzzGUI.Interfaces;
using System.Linq;

namespace ReBuzz.Core.Actions.GraphActions
{
    internal class ConnectMachinesAction : BuzzAction
    {
        private readonly string src;
        private readonly string dst;
        private readonly int srcchn;
        private readonly int dstchn;
        private readonly int amp;
        private readonly int pan;
        private readonly ReBuzzCore buzz;

        public ConnectMachinesAction(ReBuzzCore buzz, IMachine src, IMachine dst, int srcchn, int dstchn, int amp, int pan)
        {
            this.src = src.Name;
            this.dst = dst.Name;
            this.srcchn = srcchn;
            this.dstchn = dstchn;
            this.amp = amp;
            this.pan = pan;
            this.buzz = buzz;
        }

        public ConnectMachinesAction(ReBuzzCore buzz, MachineConnectionCore mc)
        {
            this.src = mc.Source.Name;
            this.dst = mc.Destination.Name;
            this.srcchn = mc.SourceChannel;
            this.dstchn = mc.DestinationChannel;
            this.amp = mc.Amp;
            this.pan = mc.Pan;
            this.buzz = buzz;
        }

        protected override void DoAction()
        {
            lock (ReBuzzCore.AudioLock)
            {
                var mcsrc = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == src);
                var mcdst = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == dst);
                MachineConnectionCore mcc = new MachineConnectionCore() { Source = mcsrc, Destination = mcdst, SourceChannel = srcchn, DestinationChannel = dstchn, Amp = amp, Pan = pan, HasPan = mcdst.HasStereoInput };

                mcsrc.AddOutput(mcc);
                mcdst.AddInput(mcc);

                // Don't draw connections for hidden or control machines
                if (!mcsrc.Hidden && !mcdst.Hidden && !mcsrc.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE))
                {
                    ParameterGroup.AddInputs(mcc, mcdst.ParameterGroupsList[0]);
                    buzz.SongCore.InvokeConnectionAdded(mcc);
                }

            }
        }

        protected override void UndoAction()
        {
            lock (ReBuzzCore.AudioLock)
            {
                var mcsrc = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == src);
                var mcdst = buzz.SongCore.MachinesList.FirstOrDefault(m => m.Name == dst);
                var connection = mcsrc.Outputs.FirstOrDefault(c => c.Destination.Name == dst) as MachineConnectionCore;
                if (connection != null)
                {
                    mcsrc.RemoveOutput(connection);
                    mcdst.RemoveInput(connection);
                    if (!mcsrc.Hidden && !mcdst.Hidden)
                    {
                        buzz.SongCore.InvokeConnectionRemoved(connection);
                    }
                }
            }
        }
    }
}
