using BuzzGUI.Common.Actions;
using BuzzGUI.Common.InterfaceExtensions;
using System.Collections.Generic;

namespace BuzzGUI.WavetableView.Actions
{
    internal class LoadAction : BuzzAction
    {
        private readonly WavetableVM wt;

        public int SelectedWaveIndex { get; }

        private readonly IEnumerable<WavetableVM.LoadWaveRef> wavesToLoad;

        public LoadAction(WavetableVM wt, int index, IEnumerable<object> waves, bool add)
        {
            this.wt = wt;
            this.SelectedWaveIndex = wt.SelectedWaveIndex;
            this.wavesToLoad = wt.PrepareLoadWaves(index, waves, add);
        }

        protected override void DoAction()
        {
            foreach (var wave in wavesToLoad)
            {
                wt.Wavetable.LoadWaveEx(wave.Index, wave.FullPath, wave.Name, wave.Add);
            }
            wt.SelectedItem = wt.Waves[wt.SelectedWaveIndex];

            wt.UpdateFocus();
        }

        protected override void UndoAction()
        {
            foreach (var wave in wavesToLoad)
            {
                wt.Wavetable.LoadWave(wave.Index, null, null, wave.Add);
            }
            wt.SelectedItem = wt.Waves[wt.SelectedWaveIndex];
        }
    }
}
