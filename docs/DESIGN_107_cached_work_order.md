# Design note: cached audio work order (#107)

Companion to the PR. Records the problem, the design, the faithfulness argument, the
correctness analysis, how it was validated, and what the measurements actually
showed — including where the original issue's premise was wrong.

---

## 1. Problem

On the re-scan dispatch family (WorkAlgorithm 0 "Groups", 2 "Threads", and the
single-thread path), every audio chunk drains the graph in dependency layers. Each
drain iteration calls, in order:

- `CollectEditorMachinesThatCanWork` — hidden control machines,
- `CollectControlMachinesThatCanWork` — visible control machines,
- `CollectMachinesThatCanWork(master)` — the audio frontier, recursively.

Each is a full pass over the graph, and there are D iterations per chunk (D = number
of dependency layers). The result is an O(N·D) re-scan **per chunk** whose output is
identical from chunk to chunk until the topology changes. That is the waste #107
targets.

Algorithm 1 ("Recursive Tasks", the default) does **not** re-scan — it recurses the
graph directly — so it is out of scope here.

## 2. Design

### 2.1 Three partitions, rebuilt on topology change

`CachedWorkOrder.Rebuild(song)` produces:

- `EditorPrefix` — control machines with `Hidden == true`.
- `ControlPrefix` — control machines with `Hidden == false`.
- `AudioWaves` — `MachineCore[][]`, Kahn layers of the audio cone.

Rebuild is triggered when either invalidation key changes:

- `BuiltGeneration` vs `ReBuzzCore.TopologyGeneration` — bumped on connection
  add/remove (`MachineCore.AddInput` / `RemoveInput`).
- `BuiltMachineCount` vs `MachinesList.Count` — catches machine add/remove without
  instrumenting every list mutation site.

The check and any rebuild happen at the top of the chunk under `AudioLock`, so a
walk can never observe a half-mutated graph or a stale machine reference.

### 2.2 The audio cone and Kahn layering

The cone is the set of machines reachable from Master (`MachinesList[0]`) via
`AllInputs`, with control/editor machines and Master excluded as work nodes.
In-degree counts only edges whose source is itself a cone node; control inputs are
treated as pre-satisfied (the prefix runs first). Parallel (multi-channel) edges are
counted and decremented symmetrically, so multi-channel connections do not corrupt
the ordering. If the Kahn drain places fewer nodes than the cone contains, a cycle
exists and `HasCycle` is set.

## 3. Faithfulness to the legacy walk

For the cache to be a drop-in, the **same machines** must run in a **dependency-valid
order** producing the **same output**. Four observations underwrite that:

- **Master is a pure sink.** `ReadWork` computes the master output by summing its
  input buffers (`master.GetStereoSamples`); `master.Work` is never dispatched, and
  the legacy drain breaks before reaching it. The cache excludes Master from the
  cone for the same reason.
- **Within-wave order is numerically neutral.** A machine sums its inputs in
  connection-list order, independent of the order its siblings were dispatched. So
  leaving within-wave order dynamic (sorted by cost each chunk, as today) changes
  nothing numerically.
- **`Ready` is not baked into membership.** A native machine crash can clear `Ready`
  mid-buffer. The prefix walk re-tests `Ready` every chunk (matching the legacy
  control/editor collects); the audio waves do not test `Ready`, matching
  `CollectMachinesThatCanWork`, which has no `Ready` check. The actual skip happens
  at the shared dispatch guard (`Machine.Ready && host != null`), identical on both
  paths.
- **The prefix is preserved verbatim** as editors-then-controls, run unconditionally
  before the audio waves.

## 4. The four correctness questions

1. **Cycles.** The engine assumes a DAG; there is no runtime cycle handling, and the
   legacy recursive collect would stack-overflow on a cycle. Acyclicity is enforced
   upstream by `SongCore.CanConnectMachines` /
   `FindDestinationFromSourceConnetions` (a connection that would close a loop is
   refused). The cache inherits that precondition but additionally detects any cycle
   deterministically at build time and falls back to the legacy walk — so it is
   strictly safer than the status quo, never worse.
2. **Mid-chunk eligibility.** `AudioLock` is held across the entire buffer. Every
   topology and `Ready` mutation takes `AudioLock`. The only thing that changes
   within a chunk is `workDone`, which the cached walk drives itself. Native crash
   paths clear `Ready`; the prefix re-test and the dispatch guard handle it.
3. **Editor/control pre-passes.** These exist because control/editor machines often
   have no audio path to Master and so would never be reached by an audio-only walk;
   they are run first, flat, unconditionally, every wave. The cache keeps them as a
   fixed prefix partition rather than folding them into the topo-sort.
