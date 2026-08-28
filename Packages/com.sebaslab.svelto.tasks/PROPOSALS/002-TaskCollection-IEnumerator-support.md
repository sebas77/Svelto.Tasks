# Proposal 002 - Full IEnumerator support in task collections

Status: Deferred

## Summary

Make `SerialTaskCollection` and `ParallelTaskCollection` capable of executing and nesting both:

- `IEnumerator<TaskContract>` tasks (Lean tasks)
- plain `IEnumerator` tasks

The intended result is that composing a task inside a collection has the same supported task shapes and execution semantics as composing it directly on a runner.

This proposal records the design direction only. It does not change the current implementation.

## Background

In Svelto.Tasks 1.0, `SerialTaskCollection` was also the task execution core. It maintained stacks of enumerators so that an enumerator returned by another task could be pushed, executed, and removed before its parent resumed.

Svelto.Tasks 2.0 moved task execution into the runners and introduced `TaskContract`. Task collections became composite tasks executed by a runner rather than being the core scheduler themselves. Their internal stacks were restricted to `T : IEnumerator<TaskContract>`.

The runners can handle both generic and plain enumerators, while task collections cannot currently put a plain `IEnumerator` on their internal stacks. This leaves an execution asymmetry: a task may be valid when run directly on a runner but unsupported when composed through a task collection.

The existing `ITaskCollection<T>` interface does not solve this. It declares `T : IEnumerator`, while `TaskCollection<T>` requires `T : IEnumerator<TaskContract>`, so the interface advertises a capability its implementation cannot provide.

## Goals

- Allow plain `IEnumerator` tasks to be added to serial and parallel task collections.
- Allow tasks in a collection to return or continue nested enumerators of either supported kind.
- Preserve runner semantics for yielding, breaking, exception propagation, and disposal.
- Preserve allocation-free steady-state execution.
- Avoid accessing a Lean task through `IEnumerator.Current`, which would box its `TaskContract` value.
- Keep the first implementation focused on capability rather than unrelated public API cleanup.

## Non-Goals

- Redesigning runners or `TaskContract`.
- Replacing the current task collection scheduling policies.
- De-genericizing `TaskCollection<T>` in the same change.
- Preserving `ITaskCollection<T>` only for theoretical compatibility if it has no practical consumers.
- Introducing allocation-producing adapters from `IEnumerator` to `IEnumerator<TaskContract>`.

## Proposed Design

### Internal Stack Entry

Introduce an internal discriminated stack entry that can contain either a Lean enumerator or a plain enumerator. The exact name is intentionally left for implementation time.

Conceptually:

```csharp
struct TaskCollectionEntry
{
    IEnumerator<TaskContract> _leanTask;
    IEnumerator               _plainTask;
    TaskCollectionEntryType   _type;
}
```

The final representation should be selected after reviewing the current `StructFriendlyStack` implementation and measuring its size and hot-path cost.

`TaskContract` should not automatically become the stack-entry type. It represents a yielded contract/current value, while a stack entry represents executable state. Reusing it for both purposes could obscure ownership and lifecycle semantics.

### Native Enumerator Access

Lean entries must be advanced through `IEnumerator<TaskContract>` so that reading `Current` remains strongly typed and does not box `TaskContract`.

Plain entries must be advanced through `IEnumerator`. Their yielded values must follow the same rules used by the runner's plain/ExtraLean execution path.

### Adding Tasks

Collections should accept both task kinds as first-class entries. The exact overload shape must avoid ambiguous calls for types implementing both interfaces, but should conceptually support:

```csharp
void Add(IEnumerator<TaskContract> task);
void Add(IEnumerator task);
```

Existing Lean call sites must continue to use the typed path without additional allocations or boxing.

### Nested Tasks

When a task returns or continues another enumerator, the collection should push a new stack entry for that enumerator and execute it before resuming the parent according to the collection's existing serial or parallel scheduling rules.

Nesting should support all meaningful combinations:

