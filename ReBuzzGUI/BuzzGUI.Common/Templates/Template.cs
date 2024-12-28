using BuzzGUI.Common.InterfaceExtensions;
using BuzzGUI.Interfaces;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Xml;
using System.Xml.Serialization;

namespace BuzzGUI.Common.Templates
{
    public enum TemplatePatternMode { NoPatterns, PatternsOnly, PatternsAndSequences };
    public enum TemplateWavetableMode { NoWavetable, WaveRefsOnly, WaveFiles };

    public class Template
    {
        public List<Machine> Machines;
        public List<Connection> Connections;
        public List<Sequence> Sequences;
        public List<Wave> Wavetable;
        public Markers Markers;

        [XmlIgnore]
        public string Path;

        public Template() { }
        public Template(IEnumerable<IMachine> machines, TemplatePatternMode patternmode, TemplateWavetableMode wtmode)
        {
            var song = machines.First().Graph as ISong;

            Machines = machines.Select(m => new Machine(m, patternmode != TemplatePatternMode.NoPatterns)).ToList();
            Connections = machines.SelectMany(m => m.Inputs.Where(c => machines.Contains(c.Source))).Select(c => new Connection(c)).ToList();
            if (patternmode == TemplatePatternMode.PatternsAndSequences) Sequences = song.Sequences.Where(s => machines.Contains(s.Machine)).Select(s => new Sequence(s)).ToList();
            if (wtmode != TemplateWavetableMode.NoWavetable) Wavetable = song.Wavetable.Waves.Where(w => w != null).Select(w => new Wave(w)).ToList();
            Markers = new Markers(song);
        }

        public void Save(Stream output)
        {
            var ws = new XmlWriterSettings() { NamespaceHandling = System.Xml.NamespaceHandling.OmitDuplicates, NewLineOnAttributes = false, Indent = true };
            var w = XmlWriter.Create(output, ws);
            var s = new XmlSerializer(GetType());
            s.Serialize(w, this);
            w.Close();
        }

        // NOTE: changes layer paths in the template
        public void SaveZip(IWavetable wt, string filename)
        {
            var templatename = ZipEntry.CleanName(System.IO.Path.GetFileNameWithoutExtension(filename) + ".xml");

            if (Wavetable != null)
            {
                var usednames = new HashSet<string>();

                foreach (var w in Wavetable)
                {
                    foreach (var l in w.Layers)
                    {
                        l.Path = Enumerable.Range(0, 100).Select(n => w.Name + '_' + n.ToString("D2") + ".wav").Where(x => !usednames.Contains(x)).First();
                        usednames.Add(l.Path);
                    }
                }
            }

            using (var ofs = File.Create(filename))
            {
                var buffer = new byte[4096];

                var zipStream = new ZipOutputStream(ofs);
                zipStream.IsStreamOwner = false;
                zipStream.UseZip64 = UseZip64.Off;
                zipStream.SetLevel(6);

                var zipentry = new ZipEntry(templatename);
                zipentry.DateTime = DateTime.Now;

                zipStream.PutNextEntry(zipentry);
                Save(zipStream);
                zipStream.CloseEntry();

                if (Wavetable != null)
                {
                    foreach (var w in Wavetable)
                    {
                        int lindex = 0;

                        foreach (var l in w.Layers)
                        {
                            var il = wt.Waves[w.Index].Layers[lindex++];

                            zipentry = new ZipEntry(l.Path);
                            zipentry.DateTime = DateTime.Now;

                            var ms = new MemoryStream();
                            il.SaveAsWAV(ms);
                            ms.Position = 0;

                            zipStream.PutNextEntry(zipentry);
                            StreamUtils.Copy(ms, zipStream, buffer);
                            zipStream.CloseEntry();
                        }
                    }
                }

                zipStream.Close();
            }
        }

        public static Template Load(Stream input)
        {
            var s = new XmlSerializer(typeof(Template));

            var r = XmlReader.Create(input);
            object o = s.Deserialize(r);
            r.Close();

            var t = o as Template;
            return t;
        }

