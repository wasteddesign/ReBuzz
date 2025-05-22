using BuzzGUI.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace WDE.AudioBlock
{
    public class WaveData
    {
        public int ChannelCount { get; internal set; }
        public WaveFormat Format { get; internal set; }
        public int LoopEnd { get; internal set; }
        public int LoopStart { get; internal set; }
        public int RootNote { get; internal set; }
        public int SampleCount { get; internal set; }
        public int SampleRate { get; internal set; }
        public float[] SampleDataLeft { get; internal set; }
        public float[] SampleDataRight { get; internal set; }
        public int WaveIndex { get; internal set; }
        public string Name { get; internal set; }
        public float Volume { get; internal set; }
        public string Path { get; internal set; }
    }
    public class WaveUndo
    {
        private List<WaveData> undoData;
        private List<WaveData> redoData;
        private int maxSize = 1;
        private AudioBlock audioBlock;

        public bool Working { get; private set; }

        public WaveUndo(int maxSize, AudioBlock ab)
        {
            this.maxSize = maxSize;
            this.audioBlock = ab;
            undoData = new List<WaveData>();
            redoData = new List<WaveData>();
        }

        public void SaveData(int waveIndex)
        {
            if (undoData.Count >= maxSize)
            {
                undoData.RemoveAt(0); // Move oldest
            }

            IWavetable wt = audioBlock.host.Machine.Graph.Buzz.Song.Wavetable;
            if (wt.Waves[waveIndex] != null)
            {
                WaveData waveData = GetWaveData(waveIndex);
                undoData.Add(waveData);
            }
        }

        public WaveData GetWaveData(int waveIndex)
        {
            WaveData waveData = new WaveData();

            //lock (syncLock)
            {
                IWavetable wt = audioBlock.host.Machine.Graph.Buzz.Song.Wavetable;

                if (wt.Waves[waveIndex] != null && wt.Waves[waveIndex].Layers.Count > 0)
                {
                    IWaveLayer waveLayer = wt.Waves[waveIndex].Layers[0];

                    waveData.WaveIndex = waveIndex;
                    waveData.ChannelCount = waveLayer.ChannelCount;
                    waveData.Format = waveLayer.Format;
                    waveData.LoopEnd = waveLayer.LoopEnd;
                    waveData.LoopStart = waveLayer.LoopStart;
                    waveData.RootNote = waveLayer.RootNote;
                    waveData.SampleCount = waveLayer.SampleCount;
                    waveData.SampleRate = waveLayer.SampleRate;
                    waveData.Path = waveLayer.Path;
                    waveData.Name = wt.Waves[waveIndex].Name;
                    waveData.Volume = wt.Waves[waveIndex].Volume;

                    waveData.SampleDataLeft = new float[waveData.SampleCount];

                    waveLayer.GetDataAsFloat(waveData.SampleDataLeft, 0, 1, 0, 0, waveData.SampleCount);
                    if (waveData.ChannelCount == 2)
                    {
                        waveData.SampleDataRight = new float[waveData.SampleCount];
                        waveLayer.GetDataAsFloat(waveData.SampleDataRight, 0, 1, 1, 0, waveData.SampleCount);
                    }
                }
            }
            return waveData;
        }

        public void Undo()
        {
            //lock (syncLock)
            {
                if (undoData.Count == 0 || Working)
                    return;

                //bool playing = Global.Buzz.Playing;
                //Global.Buzz.Playing = false;

                Working = true;

                if (redoData.Count >= maxSize)
                {
                    redoData.RemoveAt(0); // Move last added
                }

                WaveData waveData = undoData.Last();
                undoData.Remove(waveData);

                redoData.Add(GetWaveData(waveData.WaveIndex));

                IWavetable wt = audioBlock.host.Machine.Graph.Buzz.Song.Wavetable;

                if (waveData.ChannelCount == 1)
                {
                    wt.AllocateWave(waveData.WaveIndex, waveData.Path, waveData.Name, waveData.SampleCount, WaveFormat.Float32, false, waveData.RootNote, false, true);
                    var targetLayer = wt.Waves[waveData.WaveIndex].Layers.Last();

                    targetLayer.LoopStart = waveData.LoopStart;
                    targetLayer.LoopEnd = waveData.LoopEnd;
                    targetLayer.SampleRate = waveData.SampleRate;
                    wt.Waves[waveData.WaveIndex].Volume = waveData.Volume;

                    targetLayer.SetDataAsFloat(waveData.SampleDataLeft, 0, 1, 0, 0, waveData.SampleCount); // Mono          
                    targetLayer.InvalidateData();
                }
                else
                {
                    wt.AllocateWave(waveData.WaveIndex, waveData.Path, waveData.Name, waveData.SampleCount, WaveFormat.Float32, true, waveData.RootNote, false, true);
                    var targetLayer = wt.Waves[waveData.WaveIndex].Layers.Last();

                    targetLayer.LoopStart = waveData.LoopStart;
                    targetLayer.LoopEnd = waveData.LoopEnd;
                    targetLayer.SampleRate = waveData.SampleRate;
                    wt.Waves[waveData.WaveIndex].Volume = waveData.Volume;

                    targetLayer.SetDataAsFloat(waveData.SampleDataLeft, 0, 1, 0, 0, waveData.SampleCount); // Left              
                    targetLayer.SetDataAsFloat(waveData.SampleDataRight, 0, 1, 1, 0, waveData.SampleCount); // Right    
                    targetLayer.InvalidateData();
                }

                audioBlock.ReCreateEnvPoints();

                //Global.Buzz.Playing = playing;
                Working = false;
            }
        }

        public void Redo()
        {
            //lock (AudioBlock.syncLock)
            {
                if (redoData.Count == 0 || Working)
                    return;

                //bool playing = Global.Buzz.Playing;
                //Global.Buzz.Playing = false;

                Working = true;

                if (undoData.Count >= maxSize)
                {
                    undoData.RemoveAt(0); // Move last added
                }

                WaveData waveData = redoData.Last();
                redoData.Remove(waveData);

                undoData.Add(GetWaveData(waveData.WaveIndex));

                IWavetable wt = audioBlock.host.Machine.Graph.Buzz.Song.Wavetable;

                if (waveData.ChannelCount == 1)
                {
                    wt.AllocateWave(waveData.WaveIndex, waveData.Path, waveData.Name, waveData.SampleCount, WaveFormat.Float32, false, waveData.RootNote, false, true);
                    var targetLayer = wt.Waves[waveData.WaveIndex].Layers.Last();

                    targetLayer.LoopStart = waveData.LoopStart;
                    targetLayer.LoopEnd = waveData.LoopEnd;
                    targetLayer.SampleRate = waveData.SampleRate;
                    wt.Waves[waveData.WaveIndex].Volume = waveData.Volume;

                    targetLayer.SetDataAsFloat(waveData.SampleDataLeft, 0, 1, 0, 0, waveData.SampleCount); // Mono          
                    targetLayer.InvalidateData();
                }
                else
                {
                    wt.AllocateWave(waveData.WaveIndex, waveData.Path, waveData.Name, waveData.SampleCount, WaveFormat.Float32, true, waveData.RootNote, false, true);
                    var targetLayer = wt.Waves[waveData.WaveIndex].Layers.Last();

                    targetLayer.LoopStart = waveData.LoopStart;
                    targetLayer.LoopEnd = waveData.LoopEnd;
                    targetLayer.SampleRate = waveData.SampleRate;
                    wt.Waves[waveData.WaveIndex].Volume = waveData.Volume;

                    targetLayer.SetDataAsFloat(waveData.SampleDataLeft, 0, 1, 0, 0, waveData.SampleCount); // Left              
                    targetLayer.SetDataAsFloat(waveData.SampleDataRight, 0, 1, 1, 0, waveData.SampleCount); // Right      
                    targetLayer.InvalidateData();
                }
                
                audioBlock.ReCreateEnvPoints();

                //Global.Buzz.Playing = playing;
                Working = false;
            }
        }
    }
}
