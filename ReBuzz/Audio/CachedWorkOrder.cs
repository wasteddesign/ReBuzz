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

        // --- Dependency graph for the dependency-driven dispatch (#107) -----------
        // The Kahn layering below already computes in-degrees and walks successors, then
        // throws that structure away and keeps only the flattened levels. Keeping it lets
        // the dispatcher start a machine the instant ITS OWN inputs are done, rather than
        // when its whole level is done: one barrier per buffer instead of one per level.
        //
        // Dense cone indices 0..Cone.Length-1. Same membership and edge semantics as
        // AudioWaves (Master excluded - a pure sink; controls/editors excluded - they are
        // prefix-worked).
        //
        // Indeg counts one per in-cone input EDGE and Succ holds one entry per in-cone
        // output EDGE, so parallel (multi-channel) connections between the same pair
        // appear repeatedly in BOTH - exactly as the Kahn drain counts and decrements
        // them. Any asymmetry would leave a successor's countdown permanently above zero
        // and hang the buffer, so both are built from the same edge enumeration.
        //
        // Populated only when !HasCycle (the cyclic case falls back to the legacy walk).
        public MachineCore[] Cone = Array.Empty<MachineCore>();
        public int[] Indeg = Array.Empty<int>();
        public int[][] Succ = Array.Empty<int[]>();
        public int[] Seed = Array.Empty<int>();

        // Set if the graph fails to reproduce AudioWaves (see SelfCheck). The dispatcher
        // refuses the dependency-driven path when this is set and uses the wave path,
        // which is unchanged.
        public bool SelfCheckFailed;

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
                Cone = Array.Empty<MachineCore>();
                Indeg = Array.Empty<int>();
                Succ = Array.Empty<int[]>();
                Seed = Array.Empty<int>();
                return;
            }

            var master = machines[0];

            // --- 1. Master-reachable audio cone (via AllInputs) ---
            // Walk the audio graph from Master, STOPPING at control/editor
            // machines. They are worked in the prefix, and the legacy
            // CollectMachinesThatCanWork stops recursing at any workDone machine
            // (controls are workDone from the prefix) - so an audio machine
            // reachable ONLY through a control is left unworked by the legacy
            // walk. We match that exactly rather than working a superset: descend
            // through audio machines only. Master is never added to the cone.
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
                    if (IsControlOrEditor(src))
                        continue; // do not descend through controls (prefix-worked)
                    stack.Push(src);
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

            // Snapshot the initial in-degrees: the Kahn drain below decrements indeg to
            // zero as it places each layer, destroying them.
            var indeg0 = new Dictionary<MachineCore, int>(indeg);

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

            // --- 3. Dependency graph -------------------------------------------
            // Same cone, same edges, same multiplicity as the layering above - kept as a
            // graph instead of flattened. Skipped on a cycle (the caller falls back to the
            // legacy recursive walk; a cyclic graph could never drain anyway).
            SelfCheckFailed = false;
            if (HasCycle)
            {
                Cone = Array.Empty<MachineCore>();
                Indeg = Array.Empty<int>();
                Succ = Array.Empty<int[]>();
                Seed = Array.Empty<int>();
                return;
            }

            // Index order is arbitrary (HashSet enumeration) and does NOT affect output: a
            // machine sums its inputs in connection-list order, independent of the order
            // its siblings were dispatched in.
            var coneArr = new MachineCore[cone.Count];
            var coneIndex = new Dictionary<MachineCore, int>(cone.Count);
            int ci = 0;
            foreach (var cmm in cone)
            {
                coneArr[ci] = cmm;
                coneIndex[cmm] = ci;
                ci++;
            }

            var indegArr = new int[coneArr.Length];
            var succArr = new int[coneArr.Length][];
            var succTmp = new List<int>();
            for (int ii = 0; ii < coneArr.Length; ii++)
            {
                var cm = coneArr[ii];
                indegArr[ii] = indeg0[cm];

                // One entry per EDGE (not per distinct destination), mirroring the drain's
                // `foreach (output in m.AllOutputs) --indeg[dst]`.
                succTmp.Clear();
                foreach (var cout in cm.AllOutputs)
                {
                    if (!(cout.Destination is MachineCore cdst))
                        continue;
                    if (!cone.Contains(cdst))
                        continue;
                    succTmp.Add(coneIndex[cdst]);
                }
                succArr[ii] = succTmp.ToArray();
            }

            var seedList = new List<int>();
            for (int si = 0; si < indegArr.Length; si++)
            {
                if (indegArr[si] == 0)
                    seedList.Add(si);
            }

            Cone = coneArr;
            Indeg = indegArr;
            Succ = succArr;
            Seed = seedList.ToArray();

            SelfCheck();
        }

        // Validate the graph against the structure we already trust. Runs once per
        // TOPOLOGY rebuild - never per buffer - so the cost is irrelevant.
        //
        // Drains Indeg/Succ/Seed with the same rule the dispatcher uses and requires the
        // result to match AudioWaves exactly: same machines, same layer assignment.
        // AudioWaves already drives the shipping cached dispatch, so it is a sound oracle.
        //
        // The failure this guards against is nasty: a wrong Succ (e.g. deduplicating
        // parallel edges) leaves a successor's countdown permanently above zero, and the
        // audio thread stalls mid-render. Catching it here turns a silent hang into a
        // flag, and the dispatcher then simply uses the wave path.
        private void SelfCheck()
        {
            var remaining = (int[])Indeg.Clone();
            var layerOf = new int[Cone.Length];
            for (int k = 0; k < layerOf.Length; k++)
                layerOf[k] = -1;

            var cur = new List<int>(Seed);
            int layer = 0;
            int drained = 0;
            while (cur.Count > 0)
            {
                var nxt = new List<int>();
                foreach (int idx in cur)
                {
                    layerOf[idx] = layer;
                    drained++;
                    foreach (int s in Succ[idx])
                    {
                        if (--remaining[s] == 0)
                            nxt.Add(s);
                    }
                }
                cur = nxt;
                layer++;
            }

            // Everything must drain: a stuck countdown is exactly the hang we fear.
            if (drained != Cone.Length || layer != AudioWaves.Length)
            {
                SelfCheckFailed = true;
                return;
            }

            // Every machine must land in the layer AudioWaves put it in.
            var waveLayer = new Dictionary<MachineCore, int>(Cone.Length);
            for (int w = 0; w < AudioWaves.Length; w++)
            {
                foreach (var wm in AudioWaves[w])
                    waveLayer[wm] = w;
            }

            if (waveLayer.Count != Cone.Length)
            {
                SelfCheckFailed = true;
                return;
            }

            for (int k = 0; k < Cone.Length; k++)
            {
                if (!waveLayer.TryGetValue(Cone[k], out int wl) || wl != layerOf[k])
                {
                    SelfCheckFailed = true;
                    return;
                }
            }
        }
    }
}
