using BuzzGUI.Interfaces;
using libsndfile;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;


namespace BuzzGUI.Common
{
    public class WaveCommandHelpers
    {
        private WaveCommandHelpers() { }

        public static List<TemporaryWave> BackupLayersInSlot(IEnumerable<IWaveLayer> waves)
        {
            var newLayers = new List<TemporaryWave>();
            foreach (var layer in waves)
            {
                var l = new TemporaryWave(layer);
                newLayers.Add(l);
            }
            return newLayers;
        }

        private static void RestoreLayerFromBackup(IWavetable wavetable, IWave sourceSlot, TemporaryWave sourceLayer, int targetSlotIndex, bool add, bool ToFloat, bool ToStereo)
        {
            WaveFormat wf = sourceLayer.Format;
            if (ToFloat == true)
            {
                wf = WaveFormat.Float32;
            }

            int ChannelCount = sourceLayer.ChannelCount;
            if (ToStereo == true)
            {
                ChannelCount = 2; //target layer will have 2 channels and left will be copied to right automatically in CopyAudioData()
            }

            wavetable.AllocateWave(targetSlotIndex, sourceLayer.Path, sourceLayer.Name, sourceLayer.SampleCount, wf, ChannelCount == 2, sourceLayer.RootNote, add, false);
            var targetLayer = wavetable.Waves[targetSlotIndex].Layers.Last();

            CopyMetaData(sourceLayer, targetLayer);
            CopyAudioData(sourceLayer, targetLayer);
            targetLayer.InvalidateData();
        }

        private static void RestoreLayerFromBackup(IWavetable wavetable, IWave sourceSlot, TemporaryWave sourceLayer, int targetSlotIndex, bool add)
        {
            //convenience method to call if sourceSlot != targetSlot
            RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, targetSlotIndex, add, false, false);
        }

