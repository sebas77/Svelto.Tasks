# Proposal: Controlled Access to Managed Buffers

Status: Deferred

## Summary

Make the managed array wrapped by `MB<T>` accessible only through explicit access contracts such as
`AsReader`, `AsWriter`, `AsReadOnly`, `AsParallelReader`, and `AsParallelWriter`.

The final API must not expose the live backing `T[]` publicly. Direct mutable access must communicate its
intent through a writer contract, while read access must not provide a mutable reference.

This proposal records the intended direction only. It does not change the current implementation.

## Background

`MB<T>` is a non-owning, fixed-size wrapper around an external managed array. It currently exposes several
ways to bypass an access contract:

- `ToManagedArray()` returns the live backing array, not a copy.
- The public indexers return mutable `ref T` values.
- `Set`, `CopyFrom`, and `Clear` mutate the buffer directly.
- `Reader` currently returns mutable `ref T` values despite its name.

`AsReader()` and `AsWriter()` exist today, but their conflict detection is enabled only in DEBUG builds and
is tied to state stored in one `MB<T>` value. A copied or independently-created alias of the same array may
therefore bypass that state.

Returning the backing array is particularly unsafe because the array can outlive the `MB<T>` ref struct and
can be accessed at any later time without the reader/writer checks observing it.

## Goals

- Make the live backing array an internal implementation detail.
- Require external reads and writes to use an access contract with explicit intent.
- Make reader contracts structurally read-only; they must not return mutable references.
- Detect conflicting access contracts in checked builds, including access through aliases of the same buffer.
- Define contracts suitable for single-threaded, read-only, and parallel access patterns.
- Preserve allocation-free element access and avoid boxing in hot paths.
- Preserve efficient internal bulk operations such as `Array.Copy`.
- Provide a staged migration path for Svelto.Common, Svelto.Tasks, Svelto.ECS, and downstream consumers.

## Non-Goals

- Implementing the change as part of this proposal.
- Making arbitrary operations on `T` thread-safe.
- Copying the backing array every time a reader is requested.
- Removing internal unsafe access required by framework data structures.
- Redesigning native-buffer ownership in the same initial change. `NB<T>` should eventually use compatible
  contract terminology, but its Burst, Jobs, and unmanaged-lifetime constraints require separate analysis.

## Proposed Access Model

`MB<T>` should become a handle used to inspect metadata and request an access view. Normal element access
should happen through one of the views below.

The exact names remain subject to implementation review, but their semantics must be defined before code is
changed.

### Reader

`AsReader()` acquires a read contract.

- Multiple readers may coexist.
- A reader cannot coexist with an exclusive writer.
- Its indexer returns `ref readonly T` or a value, never `ref T`.
- Disposing the reader releases its contract in checked builds.

### Writer

`AsWriter()` acquires exclusive mutable access.

- It cannot coexist with any reader or another writer unless a more specific parallel contract permits it.
- Its indexer returns `ref T`.
- Bulk mutation helpers may be exposed by the writer when they preserve the same access rules.
- Disposing the writer releases its contract in checked builds.

### Read-Only View

`AsReadOnly()` exposes a view whose API cannot mutate the buffer. Its exact distinction from `AsReader()`
must be settled before implementation. Possible uses include a durable read-only projection that can be
stored or passed without representing an active debug lease.

It must never provide a mutable reference or a path back to the live array.

### Parallel Reader

`AsParallelReader()` permits concurrent reads and must remain structurally read-only. Its representation must
be compatible with the execution environments that consume it, including Unity Jobs where applicable.

### Parallel Writer

`AsParallelWriter()` permits only explicitly-supported concurrent writes. It must not imply that arbitrary
writes to arbitrary indices are safe.

The implementation should select and document one model, such as:

- disjoint range ownership established when the writer is created;
- atomic operations exposed by a restricted writer API;
- an external scheduler guarantee validated in checked builds where possible.

An unrestricted mutable `ref T` over the whole buffer is not a meaningful parallel-safety contract.

## Shared Safety State

The current `_rwState` belongs to an `MB<T>` value. Because `MB<T>` wraps an external array and can be copied,
independent wrappers may refer to the same storage while carrying independent state.

Before the access contracts can provide reliable diagnostics, all aliases of the same buffer must coordinate
through shared safety state. The implementation must determine how this state is represented without adding
allocations to normal element access.

Candidate approaches include a shared handle created with the wrapper, framework-owned buffer metadata, or a
debug-only registry keyed by array identity. Each option must be evaluated for:

- alias detection;
- lifetime and cleanup;
- ref-struct and Unity compatibility;
- allocation behavior;
- cost when checks are compiled out.

