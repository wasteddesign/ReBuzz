using BuzzGUI.Interfaces;
using ReBuzz.Core;
using System;
using System.Collections.Generic;

namespace ReBuzz.Audio
{
    // Prototype (#107): per-topology cached work order.
    //
    // Replaces the per-chunk O(N*D) re-scan performed by
    // Collect{Editor,Control,}MachinesThatCanWork (one full pass per drain
    // iteration, D iterations per chunk) with an O(N) walk of a layered order
    // that is rebuilt only when the graph topology changes.
    //
    // Faithfulness to the legacy walk (see WorkManager.HandleWorkAlgorithm2 /
    // HandleWorkAlgorithmSingleThread):
    //   * Editors (hidden control machines) run first, then controls. Both are
    //     run unconditionally (no input gating), exactly as the two pre-passes.
    //   * Ready is NOT baked into membership: a native crash can clear Ready
    //     mid-buffer, so Ready is re-tested when the prefix is walked (the
    //     legacy control/editor collects test Ready every wave). The audio
    //     waves do NOT test Ready, matching CollectMachinesThatCanWork which
    //     has no Ready check.
    //   * Audio waves are Kahn layers over the master-reachable cone, EXCLUDING
    //     control/editor machines (worked in the prefix) and Master itself
    //     (a pure sink: ReadWork reads master.GetStereoSamples, master.Work is
    //     never dispatched, and the legacy drain breaks before reaching it).
    //   * Within-wave ordering is left to the caller (kept dynamic, sorted by
    //     performanceLastCount each chunk). It is numerically neutral: a machine
    //     sums its inputs in connection-list order, independent of the order its
    //     siblings were dispatched in, so output is bit-identical to the legacy
    //     walk for the same algorithm.
    //
    // Master is identified as MachinesList[0], mirroring ReadWork.
    internal sealed class CachedWorkOrder
    {
        public MachineCore[] EditorPrefix = Array.Empty<MachineCore>();
        public MachineCore[] ControlPrefix = Array.Empty<MachineCore>();
        public MachineCore[][] AudioWaves = Array.Empty<MachineCore[]>();

        // Invalidation keys. BuiltGeneration tracks connection add/remove
        // (ReBuzzCore.TopologyGeneration); BuiltMachineCount catches machine
        // add/remove without hunting every MachinesList.Add site.
        public long BuiltGeneration = long.MinValue;
        public int BuiltMachineCount = -1;

        // If a cycle is detected during the Kahn drain, the caller must fall
        // back to the legacy recursive walk for this build. (The legacy
        // CollectMachinesThatCanWork would stack-overflow on a cycle; the build
        // detects it deterministically instead.)
        public bool HasCycle;

        private static bool IsControlOrEditor(MachineCore m)
            => m.DLL.Info.Flags.HasFlag(MachineInfoFlags.CONTROL_MACHINE);

        public void Rebuild(SongCore song)
        {
            HasCycle = false;

            var machines = song.MachinesList;
            BuiltMachineCount = machines.Count;

            // --- Prefix partitions (editors = hidden controls, then controls) ---
            var editors = new List<MachineCore>();
            var controls = new List<MachineCore>();
            foreach (var m in machines)
            {
                if (!IsControlOrEditor(m))
                    continue;
                if (m.Hidden)
                    editors.Add(m);
                else
                    controls.Add(m);
            }
            EditorPrefix = editors.ToArray();
            ControlPrefix = controls.ToArray();

            if (machines.Count == 0)
            {
                AudioWaves = Array.Empty<MachineCore[]>();
                return;
            }

            var master = machines[0];

            // --- 1. Master-reachable audio cone (via AllInputs) ---
            // Descend through everything so audio machines sitting behind a
            // control machine are still reachable, but only audio machines
            // become work nodes. Master is never added to the cone.
            var cone = new HashSet<MachineCore>();
            var seen = new HashSet<MachineCore> { master };
            var stack = new Stack<MachineCore>();
            stack.Push(master);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                foreach (var input in cur.AllInputs)
                {
                    if (!(input.Source is MachineCore src))
                        continue;
                    if (!seen.Add(src))
                        continue;
                    stack.Push(src);
                    if (!IsControlOrEditor(src))
                        cone.Add(src);
                }
            }

            // --- 2. Kahn layering over the cone ---
            // in-degree counts only edges whose source is itself an audio node
            // in the cone (control inputs are pre-satisfied by the prefix;
            // edges from master/out-of-cone do not occur for valid graphs and
            // are ignored). Parallel edges (multi-channel) are counted and
            // decremented symmetrically.
            var indeg = new Dictionary<MachineCore, int>(cone.Count);
            foreach (var m in cone)
            {
                int d = 0;
                foreach (var input in m.AllInputs)
                {
                    if (input.Source is MachineCore src && cone.Contains(src))
                        d++;
                }
                indeg[m] = d;
            }

            var waves = new List<MachineCore[]>();
            var frontier = new List<MachineCore>();
            foreach (var kv in indeg)
            {
                if (kv.Value == 0)
                    frontier.Add(kv.Key);
            }

            int placed = 0;
            while (frontier.Count > 0)
            {
                waves.Add(frontier.ToArray());
                placed += frontier.Count;

                var next = new List<MachineCore>();
                foreach (var m in frontier)
                {
                    foreach (var output in m.AllOutputs)
                    {
                        if (!(output.Destination is MachineCore dst))
                            continue;
                        if (!cone.Contains(dst))
                            continue;
                        if (--indeg[dst] == 0)
                            next.Add(dst);
                    }
                }
                frontier = next;
            }

            // Unresolved in-degrees => at least one cycle in the cone.
            if (placed != cone.Count)
                HasCycle = true;

            AudioWaves = waves.ToArray();
        }
    }
}
