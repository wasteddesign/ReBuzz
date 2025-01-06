using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Interfaces;
using BuzzGUI.WaveformControl.Commands;

namespace BuzzGUI.WaveformControl
{
    public class WaveCommandHelpers
    {
        private WaveCommandHelpers(){}

        private static void CopyMetaData(IWaveformBase sourceLayer, IWaveformBase targetLayer)
        {
            targetLayer.SampleRate = sourceLayer.SampleRate;
            targetLayer.LoopStart = sourceLayer.LoopStart;
            targetLayer.LoopEnd = sourceLayer.LoopEnd;

            if (targetLayer.LoopStart < 0)
            {
                targetLayer.LoopStart = 0;
            }
            else if (targetLayer.LoopStart > targetLayer.SampleCount)
            {
                targetLayer.LoopStart = 0;                
            }

            if (targetLayer.LoopEnd < 0)
            {
                targetLayer.LoopEnd = targetLayer.SampleCount;
            }
            else if (targetLayer.LoopEnd > targetLayer.SampleCount)
            {
                targetLayer.LoopEnd = targetLayer.SampleCount;
            }
        }

        private static void CopyAudioDataMono(float[] left, IWaveformBase targetLayer, int StartSample, int EndSample)
        {
            var SampleCount = EndSample - StartSample;
            targetLayer.SetDataAsFloat(left, StartSample, 1, 0, 0, SampleCount);
        }
        private static void CopyAudioDataStereo(float[] left, float[] right, IWaveformBase targetLayer, int StartSample, int EndSample)
        {
            var SampleCount = EndSample - StartSample;
            targetLayer.SetDataAsFloat(left, StartSample, 1, 0, 0, SampleCount);
            targetLayer.SetDataAsFloat(right, StartSample, 1, 1, 0, SampleCount);
        }

        private static void CopyAudioData(BackupWaveLayer sourceLayer, IWaveformBase targetLayer)
        {
            if (sourceLayer.ChannelCount == 1)
            {
                CopyAudioDataMono(sourceLayer.Left, targetLayer, 0, targetLayer.SampleCount);
                targetLayer.InvalidateData();
            }
            else if (sourceLayer.ChannelCount == 2)
            {
                CopyAudioDataStereo(sourceLayer.Left, sourceLayer.Right, targetLayer, 0, targetLayer.SampleCount);
                targetLayer.InvalidateData();
            }
        }

        private static void CopyAudioData(IWaveformBase sourceLayer, IWaveformBase targetLayer)
        {
            CopyAudioData(sourceLayer, targetLayer, 0, sourceLayer.SampleCount);
        }

        private static void CopyAudioData(IWaveformBase sourceLayer, IWaveformBase targetLayer, int StartSample, int EndSample)
        {
            if (sourceLayer.ChannelCount == 1)
            {
                float[] left = new float[sourceLayer.SampleCount];
                sourceLayer.GetDataAsFloat(left, 0, 1, 0, 0, sourceLayer.SampleCount);
                CopyAudioDataMono(left, targetLayer, StartSample, EndSample);
            }
            else if (sourceLayer.ChannelCount == 2)
            {
                float[] left = new float[sourceLayer.SampleCount];
                float[] right = new float[sourceLayer.SampleCount];

                sourceLayer.GetDataAsFloat(left, 0, 1, 0, 0, sourceLayer.SampleCount);
                sourceLayer.GetDataAsFloat(right, 0, 1, 1, 0, sourceLayer.SampleCount);
                CopyAudioDataStereo(left, right, targetLayer, StartSample, EndSample);            
            }
        }

        public static void ClearWaveSlot(IWavetable wavetable, int sourceSlotIndex)
        {
            //Deletes all layers in the slot
            wavetable.LoadWave(sourceSlotIndex, null, null, false);
        }

        public static void CopyWaveSlotToWaveSlot(IWavetable wavetable, int sourceSlotIndex, int targetSlotIndex)
        {
            //Copy all layers into a new slot
            if (sourceSlotIndex != targetSlotIndex)
            {
                IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

                bool add = false; //first layer allocates the whole slot
                foreach (IWaveLayer sourceLayer in sourceSlot.Layers)
                {
                    wavetable.AllocateWave(targetSlotIndex, "", sourceSlot.Name + "_copy", sourceLayer.SampleCount, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                    IWave targetSlot = wavetable.Waves[targetSlotIndex]; //contains the slot we just allocated with AllocateWave
                    IWaveLayer targetLayer = targetSlot.Layers.Last(); //contains the layer we just allocated with AllocateWave

                    CopyMetaData(sourceLayer, targetLayer);
                    CopyAudioData(sourceLayer, targetLayer);
                    targetLayer.InvalidateData();

                    add = true; //all subsequent layers are added to this slot
                }
            }
        }

        public static void CopySelectionToNewWaveSlot(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int targetSlotIndex, int targetLayerIndex, int StartSample, int EndSample, string name = "copy")
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];
            IWaveLayer sourceLayer = sourceSlot.Layers[sourceLayerIndex];

