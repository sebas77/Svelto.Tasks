# Proposal 001 - TaskStore and wrapper-state handle indirection

**Status:** Deferred
**Decision date:** 2026-08-27
**Scope:** Lean task storage, struct-task boxing and transient wrapper state

## Decision

Do not implement TaskStore or wrapper-state handle indirection now.

The shipped plain-field fix in `SveltoTaskWrapper<TTask, TRunner>` is the best current
implementation:

```csharp
TTask _task;
```

The scheduler already reaches `LeanSveltoTask<TTask>` and its wrapper by reference through the
`TombstoneList` slot. Calling `_task.MoveNext()` therefore mutates a struct task in place without
boxing or defensive copies.

This fixes the concrete problem with minimal complexity:

- Generic Lean runners execute struct tasks correctly.
- Struct root tasks use the struct-typed `RunOn` overload and do not box.
- The runner remains allocation-free after warmup and preallocation.
- There is no new lookup, lock, handle lifecycle, or container to maintain.
- Existing runner reset, kill, parent-chain disposal and continuation behavior remain unchanged.

`ZeroAllocationTests.Lean_GenericSteppableRunner_StructTasks_AreZeroAllocation` verifies the
result. Before the field fix the same task never progressed because the old get-only property
returned a copy on every `MoveNext`; it now completes with zero measured allocations.

The wrapper also keeps continuation-related state inline:

```csharp
TaskContract               _current;
IEnumerator<TaskContract>  _continuingTask;
TRunner                     _runner;
```

Moving this transient state to a pooled continuation object or runner-owned store could reduce the
size of every wrapper, because tasks that never continue a child currently still pay for these
fields. This remains an unproven layout optimization. Moving the fields does not by itself reduce
total memory if equivalent state must remain allocated elsewhere, and it introduces the same handle,
ownership and lifecycle concerns as external task storage.

### Before and after trade-off summary

Before the proposed change, each wrapper owns its task and continuation state directly. This makes
task advancement a single by-reference field access with no lookup, lock, handle validation or
separate allocation, but every runner slot includes the full `TTask`, `_current`, `_continuingTask`
and `_runner` footprint even when most of that state is inactive. After the proposed change, runner
slots would contain compact generational handles while task and continuation data lived in separate,
stable stores or pooled objects. The potential benefit is a smaller hot scheduler working set,
better cache-line density at high task counts, cheaper movement/copying of scheduler metadata and a
path to avoiding struct-child boxing. The costs are an additional lookup and type/liveness check on
the step path, more pointer chasing, less data locality when task state is actually needed, store and
free-list memory, thread-safe admission/removal, rollback paths and substantially more ownership and
lifecycle complexity. Total memory may increase rather than decrease unless the compact wrapper and
conditional continuation storage save more than the handles, stores and metadata consume. The
change is therefore an optimization only when benchmarks demonstrate lower memory use, fewer cache
misses or higher throughput for representative runner capacities; otherwise the current inline
layout remains faster, smaller in total machinery and easier to prove correct.

## Current limitations accepted by this decision

### Non-generic runners still box struct roots

The non-generic Lean runner stores:

```csharp
LeanSveltoTask<IEnumerator<TaskContract>>
```

A struct enumerator must be converted to `IEnumerator<TaskContract>` to enter that heterogeneous
container, so it boxes once per `RunOn`. This cannot be removed by overload resolution or generic
variance: `LeanSveltoTask<WorkTask>` and
`LeanSveltoTask<IEnumerator<TaskContract>>` are different value types.

The supported zero-box path for struct roots is the generic runner:

```csharp
var runner = new Lean.SteppableRunner<WorkTask>("runner", capacity);
new WorkTask(...).RunOn(runner);
```

Class tasks remain appropriate for non-generic runners.

### Struct children still box through Continue and Forget

`TaskContract` currently stores child enumerators as `IEnumerator<TaskContract>` references.
Calling `.Continue()` or `.Forget()` with a struct child therefore requires boxing.

Fixing this is not a simple storage change. A handle allocated when creating a `TaskContract`
would need defined ownership when the contract is:

- never yielded,
- overwritten,
- abandoned because the parent faults,
- yielded to the wrong runner,
- retained until the parent reads the completed child's `Current` value.

A store slot cannot be recycled immediately when the child completes because the parent may still
need the child's final `Current`. A correct solution needs delayed release, result copying, or a
lease/reference-count protocol. The requirements for such a redesign are included in this proposal
so task storage and continuation-state indirection are evaluated as one architecture.

### Continuation state remains inline

`SveltoTaskWrapper<TTask, TRunner>` stores `_current`, `_continuingTask` and `_runner` directly.
Externalizing them could improve wrapper density and cache locality at high runner capacities, but
the state is not merely temporary scratch data:

- `_current` can retain a continuation and the completed child's final result until the parent
  consumes it.
- `_continuingTask` identifies the child blocking a same-runner parent.
- `_runner` participates in continuation scheduling and can represent a cross-runner lifetime.