4. **Wireless / dynamic.** `IsWireless` is a per-machine display property (read only
   in file ops), never consulted in scheduling. All dynamic connect/disconnect and
   machine add/remove route through `AudioLock`-guarded actions that fire the
   add/remove events and bump `TopologyGeneration`, forcing a rebuild on the next
   chunk.

## 5. Cone parity with the legacy walk

The cone walk **stops at control/editor machines** — when the walk reaches a control
source it does not descend through it. This matches the legacy
`CollectMachinesThatCanWork`, which stops recursing at any `workDone` machine, and
controls are `workDone` from the prefix. So an audio machine whose *only* path to
Master runs through a control is left unworked by both the cache and the legacy walk;
the cache works exactly the legacy set, not a superset.

This is well-formed, not just matched by luck: any audio machine `A` that feeds a
cone machine `M` necessarily has its own audio path to Master (through `M`), so `A`
is still reached by the descent. The only machines excluded are those reachable
*solely* through a control — precisely the set legacy never worked. The Kahn pass is
unaffected: in-degree counts only cone-internal sources, and a now-excluded machine
cannot be an input to a cone machine, so every counted edge still resolves.

(An earlier prototype descended through controls, which worked a harmless superset on
sane graphs. That was tightened to the strict-parity walk above before this PR.)

## 6. How it was validated

- **WAV renders, cache off vs on**, across all three affected algorithms. Every pair
  diverged by ~100–170 LSB RMS (−46 to −50 dBFS), identical through the silent
  count-in then diverging at the first audible sample. Crucially, **off-vs-off across
  the three shipping legacy algorithms diverges by the same magnitude** — so
  bit-exactness across orderings was never an engine invariant, and the cache's
  divergence sits inside the existing inter-algorithm envelope. Verdict: correct to
  the precision the engine itself permits.
- **Direct stopwatch instrumentation.** Because the song is not bit-reproducible and
  is CPU-saturated, black-box A/B on aggregate timing was inconclusive (different
  windows, GC, saturation). A `Stopwatch` around the collection phase
  (`SchedulingOverheadUs`, EMA) and around the per-wave barrier (`BarrierWaitUs`)
  measured the two slices directly and confound-free.
- **Long-run stability.** Cache-on ran fault-free across the full test windows.
  A startup crash observed once was a pre-existing construction-window teardown race
  (NRE past the `Ready && host != null` guard, i.e. `DLL.Info`/host torn down on
  another thread during init) — not introduced by the cache, gone once the graph
  settled, and now covered by the worker-thread fault probe (see PR "not in this
  PR").

## 7. What the measurements showed (and corrected)

Per-chunk decomposition on the audio thread (~3.9 ms budget, Threads algorithm):

```
OtherMs p50  ≈ 3.8 ms   (audio-thread wall per chunk)
  ├─ collection  (SchedulingOverheadUs)  ~260 µs off  →  ~11 µs on   ← this PR
  ├─ barrier     (BarrierWaitUs)         ~1.1 ms      (unchanged on/off)
  └─ serial floor (remainder)            ~2.4 ms      (tick/sub-tick/event/mix/taps)
```

Findings:

- The cache does exactly one thing and does it well: **~24–27× on the collection
  slice** (≈260 µs → ≈11 µs; 290 → 10.6 on the first clean A/B). It leaves
  everything else byte-for-byte identical, including the barrier — confirmed.
- **The re-scan was not the dominant cost.** It is ~7% of the budget. The original
  issue attributed a multi-millisecond floor to it via black-box toggling; the
  direct stopwatch shows that was an over-attribution.
- **The real floor is elsewhere and out of scope:** the per-wave barrier (~1.1 ms,
  at ~2× parallel efficiency — the worker pool is under-fed) and a ~2.4 ms serial
  audio-thread floor. Caveat: `BarrierWaitUs` includes DSP that runs during the
  wait, and the serial-floor figure is sensitive to which musical section the EMA
  samples, so treat ~2.4 ms as the long-run estimate (±a few hundred µs), not exact.
- Consequently the cache **does not** move the dropout rate on a saturated song, and
  was never going to. Its value is structural: O(N·D) → O(N) removes a
  super-linearly-growing cost from the hot path, so the win scales with graph
  complexity, and the layered order it produces is the prerequisite for batching
  barriers later.

## 8. Follow-ups (each its own change)

- A deterministic test song for an absolute bit-exact gate (§6).
- `WorkThreadEngine` robustness: catch/record/`finally`-`WorkDone` around
  `TickAndWork` (also closes a hang-on-throw hole); independent of the cache.
- The wave-boundary `workWaitHandle` Set/Reset race (rare pre-existing hang).
- Phase-split probe for the ~2.4 ms serial floor (`TickPhaseUs` / `MixPhaseUs`),
  ideally on a non-saturated song so the signal isn't buried — the real next lever.
- Barrier batching over the cached layered order — the largest lever for saturated
  songs.
