using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using ModernSequenceEditor.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WDE.AudioBlock
{
    [MachineDecl(Name = "WDE AudioBlock", ShortName = "AudioBlock", Author = "WDE", MaxTracks = 1)]
    public class AudioBlock : IBuzzMachine, INotifyPropertyChanged, IModernSequencerMachineInterface
    {   
        // Some defines used by AudioBlock
        private string AUDIO_BLOCK_VERSION = "1.2.10";

        public const int NUMBER_OF_AUDIO_BLOCKS = 16;
        public const int CLICK_REMOVAL_SAMPLES = 200;
        public const string AUDIO_BLOCK_PATTERN_BASE_NAME = "Audio ";
        public const double WAVE_CANVAS_WIDTH = 200;
        public const double WAVE_CANVAS_HEIGTH = 1000;
        public const double AUDIO_BLOCK_GUI_PARAM_WIDTH = 640;
        public const double AUDIO_BLOCK_GUI_PARAM_MIN_HEIGHT = 540;
        public const double AUDIO_BLOCK_GUI_WAVE_VIEW_MIN_WIDTH = 400;
        
        public IBuzzMachineHost host;
        public static Lock syncLock = new();

        public const int NUMBER_OF_SPLINE_POINTS_PER_SECOND = 100;
        public SplineCache[] splineCacheVol;
        public SplineCache[] splineCachePan;

        private RealTimeResamplerManager realTimeResamplerManager;
        
        private IPattern previouslyPlayingPatternEditorPattern;

        private AboutWindow aboutWindow;

        public static AudioBlockSettings Settings = new AudioBlockSettings();

        // If patterns changed
        private Dictionary<IPattern, string> PatternAssociations = new Dictionary<IPattern, string>();

        public WaveUndo WaveUndo { get; set; }

        private Dictionary<ISequence, bool> PreviouslyPlayedSequences = new Dictionary<ISequence, bool>();

        internal bool MachineClosing { get; set; }
        public bool ResetRealtimeResamplersFlag { get; set; }

        public enum EEnvelopeType
        {
            Volume,
            Pan
        }

        public AudioBlock(IBuzzMachineHost host)
        {
            this.host = host;
            
            Global.Buzz.Song.MachineAdded += Song_MachineAdded;
            Global.Buzz.OpenSong += Buzz_OpenSong;
            Global.Buzz.MasterTap += Buzz_MasterTap;
            Global.Buzz.Song.Wavetable.WaveChanged += Wavetable_WaveChanged;
            Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;

            MachineInitialized = false;

            WaveUndo = new WaveUndo(1, this);

            splineCacheVol = new SplineCache[NUMBER_OF_AUDIO_BLOCKS];
            splineCachePan = new SplineCache[NUMBER_OF_AUDIO_BLOCKS];

            MachineClosing = false;
            ResetRealtimeResamplersFlag = false;

            realTimeResamplerManager = new RealTimeResamplerManager();
        }

        public void ImportFinished(IDictionary<string, string> machineNameMap)
        {
            ValidateWaveData();
        }

        private void ValidateWaveData()
        {
            IWavetable wt = Global.Buzz.Song.Wavetable;

            for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
            {
                int index = MachineState.AudioBlockInfoTable[i].WavetableIndex;
                if (index >= 0 && wt.Waves[index] == null)
                {
                    MachineState.AudioBlockInfoTable[i].WavetableIndex = -1;
                    ResetVolEnvPoints(i);
                    ResetPanEnvPoints(i);
                }
            }
            UpdateUI();
        }


        // Wavetable ready (song loaded), we are ready to react to user events.
        private void Buzz_MasterTap(float[] arg1, bool arg2, SongTime arg3)
        {
            Global.Buzz.MasterTap -= Buzz_MasterTap;
            MachineInitialized = true;

            InitAudioBlockSettingsUI();
        }

        public void Stop()
        {
            realTimeResamplerManager.ResetRealTimeResamplers();
        }

        // Here we can check if pattern was renamed in Buzz
        private void Machine_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Patterns")
            {
                foreach (IPattern pat in this.host.Machine.Patterns)
                {
                    if (PatternAssociations.ContainsKey(pat))
                    {
                        if (PatternAssociations[pat] != pat.Name)
                        {
                            PatternRenamed(PatternAssociations[pat], pat.Name);
                            PatternAssociations[pat] = pat.Name;
                        }
                    }
                }
            }
        }

        private void PatternRenamed(string oldName, string newName)
        {
            for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
            {
                if (MachineState.AudioBlockInfoTable[i].Pattern == oldName)
                {
                    MachineState.AudioBlockInfoTable[i].Pattern = newName;
                    UpdateUI();
                    RaiseUpdateWaveGraphEvent(i);
                    break;
                }
            }
        }

        private void InitAudioBlockSettingsUI()
        {
            bool settingsViewContainsAudioBlock = false;

            foreach (Dictionary<string, string> dic in BuzzGUI.Common.SettingsWindow.GetSettings())
            {
                if (dic.ContainsKey("AudioBlock/WaveViewLayout"))
                    settingsViewContainsAudioBlock = true;
            }

            AudioBlock.Settings.PropertyChanged += Settings_PropertyChanged;
            SettingsWindow.AddSettings("AudioBlock", Settings);

            if (host.Machine.Graph.Buzz.IsSettingsWindowVisible && !settingsViewContainsAudioBlock)
            {
                // Redraw UI to display AudioBlock settings
                host.Machine.Graph.Buzz.IsSettingsWindowVisible = false;
                host.Machine.Graph.Buzz.IsSettingsWindowVisible = true;
            }
        }

        private void Settings_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {

            if (e.PropertyName == "DefaultWaveWidth" && this.AudioBlockGUI != null)
            {
                double height = (double)((AudioBlockSettings)sender).DefaultWaveWidth;
                AudioBlockGUI.WaveView.UpdateWaveHeights(height);
            }
            else if (e.PropertyName == "PatternLenghtToTick")
            {
                for (int i = 0; i < MachineState.AudioBlockInfoTable.Length; i++)
                    SetPatternLength(i);
            }
            else if (e.PropertyName == "ShowEnvelopeLimits" && this.AudioBlockGUI != null)
            {
                AudioBlockGUI.WaveView.ForceUpdateCanvases();
            }
        }

        /// <summary>
        /// Do some initialization once song has been loaded.
        /// </summary>
        /// <param name="obj"></param>
        private void Buzz_OpenSong(IOpenSong obj)
        {
            ReCreateEnvPoints();
        }

        /// <summary>
        /// Add some event handling once the machine has been laoded.
        /// </summary>
        /// <param name="obj"></param>
        private void Song_MachineAdded(IMachine obj)
        {
            // host is available here.
            if (host.Machine == obj)
            {
                host.Machine.PatternAdded += Machine_PatternAdded;
                host.Machine.PatternRemoved += Machine_PatternRemoved;

                host.Machine.PropertyChanged += Machine_PropertyChanged;

                // Strange Buzz menu crash so moving this here from constructor
                Global.Buzz.Song.SequenceAdded += Song_SequenceAdded;
            }
        }

        private void Song_MachineRemoved(IMachine obj)
        {
            if (host.Machine == obj)
            {
                RaiseUpdateWaveGraphEvent(0, WaveUpdateEventType.MachineClosing); // Notify canvases to not try to fetch data etc.
                host.Machine.PropertyChanged -= Machine_PropertyChanged;

                host.Machine.PatternAdded -= Machine_PatternAdded;
                host.Machine.PatternRemoved -= Machine_PatternRemoved;

                Global.Buzz.Song.SequenceAdded -= Song_SequenceAdded;

                Global.Buzz.Song.MachineAdded -= Song_MachineAdded;
                Global.Buzz.OpenSong -= Buzz_OpenSong;
                Global.Buzz.MasterTap -= Buzz_MasterTap;
                Global.Buzz.Song.Wavetable.WaveChanged -= Wavetable_WaveChanged;
                Global.Buzz.Song.MachineRemoved -= Song_MachineRemoved;

                Settings.PropertyChanged -= Settings_PropertyChanged;

                MachineClosing = true;
            }
        }

        /// <summary>
        /// Sequece was added to the somg.
        /// </summary>
        /// <param name="obj"></param>
        private void Song_SequenceAdded(int obj, ISequence seq)
        {
            // At this point the machines and patterns are loaded(?), but not sequences.

            // If only one pattern --> rename
            if (host.Machine.Patterns.Count == 1)
                if (host.Machine.Patterns[0].Name == "00")
                {
                    string name = AUDIO_BLOCK_PATTERN_BASE_NAME + "00";
                    var pat = host.Machine.Patterns.FirstOrDefault();
                    host.Machine.CreatePattern(name, 16);
                    MachineState.AudioBlockInfoTable[0].Pattern = name;
                    host.Machine.DeletePattern(pat);
                }
        }

        /// <summary>
        /// Update UI if pattern removed.
        /// </summary>
        /// <param name="obj"></param>
        private void Machine_PatternRemoved(IPattern obj)
        {
            PatternAssociations.Remove(obj);
            UpdateUI();
        }

        public void RemoveSequence(IPattern obj)
        {
            if (MachineState.AutoDeleteSequence)
            {
                List<ISequence> seqsToDel = new List<ISequence>();

                foreach (ISequence seq in host.Machine.Graph.Buzz.Song.Sequences)
                {
                    foreach (var seqEventdictionary in seq.Events)
                    {
                        // Delete all sequences where pattern to be deleted is the only one in the sequence
                        if (seqEventdictionary.Value.Pattern == obj && seq.Events.Count == 1)
                        {
                            seqsToDel.Add(seq);
                            break;
                        }
                    }
                }
                foreach (ISequence seq in seqsToDel)
                {
                    host.Machine.Graph.Buzz.Song.RemoveSequence(seq);
                }
            }
        }

        /// <summary>
        /// Update UI if pattern added.
        /// </summary>
        /// <param name="obj"></param>
        private void Machine_PatternAdded(IPattern obj)
        {
            PatternAssociations.Add(obj, obj.Name);
            UpdateUI();
        }

        /// <summary>
        /// Called when something changes in Wavetable. Note that this is also called when song is loading, so this event is rised both from user action or from loading.
        /// </summary>
        /// <param name="obj"></param>
        private void Wavetable_WaveChanged(int obj)
        {
            // Find out if there was a Audio Block linked to that wave and clear them.
            var wt = host.Machine.Graph.Buzz.Song.Wavetable;

            if (wt.Waves[obj] == null) // Removed
            {
                for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
                {
                    if (MachineState.AudioBlockInfoTable[i].WavetableIndex == obj)
                    {
                        MachineState.AudioBlockInfoTable[i].WavetableIndex = -1;
                        ResetVolEnvPoints(i);
                        ResetPanEnvPoints(i);
                        RaiseUpdateWaveGraphEvent(i, WaveUpdateEventType.WaveDeleted);
                    }
                }
            }
            else if (MachineInitialized) // Machine has been initialized so loading new wave
            {
                for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
                {
                    if (MachineState.AudioBlockInfoTable[i].WavetableIndex == obj)
                    {
                        // Buzz sends two events when new wave is loaded: First allocate then invalidate. Therefore Machine_PatternAdded will be called twise.
                        // Need to handle it. Disable auto resample from wavetable for now.
                        // Only enalbed in AudioBlock UI

                        /*
                        if (MachineState.AutoResample && Resampling == false)
                        {                            
                            Mouse.OverrideCursor = Cursors.Wait;
                            WaveUndo.SaveData(obj);
                            Resample(obj);
                            Mouse.OverrideCursor = null;                         
                        } 
                        */

                        UpdateLoopRange(i);
                        UpdateEnvData(i);
                        SetPatternLength(i);
                        RaiseUpdateWaveGraphEvent(i, WaveUpdateEventType.NewWaveAdded);
                        //RefreshBuzzViews();
                    }
                }
            }

            UpdateUI();
        }

        public void UpdateLoopRange(int audioBlockIndex)
        {
            lock (syncLock)
            {
                AudioBlockInfo abi = MachineState.AudioBlockInfoTable[audioBlockIndex];
                var wt = host.Machine.Graph.Buzz.Song.Wavetable;
                var targetLayer = wt.Waves[abi.WavetableIndex].Layers[0];
                double loopStartInSeconds = targetLayer.LoopStart / (double)targetLayer.SampleRate;
                double offsetInSeconds = abi.OffsetInMs * 1000 + abi.OffsetInSeconds;

                if (abi.LoopEnabled && loopStartInSeconds < offsetInSeconds)
                {
                    abi.OffsetInMs = 0;
                    abi.OffsetInSeconds = 0;
                }
            }
        }

        /// <summary>
        /// Updates spline data and makes some sanity checks os that envelope points do not go below/above max/min possible values.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        public void UpdateEnvData(int audioBlockIndex)
        {
            UpdatePanEnvPoints(audioBlockIndex);
            UpdateVolumeEnvPoints(audioBlockIndex);
            UpdateSplinePan(audioBlockIndex);
            UpdateSplineVol(audioBlockIndex);
        }

        /// <summary>
        /// This is one way to refres/invalidate Buzz views...
        /// </summary>
        public void RefreshBuzzViews()
        {
            if (host.Machine.Graph.Buzz.ActiveView == BuzzView.SequenceView)
            {
                host.Machine.Graph.Buzz.ActiveView = BuzzView.PatternView;
                host.Machine.Graph.Buzz.ActiveView = BuzzView.SequenceView; // update view;
            }
            else if (host.Machine.Graph.Buzz.ActiveView == BuzzView.PatternView)
            {
                host.Machine.Graph.Buzz.ActivatePatternEditor();
            }
        }

        /// <summary>
        /// Call this when data has changed and UI needs to be updated.
        /// </summary>
        private void UpdateUI()
        {
            if (AudioBlockGUI != null)
                AudioBlockGUI.UpdateUI();
        }

        [ParameterDecl(MaxValue = 1, DefValue = 0, ValueDescriptions = new String[] { "No", "Yes" })]
        public int Mute { get; set; }

        enum EnumMute : int
        {
            No,
            Yes
        }

        /// <summary>
        /// Work method is called by Buzz and here we generate the sound. Try to use as small buffers as possible. Use locks to make sure no other thread is touching wavetable data.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="numsamples"></param>
        /// <param name="mode"></param>
        /// <returns></returns>
        public bool Work(Sample[] output, int numsamples, WorkModes mode)
        {
            bool ret = false;

            if (this.Mute == (int)EnumMute.Yes)
                return ret;

            if (ResetRealtimeResamplersFlag)
            {
                realTimeResamplerManager.ResetRealTimeResamplers();
                ResetRealtimeResamplersFlag = false;
            }

            Dictionary<ISequence, bool> CurrentlyPlayingSequences = new Dictionary<ISequence, bool>();

            if (Global.Buzz.Playing || Global.Buzz.Recording)
            {
                for (int i = 0; i < numsamples; i++)
                {
                    output[i].L = 0.0f;
                    output[i].R = 0.0f;
                }
                
                lock (syncLock)
                {   
                    ReadOnlyCollection<ISequence> seqs = GetPlayingSequences(host.Machine);

                    foreach (ISequence seq in seqs)
                    {
                        if (seq.IsDisabled)
                            continue;

                        IPattern pat = null;
                        int playPosition = GetPositionInPattern(out pat, seq);

                        if (playPosition != -1 && pat != null)
                        {
                            float gain = GetGainForPattern(pat.Name);
                            gain *= (float)GetVolumeEnvelopeGain(pat.Name, playPosition);
                            Tuple<double, double> panGain = GetPanEnvelopeGain(pat.Name, playPosition);
                            float gainL = gain * (float)panGain.Item1;
                            float gainR = gain * (float)panGain.Item2;

                            realTimeResamplerManager.Check(seq, pat);

                            ret |= FetchAudioFromWavetableWithSampleRateCorrection(playPosition, pat, seq, numsamples, ref output, gainL, gainR, true);

                            CurrentlyPlayingSequences.Add(seq, true);
                        }
                    }

                    seqs = GetPlayingSequences(host.Machine);
                    // If playing in pattern editor
                    if (seqs.Count == 0)
                    {
                        IPattern pat = null;
                        int playPosition = GetPositionInPlayingPatternEditorPattern(out pat);
                        if (playPosition != -1)
                        {
                            float gain = GetGainForPattern(pat.Name);
                            gain *= (float)GetVolumeEnvelopeGain(pat.Name, playPosition);
                            Tuple<double, double> panGain = GetPanEnvelopeGain(pat.Name, playPosition);
                            float gainL = gain * (float)panGain.Item1;
                            float gainR = gain * (float)panGain.Item2;
                            if (previouslyPlayingPatternEditorPattern != null && previouslyPlayingPatternEditorPattern != pat)
                                realTimeResamplerManager.GetResampler(null).Clear();
                            ret |= FetchAudioFromWavetableWithSampleRateCorrection(playPosition, pat, null, numsamples, ref output, gainL, gainR, true);
                            previouslyPlayingPatternEditorPattern = pat;
                        }
                    }

                    foreach (ISequence seq in PreviouslyPlayedSequences.Keys)
                    {
                        if (!CurrentlyPlayingSequences.ContainsKey(seq))
                        {
                            realTimeResamplerManager.Clear(seq);
                        }
                    }
                }
            }
            else
                ret = false;

            return ret;
        }

        internal void RaisePropertyChangedPanEnv(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "PanEnvelopeChanged");
        }

        internal void RaisePropertyChangedVolumeEnv(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "VolumeEnvelopeChanged");
        }
        internal void RaisePropertyChangedPanVisibility(EnvelopeBase eb)
        {
            PropertyChanged.Raise(eb, "PanEnvelopeVisibility");
        }
        internal void RaisePropertyChangedVolumeVisibility(EnvelopeBase eb)
        {
            PropertyChanged.Raise(eb, "VolumeEnvelopeVisibility");
        }

        internal void RaisePropertyChangedPanEnvBoxAdded(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "PanEnvelopeBoxAdded");
        }

        internal void RaisePropertyChangedVolumeEnvBoxAdded(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "VolumeEnvelopeBoxAdded");
        }

        internal void RaisePropertyChangedPanEnvBoxRemoved(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "PanEnvelopeBoxRemoved");
        }

        internal void RaisePropertyChangedVolumeEnvBoxRemoved(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "VolumeEnvelopeBoxRemoved");
        }

        internal void RaisePropertyReDrawPan(EnvelopeLayerPan envelopeLayer)
        {
            PropertyChanged.Raise(envelopeLayer, "PanEnvelopeReDraw");
        }

        internal void RaisePropertyReDrawVolume(EnvelopeLayerVolume envelopeLayer)
        {
            PropertyChanged.Raise(envelopeLayer, "VolumeEnvelopeReDraw");
        }

        internal void RaisePropertyUpdateMenu(WaveCanvas waveCanvas)
        {
            PropertyChanged.Raise(waveCanvas, "UpdateMenus");
        }

        internal void RaisePropertyChangedPanEnvBoxFreezedState(EnvelopeBox envelopeBox)
        {
            PropertyChanged.Raise(envelopeBox, "PanEnvBoxFreezedState");
        }

        internal void RaisePropertyChangedVolumeEnvBoxFreezedState(EnvelopeBox envelopeBox)
        {
            PropertyChanged.Raise(envelopeBox, "VolumeEnvBoxFreezedState");
        }

        internal void RaisePropertyChangedPanEnvBoxesFreezedState(EnvelopeLayerPan envelopeLayerPan)
        {
            PropertyChanged.Raise(envelopeLayerPan, "PanEnvBoxesFreezedState");
        }

        internal void RaisePropertyChangedVolumeEnvBoxesFreezedState(EnvelopeLayerVolume envelopeLayerVolume)
        {
            PropertyChanged.Raise(envelopeLayerVolume, "VolumeEnvBoxesFreezedState");
        }

        /// <summary>
        /// Enable/disable audio looping.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        /// <param name="enabled"></param>
        internal void SetLoopingState(int audioBlockIndex, bool enabled)
        {
            lock (syncLock)
            {
                // Check bounds
                var wt = host.Machine.Graph.Buzz.Song.Wavetable;
                AudioBlockInfo abi = MachineState.AudioBlockInfoTable[audioBlockIndex];
                var targetLayer = wt.Waves[abi.WavetableIndex].Layers[0];
                double loopStartInSeconds = targetLayer.LoopStart / (double)targetLayer.SampleRate;

                // Don't go to the wrong side of loop beginning
                if (enabled && (loopStartInSeconds < abi.OffsetInSeconds + abi.OffsetInMs / 1000.0))
                {
                    abi.OffsetInSeconds = 0;
                    abi.OffsetInMs = 0;
                    UpdateUI();
                }
                MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled = enabled;
            }
        }

        /// <summary>
        /// Open loop is an another way of looping samples. Not used currently.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        /// <param name="v"></param>
        internal void SetOpenLoopState(int audioBlockIndex, bool v)
        {
            lock (syncLock)
            {
                MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop = v;
            }
        }

        /// <summary>
        /// Resturs volume gain from curves or lines. Returs 1.0f is envelopes are notused.
        /// </summary>
        /// <param name="patternName"></param>
        /// <param name="playPosition"></param>
        /// <returns></returns>
        private double GetVolumeEnvelopeGain(string patternName, int playPosition)
        {
            double ret = 1.0f;

            AudioBlockInfo abiFound = null;
            foreach (AudioBlockInfo abi in MachineState.AudioBlockInfoTable)
            {
                if (abi.Pattern == patternName)
                {
                    abiFound = abi;
                    break;
                }
            }

            if (abiFound != null && abiFound.VolumeEnvelopePoints.Count >= 2 && abiFound.VolumeEnvelopeEnabled)
            {
                if (abiFound.VolumeEnvelopeCurves)
                {
                    ret = CalculateValueFromEnvelopeCurve(abiFound, playPosition, patternName, EEnvelopeType.Volume);
                }
                else
                {
                    List<IEnvelopePoint> volPointListLine = abiFound.VolumeEnvelopePoints.ConvertAll<IEnvelopePoint>(x => (IEnvelopePoint)x);
                    ret = CalculateValueFromEnvelopeLine(patternName, playPosition, volPointListLine);
                }
            }
            return ret;
        }

        /// <summary>
        /// Returns value from specific point in spline.
        /// </summary>
        /// <param name="abi"></param>
        /// <param name="playPosition"></param>
        /// <param name="patternName"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private double CalculateValueFromEnvelopeCurve(AudioBlockInfo abi, int playPosition, string patternName, EEnvelopeType type)
        {
            double ret = 1.0;
            int index = -1;
            for (int i = 0; i < MachineState.AudioBlockInfoTable.Length; i++)
                if (MachineState.AudioBlockInfoTable[i] == abi)
                {
                    index = i;
                    break;
                }

            if (index >= 0)
            {
                SplineCache splineCache;
                if (type == EEnvelopeType.Volume)
                {
                    splineCache = GetSplineVol(index);
                }
                else
                {
                    splineCache = GetSplinePan(index);

                }

                float[] yValues = splineCache.YValues;
                double playPositionInSeconds = playPosition / (double)Global.Buzz.SelectedAudioDriverSampleRate;

                playPositionInSeconds = UpdatePlayPosInSecondsIfLooped(playPositionInSeconds, GetWavetableIndexForPattern(patternName), patternName);

                double stepSizeInSeconds = splineCache.StepSizeInSeconds;

                if (stepSizeInSeconds > 0)
                {

                    int startPos = (int)(playPositionInSeconds / stepSizeInSeconds);

                    if (startPos >= yValues.Length - 1)
                    {
                        ret = yValues[yValues.Length - 1];
                    }
                    else
                    {
                        double posInStep = playPositionInSeconds - stepSizeInSeconds * (double)startPos;
                        double mul = (yValues[startPos + 1] - yValues[startPos]) / stepSizeInSeconds;
                        ret = yValues[startPos] + mul * posInStep;
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Returns spline volume cache for AudioBlock
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        /// <returns></returns>
        internal SplineCache GetSplineVol(int audioBlockIndex)
        {
            return splineCacheVol[audioBlockIndex];
        }

        /// <summary>
        /// Returns spline pan cache for AudioBlock
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        /// <returns></returns>
        internal SplineCache GetSplinePan(int audioBlockIndex)
        {
            return splineCachePan[audioBlockIndex];
        }

        /// <summary>
        /// Resturs pan from curves or lines. Returs 1.0f for both left and right if envelopes are not used. User power panning algorithm.
        /// </summary>
        /// <param name="patternName"></param>
        /// <param name="playPosition"></param>
        /// <returns></returns>
        private Tuple<double, double> GetPanEnvelopeGain(string patternName, int playPosition)
        {
            Tuple<double, double> ret = new Tuple<double, double>(1.0, 1.0);

            AudioBlockInfo abiFound = null;
            foreach (AudioBlockInfo abi in MachineState.AudioBlockInfoTable)
            {
                if (abi.Pattern == patternName)
                {
                    abiFound = abi;
                    break;
                }
            }

            if (abiFound != null && abiFound.PanEnvelopePoints.Count >= 2 && abiFound.PanEnvelopeEnabled)
            {
                double pan = 1.0;

                if (abiFound.PanEnvelopeCurves)
                {
                    pan = CalculateValueFromEnvelopeCurve(abiFound, playPosition, patternName, EEnvelopeType.Pan);
                }
                else
                {
                    List<IEnvelopePoint> abiFondI = abiFound.PanEnvelopePoints.ConvertAll<IEnvelopePoint>(x => (IEnvelopePoint)x);
                    pan = CalculateValueFromEnvelopeLine(patternName, playPosition, abiFondI); // 0-2
                }
                pan = (pan / 2.0) * Math.PI / 2.0;
                ret = new Tuple<double, double>(2.0 / Math.Sqrt(2.0) * Math.Cos(pan), 2.0 / Math.Sqrt(2) * Math.Sin(pan));

            }
            return ret;
        }

        /// <summary>
        /// Returns value from specific point in envelope line.
        /// </summary>
        /// <param name="patternName"></param>
        /// <param name="playPosition"></param>
        /// <param name="envelopePoints"></param>
        /// <returns></returns>
        internal double CalculateValueFromEnvelopeLine(string patternName, int playPosition, List<IEnvelopePoint> envelopePoints)
        {
            double ret;

            double playPositionInSeconds = playPosition / (double)Global.Buzz.SelectedAudioDriverSampleRate;

            playPositionInSeconds = UpdatePlayPosInSecondsIfLooped(playPositionInSeconds, GetWavetableIndexForPattern(patternName), patternName);

            IEnvelopePoint prevPoint = envelopePoints[0];

            ret = prevPoint.Value;

            bool found = false;
            for (int i = 1; i < envelopePoints.Count; i++)
            {
                if (prevPoint.TimeStamp <= playPositionInSeconds && playPositionInSeconds < envelopePoints[i].TimeStamp)
                {
                    found = true;
                    double lenght = envelopePoints[i].TimeStamp - prevPoint.TimeStamp;
                    double timeX = playPositionInSeconds - prevPoint.TimeStamp;
                    double mul = (envelopePoints[i].Value - prevPoint.Value) / lenght;
                    ret = prevPoint.Value + timeX * mul;
                    break;
                }
                prevPoint = envelopePoints[i];
            }
            if (!found)
                ret = envelopePoints[envelopePoints.Count - 1].Value;

            return ret;
        }



        /// <summary>
        /// Reads samples from wavetable based on position in AudioBlock pattern. Takes into account diffrerent Buzz sample rates, and wavetable samplerates.
        /// </summary>
        /// <param name="playPosition"></param>
        /// <param name="patternName"></param>
        /// <param name="numsamples"></param>
        /// <param name="output"></param>
        /// <param name="gainL"></param>
        /// <param name="gainR"></param>
        /// <param name="scaleUp"></param>
        /// <returns></returns>
        public bool FetchAudioFromWavetableWithSampleRateCorrection(int playPosition, IPattern pat, ISequence seq, int numsamples, ref Sample[] output, float gainL, float gainR, bool scaleUp)
        {
            if (Settings.RealTimeResampler)
                return FetchAudioFromWavetableWithSampleRateCorrectionRT(playPosition, pat, seq, numsamples, ref output, gainL, gainR, scaleUp);
            else
                return FetchAudioFromWavetableWithSampleRateCorrectionLin(playPosition, pat, seq, numsamples, ref output, gainL, gainR, scaleUp);
        }

        private bool FetchAudioFromWavetableWithSampleRateCorrectionRT(int playPosition, IPattern pat, ISequence seq, int numsamples, ref Sample[] output, float gainL, float gainR, bool scaleUp)
        {
            //lock (syncLock)
            {
                double playPositionInSample = (double)playPosition;

                bool ret = false;

                int sampleIndex = GetWavetableIndexForPattern(pat.Name);

                RealTimeResampler realTimeResampler = realTimeResamplerManager.GetResampler(seq);

                if (sampleIndex >= 0)
                {
                    var wt = host.Machine.Graph.Buzz.Song.Wavetable;
                    var targetLayer = wt.Waves[sampleIndex].Layers[0];

                    if (targetLayer == null)
                        return false;

                    // Check if resampler is set up correctly
                    int tableIndex = GetTableIndexFromPatternName(pat.Name);

                    if (tableIndex != -1)
                    {
                        if (realTimeResampler.InputRate != GetSampleFrequency(sampleIndex) ||
                            realTimeResampler.OutputRate != Global.Buzz.SelectedAudioDriverSampleRate)
                            realTimeResampler.Reset(Global.Buzz.SelectedAudioDriverSampleRate, GetSampleFrequency(sampleIndex));
                    }

                    double step = GetSampleFrequency(sampleIndex) / (double)Global.Buzz.SelectedAudioDriverSampleRate;
                    double offset = step * GetOffsetForPatternInMs(pat.Name) / 1000.0 * ((double)Global.Buzz.SelectedAudioDriverSampleRate);

                    playPositionInSample *= step;
                    playPositionInSample += offset;

                    // Update loop position
                    playPositionInSample = UpdatePlayPosIfLooped(playPositionInSample, sampleIndex, pat.Name);

                    #region ADD_SILENCE
                    // Add silence if offset
                    int outputOffset = 0;

                    if (playPositionInSample < 0)
                    {
                        for (int i = 0; i < numsamples; i++)
                        {
                            playPositionInSample += step;
                            outputOffset++;
                            if (playPositionInSample >= 0)
                                break;
                        }
                    }
                    #endregion

                    int buffersize = 0;
                    buffersize = (int)((numsamples - outputOffset) * step);

                    if (playPositionInSample >= 0)
                    {
                        int readStartPos = (int)playPositionInSample;
                        if (readStartPos - realTimeResampler.ReadEndPos > 0)
                        {
                            buffersize += readStartPos - realTimeResampler.ReadEndPos;
                            playPositionInSample = realTimeResampler.ReadEndPos;
                        }

                        realTimeResampler.ReadEndPos = (int)playPositionInSample + buffersize;
                    }

                    // Need to check if we are looping
                    double loopEnd = 0;
                    double loopStart = 0;
                    int reminingBufferSize = 0;

                    bool looping = IsLoopingPattern(pat.Name);
                    if (looping)
                    {
                        loopEnd = targetLayer.LoopEnd;
                        loopStart = targetLayer.LoopStart;

                        if (playPositionInSample + buffersize > loopEnd)
                        {
                            reminingBufferSize = (int)(playPositionInSample + buffersize - loopEnd);
                            realTimeResampler.ReadEndPos = (int)(loopStart + playPositionInSample + buffersize - loopEnd);

                            buffersize -= reminingBufferSize;
                        }
                    }

                    Sample[] sampleDataTmp = new Sample[buffersize];

                    GetSamples(sampleIndex, ref sampleDataTmp, sampleDataTmp.Length, (int)playPositionInSample, scaleUp);

                    #region CLICK_REMOVAL

                    // Very basic click removal if using offset (cut from beginning)
                    if (playPosition == 0 && offset > 0)
                    {
                        int end = CLICK_REMOVAL_SAMPLES;
                        end = end > numsamples ? numsamples : end;
                        SmoothCut(ref sampleDataTmp, 0, end);
                    }
                    #endregion

                    realTimeResamplerManager.FillSilenceInSamples(seq, outputOffset);
                    realTimeResamplerManager.FillBuffer(seq, ref sampleDataTmp);

                    // Beginning of the loop
                    sampleDataTmp = new Sample[reminingBufferSize];
                    GetSamples(sampleIndex, ref sampleDataTmp, sampleDataTmp.Length, (int)loopStart, scaleUp);

                    realTimeResamplerManager.FillBuffer(seq, ref sampleDataTmp);
                    realTimeResamplerManager.GetSamples(seq, ref output, numsamples, gainL, gainR);

                    ret = true;
                }
                return ret;

            }
        }

        private bool FetchAudioFromWavetableWithSampleRateCorrectionLin(int playPosition, IPattern pat, ISequence seq, int numsamples, ref Sample[] output, float gainL, float gainR, bool scaleUp)
        {
            //lock (syncLock)
            {

                double playPositionInSample = (double)playPosition;

                bool ret = false;

                int sampleIndex = GetWavetableIndexForPattern(pat.Name);

                if (sampleIndex >= 0)
                {
                    var wt = host.Machine.Graph.Buzz.Song.Wavetable;
                    var targetLayer = wt.Waves[sampleIndex].Layers[0];

                    double step = GetSampleFrequency(sampleIndex) / (double)Global.Buzz.SelectedAudioDriverSampleRate;
                    double offset = step * GetOffsetForPatternInMs(pat.Name) / 1000.0 * ((double)Global.Buzz.SelectedAudioDriverSampleRate);

                    playPositionInSample *= step;
                    playPositionInSample += offset;

                    // Update loop position
                    playPositionInSample = UpdatePlayPosIfLooped(playPositionInSample, sampleIndex, pat.Name);

                    #region ADD_SILENCE
                    // Add silence if offset
                    int outputOffset = 0;

                    if (playPositionInSample < 0)
                    {
                        for (int i = 0; i < numsamples; i++)
                        {
                            playPositionInSample += step;
                            outputOffset++;
                            if (playPositionInSample >= 0)
                                break;
                        }
                    }
                    #endregion

                    int buffersize = 0;
                    buffersize = (int)(Math.Ceiling((numsamples - outputOffset) * step));

                    Sample[] sampleDataTmp = new Sample[buffersize];

                    GetSamples(sampleIndex, ref sampleDataTmp, sampleDataTmp.Length, (int)playPositionInSample, scaleUp);

                    #region CLICK_REMOVAL

                    // Very basic click removal if using offset (cut from beginning)
                    if (playPosition == 0 && offset > 0)
                    {
                        int end = CLICK_REMOVAL_SAMPLES;
                        end = end > numsamples ? numsamples : end;
                        SmoothCut(ref sampleDataTmp, 0, end);
                    }
                    #endregion

                    // Need to check if we are looping
                    double loopEnd = 0;
                    double loopStart = 0;
                    bool looping = IsLoopingPattern(pat.Name);
                    if (looping)
                    {
                        loopEnd = targetLayer.LoopEnd;
                        loopStart = targetLayer.LoopStart;
                    }
                    double stepper = 0;

                    int copyIndex;
                    int samplesToCopy = numsamples - outputOffset;
                    for (copyIndex = 0; copyIndex < samplesToCopy; copyIndex++)
                    {
                        if (looping && (playPositionInSample + stepper >= loopEnd))
                        {
                            // There are still samples to copy but we have reached the end of the loop.
                            break;
                        }

                        if (!Settings.RealTimeResampler)
                        {
                            output[copyIndex + outputOffset].L += sampleDataTmp[(int)stepper].L * gainL;
                            output[copyIndex + outputOffset].R += sampleDataTmp[(int)stepper].R * gainR;
                        }
                        stepper += step;
                    }

                    // If still samples to copy in the beginning of the loop                
                    if (copyIndex < samplesToCopy)
                    {
                        stepper = 0;
                        playPositionInSample = loopStart;
                        GetSamples(sampleIndex, ref sampleDataTmp, sampleDataTmp.Length, (int)playPositionInSample, scaleUp);

                        for (; copyIndex < samplesToCopy; copyIndex++)
                        {
                            output[copyIndex + outputOffset].L += sampleDataTmp[(int)stepper].L * gainL;
                            output[copyIndex + outputOffset].R += sampleDataTmp[(int)stepper].R * gainR;
                            stepper += step;
                        }
                    }

                    ret = true;
                }
                return ret;

            }
        }

        /// <summary>
        /// Returns updated play position if sample is looping.
        /// </summary>
        /// <param name="playPositionInSample"></param>
        /// <param name="sampleIndex"></param>
        /// <param name="patternName"></param>
        /// <returns></returns>
        internal double UpdatePlayPosIfLooped(double playPositionInSample, int sampleIndex, string patternName)
        {
            double ret = playPositionInSample;

            if (IsLoopingPattern(patternName))
            {
                lock (syncLock)
                {
                    var wt = host.Machine.Graph.Buzz.Song.Wavetable;
                    var targetLayer = wt.Waves[sampleIndex].Layers[0];

                    double loopStart = targetLayer.LoopStart;
                    double loopEnd = targetLayer.LoopEnd;
                    double loopLenght = loopEnd - loopStart;

                    // Are we in the loop?
                    if (playPositionInSample > loopStart)
                    {
                        ret = ((playPositionInSample - (loopStart)) % loopLenght) + loopStart;
                    }
                }
            }
            return ret;
        }



        /// <summary>
        /// Returns updated play position in seconds if looping enabled.
        /// </summary>
        /// <param name="playPositionInSeconds"></param>
        /// <param name="sampleIndex"></param>
        /// <param name="patternName"></param>
        /// <returns></returns>
        public double UpdatePlayPosInSecondsIfLooped(double playPositionInSeconds, int sampleIndex, string patternName)
        {
            double ret = playPositionInSeconds;

            if (IsLoopingPattern(patternName) && !IsOpenLoop(patternName))
            {
                lock (syncLock)
                {
                    var wt = host.Machine.Graph.Buzz.Song.Wavetable;
                    var targetLayer = wt.Waves[sampleIndex].Layers[0];
                    double loopStart = (targetLayer.LoopStart / (double)targetLayer.SampleRate) - GetOffsetForPatternInMs(patternName) / 1000.0;
                    double loopEnd = (targetLayer.LoopEnd / (double)targetLayer.SampleRate) - GetOffsetForPatternInMs(patternName) / 1000.0;
                    double loopLenght = loopEnd - loopStart;

                    // Are we in the loop?
                    if (playPositionInSeconds > loopStart)
                    {
                        ret = ((playPositionInSeconds - (loopStart)) % loopLenght) + loopStart;
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Update offset for AudioBlock item. Can be silnce (negative value) or cut (positive value).
        /// </summary>e
        /// <param name="audioBlockIndex"></param>
        /// <param name="offsetInSeconds"></param>
        /// <param name="offsetInMs"></param>
        /// <returns></returns>
        internal bool UpdateOffsets(int audioBlockIndex, double offsetInSeconds, double offsetInMs)
        {
            bool ret = false;
            AudioBlockInfo abi = MachineState.AudioBlockInfoTable[audioBlockIndex];
            double changeDeltaInSeconds = offsetInSeconds - abi.OffsetInSeconds
                + (offsetInMs - abi.OffsetInMs) / 1000.0;

            if (abi.WavetableIndex == -1)
            {
                abi.OffsetInMs = 0;
                abi.OffsetInSeconds = 0;
                return false;
            }

            lock (syncLock)
            {
                var wt = host.Machine.Graph.Buzz.Song.Wavetable;
                var targetLayer = wt.Waves[abi.WavetableIndex].Layers[0];
                double loopStartInSeconds = targetLayer.LoopStart / (double)targetLayer.SampleRate;

                // Don't go to the wrong side of loop beginning
                if (abi.LoopEnabled && (loopStartInSeconds < offsetInSeconds + offsetInMs / 1000.0))
                {
                    ret = false;
                }
                else
                {
                    ret = true;

                    abi.OffsetInSeconds = offsetInSeconds;
                    abi.OffsetInMs = offsetInMs;

                    List<VolumeEnvelopePoint> volPointList = abi.VolumeEnvelopePoints;

                    for (int i = 1; i < volPointList.Count; i++)
                    {
                        VolumeEnvelopePoint vep = volPointList[i];
                        vep.TimeStamp -= changeDeltaInSeconds;
                    }

                    for (int i = 1; i < volPointList.Count - 1; i++)
                    {
                        VolumeEnvelopePoint vep = volPointList[i];
                        if (vep.TimeStamp < 0)
                        {
                            volPointList.RemoveAt(i);
                            i--;
                        }
                    }


                    volPointList[volPointList.Count - 1].TimeStamp = GetSampleLengthInSecondsWithOffset(audioBlockIndex);

                    List<PanEnvelopePoint> panPointList = abi.PanEnvelopePoints;

                    for (int i = 1; i < panPointList.Count; i++)
                    {
                        PanEnvelopePoint vep = panPointList[i];
                        vep.TimeStamp -= changeDeltaInSeconds;
                    }

                    for (int i = 1; i < panPointList.Count - 1; i++)
                    {
                        PanEnvelopePoint vep = panPointList[i];
                        if (vep.TimeStamp < 0)
                        {
                            panPointList.RemoveAt(i);
                            i--;
                        }
                    }

                    panPointList[panPointList.Count - 1].TimeStamp = GetSampleLengthInSecondsWithOffset(audioBlockIndex);

                    UpdateEnvPoints(audioBlockIndex);
                    UpdateEnvData(audioBlockIndex);
                    ResetRealtimeResamplersFlag = true;
                }
            }
            return ret;
        }

        /// <summary>
        /// Refresh/update envelope points and spline cache.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void UpdateEnvPoints(int audioBlockIndex)
        {
            UpdateVolumeEnvPoints(audioBlockIndex);
            UpdatePanEnvPoints(audioBlockIndex);
            if (MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves)
                UpdateSplineVol(audioBlockIndex);
            if (MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves)
                UpdateSplinePan(audioBlockIndex);
        }

        /// <summary>
        /// Refres envelope data for all items.
        /// </summary>
        internal void ReCreateEnvPoints()
        {
            for (int i = 0; i < NUMBER_OF_AUDIO_BLOCKS; i++)
            {
                // Make sure everything is ok.
                UpdateEnvPoints(i);
            }
        }

        /// <summary>
        /// Sanity checks for Volume envelope.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void UpdateVolumeEnvPoints(int audioBlockIndex)
        {
            double maxLength = GetSampleLengthInSecondsWithOffset(audioBlockIndex);
            List<VolumeEnvelopePoint> volPoints = MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints;
            if (volPoints.Count < 2)
            {
                volPoints.Clear();

                VolumeEnvelopePoint vep = new VolumeEnvelopePoint();
                vep.TimeStamp = 0;
                vep.Value = 1;
                vep.Freezed = !AudioBlock.Settings.StartUnfreezed;
                volPoints.Add(vep);

                vep = new VolumeEnvelopePoint();
                vep.TimeStamp = maxLength;
                vep.Value = 1;
                vep.Freezed = !AudioBlock.Settings.StartUnfreezed;
                volPoints.Add(vep);
            }

            // Update first and last
            volPoints[0].TimeStamp = 0;
            volPoints[volPoints.Count - 1].TimeStamp = maxLength;

            for (int i = 1; i < volPoints.Count - 1; i++)
            {
                VolumeEnvelopePoint vep = volPoints[i];
                if (vep.TimeStamp < 0)
                {
                    volPoints.RemoveAt(i);
                    i--;
                }
                if (vep.TimeStamp > maxLength)
                {
                    volPoints.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// Sanity checks for pan envelope.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void UpdatePanEnvPoints(int audioBlockIndex)
        {
            double maxLength = GetSampleLengthInSecondsWithOffset(audioBlockIndex);
            List<PanEnvelopePoint> panPoints = MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints;
            if (panPoints.Count < 2)
            {
                panPoints.Clear();

                PanEnvelopePoint pep = new PanEnvelopePoint();
                pep.TimeStamp = 0;
                pep.Value = 1;
                pep.Freezed = !AudioBlock.Settings.StartUnfreezed;
                panPoints.Add(pep);

                pep = new PanEnvelopePoint();
                pep.TimeStamp = maxLength;
                pep.Value = 1;
                pep.Freezed = !AudioBlock.Settings.StartUnfreezed;
                panPoints.Add(pep);
            }

            // Update first and last
            panPoints[0].TimeStamp = 0;
            panPoints[panPoints.Count - 1].TimeStamp = maxLength;

            for (int i = 1; i < panPoints.Count - 1; i++)
            {
                PanEnvelopePoint vep = panPoints[i];
                if (vep.TimeStamp < 0)
                {
                    panPoints.RemoveAt(i);
                    i--;
                }
                if (vep.TimeStamp > maxLength)
                {
                    panPoints.RemoveAt(i);
                    i--;
                }
            }
        }

        /// <summary>
        /// Reset volume envelopes and clear previous data.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void ResetVolEnvPoints(int audioBlockIndex)
        {
            bool state = MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves;

            MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves = false;
            MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints.Clear();
            UpdateSplineVol(audioBlockIndex);
            MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves = state;
        }

        /// <summary>
        /// Reset pan envelopes and clear previous data.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void ResetPanEnvPoints(int audioBlockIndex)
        {
            bool state = MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves;

            MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves = false;
            MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints.Clear();
            UpdateSplinePan(audioBlockIndex);
            MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves = state;
        }

        /// <summary>
        /// Is the pattern looping?
        /// </summary>
        /// <param name="patternName"></param>
        /// <returns></returns>
        private bool IsLoopingPattern(string patternName)
        {
            foreach (AudioBlockInfo abi in MachineState.AudioBlockInfoTable)
                if (abi.Pattern == patternName && abi.LoopEnabled)
                    return true;
            return false;
        }

        /// <summary>
        /// Is open loop mode?
        /// </summary>
        /// <param name="patternName"></param>
        /// <returns></returns>
        private bool IsOpenLoop(string patternName)
        {
            foreach (AudioBlockInfo abi in MachineState.AudioBlockInfoTable)
                if (abi.Pattern == patternName && abi.OpenLoop)
                    return true;
            return false;
        }

        /// <summary>
        /// Returns offset for pattern in milliseconds.
        /// </summary>
        /// <param name="patternName"></param>
        /// <returns></returns>
        public double GetOffsetForPatternInMs(string patternName)
        {
            foreach (AudioBlockInfo abi in MachineState.AudioBlockInfoTable)
            {
                if (abi.Pattern == patternName)
                {
                    return abi.OffsetInMs + abi.OffsetInSeconds * 1000.0; // Milliseconds
                }
            }
            return 0;
        }

        /// <summary>
        /// Returns wavetable index of the associated wave for AudioBlock item.
        /// </summary>
        /// <param name="patternName"></param>
        /// <returns></returns>
        public int GetWavetableIndexForPattern(string patternName)
        {
            foreach (AudioBlockInfo abi in MachineState.AudioBlockInfoTable)
            {
                if (abi.Pattern == patternName)
                {
                    return abi.WavetableIndex;
                }
            }
            return -1;
        }

        /// <summary>
        /// Returns gain for AudioBlock item.
        /// </summary>
        /// <param name="patternName"></param>
        /// <returns></returns>
        public float GetGainForPattern(string patternName)
        {
            foreach (AudioBlockInfo abi in MachineState.AudioBlockInfoTable)
            {
                if (abi.Pattern == patternName)
                {
                    return abi.Gain;
                }
            }
            return 1.0f;
        }

        public int GetTableIndexFromPatternName(string patternName)
        {
            int ret = -1;
            for (int i = 0; i < MachineState.AudioBlockInfoTable.Length; i++)
            {
                if (MachineState.AudioBlockInfoTable[i].Pattern == patternName)
                {
                    ret = i;
                    break;
                }
            }
            return ret;
        }

        /// <summary>
        /// Update/recreate volume spline cache.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void UpdateSplineVol(int audioBlockIndex)
        {
            lock (syncLock)
            {
                if (MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeCurves)
                {
                    SplineCache spline = new SplineCache();
                    splineCacheVol[audioBlockIndex] = spline;

                    float min = 0.0f;
                    float max = 1.0f;
                    if (MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopeMode == (int)VolumeEnvelopeModes.dB6)
                        max = (float)Decibel.FromAmplitude(6.0);

                    List<IEnvelopePoint> volPoints = MachineState.AudioBlockInfoTable[audioBlockIndex].VolumeEnvelopePoints.ConvertAll<IEnvelopePoint>(x => (IEnvelopePoint)x);
                    spline.CreateSpline(volPoints.ToArray(), NUMBER_OF_SPLINE_POINTS_PER_SECOND);
                    spline.CutMinMax(min, max);
                }
            }
        }

        /// <summary>
        /// Update/recureate spline pan cache.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void UpdateSplinePan(int audioBlockIndex)
        {
            lock (syncLock)
            {
                if (MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopeCurves)
                {
                    SplineCache spline = new SplineCache();
                    splineCachePan[audioBlockIndex] = spline;

                    float min = 0.0f;
                    float max = 2.0f;

                    List<IEnvelopePoint> envPoints = MachineState.AudioBlockInfoTable[audioBlockIndex].PanEnvelopePoints.ConvertAll<IEnvelopePoint>(x => (IEnvelopePoint)x);
                    spline.CreateSpline(envPoints.ToArray(), NUMBER_OF_SPLINE_POINTS_PER_SECOND);
                    spline.CutMinMax(min, max);
                }
            }
        }


        // Different synclock used here to allow only one request at a time to read sample data. Lowers the pressure towards audio thread.
        // Should be used only low priority requests.
        public static object syncLock2 = new object();
        //int getSamplesReadLowPriority = 0;
        public void GetSamplesLowPriority(int wabetableWaveIndex, ref Sample[] output, int numsamples, int positionInSample, bool scaleUp)
        {
            lock (syncLock2)
            {
                //if (getSamplesReadLowPriority > 400)
                //{
                //    getSamplesReadLowPriority = 0;
                    //Thread.Sleep(1);
                //}

                if (MachineClosing)
                    return;

                GetSamples(wabetableWaveIndex, ref output, numsamples, positionInSample, scaleUp);
                //getSamplesReadLowPriority += numsamples;
            }
        }

        /// <summary>
        /// Get the actual sample data from wavetable.
        /// </summary>
        /// <param name="wabetableWaveIndex"></param>
        /// <param name="output"></param>
        /// <param name="numsamples"></param>
        /// <param name="positionInSample"></param>
        /// <param name="scaleUp"></param>
        public void GetSamples(int wabetableWaveIndex, ref Sample[] output, int numsamples, int positionInSample, bool scaleUp)
        {
            lock (syncLock)
            {
                float[] tmpSamples = new float[numsamples * 2];
                IWavetable wt = Global.Buzz.Song.Wavetable;
                var targetLayer = wt.Waves[wabetableWaveIndex] != null ? wt.Waves[wabetableWaveIndex].Layers[0] : null;

                if ( targetLayer != null )
                {   
                    if (targetLayer.SampleCount < positionInSample + numsamples)
                        numsamples = targetLayer.SampleCount - positionInSample;

                    if (numsamples > 0)
                    {
                        if (!IsStereo(wabetableWaveIndex))
                        {
                            targetLayer.GetDataAsFloat(tmpSamples, 0, 2, 0, positionInSample, numsamples);
                            targetLayer.GetDataAsFloat(tmpSamples, 1, 2, 0, positionInSample, numsamples);
                        }
                        else
                        {
                            targetLayer.GetDataAsFloat(tmpSamples, 0, 2, 0, positionInSample, numsamples);
                            targetLayer.GetDataAsFloat(tmpSamples, 1, 2, 1, positionInSample, numsamples);
                        }
                    }
                }


                // We could also use volume settings for samples from Wavetable. Ignoring now.
                // scaleUp flag is used to avoid unncessary math operation if wave is for example drawn canvas.
                if (scaleUp)
                {
                    for (int i = 0; i < numsamples; i++)
                    {
                        output[i].L = tmpSamples[i * 2] * 32768.0f;
                        output[i].R = tmpSamples[(i * 2) + 1] * 32768.0f;
                    }
                }
                else
                {
                    for (int i = 0; i < numsamples; i++)
                    {
                        output[i].L = tmpSamples[i * 2];
                        output[i].R = tmpSamples[(i * 2) + 1];
                    }
                }
            }
        }



        /// <summary>
        /// Returns sample frequency.
        /// </summary>
        /// <param name="wabetableWaveIndex"></param>
        /// <returns></returns>
        public int GetSampleFrequency(int wabetableWaveIndex)
        {
            int sampleRate = 44100;
            lock (syncLock)
            {
                if (!MachineClosing)
                {
                    IWavetable wt = host.Machine.Graph.Buzz.Song.Wavetable;
                    var targetLayer = wt.Waves[wabetableWaveIndex].Layers[0];

                    if (wt.Waves[wabetableWaveIndex] != null && wt.Waves[wabetableWaveIndex].Layers.Count > 0)
                    {
                        sampleRate = targetLayer.SampleRate;
                    }
                }
            }
            return sampleRate;
        }

        /// <summary>
        /// Is stereo sample?
        /// </summary>
        /// <param name="wabetableWaveIndex"></param>
        /// <returns></returns>
        public bool IsStereo(int wabetableWaveIndex)
        {
            lock (syncLock)
            {
                IWavetable wt = host.Machine.Graph.Buzz.Song.Wavetable;
                var targetLayer = wt.Waves[wabetableWaveIndex].Layers[0];
               bool ret = true;

                if (wabetableWaveIndex >= 0 && wt.Waves.Count > wabetableWaveIndex && wt.Waves[wabetableWaveIndex] != null && wt.Waves[wabetableWaveIndex].Layers.Count > 0)
                {
                    if (targetLayer.ChannelCount < 2)
                        ret = false;
                }
                return ret;
            }
            
        }

        /// <summary>
        /// Returns playing position in pattern editor pattern when pattern is being played solo.
        /// </summary>
        /// <param name="patternName"></param>
        /// <returns></returns>
        public int GetPositionInPlayingPatternEditorPattern(out IPattern patOut)
        {
            patOut = null;
            foreach (IPattern pat in host.Machine.Patterns)
            {
                if (pat.IsPlayingSolo && pat.PlayPosition >= 0)
                {
                    patOut = pat;
                    double avgSamplesPerTick = (60.0 * host.MasterInfo.SamplesPerSec) / (host.MasterInfo.BeatsPerMin * host.MasterInfo.TicksPerBeat);
                    return (int)((pat.PlayPosition * avgSamplesPerTick + host.MasterInfo.PosInTick) / (double)PatternEvent.TimeBase);
                }
                else return -1;
            }
            return -1;
        }

        /// <summary>
        /// Returns playing position in samples in pattern or -1.
        /// </summary>
        /// <param name="patternName"></param>
        /// <param name="seq"></param>
        /// <returns></returns>
        public int GetPositionInPattern(out IPattern pat, ISequence seq)
        {
            pat = null;

            if (seq.Machine.Name == this.host.Machine.Name && seq.PlayingPattern != null)
            {
                pat = seq.PlayingPattern;
                if (pat != null && pat.PlayPosition >= 0)
                {   
                    double avgSamplesPerTick = (60.0 * host.MasterInfo.SamplesPerSec) / (host.MasterInfo.BeatsPerMin * host.MasterInfo.TicksPerBeat);
                    return (int)(seq.PlayingPatternPosition * avgSamplesPerTick + host.MasterInfo.PosInTick);
                }
                else return -1;
            }
            return -1;
        }

        /// <summary>
        /// Returns plauing position in specific pattern or -1.
        /// </summary>
        /// <param name="patternName"></param>
        /// <returns></returns>
        public int GetPlayPositionInPatternName(string patternName)
        {
            foreach (IPattern pat in host.Machine.Patterns)
            {
                if (pat.Name == patternName && pat.PlayPosition >= 0)
                {
                    double avgSamplesPerTick = (60.0 * host.MasterInfo.SamplesPerSec) / (host.MasterInfo.BeatsPerMin * host.MasterInfo.TicksPerBeat);
                    return (int)((pat.PlayPosition * avgSamplesPerTick + host.MasterInfo.PosInTick) / (double)PatternEvent.TimeBase);
                }
            }
            return -1;
        }

        /// <summary>
        /// Return all sequences that are being played.
        /// </summary>
        /// <param name="machine"></param>
        /// <returns></returns>
        public ReadOnlyCollection<ISequence> GetPlayingSequences(IMachine machine)
        {
            List<ISequence> sequences = new List<ISequence>();
            foreach (ISequence seq in host.Machine.Graph.Buzz.Song.Sequences)
            {
                if (seq.Machine.Name == this.host.Machine.Name)
                {
                    IPattern pat = seq.PlayingPattern;
                    if (pat != null)
                    {
                        sequences.Add(seq);
                    }
                }
            }
            return sequences.AsReadOnly();
        }

        /// <summary>
        /// Linear volume ramp up for sample if sample is being cut.
        /// </summary>
        /// <param name="output"></param>
        /// <param name="start"></param>
        /// <param name="end"></param>
        public void SmoothCut(ref Sample[] output, int start, int end)
        {
            int pos = start;
            int direction = start < end ? 1 : -1;
            end = end < output.Length ? end : output.Length;

            float smoothMulStep = 1.0f / Math.Abs(end - start);
            float mul = 0.0f;

            while (pos != end)
            {
                output[pos].L *= mul;
                output[pos].R *= mul;
                mul += smoothMulStep;
                pos += direction;
            }
        }

        /// <summary>
        /// Get full file name for grag&drop file.
        /// </summary>
        /// <param name="filename"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        public bool GetFilename(out string filename, DragEventArgs e)
        {
            bool ret = true;
            filename = String.Empty;

            if ((e.AllowedEffects & DragDropEffects.Copy) == DragDropEffects.Copy)
            {
                Array data = ((IDataObject)e.Data).GetData("FileName") as Array;
                if (data != null)
                {
                    if ((data.Length == 1) && (data.GetValue(0) is String))
                    {
                        filename = ((string[])data)[0];
                        FileInfo fi = new FileInfo(filename);
                        filename = fi.FullName;
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// Create new AudioBlock pattern.
        /// </summary>
        /// <param name="patternLength"></param>
        /// <returns></returns>
        public string AddPattern(int patternLength)
        {
            string patternName = AudioBlock.AUDIO_BLOCK_PATTERN_BASE_NAME;
            for (int i = 0; i < 500; i++)
            {
                if (IsPatternNameAvailable(patternName + i.ToString("00")))
                {
                    patternName = patternName + i.ToString("00");
                    break;
                }
            }
            host.Machine.CreatePattern(patternName, patternLength);

            AddSequence(patternName);

            return patternName;
        }

        /// <summary>
        /// Get pattern object based on pattern name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IPattern GetPattern(string name)
        {
            IPattern ret = null;

            for (int i = 0; i < host.Machine.Patterns.Count; i++)
            {
                if (host.Machine.Patterns[i].Name == name)
                {
                    ret = host.Machine.Patterns[i];
                    break;
                }
            }
            return ret;
        }


        /// <summary>
        /// Calculate pattern lenght needed for sample.
        /// </summary>
        /// <param name="samples"></param>
        /// <param name="sampleRate"></param>
        /// <param name="offset"></param>
        /// <returns></returns>
        public int CalculatePatternLength(int samples, int sampleRate, double offset)
        {
            double sampleCount = samples - offset * sampleRate;
            sampleCount = sampleCount < 0 ? 1 : sampleCount;
            double samplePlaySeconds = sampleCount / (double)sampleRate;
            double avgTicks = (samplePlaySeconds / 60.0) * (double)host.MasterInfo.BeatsPerMin * (double)host.MasterInfo.TicksPerBeat;
            double patternBlocks = avgTicks;

            int patternLength = (int)Math.Ceiling(patternBlocks);

            if (patternLength % AudioBlock.Settings.PatternLenghtToTick > 0)
            {
                patternLength = patternLength + (AudioBlock.Settings.PatternLenghtToTick - patternLength % AudioBlock.Settings.PatternLenghtToTick);
            }

            return patternLength;
        }

        /// <summary>
        /// Return pattern lenght in seconds.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public double GetPatternLenghtInSeconds(int audioBlockIndex)
        {
            double ret = 0;
            var wt = host.Machine.Graph.Buzz.Song.Wavetable;
            int waveIndex = MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;

            // Don't set lenght if looping
            if (waveIndex > -1)
            { 
                if (!MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled)
                {
                    IPattern pat = GetPattern(MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern);
                    if (pat != null && wt.Waves[waveIndex] != null && wt.Waves[waveIndex].Layers.Count > 0)
                    {
                        var targetLayer = wt.Waves[waveIndex].Layers.Last();
                        double offset = MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs / 1000.0 + MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds;
                        int samples = targetLayer.SampleCount;
                        int sampleRate = targetLayer.SampleRate;

                        double ticksPerSecond = (double)host.MasterInfo.BeatsPerMin / 60.0 * (double)host.MasterInfo.TicksPerBeat;

                        ret = CalculatePatternLength(samples, sampleRate, offset) / ticksPerSecond;
                    }
                }
                else
                {
                    IPattern pat = GetPattern(MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern);
                    if (pat != null && wt.Waves[waveIndex] != null && wt.Waves[waveIndex].Layers.Count > 0)
                    {
                        double ticksPerSecond = (double)host.MasterInfo.BeatsPerMin / 60.0 * (double)host.MasterInfo.TicksPerBeat;
                        ret = pat.Length / ticksPerSecond;
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Note that variable name 'audioBlockIndex' is used in AudioBlock table and 'wavetableIndex' (or similar) in Buzz Wavetable.
        /// Don't mix them up!
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        public void SetPatternLength(int audioBlockIndex)
        {
            //bool playing = Global.Buzz.Playing;
            //Global.Buzz.Playing = false;

            var wt = host.Machine.Graph.Buzz.Song.Wavetable;
            int waveIndex = MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;

            // Don't set lenght if looping
            if (waveIndex > -1 && !MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled)
            {
                IPattern pat = GetPattern(MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern);
                if (pat != null && wt.Waves[waveIndex] != null && wt.Waves[waveIndex].Layers.Count > 0 )
                {
                    var targetLayer = wt.Waves[waveIndex].Layers[0];
                    pat.Length = CalculatePatternLength(targetLayer.SampleCount, targetLayer.SampleRate,
                        MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs / 1000.0 + MachineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds);

                    //MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex = waveIndex;
                    RefreshBuzzViews();
                }
            }
            else if (waveIndex < 0)
            {
                IPattern pat = GetPattern(MachineState.AudioBlockInfoTable[audioBlockIndex].Pattern);
                if (pat != null)
                {
                    pat.Length = 16;
                    RefreshBuzzViews();
                }
            }

            //Global.Buzz.Playing = playing;
        }

        /// <summary>
        /// Is pattern name available?
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public bool IsPatternNameAvailable(string name)
        {
            bool ret = true;

            for (int i = 0; i < host.Machine.Patterns.Count; i++)
            {
                if (host.Machine.Patterns[i].Name == name)
                {
                    ret = false;
                    break;
                }
            }
            return ret;
        }

        /// <summary>
        /// Returns next available (empty) slot in Buzz Wavetable.
        /// </summary>
        /// <returns></returns>
        public int FindNextAvaialbeIndexInWavetable()
        {
            int ret = -1;
            var wt = host.Machine.Graph.Buzz.Song.Wavetable;
            for (int i = 0; i < wt.Waves.Count; i++)
            {
                if (wt.Waves[i] == null)
                {
                    ret = i;
                    break;
                }
            }
            return ret;
        }

        /// <summary>
        /// Returns longes sample in seconds. Offset is calcualted in the result.
        /// </summary>
        /// <returns></returns>
        public double GetLongestSampleInSecondsWithOffset()
        {
            double longest = 0;

            lock (syncLock)
            {
                IWavetable wt = host.Machine.Graph.Buzz.Song.Wavetable;
                for (int abIndex = 0; abIndex < this.MachineState.AudioBlockInfoTable.Length; abIndex++)
                {
                    int wavetableIndex = MachineState.AudioBlockInfoTable[abIndex].WavetableIndex;
                    if (wavetableIndex >= 0 && wt.Waves[wavetableIndex] != null)
                    {
                        double length = GetSampleLengthInSecondsWithOffset(abIndex);
                        if (length > longest)
                            longest = length;
                    }
                }
            }

            longest = longest == 0 ? 10 : longest;
            return longest;
        }

        /// <summary>
        /// Returns sample lenght in seconds.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        /// <returns></returns>
        public double GetSampleLenghtInSeconds(int audioBlockIndex)
        {
            double length = 0;
            //lock (syncLock)
            {
                if (MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled &&
                    MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop)
                {
                    length = GetPatternLenghtInSeconds(audioBlockIndex);
                }
                else
                {
                    IWavetable wt = host.Machine.Graph.Buzz.Song.Wavetable;
                    int wavetableIndex = MachineState.AudioBlockInfoTable[audioBlockIndex].WavetableIndex;
                    if (wavetableIndex >= 0 && wt.Waves[wavetableIndex] != null)
                    {
                        var targetLayer = wt.Waves[wavetableIndex].Layers.Last();
                        length = targetLayer.SampleCount / (double)targetLayer.SampleRate;
                    }
                }
            }

            return length;
        }

        /// <summary>
        /// Returns sample lenght in seconds. Offset is calcualted in to the result.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        /// <returns></returns>
        public double GetSampleLengthInSecondsWithOffset(int audioBlockIndex)
        {
            double length = GetSampleLenghtInSeconds(audioBlockIndex);
            if (MachineState.AudioBlockInfoTable[audioBlockIndex].LoopEnabled && MachineState.AudioBlockInfoTable[audioBlockIndex].OpenLoop)
            {
                // Do nothing
            }
            else
            {
                // Add offset
                length -= (machineState.AudioBlockInfoTable[audioBlockIndex].OffsetInSeconds + machineState.AudioBlockInfoTable[audioBlockIndex].OffsetInMs / 1000.0);
                length = length < 0 ? 0 : length;
            }

            return length;
        }

        /// <summary>
        /// Small hack to notfity Buzz that user needs to save song when exiting Buzz.
        /// </summary>
        public void NotifyBuzzDataChanged()
        {
            int val = host.Machine.ParameterGroups[0].Parameters[0].GetValue(0);
            host.Machine.ParameterGroups[0].Parameters[0].SetValue(0, val);
        }

        public void AddSequence(string patName)
        {
            if (!MachineState.AutoAddSequence)
                return;

            // Find last AudioBlock seqence
            int pos = -1;

            foreach (ISequence seq in this.host.Machine.Graph.Buzz.Song.Sequences)
            {
                if (seq.Machine == this.host.Machine)
                {
                    pos = this.host.Machine.Graph.Buzz.Song.Sequences.IndexOf(seq) + 1;
                }
            }

            this.host.Machine.Graph.Buzz.Song.AddSequence(this.host.Machine, pos);

            IPattern pat = this.GetPattern(patName);

            // Find our new sequence
            ISequence seqTarget = null;
            foreach (ISequence seq in this.host.Machine.Graph.Buzz.Song.Sequences)
            {
                if (seq.Machine == this.host.Machine)
                {
                    seqTarget = seq;
                }
            }

            if (seqTarget != null)
                seqTarget.SetEvent(0, new SequenceEvent(SequenceEventType.PlayPattern, pat));
        }


        // Helper interface
        public interface IEnvelopePoint
        {
            double TimeStamp { get; set; }
            double Value { get; set; }
        }


        // Here we save all the Volume Envelope time stamps and values. Could use struct, but class it is.
        public class VolumeEnvelopePoint : IEnvelopePoint
        {
            double timeStamp;
            double value;

            public double TimeStamp { get => timeStamp; set => timeStamp = value; }
            public double Value { get => value; set => this.value = value; }
            public bool Freezed { get; set; }

            public VolumeEnvelopePoint()
            {
                TimeStamp = -1;
                Value = 0;
                Freezed = true;
            }
        }

        public class PanEnvelopePoint : IEnvelopePoint
        {
            double timeStamp;
            double value;

            public double TimeStamp { get => timeStamp; set => timeStamp = value; }
            public double Value { get => value; set => this.value = value; }
            public bool Freezed { get; set; }
            public PanEnvelopePoint()
            {
                TimeStamp = -1;
                Value = 0;
                Freezed = true;
            }
        }

        // This is our data structure that is saved in to song.
        public class AudioBlockInfo
        {
            private int wavetableIndex;
            private double offsetInMs;
            private double offsetInSeconds;
            private string pattern;
            private float gain;
            private int color;

            private bool volumeEnvelopeEnabled;
            private int volumeEnvelopeMode;
            private List<VolumeEnvelopePoint> volumeEnvelopePoints;

            private bool panEnvelopeEnabled;
            private List<PanEnvelopePoint> panEnvelopePoints;

            private bool loopEnabled;
            private bool openLoop;
            private bool volumeEnvelopeCurves;
            private bool panEnvelopeCurves;

            private bool snapToTick;
            private bool snapToBeat;
            private bool panEnvelopeVisible;
            private bool volumeEnvelopeVisible;

            public int WavetableIndex { get => wavetableIndex; set => wavetableIndex = value; }
            public string Pattern { get => pattern; set => pattern = value; }
            public double OffsetInMs { get => offsetInMs; set => offsetInMs = value; }
            public float Gain { get => gain; set => gain = value; }
            public double OffsetInSeconds { get => offsetInSeconds; set => offsetInSeconds = value; }
            public int Color { get => color; set => color = value; }
            public List<VolumeEnvelopePoint> VolumeEnvelopePoints { get => volumeEnvelopePoints; set => volumeEnvelopePoints = value; }
            public bool VolumeEnvelopeEnabled { get => volumeEnvelopeEnabled; set => volumeEnvelopeEnabled = value; }
            public int VolumeEnvelopeMode { get => volumeEnvelopeMode; set => volumeEnvelopeMode = value; }
            public bool LoopEnabled { get => loopEnabled; set => loopEnabled = value; }
            public bool OpenLoop { get => openLoop; set => openLoop = value; }
            public bool PanEnvelopeEnabled { get => panEnvelopeEnabled; set => panEnvelopeEnabled = value; }
            public List<PanEnvelopePoint> PanEnvelopePoints { get => panEnvelopePoints; set => panEnvelopePoints = value; }
            public bool VolumeEnvelopeCurves { get => volumeEnvelopeCurves; set => volumeEnvelopeCurves = value; }
            public bool PanEnvelopeCurves { get => panEnvelopeCurves; set => panEnvelopeCurves = value; }
            public bool SnapToTick { get => snapToTick; set => snapToTick = value; }
            public bool SnapToBeat { get => snapToBeat; set => snapToBeat = value; }
            public bool PanEnvelopeVisible { get => panEnvelopeVisible; set => panEnvelopeVisible = value; }
            public bool VolumeEnvelopeVisible { get => volumeEnvelopeVisible; set => volumeEnvelopeVisible = value; }
            public bool SpectrogramEnabled { get; set; }

            public AudioBlockInfo()
            {
                wavetableIndex = -1;
                offsetInMs = 0.0;
                offsetInSeconds = 0.0;
                pattern = "";
                gain = 1.0f;
                color = -1;

                SpectrogramEnabled = false;

                loopEnabled = false;
                openLoop = false;

                snapToTick = AudioBlock.Settings.SnapTo == SnapTo.Tick;
                snapToBeat = AudioBlock.Settings.SnapTo == SnapTo.Beat;

                volumeEnvelopeCurves = false;
                panEnvelopeCurves = false;

                VolumeEnvelopeEnabled = false;
                VolumeEnvelopeMode = 0;
                VolumeEnvelopePoints = new List<VolumeEnvelopePoint>();

                PanEnvelopeEnabled = false;
                PanEnvelopePoints = new List<PanEnvelopePoint>();

                panEnvelopeVisible = false;
                volumeEnvelopeVisible = false;
            }
        }

        public class State
        {
            public State()
            {
                for (int i = 0; i < NUMBER_OF_AUDIO_BLOCKS; i++)
                {
                    AudioBlockInfoTable[i] = new AudioBlockInfo();
                }

                waveViewOrientation = AudioBlock.Settings.WaveViewLayout == WaveViewLayout.Vertical ? 0 : 1;
                overwriteSample = AudioBlock.Settings.OverwriteSample;
                autoResample = AudioBlock.Settings.AutoResample;
                autoAddSequence = AudioBlock.Settings.AutoAddSequence;
                autoDeleteSequence = AudioBlock.Settings.AutoDeleteSequence;


            }	// NOTE: parameterless constructor is required by the xml serializer

            private AudioBlockInfo[] audioBlockInfoTable = new AudioBlockInfo[NUMBER_OF_AUDIO_BLOCKS];
            private int waveViewOrientation;
            private bool overwriteSample;
            private bool autoResample;
            private bool autoAddSequence;
            private bool autoDeleteSequence;

            public AudioBlockInfo[] AudioBlockInfoTable
            {
                get
                {
                    return audioBlockInfoTable;
                }
                set
                {
                    audioBlockInfoTable = value;

                    // Disable open loop until better solution is found.
                    foreach (AudioBlockInfo abi in audioBlockInfoTable)
                    {
                        abi.OpenLoop = false;
                        //abi.PanEnvelopeVisible = false;
                        //abi.VolumeEnvelopeVisible = false;
                        foreach (PanEnvelopePoint pep in abi.PanEnvelopePoints)
                            pep.Freezed = !AudioBlock.Settings.StartUnfreezed; // Start freezed?
                        foreach (VolumeEnvelopePoint vep in abi.VolumeEnvelopePoints)
                            vep.Freezed = !AudioBlock.Settings.StartUnfreezed; // Start freezed?
                    }
                }
            }

            public int WaveViewOrientation { get => waveViewOrientation; set => waveViewOrientation = value; }
            public bool OverwriteSample { get => overwriteSample; set => overwriteSample = value; }
            public bool AutoResample { get => autoResample; set => autoResample = value; }
            public bool AutoAddSequence { get => autoAddSequence; set => autoAddSequence = value; }
            public bool AutoDeleteSequence { get => autoDeleteSequence; set => autoDeleteSequence = value; }
        }

        State machineState = new State();
        public State MachineState           // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get { return machineState; }
            set
            {
                machineState = value;
                for (int i = 0; i < machineState.AudioBlockInfoTable.Length; i++)
                {
                    if (machineState.AudioBlockInfoTable[i] != null)
                        UpdateEnvData(i);
                }
            }
        }

        /// <summary>
        /// Show Amiga influenced about box.
        /// </summary>
        private void ShowAboutWindow()
        {
            if (aboutWindow != null && aboutWindow.Visibility == Visibility.Visible)
                aboutWindow.Close();

            aboutWindow = new AboutWindow(AUDIO_BLOCK_VERSION);
            aboutWindow.Show();
        }

        // Right click machine menu
        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                yield return new MenuItemVM()
                {
                    Text = "Help",
                    Command = new SimpleCommand()
                    {
                        CanExecuteDelegate = p => true,
                        ExecuteDelegate = p => MessageBox.Show("AudioBlock turns patterns into audio blocks. Use Buzz Wavetable or drag & drop audio files to AudioBlock.")
                    }
                };

                yield return new MenuItemVM()
                {
                    Text = "About...",
                    Command = new SimpleCommand()
                    {
                        CanExecuteDelegate = p => true,
                        ExecuteDelegate = p => ShowAboutWindow()
                    }
                };
            }
        }

        public bool MachineInitialized { get; set; } = false;
        public AudioBlockGUI AudioBlockGUI { get; private set; }
        
        internal void SetGUI(AudioBlockGUI audioBlockGUI)
        {
            this.AudioBlockGUI = audioBlockGUI;
        }

        // Raise event when wave graphs need to be updated
        public event OnUpdateWaveGraph UpdateWaveGraph;
        public delegate void OnUpdateWaveGraph(AudioBlock ab, EventArgs e);

        /// <summary>
        /// Sends notification to registered callers.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        public void RaiseUpdateWaveGraphEvent(int audioBlockIndex)
        {
            EventArgsWaveUpdate e = new EventArgsWaveUpdate();
            e.AudioBlockIndex = audioBlockIndex;
            //Application.Current.Dispatcher.BeginInvoke((Action)(() =>
            //{
                UpdateWaveGraph(this, e);
            //}));
        }

        /// <summary>
        /// Send notification toregisterd callers.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        /// <param name="type"></param>
        public void RaiseUpdateWaveGraphEvent(int audioBlockIndex, WaveUpdateEventType type)
        {
            EventArgsWaveUpdate e = new EventArgsWaveUpdate();
            e.AudioBlockIndex = audioBlockIndex;
            e.Type = type;
  
            if (UpdateWaveGraph != null)
                UpdateWaveGraph(this, e);
        }

        public Canvas PrepareCanvasForSequencer(IPattern pat, SequencerLayout layout, double tickHeight, int time, double width, double height)
        {
            WaveCanvas wc = null;
            int abInfoTableIndex;
            for (abInfoTableIndex = 0; abInfoTableIndex < NUMBER_OF_AUDIO_BLOCKS; abInfoTableIndex++)
            {
                if (MachineState.AudioBlockInfoTable[abInfoTableIndex].Pattern == pat.Name)
                    break;
            }
            if (abInfoTableIndex < NUMBER_OF_AUDIO_BLOCKS && layout == SequencerLayout.Vertical)
            {
                wc = new WaveCanvas(this, abInfoTableIndex);
                wc.ViewOrientationMode = ViewOrientationMode.Vertical;
                wc.Width = width;
                wc.Height = height;
                wc.TickHeight = tickHeight;
                wc.Time = time;
                wc.DisplayPlayPosition = false;
                wc.EnableResizing = false;
                wc.DrawBackground = true;
                wc.DrawPatternText = false;
                wc.PatternLengthInSeconds = GetPatternLenghtInSeconds(abInfoTableIndex);
                wc.WaveBrush = Utils.GetBrushForPattern(this, abInfoTableIndex);
                wc.PatternEditorWave = true;
            }
            else if (abInfoTableIndex < NUMBER_OF_AUDIO_BLOCKS && layout == SequencerLayout.Horizontal)
            {
                wc = new WaveCanvas(this, abInfoTableIndex);
                wc.ViewOrientationMode = ViewOrientationMode.Horizontal;
                wc.Width = width;
                wc.Height = height;
                wc.TickHeight = tickHeight;
                wc.Time = time;
                wc.DisplayPlayPosition = false;
                wc.EnableResizing = false;
                wc.DrawBackground = true;
                wc.DrawPatternText = false;
                wc.PatternLengthInSeconds = GetPatternLenghtInSeconds(abInfoTableIndex);
                wc.WaveBrush = Utils.GetBrushForPattern(this, abInfoTableIndex);
                wc.PatternEditorWave = true;
            }

            return wc;
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    // Event types to use for the UpdateWaveGraph event.
    public enum WaveUpdateEventType
    {
        Other,
        Drag,
        NewWaveAdded,
        PatternChanged,
        ColorChanged,
        ChangeRate,
        ChangeTempo,
        ChangePitch,
        WaveDeleted,
        MachineClosing
    }

    // Our own event agrs class to be used with UpdateWaveGraph event.
    public class EventArgsWaveUpdate : EventArgs
    {
        private WaveUpdateEventType type = WaveUpdateEventType.Other;
        private int audioBlockIndex = -1;
        public int AudioBlockIndex { get => audioBlockIndex; set => audioBlockIndex = value; }
        internal WaveUpdateEventType Type { get => type; set => type = value; }
    }

    /// <summary>
    /// GUI Starts here.
    /// </summary>
    /// 
    public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new AudioBlockGUI(); } }
    public class AudioBlockGUI : UserControl, IMachineGUI
    {
        IMachine machine;
        AudioBlock audioBlockMachine;
        AudioBlockUIItem[] audioBlockUIItems;
        public Grid gridMain;
        private WaveView waveView;
        private ViewMode viewMode = ViewMode.Single;

        private MenuItem miOverwriteSample;
        private MenuItem miAutoResample;
        private MenuItem miAutoAddSeq;
        private MenuItem miAutoDeleteSeq;

        internal WaveView WaveView { get => waveView; set => waveView = value; }

        public IMachine Machine
        {
            get { return machine; }
            set
            {
                machine = value;

                if (machine != null)
                {
                    audioBlockMachine = (AudioBlock)machine.ManagedMachine;
                    audioBlockMachine.SetGUI(this);

                    for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
                    {
                        audioBlockUIItems[i].SetAudioBlockMachine(audioBlockMachine, this);
                    }
                }
            }
        }


        /// <summary>
        /// Fetch data from AudioBlock machine State and restore UI selections.
        /// </summary>
        public void UpdateUI()
        {
            for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
            {
                audioBlockUIItems[i].DisableEvents();
                audioBlockUIItems[i].PopulateData();
                audioBlockUIItems[i].RestoreSelection(
                    audioBlockMachine.MachineState.AudioBlockInfoTable[i].Pattern,
                    audioBlockMachine.MachineState.AudioBlockInfoTable[i].WavetableIndex,
                    audioBlockMachine.MachineState.AudioBlockInfoTable[i].OffsetInSeconds,
                    audioBlockMachine.MachineState.AudioBlockInfoTable[i].OffsetInMs,
                    audioBlockMachine.MachineState.AudioBlockInfoTable[i].Gain);
                audioBlockUIItems[i].EnableEvents();
            }
        }

        public AudioBlockGUI()
        {
            gridMain = new Grid() { VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(10, 8, 0, 10) };
            ColumnDefinition gridCol1 = new ColumnDefinition();
            ColumnDefinition gridCol2 = new ColumnDefinition();
            gridMain.ColumnDefinitions.Add(gridCol1);
            gridMain.ColumnDefinitions.Add(gridCol2);

            var sp = new StackPanel() { };

            Grid.SetColumn(sp, 0);
            gridMain.Children.Add(sp);

            // Grid for controls. Maybe settings button in the future.
            Grid gridControls = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
            gridControls.RowDefinitions.Add(new RowDefinition());
            gridControls.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(60) });
            gridControls.ColumnDefinitions.Add(new ColumnDefinition() { });

            Menu menuSettings = new Menu() { Height = 20 };
            Grid.SetColumn(menuSettings, 0);
            gridControls.Children.Add(menuSettings);

            Button bt = new Button() { Margin = new Thickness(8, 8, 20, 8), Content = new TextBlock() { Text = "Show / Hide Audio View", FontSize = 12 } };
            bt.Click += Bt_Click;
            Grid.SetColumn(bt, 1);
            gridControls.Children.Add(bt);

            //ContextMenu menuOpt = new ContextMenu();
            MenuItem menuOpt = new MenuItem() { Header = "_Options", Width = 70 };
            miOverwriteSample = new MenuItem();
            miOverwriteSample.Header = "Drag & Drop: Overwrite Sample in Wavetable";
            miOverwriteSample.IsCheckable = true;
            miOverwriteSample.Checked += Mi_Checked;
            miOverwriteSample.Unchecked += Mi_Unchecked;
            menuOpt.Items.Add(miOverwriteSample);

            menuSettings.Items.Add(menuOpt);

            menuOpt.Items.Add(new Separator());

            miAutoAddSeq = new MenuItem();
            miAutoAddSeq.Header = "Auto Add Sequence";
            miAutoAddSeq.IsCheckable = true;
            miAutoAddSeq.Checked += MiAutoAddSeq_Checked;
            miAutoAddSeq.Unchecked += MiAutoAddSeq_Unchecked;
            menuOpt.Items.Add(miAutoAddSeq);

            miAutoDeleteSeq = new MenuItem();
            miAutoDeleteSeq.Header = "Auto Delete Sequence";
            miAutoDeleteSeq.IsCheckable = true;
            miAutoDeleteSeq.Checked += MiAutoDeleteSeq_Checked;
            miAutoDeleteSeq.Unchecked += MiAutoDeleteSeq_Unchecked;
            menuOpt.Items.Add(miAutoDeleteSeq);

            menuOpt.Items.Add(new Separator());

            miAutoResample = new MenuItem();
            miAutoResample.Header = "Auto-resample to Buzz sample rate";
            miAutoResample.IsCheckable = true;
            miAutoResample.Checked += MiAutoResample_Checked;
            miAutoResample.Unchecked += MiAutoResample_Unchecked;
            menuOpt.Items.Add(miAutoResample);

            MenuItem miResampleAllWt = new MenuItem();
            miResampleAllWt.Header = "Resample all in Buzz Wavetable...";
            miResampleAllWt.Click += MiResampleAllWt_Click;
            menuOpt.Items.Add(miResampleAllWt);

            MenuItem miSettings = new MenuItem();
            miSettings.Header = "Settings...";
            miSettings.Click += (sender, e) =>
            {
                SettingsWindow.Show(machine.ParameterWindow, "AudioBlock");
            };
            menuOpt.Items.Add(miSettings);

            var spMainLabel = new StackPanel();
            spMainLabel.Orientation = Orientation.Horizontal;
            Label labelPattern = new Label() { Content = "Pattern", Width = 110 };
            spMainLabel.Children.Add(labelPattern);
            Label labelSampleNumber = new Label() { Content = "Wavetable Audio", Width = 306 };
            //labelSampleNumber.ContextMenu = menuOpt;
            spMainLabel.Children.Add(labelSampleNumber);
            Label labelSampleOffsetSeconds = new Label() { Content = "Offset (sec)", Width = 88, ToolTip = "Positive value cuts, negative adds silence/delay." };
            spMainLabel.Children.Add(labelSampleOffsetSeconds);
            Label labelSampleOffsetMs = new Label() { Content = "Offset (ms)", Width = 88, ToolTip = "Positive value cuts, negative adds silence/delay." };
            spMainLabel.Children.Add(labelSampleOffsetMs);
            Label labelGain = new Label() { Content = "Vol", Width = 30, ToolTip = "Audio block gain." };
            spMainLabel.Children.Add(labelGain);

            sp.Children.Add(gridControls);
            sp.Children.Add(spMainLabel);

            gridCol1.Width = new GridLength(AudioBlock.AUDIO_BLOCK_GUI_PARAM_WIDTH);

            audioBlockUIItems = new AudioBlockUIItem[AudioBlock.NUMBER_OF_AUDIO_BLOCKS];
            for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
            {
                audioBlockUIItems[i] = new AudioBlockUIItem(i);
                sp.Children.Add(audioBlockUIItems[i]);
            }

            this.Content = gridMain;
            this.Loaded += AudioBlockGUI_Loaded;
            this.Unloaded += AudioBlockGUI_Unloaded;


        }

        private void MiResampleAllWt_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult res = MessageBox.Show("Resample all Wavetable waves to " + Global.Buzz.SelectedAudioDriverSampleRate + "Hz?", "Resample?", MessageBoxButton.YesNo);

            if (res == MessageBoxResult.Yes)
            {
                IWavetable wt = Global.Buzz.Song.Wavetable;
                for (int i = 0; i < wt.Waves.Count; i++)
                {
                    if (wt.Waves[i] != null)
                        Effects.Resample(audioBlockMachine, i);
                }
            }
        }

        private void MiAutoDeleteSeq_Unchecked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AutoDeleteSequence = false;
        }

        private void MiAutoDeleteSeq_Checked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AutoDeleteSequence = true;
        }

        private void MiAutoAddSeq_Unchecked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AutoAddSequence = false;
        }

        private void MiAutoAddSeq_Checked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AutoAddSequence = true;
        }

        private void MiAutoResample_Unchecked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AutoResample = false;
        }

        private void MiAutoResample_Checked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.AutoResample = true;
        }

        private void Mi_Unchecked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.OverwriteSample = false;
        }

        private void Mi_Checked(object sender, RoutedEventArgs e)
        {
            audioBlockMachine.MachineState.OverwriteSample = true;
        }

        private void AudioBlockGUI_SizeChanged(object sender, SizeChangedEventArgs e)
        {

            if (/*WaveView.Visibility == Visibility.Visible && */ e.HeightChanged)
                //WaveView.UpdateHeight(machine.ParameterWindow.ActualHeight - 200);                                   
                WaveView.UpdateHeight(gridMain.ActualHeight);

        }

        enum ViewMode
        {
            Single,
            Double
        }

        /// <summary>
        /// Change the AudioBlock UI between param view and param view + audio wave view.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Bt_Click(object sender, RoutedEventArgs e)
        {
            if (viewMode == ViewMode.Single)
            {
                viewMode = ViewMode.Double;

                machine.ParameterWindow.MinWidth = AudioBlock.AUDIO_BLOCK_GUI_PARAM_WIDTH + AudioBlock.AUDIO_BLOCK_GUI_WAVE_VIEW_MIN_WIDTH;
                machine.ParameterWindow.MaxWidth = double.PositiveInfinity;
                WaveView.UpdateScale();
                WaveView.Visibility = Visibility.Visible;
                WaveView.UpdateWaveViewContent();
            }
            else
            {
                viewMode = ViewMode.Single;

                WaveView.Visibility = Visibility.Hidden;
                machine.ParameterWindow.MinWidth = AudioBlock.AUDIO_BLOCK_GUI_PARAM_WIDTH;
                machine.ParameterWindow.MaxWidth = AudioBlock.AUDIO_BLOCK_GUI_PARAM_WIDTH;
            }
        }


        /// <summary>
        /// Do some resizing here.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AudioBlockGUI_Loaded(object sender, RoutedEventArgs e)
        {  
            WaveView = new WaveView(audioBlockMachine, AudioBlock.Settings.DefaultWaveWidth, AudioBlock.WAVE_CANVAS_HEIGTH);

            Grid.SetColumn(WaveView, 1);

            WaveView.Init(); // Get the UpdateWaveGraph events after wavecanvases get it.
            WaveView.Visibility = Visibility.Hidden;
            gridMain.Children.Add(WaveView);

            miOverwriteSample.IsChecked = audioBlockMachine.MachineState.OverwriteSample;
            miAutoResample.IsChecked = audioBlockMachine.MachineState.AutoResample;
            miAutoAddSeq.IsChecked = audioBlockMachine.MachineState.AutoAddSequence;
            miAutoDeleteSeq.IsChecked = audioBlockMachine.MachineState.AutoDeleteSequence;

            // Resize param window
            machine.ParameterWindow.MinWidth = AudioBlock.AUDIO_BLOCK_GUI_PARAM_WIDTH;
            machine.ParameterWindow.MaxWidth = AudioBlock.AUDIO_BLOCK_GUI_PARAM_WIDTH;
            machine.ParameterWindow.MinHeight = AudioBlock.AUDIO_BLOCK_GUI_PARAM_MIN_HEIGHT;
            machine.ParameterWindow.InvalidateVisual();

            machine.ParameterWindow.Content = this;
            machine.ParameterWindow.SizeChanged += AudioBlockGUI_SizeChanged;
            machine.ParameterWindow.PreviewKeyDown += AudioBlockGUI_PreviewKeyDown;

            UpdateUI();
        }

        private void AudioBlockGUI_Unloaded(object sender, RoutedEventArgs e)
        {
            if (machine != null && machine.ParameterWindow != null)
            {
                for (int i = 0; i < AudioBlock.NUMBER_OF_AUDIO_BLOCKS; i++)
                {
                    audioBlockUIItems[i].DisableEvents();
                }

                machine.ParameterWindow.SizeChanged -= AudioBlockGUI_SizeChanged;
                machine.ParameterWindow.PreviewKeyDown -= AudioBlockGUI_PreviewKeyDown;
            }
        }

        private void AudioBlockGUI_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Z && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                audioBlockMachine.WaveUndo.Undo();
            }
            else if (e.Key == Key.Y && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                audioBlockMachine.WaveUndo.Redo();
            }
            e.Handled = true;
        }
    }
}
