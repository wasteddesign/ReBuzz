using Buzz.MachineInterface;
using BuzzGUI.Common;
using BuzzGUI.Interfaces;
using ModernSequenceEditor.Interfaces;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wintellect.PowerCollections;

namespace EnvelopeBlock
{
    [MachineDecl(Name = "Envelope Block", ShortName = "EnvBlock", Author = "WDE", MaxTracks = 1)]
    public class EnvelopeBlockMachine : IBuzzMachine, INotifyPropertyChanged, IModernSequencerMachineInterface
    {
        public static int MAX_ENVELOPE_BOX_PATTERNS = 100;
        public static int MAX_ENVELOPE_BOX_PARAMS = 6;
        public const int NUMBER_OF_SPLINE_POINTS_PER_SECOND = 100;

        public SplineCache[,] splineCache;

        public IBuzzMachineHost host;
        public static object syncLock = new object();

        private double oldSpeed;

        public static EnvelopeBlockSettings Settings = new EnvelopeBlockSettings();

        // If patterns changed
        private readonly Dictionary<IPattern, string> PatternAssociations = new Dictionary<IPattern, string>();
        // If machines changed
        private readonly Dictionary<IMachine, string> MachineAssociations = new Dictionary<IMachine, string>();

        // ConcurrentDictionary<IPattern, bool> taskMonitorForPatterns = new ConcurrentDictionary<IPattern, bool>();
        readonly Dictionary<IMachine, int> controlChangeBuffer = new Dictionary<IMachine, int>();

        
        public bool SendParameterChangesPending { get; private set; }
        public bool ReDrawAllCanvases { get; private set; }
        public bool UpdateMenu { get; private set; }
        public bool UpdateAllMenus { get; private set; }
        public bool EnvelopeBoxAdded { get; private set; }
        public bool EnvelopeBoxRemoved { get; private set; }
        public bool EnvelopeReDraw { get; private set; }

        Task MainTask;
        public EnvelopeBlockMachine(IBuzzMachineHost host)
        {
            this.host = host;
            splineCache = new SplineCache[MAX_ENVELOPE_BOX_PATTERNS, MAX_ENVELOPE_BOX_PARAMS];
            Global.Buzz.Song.MachineAdded += Song_MachineAdded;
            Global.Buzz.Song.MachineRemoved += Song_MachineRemoved;

            Global.Buzz.PropertyChanged += Buzz_PropertyChanged;

            EnvelopeBlockMachine.Settings.PropertyChanged += Settings_PropertyChanged;
            SettingsWindow.AddSettings("EnvelopeBlock", Settings);
        }

        public void Work()
        {
            if (Settings.IgnoreWhenRecording && Global.Buzz.Recording)
                return;

            if (!SendParameterChangesPending && host.Machine.Graph.Buzz.Playing)
            {
                SendParameterChangesPending = true;

                MainTask = Task.Run(() =>
                {
                    if ((EnvelopeBlockMachine.Settings.UpdateRate == UpdateRateEnum.Tick) && (host.MasterInfo.PosInTick == 0))
                    {
                        UpdateMachineParameters();
                    }
                    else if ((EnvelopeBlockMachine.Settings.UpdateRate == UpdateRateEnum.SubTick) && (host.SubTickInfo.PosInSubTick == 0))
                    {
                        // FYI. Sending too many param changes to VSTs from Work does not seem to be a good idea...
                        UpdateMachineParameters();
                    }
                    SendParameterChangesPending = false;
                } );
            }
        }

