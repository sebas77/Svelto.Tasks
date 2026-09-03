# Svelto.Tasks AI Development Guide

`Svelto.Tasks` schedules iterator-based work. It is not an `async`/`await` task framework: Lean tasks yield `TaskContract`; ExtraLean tasks yield plain `IEnumerator` values.

## Read first

- [Complete API and behavior reference](.aiguides/AI_GUIDE_Svelto.Tasks.md)
- `Svelto.Common` is the companion low-level package. Consult its own `AGENTS.md` when working with its APIs.
- `README.md` for package installation.

The deep guide is authoritative for public API behavior. Update it when source behavior changes, and keep this entry point concise.

## Implementation rules

- Use Lean tasks only when their `TaskContract` features are needed; use ExtraLean tasks for plain cooperative iteration.
- Inside a Lean parent, use `.Continue()` to run a child on the same runner and wait. `RunOn(runner)` schedules a root task and returns a `Continuation`; `.Forget()` schedules a same-runner child without waiting.
- `TaskContract.Continue.It` is not `.Continue()`: it requests another immediate `MoveNext()` from a custom enumerator.
- Keep a strong reference to every runner and dispose it. A `MultiThreadRunner` owns a worker thread; do not call `Flush()` or `Dispose()` from that worker.
- Do not use Unity APIs from `MultiThreadRunner` tasks. Use `TaskSynchronizationContext` only when intentionally hosting .NET async code on a Lean runner.
- Do not add tasks to a running task collection. Dispose parallel collections after their final use.

## Verification

Run the affected test suite in both configurations:

```powershell
dotnet test "Svelto.Tasks.Tests.csproj" -c Debug
dotnet test "Svelto.Tasks.Tests.csproj" -c Release
```

Run these commands from `Svelto.Tasks.Tests~`.