A pooled `Continuation` class is a plausible owner, but the move is correct only if `Continue`,
`RunOn` and `Forget` each have explicit ownership and release rules. Parent-chain disposal,
`Break.AndStop`, faults, reset, kill and runner reuse must not leak or prematurely recycle the
external state.

The inline fields remain preferred until measurements show that wrapper size or cache density is a
material cost.

## Why TaskStore was considered

An external indexed store would make this legal:

```csharp
internal ref TTask task => ref _store[handle];
```

C# forbids a struct from returning one of its own fields by reference (CS8170), but returning a
reference to an array element is legal. Handles can also survive container growth better than
long-lived references.

These properties make TaskStore a plausible future architecture, especially if task metadata and
task state need different cache layouts. They do not justify its cost today because the plain field
already solves the active correctness and allocation problem.

## Why the original TaskStore sketch was rejected

### A flat growable array is unsafe with lock-free readers

The original sketch used a growable `T[]` and returned refs to its elements while another thread
could grow the store. Growth copies the old array and swaps the root reference.

Possible lost-update race:

1. Runner takes a ref to task A in the old array.
2. Producer copies the old array during growth.
3. Runner mutates task A through the old-array ref.
4. Producer publishes the copied array.
5. The mutation is lost because future lookups use the new array containing the pre-mutation value.

Memory barriers do not solve this. A future store must use stable storage, such as fixed-size
chunks whose elements never move.

### Phase 1 would not save total memory

Today each task and its scheduler state exist inside the spawned-task slot. Moving either to a store
adds a handle, store reference, free-list metadata and another container. Total memory would
increase unless enough inline task or continuation fields were removed at the same time.

The layout might improve cache locality by separating scheduler metadata from task state, but this
must be demonstrated by benchmarks. It must not be assumed.

### Submission-time insertion requires rollback

If a future implementation adds the task to a store before queue admission, every failure after
the add must return the slot:

```csharp
TaskHandle handle = store.Add(in task);

try
{
    runner.AddTask(meta, index);
}
catch
{
    store.Remove(handle);
    throw;
}
```

Continuation-pool resources acquired before admission would require equivalent rollback.

### Deferred handles require generations

A plain integer handle can refer to a different task after slot reuse. Any future handle that can
outlive immediate scheduler access must contain a generation:

```csharp
readonly struct TaskHandle
{
    public readonly int index;
    public readonly uint generation;
}
```

The `ValueIndex`/`SparseIndex` types in Svelto.Common demonstrate the generation pattern, although
the current `ManagedSlotMap<T>` is experimental, non-thread-safe and not stable enough to use
directly for this purpose.

### Additional synchronization would enter task admission and completion

Cross-thread `RunOn` requires store mutation to be thread-safe. Add, Remove, growth and free-list
reuse would need a lock or a carefully verified concurrent slot map. The worker removes tasks while
producer threads add them, so this is real contention not present on the step path today.

The proposal would therefore add significant concurrency code to replace a field that already
works.

## Requirements if this proposal is reopened

A future implementation must satisfy all of the following before it can replace any inline field:

1. **Stable segmented storage:** existing task elements never move. Growth appends fixed-size
   chunks rather than copying live elements.
2. **Generational handles:** stale handles cannot access a reused slot.
3. **No struct constraint regression:** `TaskStore<T>` must support
   `where T : IEnumerator<TaskContract>`; a `class` constraint would defeat the purpose.
4. **Thread-safe mutation:** Add/Remove/Grow/free-list operations are correct under concurrent
   producer and runner threads.
5. **Lock-free step reads:** resolving a live handle to `ref T` takes no lock and allocates nothing.
6. **Admission rollback:** rejected/flushing/disposed runners cannot leak store slots or
   continuation-pool entries.
7. **Complete lifecycle coverage:** natural completion, queued reset, running reset, kill, fault,
   parent-chain disposal and runner reuse free exactly one slot.
8. **Explicit child ownership:** Continue/RunOn/Forget handles define who releases them if the
   `TaskContract` is never consumed, and preserve the child's final result until the parent reads
   it.
9. **Continuation parity:** external `_current`, `_continuingTask` or `_runner` state preserves
   same-runner and cross-runner ordering, result delivery, `Break.AndStop`, faults and disposal.
10. **Conditional storage benefit:** tasks that never continue children do not pay an equal or
    greater hidden cost through eagerly acquired continuation objects or store slots.
11. **Measured benefit:** benchmarks show a meaningful cache, throughput or memory improvement over
   the plain-field implementation. Architectural neatness alone is insufficient.
12. **No allocation regression:** all Release zero-allocation tests remain at exactly zero.

## Triggers for reconsideration

Reopen this proposal only if profiling demonstrates at least one of these:

- Large struct enumerators materially increase cache misses or runner step time.
- Struct child boxing through Continue/Forget is a significant allocation source.
- Very high runner capacities make separating scheduler metadata from task state measurably
  beneficial.
- Inline continuation fields materially increase wrapper memory use or cache misses.
- Another required feature genuinely needs stable generational task handles.

Until one of these triggers exists, the inline `_task`, `_current`, `_continuingTask` and `_runner`
fields remain the preferred implementation: smallest change, lowest risk, direct access, zero
allocation and no added indirection.
