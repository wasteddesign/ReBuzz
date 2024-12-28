using BuzzGUI.Common;
using BuzzGUI.Common.Actions;
using System.Collections.Generic;
using System.Windows;

namespace BuzzGUI.WavetableView.Actions
{
    internal class PasteAction : BuzzAction
    {
        private readonly WavetableVM wt;

        public int SelectedWaveIndex { get; }
        readonly int sourceLayerIndex = 0;
        readonly int targetIndex;

        private readonly IEnumerable<WavetableVM.LoadWaveRef> wavesToLoad;
        private readonly List<TemporaryWave> layersInClipboard;
        private readonly List<TemporaryWave> layersToBackup;

        public PasteAction(WavetableVM wt, int index)
        {
            this.targetIndex = index;
            this.wt = wt;
            this.SelectedWaveIndex = wt.SelectedWaveIndex;
            if (wt.Wavetable.Waves[targetIndex] != null)
            {
                layersToBackup = WaveCommandHelpers.BackupLayersInSlot(wt.Wavetable.Waves[targetIndex].Layers);
            }

            // if we have a WaveSlot in our clipboard
            if (Clipboard.ContainsData("BuzzWaveSlot"))
            {
                WaveCommandHelpers.BuzzWaveSlot ws = Clipboard.GetData("BuzzWaveSlot") as WaveCommandHelpers.BuzzWaveSlot;
                layersInClipboard = ws.Layers;

                if (wt.Waves[ws.SourceSlotIndex].SelectedLayer != null)
                {
                    sourceLayerIndex = WaveCommandHelpers.GetLayerIndex(wt.Waves[ws.SourceSlotIndex].SelectedLayer.Layer); //must save this
                }
            }
            // if we have a TemporaryWave in our clipboard, replace the whole slot with this one wave
            else if (Clipboard.ContainsData("BuzzTemporaryWave"))
            {
                List<TemporaryWave> layersInClipboard = new List<TemporaryWave>();
                layersInClipboard.Add(Clipboard.GetData("BuzzTemporaryWave") as TemporaryWave);
            }
            // if contains audio from windows clipboard
            else if (Clipboard.ContainsAudio())
            {
                // get audio stream 
                var ms = Clipboard.GetAudioStream();

                var tw = new TemporaryWave(ms);

                if (tw != null)
                {
                    List<TemporaryWave> layersInClipboard = new List<TemporaryWave>();
                    layersInClipboard.Add(tw);
                }
            }
        }

        protected override void DoAction()
        {
            WaveCommandHelpers.ReplaceSlot(wt.Wavetable, layersInClipboard, targetIndex);
            wt.SelectedItem = wt.Waves[targetIndex]; //need to set this again otherwise there's an exception when editing in the wave editor
            wt.SelectedItem.SelectedLayer = wt.Waves[targetIndex].Layers[sourceLayerIndex]; //switch to the same layer that was selected in the original
        }

        protected override void UndoAction()
        {
            if (layersToBackup != null)
            {
                WaveCommandHelpers.ReplaceSlot(wt.Wavetable, layersToBackup, targetIndex);
            }
            else
            {
                wt.Wavetable.LoadWave(targetIndex, null, null, false);
            }
        }
    }
}