The safety state may remain a checked-build diagnostic, but the access-view types must enforce read versus
write capability at compile time in every build.

## Unsafe Framework Access

Framework internals still need direct access for allocation-free bulk operations and interop. The existing
public `ToManagedArray()` should eventually be replaced by an explicitly unsafe internal escape hatch, for
example `UnsafeToManagedArray()`.

That escape hatch must be limited to reviewed framework code. It must not be made broadly available by adding
new friend assemblies solely to avoid migrating a caller.

Existing public APIs that expose the same storage indirectly must be audited. In particular,
`FasterDictionary.unsafeValues` and `FasterDictionary.unsafeKeys` return live arrays and therefore participate
in the same design problem even though their names advertise the risk.

## Migration Plan

### Phase 1: Define and Test Contracts

- Decide the exact semantics of Reader, Writer, ReadOnly, ParallelReader, and ParallelWriter.
- Make Reader structurally read-only.
- Design shared safety state and validate alias handling.
- Add conflict, lifetime, and allocation tests before restricting existing APIs.

### Phase 2: Add the Complete View API

- Add the missing view types alongside the existing API.
- Provide required operations on the appropriate view rather than on `MB<T>` directly.
- Keep direct access temporarily for compatibility.
- Mark direct APIs obsolete with migration guidance when supported by the package's Unity compatibility range.

### Phase 3: Migrate Framework Consumers

- Migrate Svelto.Common internals, retaining only reviewed internal unsafe accesses.
- Change `TaskProfiler.ResetDurations` in Svelto.Tasks to use a writer because it mutates `TaskInfo` values.
- Migrate Svelto.ECS using its existing friend-assembly relationship only where an internal fast path is truly
  required.
- Audit downstream packages before changing visibility.
- Replace or restrict APIs such as `FasterDictionary.unsafeValues` and `unsafeKeys`.

### Phase 4: Restrict the Public Surface

In a release that permits breaking API changes:

- make `ToManagedArray()` internal or replace it with an internal unsafe method;
- remove mutable indexers from `MB<T>`;
- move `Set`, `CopyFrom`, and `Clear` behind appropriate writer or internal APIs;
- ensure no public operation returns the live backing array;
- update documentation to present access views as the only supported usage pattern.

## Compatibility Considerations

Restricting the current APIs is a source-breaking change. Existing consumers may depend on holding the raw
array or mutating through `MB<T>` indexers.

The transition should therefore happen in stages and include clear obsolete diagnostics before removal where
possible. `CopyTo` or an explicit snapshot operation should be available when callers genuinely need a
standalone managed array rather than a live view.

The migration must not solve cross-assembly failures by exposing internals more broadly. Public consumers
should move to contracts; framework consumers should justify any unsafe internal path.

## Performance Requirements

The final design must verify:

- no allocation per Reader/Writer creation in release hot paths;
- no boxing of buffers or view types;
- no regression in indexed access significant enough to affect Svelto data structures;
- bulk copies remain bulk operations rather than element-by-element interface dispatch;
- checked-build safety tracking has a documented and acceptable cost;
- parallel views remain compatible with their intended Unity/Burst/Jobs environments.

## Verification Plan

1. Verify multiple readers can coexist.
2. Verify readers reject an active exclusive writer in checked builds.
3. Verify writers reject active readers and other exclusive writers in checked builds.
4. Verify aliases of the same array share conflict state.
5. Verify Reader and ReadOnly cannot produce mutable references.
6. Verify writer mutations update the wrapped array.
7. Verify contract disposal restores the expected state exactly once.
8. Verify copied view values cannot release or bypass contracts incorrectly.
9. Verify parallel contracts enforce their selected range or operation restrictions.
10. Verify snapshot/copy APIs do not expose live storage.
11. Verify Svelto.Common, Svelto.Tasks, and Svelto.ECS compile without public raw-array access.
12. Add allocation and representative throughput tests for view creation and indexed access.

## Open Questions

- What is the precise semantic distinction between `AsReader()` and `AsReadOnly()`?
- Can a parallel writer safely expose mutable refs, or must it expose only restricted operations?
- Should parallel writers own explicit index ranges?
- What shared safety-state representation handles aliases without unacceptable allocations?
- Which direct `MB<T>` operations, if any, should remain as convenience methods?
- Should a safe `ToManagedArray()` name be retained with copy semantics, or would that silently change existing
  expectations too dangerously?
- How should the equivalent native-buffer contracts be represented without breaking Burst and Jobs usage?

## Decision

The intended end state is that external code never receives the live managed array and accesses buffer
contents only through explicit reader/writer contracts. Implementation is deferred until the contract
semantics, alias-safe tracking, downstream migration, and performance requirements can be addressed together.