        public static Template LoadFromString(string text)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var t = Template.Load(ms);
            return t;
        }

        public static Template Load(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                if (System.IO.Path.GetExtension(path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var zf = new ZipFile(fs);
                    var zipentry = zf.Cast<ZipEntry>().Where(e => e.IsFile && System.IO.Path.GetExtension(e.Name).Equals(".xml", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (zipentry == null) return null;
                    var t = Load(zf.GetInputStream(zipentry));
                    if (t != null) t.Path = path;
                    zf.Close();
                    return t;
                }
                else
                {
                    var t = Load(fs);
                    if (t != null) t.Path = path;
                    return t;
                }
            }
        }

        string RenameMachine(string name, IMachineGraph graph)
        {
            // NOTE: must load dlls first if we want to use MachineInfo.ShortName to derive the name
            var names = graph.Machines.Select(m => m.Name);
            if (!names.Contains(name)) return name;
            return Enumerable.Range(2, 1000).Select(n => name + n.ToString()).Where(x => !names.Contains(x)).First();
        }

        public Machine SourceMachine
        {
            get
            {
                // machines are rotated if there is only one source machine
                return Machines.Where(m => m.Name != "Master").Where(m => !Connections.Where(c => c.Destination == m.Name).Any()).OnlyOrDefault();
            }
        }


        public IEnumerable<IMachine> Paste(IMachineGraph graph, Point pastepos, IMachine machineToReplace = null)
        {
            var song = graph as ISong;
            bool songwasempty = song.Machines.Count == 1;
            var masterless = Machines.Where(m => m.Name != "Master");
            var tmaster = Machines.Where(m => m.Name == "Master").FirstOrDefault();

            foreach (var m in masterless)
            {
                if (!graph.Buzz.MachineDLLs.ContainsKey(m.Preset.Machine))
                    throw new Exception(string.Format("Machine missing: {0}", m.Preset.Machine));
            }

            var sourceMachine = SourceMachine;
            var machinesToCreate = machineToReplace != null ? masterless.Where(m => m != sourceMachine) : masterless;

            var rename = machinesToCreate.ToDictionary(k => k.Name, v => RenameMachine(v.Name, graph));

            graph.BeginImport(rename);

            var mlist = new List<IMachine>();

            try
            {

                using (new ActionGroup(graph))
                {
                    var map = new Dictionary<Machine, IMachine>();
                    var machinesbyoldname = new Dictionary<string, IMachine>();
                    var master = graph.Machines.First(m => m.Name == "Master");
                    machinesbyoldname["Master"] = master;
                    var masterPosition = new Point(master.Position.Item1, master.Position.Item2);

                    var tmasterPosition = tmaster != null ? new Point(tmaster.PositionX, tmaster.PositionY) : masterPosition;

                    if (machineToReplace != null)
                    {
                        sourceMachine.Preset.Apply(machineToReplace, false);

                        if (machineToReplace.DLL.Info.Flags.HasFlag(MachineInfoFlags.LOAD_DATA_RUNTIME))
                            machineToReplace.Data = sourceMachine.Data;

                        machineToReplace.SendControlChanges();
                        machinesbyoldname[sourceMachine.Name] = machineToReplace;
                    }

                    foreach (var m in machinesToCreate)
                    {
                        Point p;
                        if (sourceMachine != null && !double.IsNaN(pastepos.X))
                        {
                            var sp = new Point(sourceMachine.PositionX, sourceMachine.PositionY);
                            var deltaangle = Vector.AngleBetween(pastepos - masterPosition, sp - tmasterPosition);
                            var spr = (sp - tmasterPosition).Length;
                            var deltaradius = spr > 0 ? (pastepos - masterPosition).Length / spr : 0;

                            var mp = new Point(m.PositionX, m.PositionY);
                            var mangle = Vector.AngleBetween(mp - tmasterPosition, new Vector(0, 1));
                            var mradius = (mp - tmasterPosition).Length;
                            mangle += deltaangle;
                            mradius *= deltaradius;
                            p = masterPosition + mradius * new Vector(Math.Sin(mangle * Math.PI / 180.0), Math.Cos(mangle * Math.PI / 180.0));
                        }
                        else
                        {
                            p = new Point(m.PositionX + 0.025, m.PositionY + 0.025);
                        }

                        graph.CreateMachine(m.Preset.Machine, "", rename[m.Name], m.Data, m.PatternEditor != null ? m.PatternEditor : "", m.PatternEditorData,
                            m.TrackCount > 0 ? m.TrackCount : -1, (float)p.X, (float)p.Y);
                        var newm = graph.Machines.Last();

                        //var tpg = newm.ParameterGroups.Where(pg => pg.Type == ParameterGroupType.Track).FirstOrDefault();
                        //if (tpg != null && m.TrackCount > 0) tpg.TrackCount = m.TrackCount;

                        mlist.Add(newm);
                        map[m] = newm;
                        machinesbyoldname[m.Name] = newm;
                    }

                    foreach (var c in Connections)
                        graph.ConnectMachines(machinesbyoldname[c.Source], machinesbyoldname[c.Destination], c.SourceChannel, c.DestinationChannel, c.Amp, c.Pan);

                    foreach (var m in map)
                    {
                        m.Key.Preset.Apply(m.Value, false);
                        m.Value.SendControlChanges();
                    }

                    if (songwasempty && tmaster != null)
                    {
                        tmaster.Preset.Apply(master, false);
                        master.SendControlChanges();
                    }

                    foreach (var m in map)
                    {
                        if (m.Key.Patterns != null)
                        {
                            foreach (var p in m.Key.Patterns)
                            {
                                m.Value.CreatePattern(p.Name, p.Length);
                                var pat = m.Value.Patterns.Where(q => q.Name == p.Name).First();

                                foreach (var c in p.Columns)
                                {
                                    if (c.Machine != null)
                                    {
                                        IMachine tm = null;

                                        if (machinesbyoldname.ContainsKey(c.Machine))
                                            tm = machinesbyoldname[c.Machine];

                                        pat.InsertColumn(pat.Columns.Count, tm);
                                        pat.Columns.Last().SetEvents(c.EnumerableEvents, true);
                                    }

                                }
                            }
                        }
                    }

                    if (Sequences != null)
                    {
                        foreach (var s in Sequences)
                        {
                            if (s.Machine == "Master") continue;    // TODO: paste master patterns and sequences if the song doesn't currently have them
                            if (machineToReplace != null && s.Machine == sourceMachine.Name) continue;

                            var m = machinesbyoldname[s.Machine];
                            song.AddSequence(m, song.Sequences.Count);
                            var ns = song.Sequences.Last();
                            foreach (var e in s.Events)
                            {
                                if (e.Type == SequenceEventType.PlayPattern)
                                    ns.SetEvent(e.Time, new SequenceEvent(e.Type, m.Patterns.First(p => p.Name == e.Pattern)));
                                else
                                    ns.SetEvent(e.Time, new SequenceEvent(e.Type));
                            }
                        }
                    }

                    if (Markers != null)
                    {
                        song.LoopEnd = Math.Max(song.LoopEnd, Markers.LoopEnd);
                        song.LoopStart = Math.Min(song.LoopStart, Markers.LoopStart);
                        song.SongEnd = Math.Max(song.SongEnd, Markers.SongEnd);
                    }

                    if (Wavetable != null)
                    {
                        var remap = new Dictionary<int, int>();

                        foreach (var w in Wavetable)
                        {
                            int index = AllocateWave(song, w);
                            if (index < 0) continue;
                            if (w.Index != index) remap[w.Index] = index;

                            if (song.Wavetable.Waves[index] == null)
                            {
                                foreach (var l in w.Layers)
                                {
                                    TryLoadWave(song, index, w, l);

                                    IWave iw = song.Wavetable.Waves[index];
                                    if (iw == null) continue;

                                    iw.Flags = (WaveFlags)w.Flags;
                                    iw.Volume = w.Volume;

                                    IWaveLayer il = iw.Layers.Last();
                                    il.RootNote = l.RootNote;
                                    il.SampleRate = l.SampleRate;
                                    il.LoopStart = l.LoopStart;
                                    il.LoopEnd = l.LoopEnd;
                                }
                            }
                        }

                        if (remap.Count > 0)
                        {
                            foreach (var m in mlist)
                                foreach (var p in m.Patterns)
                                    p.UpdateWaveReferences(remap);
                        }

                    }
                }
            }
            finally
            {
                graph.EndImport();
            }

            return mlist;
        }

        int AllocateWave(ISong song, Wave w)
        {
            var matching = Enumerable.Range(0, 200).Where(i => song.Wavetable.Waves[i] != null && w.Match(song.Wavetable.Waves[i]));
            if (matching.Any()) return matching.First();                // the same wave is already loaded

            if (song.Wavetable.Waves[w.Index] == null) return w.Index;  // use the original slot if it's not allocated

            var freeslots = Enumerable.Range(0, 200).Where(i => song.Wavetable.Waves[i] == null);
            if (!freeslots.Any()) return -1;
            return freeslots.First();
        }

        void TryLoadWave(ISong song, int index, Wave w, WaveLayer l)
        {
            if (System.IO.Path.GetExtension(Path).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            {
                using (var zf = new ZipFile(Path))
                {
                    var ientry = zf.FindEntry(l.Path, true);
                    if (ientry <= 0) return;
                    var ms = new MemoryStream();
                    StreamUtils.Copy(zf.GetInputStream(ientry), ms, new byte[4096]);
                    ms.Position = 0;
                    song.Wavetable.LoadWaveEx(index, ms, l.Path, w.Name, w.Layers.Count > 0);
                }

            }
            else
            {
                var path = l.Path;
                if (!File.Exists(path) && Path != null)
                    path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Path), System.IO.Path.GetFileName(l.Path));
                if (!File.Exists(path))
                {
                    MessageBox.Show(string.Format("Can't find wave '{0}' (also tried '{1}')", l.Path, path), "Buzz", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                song.Wavetable.LoadWaveEx(index, l.Path, w.Name, w.Layers.Count > 0);
            }
        }

        // get list of machines quickly without reading the whole document
        public static IEnumerable<string> GetMachines(string filename)
        {
            using (var r = XmlReader.Create(filename))
            {
                r.MoveToContent();
                while (r.Read())
                {
                    if (r.NodeType == XmlNodeType.Element && r.Name == "Machines")
                    {
                        r.MoveToElement();
                        while (r.Read())
                        {
                            if (r.NodeType == XmlNodeType.Element && r.Name == "Machine")
                            {
                                r.MoveToElement();
                                while (r.Read())
                                {
                                    if (r.NodeType == XmlNodeType.Element && r.Name == "Preset")
                                    {
                                        while (r.MoveToNextAttribute())
                                        {
                                            if (r.Name == "Machine")
                                            {
                                                r.ReadAttributeValue();
                                                if (r.NodeType == XmlNodeType.Text)
                                                    yield return r.Value;

                                                break;
                                            }
                                        }

                                        break;
                                    }
                                }
                            }
                        }
                        yield break;
                    }
                }
            }

        }

        static string cachedValidTemplateString;
        static string cachedInvalidTemplateString;

        public static bool IsValidTemplateString(string s)
        {
            if (s == cachedValidTemplateString)
                return true;
            else if (s == cachedInvalidTemplateString)
                return false;

            try
            {
                using (var r = XmlReader.Create(new StringReader(s)))
                {
                    r.MoveToContent();
                    if (r.Name == "Template")
                    {
                        cachedValidTemplateString = s;
                        return true;
                    }
                }

            }
            catch { }

            cachedInvalidTemplateString = s;
            return false;
        }

    }
}