            if (targetLayerIndex == 0)
            {
                wavetable.AllocateWave(targetSlotIndex, "", name, EndSample - StartSample, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, false, false);
                IWave targetSlot = wavetable.Waves[targetSlotIndex]; //contains the slot we just allocated with AllocateWave           
                IWaveLayer targetLayer = targetSlot.Layers.Last(); //contains the layer we just allocated with AllocateWave

                CopyMetaData(sourceLayer, targetLayer);
                CopyAudioData(sourceLayer, targetLayer, StartSample, EndSample);
                targetLayer.InvalidateData();
            }
            else
            {
                //TODO Note: there is currently no way to copy to a specific layer in a slot
                //to do that we probably have to clear the whole slot and rebuild it (use backuplayers to do it?)
                //even if we do that, it will not be guaranteed that the targetLayerIndex will match
                //we could also just append here...
            }       
        }

        public static void DeleteSelectionFromLayer(IWavetable wavetable, List<BackupWaveLayer> backupLayers, int sourceSlotIndex, int StartSample, int EndSample)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            if (StartSample != EndSample)
            {
                bool add = false; //first layer allocates the whole slot
                foreach (BackupWaveLayer sourceLayer in backupLayers)
                {

                    if (sourceLayer.IsSelected) //only delete from the selected layer
                    {
                        wavetable.AllocateWave(sourceSlotIndex, "", sourceSlot.Name, sourceLayer.Left.Length - (EndSample - StartSample), sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                        var targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

                        if (sourceLayer.ChannelCount == 1)
                        {
                            CopyMetaData(sourceLayer, targetLayer);
                            
                            //copy parts before and after selection to get rid of selected part
                            targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Left, EndSample, 1, 0, StartSample, sourceLayer.Left.Length - EndSample);
                            targetLayer.InvalidateData();
                        }
                        else if (sourceLayer.ChannelCount == 2)
                        {
                            CopyMetaData(sourceLayer, targetLayer);

                            //copy parts before and after selection to get rid of selected part
                            targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Left, EndSample, 1, 0, StartSample, sourceLayer.Left.Length - EndSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Right, 0, 1, 1, 0, StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Right, EndSample, 1, 1, StartSample, sourceLayer.Right.Length - EndSample);
                            targetLayer.InvalidateData();
                        }
                    }
                    else //if this is not the selected layer we still need to copy all the data (unaltered)
                    {
                        wavetable.AllocateWave(sourceSlotIndex, "", sourceSlot.Name, sourceLayer.SampleCount, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                        var targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

                        CopyMetaData(sourceLayer, targetLayer);
                        CopyAudioData(sourceLayer, targetLayer);
                    }

                    add = true; //all subsequent layers are added to this slot
                }

            }
        }

        public static void TrimSelectionFromLayer(IWavetable wavetable, List<BackupWaveLayer> backupLayers, int sourceSlotIndex, int StartSample, int EndSample)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            if (StartSample != EndSample)
            {
                bool add = false; //first layer allocates the whole slot
                foreach (BackupWaveLayer sourceLayer in backupLayers)
                {
                    if (sourceLayer.IsSelected) //only delete from the selected layer
                    {
                        wavetable.AllocateWave(sourceSlotIndex, "", sourceSlot.Name, EndSample - StartSample, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                        var targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

                        if (sourceLayer.ChannelCount == 1)
                        {
                            CopyMetaData(sourceLayer, targetLayer);

                            //copy selection and get rid of the rest
                            targetLayer.SetDataAsFloat(sourceLayer.Left, StartSample, 1, 0, 0, EndSample - StartSample);
                            targetLayer.InvalidateData();
                        }
                        else if (sourceLayer.ChannelCount == 2)
                        {
                            CopyMetaData(sourceLayer, targetLayer);

                            //copy selection and get rid of the rest
                            targetLayer.SetDataAsFloat(sourceLayer.Left, StartSample, 1, 0, 0, EndSample - StartSample);
                            targetLayer.SetDataAsFloat(sourceLayer.Right, StartSample, 1, 1, 0, EndSample - StartSample);
                            targetLayer.InvalidateData();
                        }
                    }
                    else //if this is not the selected layer we still need to copy all the data (unaltered)
                    {
                        wavetable.AllocateWave(sourceSlotIndex, "", sourceSlot.Name, sourceLayer.SampleCount, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                        var targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

                        CopyMetaData(sourceLayer, targetLayer);
                        CopyAudioData(sourceLayer, targetLayer);
                    }

                    add = true; //all subsequent layers are added to this slot
                }

            }
        }

    }
}
