# Svelto.Common AI Development Guide

`Svelto.Common` is the shared low-level package used by Svelto libraries. It contains high-performance collections, memory abstractions, logging, contracts, and platform utilities.

## Read first

- [Complete API and behavior reference](.aiguides/AI_GUIDE_Svelto.Common.md)
- `README.md` for Unity package setup.

The deep guide is authoritative for public API behavior. Update it when source behavior changes, and keep this entry point concise.

## Implementation rules

- Prefer the existing `FasterList`, dictionary, buffer, and native-memory abstractions over framework collections only when their documented semantics are required.
- Dispose native-memory structures explicitly. Treat copies of structs that own native allocations as aliases, not independently disposable values.
- `TombstoneHandle` is generation-aware: validate externally held handles with `TombstoneList<T>.Has`; a handle is invalid after removal, slot reuse, or `Clear()`.
- Keep Unity/Burst and non-Unity compilation paths valid. Do not introduce managed references into code intended for native/Burst contexts.

## Verification

Run the affected test suite in both configurations:

```powershell
dotnet test "Svelto.Common.Tests.csproj" -c Debug
dotnet test "Svelto.Common.Tests.csproj" -c Release
```

Run these commands from `Svelto.Common.Tests~`.
