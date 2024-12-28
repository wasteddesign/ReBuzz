using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using System.Collections.Generic;

namespace BuzzGUI.WavetableView.Actions
{
    internal class ClearAction : BuzzAction
    {
        private readonly WavetableVM wt;
        private readonly int index;

        private readonly List<TemporaryWave> layers;

        public ClearAction(WavetableVM wt, int index)
        {
            this.wt = wt;
            this.index = index;
            this.layers = WaveCommandHelpers.BackupLayersInSlot(wt.Wavetable.Waves[index].Layers);
        }

        protected override void DoAction()
        {
            wt.Wavetable.LoadWave(index, null, null, false);
        }

        protected override void UndoAction()
        {
            WaveCommandHelpers.ReplaceSlot(wt.Wavetable, layers, index);
        }
    }
}