        private static void RestoreLayerFromBackup(IWavetable wavetable, IWave sourceSlot, TemporaryWave sourceLayer, bool add, bool ToFloat, bool ToStereo)
        {
            //convenience method to call if sourceSlot == targetSlot
            RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, sourceSlot.Index, add, ToFloat, ToStereo);
        }

        private static void RestoreLayerFromBackup(IWavetable wavetable, IWave sourceSlot, TemporaryWave sourceLayer, bool add)
        {
            //convenience method to call if sourceSlot == targetSlot and no conversions are needed
            RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, sourceSlot.Index, add, false, false);
        }

        private static void CopyMetaData(TemporaryWave sourceLayer, IWaveformBase targetLayer)
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

        private static void CopyAudioData(TemporaryWave sourceLayer, IWaveformBase targetLayer)
        {
            if (sourceLayer.ChannelCount == 1 && targetLayer.ChannelCount == 1)
            {
                CopyAudioDataMono(sourceLayer.Left, targetLayer, 0, targetLayer.SampleCount);
            }
            else if (sourceLayer.ChannelCount == 2 && targetLayer.ChannelCount == 2)
            {
                CopyAudioDataStereo(sourceLayer.Left, sourceLayer.Right, targetLayer, 0, targetLayer.SampleCount);
            }
            else if (sourceLayer.ChannelCount == 1 && targetLayer.ChannelCount == 2) //convert mono to stereo
            {
                CopyAudioDataStereo(sourceLayer.Left, sourceLayer.Left, targetLayer, 0, targetLayer.SampleCount);
            }
        }

        private static void CopyAudioData(TemporaryWave sourceLayer, IWaveformBase targetLayer, int StartSample, int EndSample)
        {
            if (sourceLayer.ChannelCount == 1)
            {
                CopyAudioDataMono(sourceLayer.Left, targetLayer, StartSample, EndSample);
            }
            else if (sourceLayer.ChannelCount == 2)
            {
                CopyAudioDataStereo(sourceLayer.Left, sourceLayer.Right, targetLayer, StartSample, EndSample);
            }
        }

        public static int GetLayerIndex(IWaveformBase layer)
        {
            //TODO refactor this, should be possible without reflection ?
            var f = layer.GetType().GetField("layerIndex");
            if (f != null) return (int)f.GetValue(layer);
            return -1;
        }

        public static void ClearSlot(IWavetable wavetable, int sourceSlotIndex)
        {
            //Deletes all layers in the slot
            wavetable.LoadWave(sourceSlotIndex, null, null, false);
        }

        public static void ReplaceSlot(IWavetable wavetable, List<TemporaryWave> backupLayers, int targetSlotIndex)
        {
            IWave sourceSlot = wavetable.Waves[targetSlotIndex];

            //Copy all layers into a new slot
            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, targetSlotIndex, add); //todo sourceslot parameter needed ?
                add = true; //all subsequent layers are added to this slot
            }
        }

        public static void CopySelectionToNewSlot(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int targetSlotIndex, int targetLayerIndex, int StartSample, int EndSample, string name = "copy")
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];
            TemporaryWave sourceLayer = new TemporaryWave(sourceSlot.Layers[sourceLayerIndex]);

            string LayerName;
            if (String.IsNullOrEmpty(name))
            {
                LayerName = sourceLayer.Name + "_part";
            }
            else
            {
                LayerName = name;
            }

            if (targetLayerIndex == 0)
            {
                wavetable.AllocateWave(targetSlotIndex, sourceLayer.Path, LayerName, EndSample - StartSample, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, false, false);
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

        public static void SaveToFile(IWaveLayer wave, string name = "temp")
        {
            int WriteBufferSize = 4096;

            // throw exception instead?
            if (wave == null) return;

            // Configure save file dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = name; // Default file name
            dlg.DefaultExt = ".wav"; // Default file extension

            dlg.Filter = "Wave files|*.wav|Apple/SGI AIFF|*.aif|Sun/NeXT AU format|*.au|RAW PCM|*.raw|FLAC lossless|*.flac|Ogg Vorbis|*.ogg"; // Filter files by extension

            // Show save file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process save file dialog box results
            if (result == true)
            {
                // Save document

                // setup streams and format
                FileStream outf = System.IO.File.Create(dlg.FileName); //System.IO.File.Create(currentdir + filename);
                libsndfile.Format fmt;

                // get bit depth
                if (wave.Format == WaveFormat.Float32) fmt = Format.SF_FORMAT_FLOAT;
                else if (wave.Format == WaveFormat.Int32) fmt = Format.SF_FORMAT_PCM_32;
                else if (wave.Format == WaveFormat.Int24) fmt = Format.SF_FORMAT_PCM_24;
                else fmt = Format.SF_FORMAT_PCM_16;

                // use filterindex to figure out format (and open a dialog for options?)
                switch (dlg.FilterIndex)
                {
                    case (1):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_WAV;
                            break;
                        }

                    case (2):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_AIFF;
                            break;
                        }

                    case (3):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_AU;
                            break;
                        }

                    case (4):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_RAW;
                            break;
                        }

                    case (5):
                        {
                            fmt = fmt | libsndfile.Format.SF_FORMAT_FLAC;
                            break;
                        }

                    case (6):
                        {
                            fmt = libsndfile.Format.SF_FORMAT_VORBIS | libsndfile.Format.SF_FORMAT_OGG;
                            break;
                        }

                    default:
                        {
                            fmt = libsndfile.Format.SF_FORMAT_FLOAT | libsndfile.Format.SF_FORMAT_WAV;
                            break;
                        }
                }

                // declare file
                var outFile = SoundFile.Create(outf, wave.SampleRate, wave.ChannelCount, fmt);

                // code modified from WaveformBaseExtention
                var buffer = new float[WriteBufferSize * wave.ChannelCount];

                long frameswritten = 0;
                while (frameswritten < wave.SampleCount)
                {
                    var n = Math.Min(wave.SampleCount - frameswritten, WriteBufferSize);

                    for (int ch = 0; ch < wave.ChannelCount; ch++)
                        wave.GetDataAsFloat(buffer, ch, wave.ChannelCount, ch, (int)frameswritten, (int)n);

                    outFile.WriteFloat(buffer, 0, n);

                    frameswritten += n;
                }
                // close
                outFile.Close();
                outf.Close();
            }

        }

        public static bool IsSlotConversionNeeded(IWavetable wavetable, int sourceSlotIndex, ref WaveFormat wf, ref int ChannelCount, ref bool ConvertSlotToFloat, ref bool ConvertSlotToStereo)
        {
            if (wavetable.Waves[sourceSlotIndex] != null)
            {
                if (wavetable.Waves[sourceSlotIndex].Layers != null)
                {
                    if (wavetable.Waves[sourceSlotIndex].Layers.Count >= 1)
                    {
                        bool AllLayersAreFloat = true; //assume they are, but check if they are not
                        bool AllLayersAreStereo = true; //assume they are, but check if they are not

                        foreach (var l in wavetable.Waves[sourceSlotIndex].Layers)
                        {
                            if (l.Format != WaveFormat.Float32)
                            {
                                AllLayersAreFloat = false;
                            }
                            if (l.Format != wf && l.Format != WaveFormat.Float32)
                            {
                                ConvertSlotToFloat = true; //we need to convert if the formats don't match up and its not already 32 bit float
                            }

                            if (l.ChannelCount == 1)
                            {
                                AllLayersAreStereo = false;
                            }
                            if (l.ChannelCount != ChannelCount && l.ChannelCount != 2)
                            {
                                ConvertSlotToStereo = true; //we need to convert if the channels don't match up and its not already stereo
                            }
                        }

                        //we also need to make sure the new layer matches the format of all the old layers
                        if (ConvertSlotToFloat == true || AllLayersAreFloat == true)
                        {
                            BuzzGUI.Common.Global.Buzz.DCWriteLine("MAKE NEW LAYER FLOAT");
                            wf = WaveFormat.Float32; //also treat the new layer as 32bit float
                        }

                        if (ConvertSlotToStereo == true || AllLayersAreStereo == true)
                        {
                            BuzzGUI.Common.Global.Buzz.DCWriteLine("MAKE NEW LAYER STEREO");
                            ChannelCount = 2;
                        }

                        if (ConvertSlotToFloat == true || ConvertSlotToStereo == true)
                        {
                            BuzzGUI.Common.Global.Buzz.DCWriteLine("SLOT CONVERSION IS NEEDED");
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /*NOTE: FUNCTIONS BELOW HERE ARE DESTRUCTIVE AND NEED TO REBUILD THE WHOLE SLOT*/

        public static void ConvertSlot(IWavetable wavetable, int sourceSlotIndex, bool ToFloat, bool ToStereo)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add, ToFloat, ToStereo);
                add = true;
            }
        }

        public static void ConvertSlotIfNeeded(IWavetable wavetable, int sourceSlotIndex, ref WaveFormat wf, ref int ChannelCount)
        {
            //slot conversion is only neccessary if there is at least one layer already
            if (wavetable.Waves[sourceSlotIndex] != null)
            {
                if (wavetable.Waves[sourceSlotIndex].Layers != null)
                {
                    if (wavetable.Waves[sourceSlotIndex].Layers.Count >= 1)
                    {
                        bool AllLayersAreFloat = true; //assume they are, but check if they are not
                        bool AllLayersAreStereo = true; //assume they are, but check if they are not
                        bool ConvertSlotToFloat = false;
                        bool ConvertSlotToStereo = false;

                        foreach (var l in wavetable.Waves[sourceSlotIndex].Layers)
                        {
                            if (l.Format != WaveFormat.Float32)
                            {
                                AllLayersAreFloat = false;
                            }
                            if (l.Format != wf && l.Format != WaveFormat.Float32)
                            {
                                ConvertSlotToFloat = true; //we need to convert if the formats don't match up and its not already 32 bit float
                            }

                            if (l.ChannelCount == 1)
                            {
                                AllLayersAreStereo = false;
                            }
                            if (l.ChannelCount != ChannelCount && l.ChannelCount != 2)
                            {
                                ConvertSlotToStereo = true; //we need to convert if the channels don't match up and its not already stereo
                            }
                        }

                        if (ConvertSlotToFloat == true || ConvertSlotToStereo == true)
                        {
                            BuzzGUI.Common.Global.Buzz.DCWriteLine("CONVERTING SLOT");
                            WaveCommandHelpers.ConvertSlot(wavetable, sourceSlotIndex, ConvertSlotToFloat, ConvertSlotToStereo); //convert whole slot to 32bit float and/or stereo                        
                        }

                        //we also need to make sure the new layer matches the format of all the old layers
                        if (ConvertSlotToFloat == true || AllLayersAreFloat == true)
                        {
                            BuzzGUI.Common.Global.Buzz.DCWriteLine("MAKE NEW LAYER FLOAT");
                            wf = WaveFormat.Float32; //also treat the new layer as 32bit float
                        }

                        if (ConvertSlotToStereo == true || AllLayersAreStereo == true)
                        {
                            BuzzGUI.Common.Global.Buzz.DCWriteLine("MAKE NEW LAYER STEREO");
                            ChannelCount = 2;
                        }
                    }
                }
            }
        }


        public static void ClearLayer(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex)
        {
            //TODO its possible to end up with an unnamed slot

            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            //clear the whole slot, we're going to rebuild it without the layer that should get cleared
            WaveCommandHelpers.ClearSlot(wavetable, sourceSlotIndex);

            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                if (sourceLayer.Index == sourceLayerIndex) //only delete from the selected layer
                {
                    //do no restore the selected layer so it gets dropped
                }
                else
                {
                    RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add);
                }
                add = true;
            }
        }

        public static void DeleteSelectionFromLayer(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int StartSample, int EndSample)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained so we can operate on the selected layer
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            if (StartSample != EndSample)
            {
                bool add = false; //first layer allocates the whole slot
                foreach (TemporaryWave sourceLayer in backupLayers)
                {
                    if (sourceLayer.Index == sourceLayerIndex) //only delete from the selected layer
                    {
                        wavetable.AllocateWave(sourceSlotIndex, sourceLayer.Path, sourceLayer.Name, sourceLayer.Left.Length - (EndSample - StartSample), sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
                        IWaveLayer targetLayer = wavetable.Waves[sourceSlotIndex].Layers.Last();

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
                        RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add);
                    }

                    add = true; //all subsequent layers are added to this slot
                }
            }
        }

        public static void TrimSelectionFromLayer(IWavetable wavetable, int sourceSlotIndex, int sourceLayerIndex, int StartSample, int EndSample)
        {
            IWave sourceSlot = wavetable.Waves[sourceSlotIndex];

            //we need to backup the whole slot with all layers contained so we can operate on the selected layer
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            if (StartSample != EndSample)
            {
                bool add = false; //first layer allocates the whole slot
                foreach (TemporaryWave sourceLayer in backupLayers)
                {
                    if (sourceLayer.Index == sourceLayerIndex) //only trim the selected layer
                    {
                        wavetable.AllocateWave(sourceSlotIndex, sourceLayer.Path, sourceLayer.Name, EndSample - StartSample, sourceLayer.Format, sourceLayer.ChannelCount == 2, sourceLayer.RootNote, add, false);
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
                        RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add);
                    }

                    add = true; //all subsequent layers are added to this slot
                }
            }
        }

        public static void AddSelectionToLayer(IWavetable wavetable, int targetSlotIndex, int targetLayerIndex, int SamplePosition, TemporaryWave inputLayer)
        {
            // get right destination layer
            IWave sourceSlot = wavetable.Waves[targetSlotIndex];

            bool ConvertSlotToFloat = false;
            bool ConvertSlotToStereo = false;

            int ChannelCount = inputLayer.ChannelCount;
            WaveFormat wf = inputLayer.Format;

            if (sourceSlot.Layers != null)
            {
                if (sourceSlot.Layers.Count > 1)
                {
                    //decide what the new slot format should be based on all layers in the slot and the input layer
                    IsSlotConversionNeeded(wavetable, targetSlotIndex, ref wf, ref ChannelCount, ref ConvertSlotToFloat, ref ConvertSlotToStereo);

                    //decide if we need to convert the new layer to stereo
                    if ((ChannelCount == 2 && inputLayer.ChannelCount == 1))
                    {
                        inputLayer.CopyLeftToRight();
                    }
                }
            }

            //we need to backup the whole slot with all layers contained so we can operate on the selected layer
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                if (sourceLayer.Index == targetLayerIndex) //only add to the selected layer
                {
                    wavetable.AllocateWave(targetSlotIndex, sourceLayer.Path, sourceLayer.Name, sourceLayer.Left.Length + inputLayer.SampleCount, wf, ChannelCount == 2, sourceLayer.RootNote, add, false);
                    var targetLayer = wavetable.Waves[targetSlotIndex].Layers.Last();

                    if (ChannelCount == 1)
                    {
                        CopyMetaData(sourceLayer, targetLayer);

                        // add 0-StartSample of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, SamplePosition);
                        // add input
                        targetLayer.SetDataAsFloat(inputLayer.Left, 0, 1, 0, SamplePosition, inputLayer.SampleCount);
                        // add StartSample - length of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, SamplePosition, 1, 0, SamplePosition + inputLayer.SampleCount, sourceLayer.SampleCount - SamplePosition);

                        targetLayer.InvalidateData();
                    }
                    else if (ChannelCount == 2)
                    {
                        CopyMetaData(sourceLayer, targetLayer);

                        // add 0-StartSample of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, 0, 1, 0, 0, SamplePosition);
                        targetLayer.SetDataAsFloat(sourceLayer.Right, 0, 1, 1, 0, SamplePosition);
                        // add input
                        targetLayer.SetDataAsFloat(inputLayer.Left, 0, 1, 0, SamplePosition, inputLayer.SampleCount);
                        targetLayer.SetDataAsFloat(inputLayer.Right, 0, 1, 1, SamplePosition, inputLayer.SampleCount);
                        // add StartSample - length of old layer
                        targetLayer.SetDataAsFloat(sourceLayer.Left, SamplePosition, 1, 0, SamplePosition + inputLayer.SampleCount, sourceLayer.SampleCount - SamplePosition);
                        targetLayer.SetDataAsFloat(sourceLayer.Right, SamplePosition, 1, 1, SamplePosition + inputLayer.SampleCount, sourceLayer.SampleCount - SamplePosition);

                        targetLayer.InvalidateData();
                    }
                }
                else //if this is not the selected layer we still need to copy all the data (and convert it if needed)
                {
                    RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add, ConvertSlotToFloat, ConvertSlotToStereo);
                }

                add = true; //all subsequent layers are added to this slot
            }
        }

        public static void ReplaceLayer(IWavetable wavetable, int targetSlotIndex, int targetLayerIndex, TemporaryWave inputLayer)
        {
            // get right destination layer
            IWave sourceSlot = wavetable.Waves[targetSlotIndex];

            bool ConvertSlotToFloat = false;
            bool ConvertSlotToStereo = false;
            bool ReplacementLayerToFloat = false;
            bool ReplacementLayerToStereo = false;
            if (sourceSlot.Layers != null)
            {
                if (sourceSlot.Layers.Count > 1)
                {
                    //decide what the new slot format should be based on all layers in the slot and the input layer
                    int ChannelCount = inputLayer.ChannelCount;
                    WaveFormat wf = inputLayer.Format;
                    IsSlotConversionNeeded(wavetable, targetSlotIndex, ref wf, ref ChannelCount, ref ConvertSlotToFloat, ref ConvertSlotToStereo);

                    //decide if we need to convert the new layer
                    if (wf == WaveFormat.Float32 && inputLayer.Format != WaveFormat.Float32)
                    {
                        ReplacementLayerToFloat = true;
                    }
                    if ((ChannelCount == 2 && inputLayer.ChannelCount == 1))
                    {
                        ReplacementLayerToStereo = true;
                    }
                }
            }

            //we need to backup the whole slot with all layers contained so we can operate on the selected layer
            List<TemporaryWave> backupLayers = BackupLayersInSlot(sourceSlot.Layers);

            bool add = false; //first layer allocates the whole slot
            foreach (TemporaryWave sourceLayer in backupLayers)
            {
                if (sourceLayer.Index == targetLayerIndex) //replace the selected layer with the input (and convert it if needed)
                {
                    RestoreLayerFromBackup(wavetable, sourceSlot, inputLayer, add, ReplacementLayerToFloat, ReplacementLayerToStereo);

                }
                else //if this is not the selected layer we still need to copy all the data (and convert it if needed)
                {
                    RestoreLayerFromBackup(wavetable, sourceSlot, sourceLayer, add, ConvertSlotToFloat, ConvertSlotToStereo);
                }

                add = true; //all subsequent layers are added to this slot
            }
        }

        [Serializable]
        public class BuzzWaveSlot
        {
            public int SourceSlotIndex { get; private set; }
            public List<TemporaryWave> Layers { get; private set; }

            public BuzzWaveSlot(int sourceSlotIndex, List<TemporaryWave> layers)
            {
                SourceSlotIndex = sourceSlotIndex;
                Layers = layers;
            }
        }

    }

}