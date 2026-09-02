# Svelto.Tasks

Asynchronous Tasks handling and execution: a multithreaded, allocation-free tasks runner for C#. Serial and parallel coroutines run even on other threads; runners give you full control over when and where your code executes, and stopping a runner stops every task it owns.

Engine-agnostic: if you can compile C#, you can run Svelto.Tasks. Unity specializations (yield-instruction interop, dedicated schedulers, Burst jobs) are strictly optional add-ons behind compiler defines.

## Links

- Full documentation, examples and benchmarks: https://github.com/sebas77/Svelto.Tasks
- Companion package: https://openupm.com/packages/com.sebaslab.svelto.common/

## Install (Unity)

The easiest way is the [openupm CLI](https://openupm.com/docs/getting-started/), which resolves the dependency graph automatically (including the `org.nuget.*` packages):

```
openupm add com.sebaslab.svelto.tasks
```

If you install by hand-editing `Packages/manifest.json` instead, you need the OpenUPM scoped registry (see the [Svelto.Common README](https://github.com/sebas77/Svelto.Tasks#readme) for the exact snippet), otherwise Unity fails package resolution with an explicit error naming the missing `org.nuget.*` package.

## Install (.NET)

```
dotnet add package Svelto.Tasks
```

Requires .NET / .NET Core / Mono / Unity with `netstandard2.1` support.