- Lean parent -> Lean child
- Lean parent -> plain child
- plain parent -> plain child, if this is part of the runner's supported semantics
- plain parent -> Lean child, if this is representable without inventing collection-only behavior

The last two combinations must be confirmed against the current runner contract before implementation. Collections should mirror runner behavior rather than define a second interpretation of plain enumerator yields.

### Execution Semantics

The collection implementation should reuse or directly mirror `SveltoTaskWrapper` semantics for:

- valid yielded values from plain enumerators
- `Yield.It`
- `Break.It`
- `Break.AndStop`
- natural completion (`MoveNext()` returns `false`)
- disposal of completed or abandoned enumerators
- exception propagation and `onException`

Semantic parity is more important than preserving historical Svelto.Tasks 1.0 behavior when that behavior differs from the current runners.

## API Cleanup

`ITaskCollection<T>` should be evaluated separately after full enumerator support exists.

Likely outcome:

- Remove `ITaskCollection<T>` if it has no external value or consumers.
- Have `TaskCollection<T>` implement `IEnumerator<TaskContract>` directly.
- Reassess whether the generic parameter still provides a measurable benefit only after the functional change is complete.

This cleanup should not be combined with the initial capability work unless the implementation proves that the generic shape prevents a correct minimal solution.

## Performance Considerations

The proposed representation introduces a task-kind dispatch while advancing collection entries. This is an acceptable direction only if it preserves the library's allocation guarantees.

Implementation work must verify:

- no per-tick allocations after collection setup
- no boxing of `TaskContract`
- no adapter iterator allocation for plain enumerators
- no regression for the existing all-Lean fast path beyond the necessary type dispatch
- acceptable stack-entry size and collection memory growth

If a two-reference union makes each stack entry unnecessarily large, alternatives may be considered, but they must not trade memory savings for boxing or adapter allocations without measured justification.

## Verification Plan

Add focused tests for both `SerialTaskCollection` and `ParallelTaskCollection`:

1. Add and execute a plain root enumerator.
2. Execute a Lean parent with a Lean child.
3. Execute a Lean parent with a plain child.
4. Verify supported nesting combinations at multiple levels.
5. Verify parents resume only after their nested child completes.
6. Verify `Yield.It`, `Break.It`, and `Break.AndStop` propagation matches runner behavior.
7. Verify exceptions reach the existing collection exception handling path.
8. Verify every completed, broken, cleared, or failed enumerator is disposed exactly once when disposable.
9. Verify serial ordering remains unchanged.
10. Verify parallel progression remains unchanged.
11. Extend zero-allocation tests to cover mixed enumerator collections after warm-up.
12. Compare representative all-Lean collection performance before and after the change.

## Open Questions

- Which plain-enumerator nesting combinations are currently supported by runners and therefore must be mirrored?
- Should arbitrary values yielded by a plain enumerator be ignored or rejected? The preferred answer is to match the runner exactly.
- Is direct `Add(IEnumerator)` required as part of the first implementation, or should support initially be limited to nested plain enumerators? Full capability implies both.
- Can the current `StructFriendlyStack` hold a discriminated entry without losing support for struct tasks or introducing boxing?
- Does `ITaskCollection<T>` have any known external consumers that affect its removal timeline?

## Recommended Implementation Order

1. Document the current runner semantics for plain enumerators from `SveltoTaskWrapper` and its tests.
2. Add failing collection tests that express the required parity.
3. Introduce the internal dual-enumerator stack entry.
4. Support mixed nested enumerators.
5. Add direct plain-enumerator collection entries.
6. Validate break, exception, and disposal behavior.
7. Run allocation and performance tests.
8. Perform `ITaskCollection<T>` and generic-shape cleanup as a separate reviewed change.

## Decision

The capability is desirable: task collections should support both `IEnumerator<TaskContract>` and plain `IEnumerator` tasks so that collection composition does not impose a stricter task-shape limitation than runners.

Implementation is deferred until the current runner and collection internals can be reviewed together and the semantic questions above can be answered with tests.
