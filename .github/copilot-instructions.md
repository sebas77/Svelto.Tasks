# GitHub Copilot instructions

Read and follow `AGENTS.md` at the repository root before doing any work in this repo — it explains what Svelto.Tasks is, when to use it, how to use it correctly, and how to build/test your changes.

Key facts at a glance:

- Tasks are `IEnumerator`/`IEnumerator<TaskContract>` stepped by runners — not `async`/`await`.
- Build everything with `dotnet build Svelto.Tasks.Tests.sln`; run tests with `dotnet test` (NUnit 4) on the projects under `Svelto.Tasks.Tests~` and `Svelto.Common.Tests~`.
- Library targets `netstandard2.1`, C# 10, nullable disabled, implicit usings disabled.
- Log via `Svelto.Console`, never `Console.WriteLine` or `UnityEngine.Debug`.