        private void Buzz_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "BPM" || e.PropertyName == "TPB")
            {
                double newSpeed = host.MasterInfo.BeatsPerMin * host.MasterInfo.TicksPerBeat;
                double scale = oldSpeed / newSpeed;

                for (int i = 0; i < MachineState.Patterns.Length; i++)
                {
                    if (MachineState.Patterns[i] != null)
                    {
                        ScaleTimeStamps(i, scale);
                        UpdatePatternSplines(i);
                    }
                }

                oldSpeed = newSpeed;
                //RaisePropertyReDrawAllCanvases();
            }
        }

        private void Settings_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "NumeralSystem")
            {
                RaisePropertyReDrawAllCanvases();
            }
        }

        
        private void UpdateMachineParameters()
        {
            if (Global.Buzz.Playing || Global.Buzz.Recording)
            {
                controlChangeBuffer.Clear();
                //lock (syncLock)
                {
                    ReadOnlyCollection<ISequence> seqs = GetPlayingSequences(this.host.Machine);

                    IPattern pat;
                    foreach (ISequence seq in seqs)
                    {
                        if (!seq.IsDisabled)
                        {
                            int playPosition = GetPositionInPattern(out pat, seq);

                            if (playPosition != -1 && !Disabled(pat.Name))
                            {
                                UpdateTargetEnvelopeValues(pat, playPosition);
                                //Global.Buzz.DCWriteLine("Playposition: " + playPosition);
                            }
                        }
                    }

                    // If playing in pattern editor
                    if (seqs.Count == 0)
                    {
                        int playPosition = GetPositionInPlayingPatternEditorPattern(out pat);
                        if (playPosition != -1 && !Disabled(pat.Name))
                        {
                            UpdateTargetEnvelopeValues(pat, playPosition);
                        }
                    }
                }

                foreach(var mac in controlChangeBuffer.Keys)
                {
                    if (mac.DLL.Info.Version >= 42)
                        mac.SendControlChanges();
                }
            }
        }

        public int GetPositionInPlayingPatternEditorPattern(out IPattern pattern)
        {
            pattern = null;
            foreach (IPattern pat in host.Machine.Patterns)
            {
                if (pat.IsPlayingSolo && pat.PlayPosition >= 0)
                {
                    pattern = pat;
                    double avgSamplesPerTick = (60.0 * host.MasterInfo.SamplesPerSec) / (host.MasterInfo.BeatsPerMin * (double)host.MasterInfo.TicksPerBeat);
                    return (int)((pat.PlayPosition * avgSamplesPerTick + host.MasterInfo.PosInTick) / (double)PatternEvent.TimeBase);
                }
                else return -1;
            }
            return -1;
        }

        private bool Disabled(string pattern)
        {
            bool ret = false;

            foreach (PatternDataData pd in MachineState.Patterns)
                if (pd != null && pd.Pattern == pattern)
                {
                    ret = pd.PatternDisabled;
                }

            return ret;
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
                if (seq.Machine.Name == machine.Name)
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
        /// Returns playing position in pattern or -1.
        /// </summary>
        /// <param name="pattern"></param>
        /// <param name="seq"></param>
        /// <returns></returns>
        public int GetPositionInPattern(out IPattern pattern, ISequence seq)
        {
            pattern = null;

            if (seq.Machine.Name == this.host.Machine.Name)
            {
                IPattern pat = seq.PlayingPattern;
                
                if (pat != null)
                {
                    pattern = pat;
                    int patternPos = pattern.PlayPosition;
                    if (patternPos >= 0)
                    {
                        double avgSamplesPerTick = (60.0 * host.MasterInfo.SamplesPerSec) / (host.MasterInfo.BeatsPerMin * (double)host.MasterInfo.TicksPerBeat);
                        return (int)(patternPos * avgSamplesPerTick / PatternEvent.TimeBase + host.MasterInfo.PosInTick);
                    }
                }
                else return -1;
            }
            return -1;
        }

        internal void ScaleTimeStamps(int envelopPatternIndex, double scale)
        {
            PatternDataData pd = MachineState.Patterns[envelopPatternIndex];
            foreach (EnvelopeParameter ep in pd.Envelopes)
            {
                if (ep.MachineName != "")
                {
                    foreach (EnvelopePoint epo in ep.EnvelopePoints)
                    {
                        epo.TimeStamp *= scale;
                    }
                }
            }
        }

        private void UpdateTargetEnvelopeValues(IPattern pattern, int playPosition)
        {
            IPattern pat = pattern;
            //if (!taskMonitorForPatterns.ContainsKey(pat))
            //{
            //	taskMonitorForPatterns.TryAdd(pat, true);
            //	var t = Task.Run(() =>
            //	{
            for (int i = 0; i < MachineState.Patterns.Length; i++)
            {
                if ((MachineState.Patterns[i] != null) && (MachineState.Patterns[i].Pattern == pat.Name))
                {
                    UpdateTargetEnvelopeValues(i, playPosition);
                    break;
                }
            }
            //		taskMonitorForPatterns.TryRemove(pat, out bool val);
            //	});
            //}
        }


        private void UpdateTargetEnvelopeValues(int patternIndex, int playPosition)
        {
            int paramIndex = 0;
            foreach (EnvelopeParameter env in this.MachineState.Patterns[patternIndex].Envelopes)
            {
                if (env.EnvelopeEnabled)
                {
                    int track = env.Track;

                    if (env.EnvelopeCurves)
                    {
                        double val = CalculateValueFromEnvelopeCurve(patternIndex, paramIndex, playPosition);
                        UpdateTargetMachineParameter(patternIndex, paramIndex, track, val);
                        //if (paramIndex == 0)
                        //	Global.Buzz.DCWriteLine("Pos: " + playPosition + ", Val: " + val);
                    }
                    else
                    {
                        List<IEnvelopePoint> volPointListLine = env.EnvelopePoints.ConvertAll<IEnvelopePoint>(x => (IEnvelopePoint)x);
                        double val = CalculateValueFromEnvelopeLine(patternIndex, paramIndex, playPosition, volPointListLine);
                        UpdateTargetMachineParameter(patternIndex, paramIndex, track, val);
                    }
                }
                paramIndex++;
            }

        }

        internal double LinToLog(double x0, double x1, double y0, double y1, double val)
        {
            if (val == 0)
                return val;

            x0 = x0 == 0 ? 1 : x0;
            y0 = y0 == 0 ? 1 : y0;

            double b = Math.Log(y1 / y0) / (x1 / x0);
            double a = y1 / Math.Exp(b * x1);

            return a * Math.Exp(b * val);
        }

        /// <summary>
        /// Scale a linear range between 0.0-1.0 to an exponential scale using the equation returnValue = A + B * Math.Exp(C * inputValue);
        /// </summary>
        /// <param name="inoutValue">The value to scale</param>
        /// <param name="midValue">The value returned for input value of 0.5</param>
        /// <param name="maxValue">The value to be returned for input value of 1.0</param>
        /// <returns></returns>
        private double ExpScale(double inputValue, double midValue, double maxValue)
        {
            double returnValue = 0;
            if (inputValue < 0 || inputValue > 1) throw new ArgumentOutOfRangeException("Input value must be between 0 and 1.0");
            if (midValue <= 0 || midValue >= maxValue) throw new ArgumentOutOfRangeException("MidValue must be greater than 0 and less than MaxValue");
            // returnValue = A + B * Math.Exp(C * inputValue);
            double M = maxValue / midValue;
            double C = Math.Log(Math.Pow(M - 1, 2));
            double B = maxValue / (Math.Exp(C) - 1);
            double A = -1 * B;
            returnValue = A + B * Math.Exp(C * inputValue);
            return returnValue;
        }

        internal double CalculateValueFromEnvelopeLine(int patternIndex, int paramIndex, int playPosition, List<IEnvelopePoint> envelopePoints)
        {
            double ret;

            double playPositionInSeconds = playPosition / (double)Global.Buzz.SelectedAudioDriverSampleRate;

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


        private void UpdateTargetMachineParameter(int patternIndex, int paramIndex, int track, double val)
        {
            var pattern = MachineState.Patterns[patternIndex];
            if (pattern != null)
            {
                string machineName = pattern.Envelopes[paramIndex].MachineName;
                string paramName = pattern.Envelopes[paramIndex].ParamName;
                int paramGroup = pattern.Envelopes[paramIndex].ParamGroup;
                bool toLog = pattern.Envelopes[paramIndex].ConvertToLogarithmic;

                var mac = host.Machine.Graph.Buzz.Song.Machines.SingleOrDefault(m => m.Name == machineName);
                if (mac != null)
                {
                    foreach (IParameter par in mac.ParameterGroups[paramGroup].Parameters)
                        if (par.Name == paramName)
                        {
                            SetParameterValue(par, track, val, toLog);
                            break;
                        }
                }
            }
        }

        internal void CopyAssignments(int from, int to)
        {
            if (from >= 0 && to >= 0)
            {
                MachineState.Patterns[to].Envelopes = new EnvelopeParameter[MachineState.Patterns[from].Envelopes.Length];
                for (int i = 0; i < MachineState.Patterns[from].Envelopes.Length; i++)
                {
                    if (MachineState.Patterns[from].Envelopes[i] != null)
                    {
                        MachineState.Patterns[to].Envelopes[i] = new EnvelopeParameter();

                        MachineState.Patterns[to].Envelopes[i].MachineName = MachineState.Patterns[from].Envelopes[i].MachineName;
                        MachineState.Patterns[to].Envelopes[i].ParamGroup = MachineState.Patterns[from].Envelopes[i].ParamGroup;
                        MachineState.Patterns[to].Envelopes[i].ParamName = MachineState.Patterns[from].Envelopes[i].ParamName;
                        MachineState.Patterns[to].Envelopes[i].Track = MachineState.Patterns[from].Envelopes[i].Track;
                    }
                    else
                    {
                        MachineState.Patterns[to].Envelopes[i] = null;
                    }
                }
            }
        }

        internal void CopyAll(int from, int to)
        {
            if (from >= 0 && to >= 0)
            {
                MachineState.Patterns[to].Envelopes = new EnvelopeParameter[MachineState.Patterns[from].Envelopes.Length];
                for (int i = 0; i < MachineState.Patterns[from].Envelopes.Length; i++)
                {
                    if (MachineState.Patterns[from].Envelopes[i] != null)
                    {
                        MachineState.Patterns[to].Envelopes[i] = MachineState.Patterns[from].Envelopes[i].Clone();

                        MachineState.Patterns[to].PatternDisabled = MachineState.Patterns[from].PatternDisabled;
                        MachineState.Patterns[to].SnapToBeat = MachineState.Patterns[from].SnapToBeat;
                        MachineState.Patterns[to].SnapToTick = MachineState.Patterns[from].SnapToTick;
                    }
                    else
                    {
                        MachineState.Patterns[to].Envelopes[i] = null;
                    }
                }
                UpdatePatternSplines(to);
            }
        }

        private void SetParameterValue(IParameter par, int track, double val, bool toLog)
        {
            int parVal = (int)(((double)par.MaxValue - (double)par.MinValue) * val + (double)par.MinValue);
            
            if (toLog)
            {
                parVal = (int)LinToLog(par.MinValue, par.MaxValue, par.MinValue, par.MaxValue, parVal);
            }

            if (par.Type == ParameterType.Note)
            {
                try
                {
                    parVal = Note.FromMIDINote(parVal).Value;
                }
                catch
                {
                    parVal = Note.Max;
                }
            }

            int do_not_record_flag = 1 << 16;
            track |= do_not_record_flag;            // Don't record
            par.SetValue(track, parVal);

            controlChangeBuffer[par.Group.Machine] = 0;

            //if (par.Group.Machine.DLL.Info.Version >= 42)
            //    par.Group.Machine.SendControlChanges();
        }

        private double CalculateValueFromEnvelopeCurve(int patternIndex, int paramIndex, int playPosition)
        {
            lock (syncLock)
            {
                double ret = 1.0;

                SplineCache splineCache;

                splineCache = GetSpline(patternIndex, paramIndex);

                float[] yValues = splineCache.YValues;
                double playPositionInSeconds = playPosition / (double)Global.Buzz.SelectedAudioDriverSampleRate;

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

                return ret;
            }
        }

        private void Song_MachineAdded(IMachine obj)
        {
            if (host.Machine == obj)
            {
                host.Machine.PatternAdded += Machine_PatternAdded;
                host.Machine.PatternRemoved += Machine_PatternRemoved;

                UpdateAllSplines();

                // Global.Buzz.MasterTap += Buzz_MasterTap;

                oldSpeed = host.MasterInfo.BeatsPerMin * host.MasterInfo.TicksPerBeat;

                // Add machines added before EB machine
                foreach (IMachine mac in Global.Buzz.Song.Machines)
                {
                    if (mac != host.Machine)
                    {
                        mac.PropertyChanged += Machine_PropertyChanged;
                        MachineAssociations.Add(mac, mac.Name);
                    }
                }
            }
            obj.PropertyChanged += Machine_PropertyChanged; // for renaming etc
            MachineAssociations.Add(obj, obj.Name);
        }

        private void Song_MachineRemoved(IMachine obj)
        {
            MachineAssociations.Remove(obj);
            obj.PropertyChanged -= Machine_PropertyChanged;

            if (host.Machine == obj)
            {
                // Global.Buzz.MasterTap -= Buzz_MasterTap;

                if (MainTask != null && !MainTask.IsCompleted)
                {
                    try
                    {
                        MainTask.Wait();
                    }
                    catch { }
                }
                host.Machine.PatternAdded -= Machine_PatternAdded;
                host.Machine.PatternRemoved -= Machine_PatternRemoved;
                host.Machine.PropertyChanged -= Machine_PropertyChanged;

                Global.Buzz.PropertyChanged -= Buzz_PropertyChanged;

                Global.Buzz.Song.MachineAdded -= Song_MachineAdded;
                Global.Buzz.Song.MachineRemoved -= Song_MachineRemoved;

                EnvelopeBlockMachine.Settings.PropertyChanged -= Settings_PropertyChanged;

                foreach (IMachine mac in Global.Buzz.Song.Machines)
                    mac.PropertyChanged -= Machine_PropertyChanged;

                MachineAssociations.Clear();
            }
            else
            {
                for (int i = 0; i < MAX_ENVELOPE_BOX_PATTERNS; i++)
                    for (int j = 0; j < MAX_ENVELOPE_BOX_PARAMS; j++)
                    {
                        if (machineState.Patterns[i] != null)
                            if (obj.Name == MachineState.Patterns[i].Envelopes[j].MachineName)
                            {
                                MachineState.Patterns[i].Envelopes[j].ParamName = "";
                                MachineState.Patterns[i].Envelopes[j].ParamGroup = 0;
                                MachineState.Patterns[i].Envelopes[j].MachineName = "";
                                MachineState.Patterns[i].Envelopes[j].Track = 0;

                                MachineState.Patterns[i].Envelopes[j].EnvelopeVisible = false;
                                MachineState.Patterns[i].Envelopes[j].EnvelopePoints.Clear();
                                UpdateEnvPoints(i, j);

                            }
                    }

                RaisePropertyReDrawAllCanvases();
                RaisePropertyUpdateAllMenus();
            }
        }

        [ParameterDecl(ValueDescriptions = new[] { "Current", "Default" }, DefValue = 0, MaxValue = 1, MinValue = 0)]
        public int NewParamValue { get; set; }

        internal void UpdateSpline(int envelopePatternIndex, int envelopeParamIndex)
        {
            lock (syncLock)
            {
                if (MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves)
                {
                    SplineCache spline = new SplineCache();
                    splineCache[envelopePatternIndex, envelopeParamIndex] = spline;

                    float min = 0.0f;
                    float max = 1.0f;

                    List<IEnvelopePoint> envPoints = MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints.ConvertAll<IEnvelopePoint>(x => (IEnvelopePoint)x);
                    spline.CreateSpline(envPoints.ToArray(), NUMBER_OF_SPLINE_POINTS_PER_SECOND);
                    spline.CutMinMax(min, max);
                }
            }
        }

        internal void UpdateAllSplines()
        {
            // Update splines
            for (int i = 0; i < MAX_ENVELOPE_BOX_PATTERNS; i++)
                for (int j = 0; j < MAX_ENVELOPE_BOX_PARAMS; j++)
                {
                    if (machineState.Patterns[i] != null)
                        if (machineState.Patterns[i].Envelopes[j].EnvelopeCurves)
                            UpdateSpline(i, j);
                }
        }

        internal void UpdatePatternSplines(int patternIndex)
        {
            if (machineState.Patterns[patternIndex] != null)
                for (int j = 0; j < MAX_ENVELOPE_BOX_PARAMS; j++)
                {

                    if (machineState.Patterns[patternIndex].Envelopes[j].EnvelopeCurves)
                        UpdateSpline(patternIndex, j);
                }
        }

        /// <summary>
        /// Sanity checks for Volume envelope.
        /// </summary>
        /// <param name="audioBlockIndex"></param>
        internal void UpdateEnvPoints(int envelopePatternIndex, int envelopeParamIndex)
        {
            double maxLength = GetPatternLenghtInSeconds(MachineState.Patterns[envelopePatternIndex].Pattern);
            List<EnvelopePoint> encPoints = MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints;
            int track = MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track;

            if (encPoints.Count < 2)
            {
                encPoints.Clear();

                EnvelopePoint ep = new EnvelopePoint();
                ep.TimeStamp = 0;
                ep.Value = GetParamDefaulValueScaled(envelopePatternIndex, envelopeParamIndex, track);
                ep.Freezed = !EnvelopeBlockMachine.Settings.StartUnfreezed;
                encPoints.Add(ep);

                ep = new EnvelopePoint();
                ep.TimeStamp = maxLength;
                ep.Value = GetParamDefaulValueScaled(envelopePatternIndex, envelopeParamIndex, track);
                ep.Freezed = !EnvelopeBlockMachine.Settings.StartUnfreezed;
                encPoints.Add(ep);
            }

            // Update first and last
            encPoints[0].TimeStamp = 0;
            encPoints[encPoints.Count - 1].TimeStamp = maxLength;

            for (int i = 1; i < encPoints.Count - 1; i++)
            {
                EnvelopePoint ep = encPoints[i];
                if (ep.TimeStamp < 0)
                {
                    encPoints.RemoveAt(i);
                    i--;
                }
                if (ep.TimeStamp > maxLength)
                {
                    encPoints.RemoveAt(i);
                    i--;
                }
            }
        }

        internal void UpdateAllEnvPoints(int envelopePatternIndex)
        {
            for (int i = 0; i < MAX_ENVELOPE_BOX_PARAMS; i++)
            {
                if (machineState.Patterns[envelopePatternIndex].Envelopes[i].MachineName != "")
                {
                    UpdateEnvPoints(envelopePatternIndex, i);
                }
            }
        }

        internal int GetPatternIndex(string name)
        {
            int ret = -1;

            for (int i = 0; i < MachineState.Patterns.Length; i++)
            {
                if (MachineState.Patterns[i] != null && MachineState.Patterns[i].Pattern == name)
                {
                    ret = i; // Found
                    break;
                }
            }

            return ret;
        }

        private int GetPatternIndexOrCreate(IPattern pat)
        {
            int ret = -1;
            int i;

            for (i = 0; i < MachineState.Patterns.Length; i++)
            {
                if (MachineState.Patterns[i] != null && MachineState.Patterns[i].Pattern == pat.Name)
                {
                    return i; // Found
                }
            }

            for (i = 0; i < MachineState.Patterns.Length; i++)
            {
                if (MachineState.Patterns[i] == null)
                {
                    CreatePatternBlock(i, pat);
                    return i;
                }
            }

            return ret;
        }

        private void CreatePatternBlock(int i, IPattern pat)
        {
            MachineState.Patterns[i] = new PatternDataData();
            MachineState.Patterns[i].Pattern = pat.Name;
        }

        public IPattern GetEnvBlockPattern(string name)
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

        internal IParameter GetParameter(int envelopePatternIndex, int envelopeParamIndex)
        {
            IParameter ret = null;
            IMachine mac = null;
            int paramGroup = MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ParamGroup;

            foreach (IMachine m in host.Machine.Graph.Buzz.Song.Machines)
            {
                if (m.Name == MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].MachineName)
                {
                    mac = m;
                    break;
                }
            }

            if (mac != null)
            {
                foreach (IParameter par in mac.ParameterGroups[paramGroup].Parameters)
                {
                    if (par.Name == MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].ParamName)
                    {
                        ret = par;
                        break;
                    }
                }
            }

            return ret;
        }

        internal void GetParamValues(int envelopePatternIndex, int envelopeParamIndex, double value, out string desc, out int paramValue)
        {
            desc = "";
            paramValue = 0;

            IParameter par = GetParameter(envelopePatternIndex, envelopeParamIndex);
            string machineName = MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].MachineName;
            int track = MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].Track;

            if (par != null)
            {
                desc = machineName + " | Track: " + track + " | " + par.Name;
                paramValue = GetParamValueFromDouble(par, value);
            }
        }

        internal double GetParamDefaulValueScaled(int envelopePatternIndex, int envelopeParamIndex, int track)
        {
            double ret = 0;

            IParameter par = GetParameter(envelopePatternIndex, envelopeParamIndex);

            if (par != null)
            {
                int parValue = NewParamValue == 1 ? par.DefValue : par.GetValue(track);
                parValue = parValue > par.MaxValue ? par.MaxValue : parValue;
                ret = ((double)parValue - (double)par.MinValue) / (double)(par.MaxValue - (double)par.MinValue);
            }
            return ret;
        }

        internal int GetParamValueFromDouble(IParameter par, double value)
        {
            double min = par.MinValue;
            double max = par.MaxValue;

            return (int)(min + (max - min) * value);
        }

        internal double GetParamValueFromInt(IParameter par, int value)
        {
            double min = par.MinValue;
            double max = par.MaxValue;

            return (double)(value - min) / (max - min);
        }

        /// <summary>
        /// Return pattern lenght in seconds.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public double GetPatternLenghtInSeconds(string name)
        {
            double ret = 0;
            IPattern pat = GetEnvBlockPattern(name);
            if (pat != null)
            {
                double ticksPerSecond = (double)host.MasterInfo.BeatsPerMin / 60.0 * (double)host.MasterInfo.TicksPerBeat;
                ret = (double)pat.Length / ticksPerSecond;
            }

            return ret;
        }

        public Canvas PrepareCanvasForSequencer(IPattern pat, SequencerLayout layout, double tickHeight, int time, double width, double height)
        {
            EnvelopeCanvas ec = null;

            if (pat.Machine == host.Machine)
            {
                int patternIndex = GetPatternIndexOrCreate(pat);
                ec = new EnvelopeCanvas(this, patternIndex, layout, width, height, tickHeight, time, GetPatternLenghtInSeconds(pat.Name));
                ec.DrawBackground = true;
            }
            return ec;
        }


        // actual machine ends here. the stuff below demonstrates some other features of the api.
        public class EnvelopeParameter
        {
            private string paramName;
            private int paramGroup;
            private List<EnvelopePoint> envelopePoints;
            private bool envelopeEnabled;
            private bool envelopeVisible;
            private bool envelopeCurves;
            private bool convertToLogarithmic;
            private string machineName;

            public List<EnvelopePoint> EnvelopePoints { get => envelopePoints; set => envelopePoints = value; }
            public bool EnvelopeVisible { get => envelopeVisible; set => envelopeVisible = value; }
            public bool EnvelopeCurves { get => envelopeCurves; set => envelopeCurves = value; }
            public bool EnvelopeEnabled { get => envelopeEnabled; set => envelopeEnabled = value; }
            public string MachineName { get => machineName; set => machineName = value; }
            public string ParamName { get => paramName; set => paramName = value; }
            public int ParamGroup { get => paramGroup; set => paramGroup = value; }
            public int Track { get; set; }
            public bool ConvertToLogarithmic { get => convertToLogarithmic; set => convertToLogarithmic = value; }

            public EnvelopeParameter()
            {
                EnvelopePoints = new List<EnvelopePoint>();
                envelopeVisible = false;
                envelopeEnabled = false;
                envelopeCurves = false;
                convertToLogarithmic = false;
                machineName = "";
                Track = 0;
            }

            internal EnvelopeParameter Clone()
            {
                EnvelopeParameter epar = new EnvelopeParameter();

                foreach (EnvelopePoint epoint in EnvelopePoints)
                {
                    epar.EnvelopePoints.Add(epoint.Clone());
                }

                epar.EnvelopeCurves = this.EnvelopeCurves;
                epar.ConvertToLogarithmic = this.ConvertToLogarithmic;
                epar.EnvelopeEnabled = this.EnvelopeEnabled;
                epar.EnvelopeVisible = this.EnvelopeVisible;
                epar.MachineName = this.MachineName;
                epar.ParamGroup = this.ParamGroup;
                epar.ParamName = this.ParamName;
                epar.Track = this.Track;

                return epar;
            }
        }

        // This holds data for parameter to automate
        public class PatternDataData
        {
            private bool patternDisabled;
            public EnvelopeParameter[] envelopes = new EnvelopeParameter[MAX_ENVELOPE_BOX_PARAMS];
            private bool snapToTick;
            private bool snapToBeat;
            private string pattern;

            public PatternDataData()
            {
                snapToTick = false;
                snapToBeat = false;
                patternDisabled = false;

                for (int i = 0; i < envelopes.Length; i++)
                {
                    envelopes[i] = new EnvelopeParameter();
                }
            }

            public EnvelopeParameter[] Envelopes { get => envelopes; set => envelopes = value; }
            public bool SnapToTick { get => snapToTick; set => snapToTick = value; }
            public bool SnapToBeat { get => snapToBeat; set => snapToBeat = value; }
            public string Pattern { get => pattern; set => pattern = value; }
            public bool PatternDisabled { get => patternDisabled; set => patternDisabled = value; }
        }


        public class State : INotifyPropertyChanged
        {
            public State()
            {
                // for (int i = 0; i < patterns.Length; i++)
                //	patterns[i] = new MacData();
            }   // NOTE: parameterless constructor is required by the xml serializer

            private PatternDataData[] patterns = new PatternDataData[MAX_ENVELOPE_BOX_PATTERNS];

            public PatternDataData[] Patterns { get => patterns; set => patterns = value; }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        State machineState = new State();
        public State MachineState           // a property called 'MachineState' gets automatically saved in songs and presets
        {
            get { return machineState; }
            set
            {
                machineState = value;

                // If needs to be extended in the future
                foreach (PatternDataData pd in machineState.Patterns)
                    if (pd != null && pd.Envelopes.Length != MAX_ENVELOPE_BOX_PARAMS)
                    {
                        int oldLenght = pd.Envelopes.Length;
                        Array.Resize(ref pd.envelopes, MAX_ENVELOPE_BOX_PARAMS);
                        for (int i = oldLenght; i < MAX_ENVELOPE_BOX_PARAMS; i++)
                        {
                            pd.envelopes[i] = new EnvelopeParameter();

                        }
                    }

                // Start freezed
                foreach (PatternDataData pd in machineState.Patterns)
                    if (pd != null)
                    {
                        foreach (EnvelopeParameter epar in pd.Envelopes)
                            if (epar != null)
                            {
                                foreach (EnvelopePoint ep in epar.EnvelopePoints)
                                    ep.Freezed = !EnvelopeBlockMachine.Settings.StartUnfreezed;
                            }
                    }
            }
        }

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
            else if (e.PropertyName == "Name")
            {
                IMachine mac = (IMachine)sender;
                string oldName = MachineAssociations[mac];
                MachineAssociations[mac] = mac.Name; // New name

                for (int i = 0; i < MAX_ENVELOPE_BOX_PATTERNS; i++)
                    for (int j = 0; j < MAX_ENVELOPE_BOX_PARAMS; j++)
                    {
                        if (machineState.Patterns[i] != null)
                            if (oldName == MachineState.Patterns[i].Envelopes[j].MachineName)
                            {
                                MachineState.Patterns[i].Envelopes[j].MachineName = mac.Name;
                                //RaisePropertyReDrawAllCanvases();
                            }
                    }

                RaisePropertyUpdateAllMenus();
            }
        }

        private void PatternRenamed(string oldName, string newName)
        {
            for (int i = 0; i < MachineState.Patterns.Length; i++)
            {
                if (MachineState.Patterns[i].Pattern == oldName)
                {
                    MachineState.Patterns[i].Pattern = newName;
                    break;
                }
            }
        }

        public void RemovePattern(string name)
        {
            for (int i = 0; i < MachineState.Patterns.Length; i++)
            {
                if (MachineState.Patterns[i] != null && MachineState.Patterns[i].Pattern == name)
                {
                    MachineState.Patterns[i] = null;
                    break;
                }
            }
        }

        private void Machine_PatternRemoved(IPattern obj)
        {
            RemovePattern(obj.Name);
            PatternAssociations.Remove(obj);

            obj.PropertyChanged -= Pattern_PropertyChanged;
        }

        private void Pattern_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Length")
            {
                IPattern pat = sender as IPattern;
                int index = GetPatternIndex(pat.Name);

                if (index >= 0)
                {
                    //UpdateAllEnvPoints(index);
                    //RaisePropertyReDrawCanvas(index);
                }
            }
        }

        private void Machine_PatternAdded(IPattern obj)
        {
            PatternAssociations.Add(obj, obj.Name);

            obj.PropertyChanged += Pattern_PropertyChanged;
        }

        public IEnumerable<IMenuItem> Commands
        {
            get
            {
                yield return new MenuItemVM()
                {
                    Text = "About...",
                    Command = new SimpleCommand()
                    {
                        CanExecuteDelegate = p => true,
                        ExecuteDelegate = p => MessageBox.Show("Envelope Block Machine 0.9.4 (C) 2024 WDE")
                    }
                };
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal SplineCache GetSpline(int envelopePatternIndex, int envelopeParamIndex)
        {
            return splineCache[envelopePatternIndex, envelopeParamIndex];
        }

        internal void NotifyBuzzDataChanged()
        {
            int val = host.Machine.ParameterGroups[0].Parameters[0].GetValue(0);
            host.Machine.ParameterGroups[0].Parameters[0].SetValue(0, val);
        }

        internal void RaisePropertyChangedEnv(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "EnvelopeChanged");
        }

        internal void RaisePropertyChangedVisibility(EnvelopeLayer envelopeLayer)
        {
            PropertyChanged.Raise(envelopeLayer, "EnvelopeVisibility");
        }

        internal void ResetEnvPoints(int envelopePatternIndex, int envelopeParamIndex)
        {
            bool state = MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves;

            MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves = false;
            MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopePoints.Clear();
            UpdateSpline(envelopePatternIndex, envelopeParamIndex);
            MachineState.Patterns[envelopePatternIndex].Envelopes[envelopeParamIndex].EnvelopeCurves = state;
        }

        internal void RaisePropertyChangedEnvBoxAdded(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "EnvelopeBoxAdded");
        }

        internal void RaisePropertyChangedEnvBoxRemoved(EnvelopeBox evb)
        {
            PropertyChanged.Raise(evb, "EnvelopeBoxRemoved");
        }

        internal void RaisePropertyReDraw(EnvelopeLayer envelopeLayer)
        {
            PropertyChanged.Raise(envelopeLayer, "EnvelopeReDraw");
        }

        internal void RaisePropertyUnassign(EnvelopeLayer envelopeLayer)
        {
            PropertyChanged.Raise(envelopeLayer, "Unassign");
        }
        internal void RaisePropertyUpdateMenu(EnvelopeCanvas envelopeCanvas)
        {
            PropertyChanged.Raise(envelopeCanvas, "UpdateMenu");
        }

        internal void RaisePropertyUpdateAllMenus()
        {
            PropertyChanged.Raise(this, "UpdateAllMenus");
        }

        internal void RaisePropertyReDrawCanvas(int patternIndex)
        {
            EnvelopeBlockEvent ebe = new EnvelopeBlockEvent();
            ebe.envelopPatternIndex = patternIndex;
            PropertyChanged.Raise(ebe, "ReDrawCanvas");
        }

        internal void RaisePropertyReDrawAllCanvases()
        {
            PropertyChanged.Raise(this, "ReDrawAllCanvases");
        }

        //IActionStack EBActionStack { get; set; }

        //public void ActionStack(IActionStack actionStack)
        //      {
        //	EBActionStack = actionStack;
        //}

        //void Do(IAction a)
        //{
        //	try
        //	{
        //		if (EBActionStack != null) EBActionStack.Do(a);
        //	}
        //	catch { }
        //}
    }

    // Helper interface
    public interface IEnvelopePoint
    {
        double TimeStamp { get; set; }
        double Value { get; set; }
    }


    // Here we save all the Volume Envelope time stamps and values. Could use struct, but class it is.
    public class EnvelopePoint : IEnvelopePoint
    {
        double timeStamp;
        double value;
        //bool freezed;

        public double TimeStamp {
            get => timeStamp; 
            set => timeStamp = value;
        }
        public double Value { get => value; set => this.value = value; }
        public bool Freezed { get; set; }

        public EnvelopePoint()
        {
            TimeStamp = -1;
            Value = 0;
            Freezed = true;
        }

        internal EnvelopePoint Clone()
        {
            EnvelopePoint ep = new EnvelopePoint();
            ep.TimeStamp = timeStamp;
            ep.Value = Value;
            ep.Freezed = Freezed;
            return ep;
        }
    }

    public class EnvelopeBlockEvent : INotifyPropertyChanged
    {
        public int envelopPatternIndex;
        public event PropertyChangedEventHandler PropertyChanged;
    }

    public class MachineGUIFactory : IMachineGUIFactory { public IMachineGUI CreateGUI(IMachineGUIHost host) { return new EnvelopeMachineGUI(); } }
    public class EnvelopeMachineGUI : UserControl, IMachineGUI
    {
        IMachine machine;
        EnvelopeBlockMachine envelopeBlockMachine;

        public IMachine Machine
        {
            get { return machine; }
            set
            {
                if (machine != null)
                {
                }

                machine = value;

                if (machine != null)
                {
                    envelopeBlockMachine = (EnvelopeBlockMachine)machine.ManagedMachine;
                }
            }
        }

        public EnvelopeMachineGUI()
        {
        }
    }

}
