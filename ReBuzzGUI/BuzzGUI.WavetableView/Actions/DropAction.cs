using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.FileBrowser;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace BuzzGUI.WavetableView.Actions
{
    internal class DropAction : BuzzAction
    {
        private readonly WavetableVM wt;
        public int SelectedWaveIndex { get; }

        private readonly IEnumerable<WavetableVM.LoadWaveRef> wavesToLoad;

        public DropAction(WavetableVM wt, int index, object x)
        {
            this.wt = wt;
            SelectedWaveIndex = wt.SelectedWaveIndex;

            var p = x as Tuple<DragEventArgs, UIElement>;
            var param = DragTargetBehavior.GetParameter(p.Item2);
            var e = p.Item1;
            if (e.Data.GetDataPresent(typeof(BuzzGUI.FileBrowser.FSItemVM)))
            {
                var fsi = p.Item1.Data.GetData(typeof(BuzzGUI.FileBrowser.FSItemVM)) as FSItemVM;
                wavesToLoad = wt.PrepareLoadWaves(index, new FSItemVM[] { fsi }, param != null && param == "Add");
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var filenames = (string[])e.Data.GetData(DataFormats.FileDrop);
                wavesToLoad = wt.PrepareLoadWaves(index, filenames.Where(fn => WavetableExtensions.CanLoadFile(fn)), param != null && param == "Add");
            }
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
