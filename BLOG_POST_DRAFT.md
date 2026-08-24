# Introducing Svelto.Tasks 2.0 beta: coroutines for every C# platform

## Introduction

*Svelto.Tasks* is the platform agnostic C# library that runs serial and parallel coroutines, even on other threads. It has been the quiet engine behind all my libraries for years, shipping in real products ([Robocraft](https://robocraftgame.com/), *Robocraft Infinity*, *Cardlife*), yet it never had the structure and the documentation it deserved. The **2.0** iteration changes that: the repository is now organized as proper packages usable on every C# platform, and it ships with **21 self-contained console examples**, one per main feature, that run with a simple `dotnet run`. Small disclaimer before we start: I was able to write the examples, the test suite and even finish this article thanks to AI tools. The design decisions and the opinions you will read are all mine, though ;)

Let me be precise about the state of it: this is a **beta**. The API has settled through years of production use, the test suite covers the core semantics properly, but I obviously cannot promise there are no bugs left. Two areas are explicitly **experimental**: the .NET `Tasks` integration (`SveltoAwaiter` and the new `TaskSynchronizationContext`) and the Burst-oriented job path when used inside *Unity*. They work, the examples demonstrate them, but consider their APIs more fluid than the rest.

One important thing to understand from the start: the core of Svelto.Tasks has no dependency on any engine. If you can compile C#, you can run Svelto.Tasks — console tools, servers, *MonoGame*, *Stride*, whatever. The few Unity specializations (yield-instruction interop and the Unity-dedicated schedulers) live behind compiler defines and are strictly optional add-ons to an otherwise engine-agnostic core.

## The mental model: tasks are iterators, runners are schedulers

A task is any `IEnumerator<TaskContract>`. A runner ticks tasks according to a strategy called a **flow modifier**. That's the whole architecture, and it gives you something `async`/`await` fundamentally cannot: complete control over *when* and *where* your code executes.

This is (a trimmed version of) example number 1:

```csharp
IEnumerator<TaskContract> FrameCounterTask()
{
    for (int i = 1; i <= 10; i++)
    {
        frameCount = i;
        yield return TaskContract.Yield.It; //suspend until the next runner.Step()
    }
}

using (var runner = new SteppableRunner("GameLoopRunner"))
{
    FrameCounterTask().RunOn(runner); //enqueue the task. It doesn't run yet!

    while (runner.hasTasks)           //your loop decides everything
        runner.Step();
}
```

Nothing happens until the runner is stepped. Pause, resume, flush or kill the runner at any moment; swap the iteration strategy with one line; move the same task code to another thread by changing a single constructor call. The task code doesn't know and doesn't care where it runs — separating *what* runs from *where* it runs is the whole point.

## How is this different from the Task pattern?

Before comparing them, one fact makes everything simpler: `async` methods and iterator blocks are compiled into the very same thing, a **state machine**. The compiler chops your method into chunks around every `await` or `yield` and stores the chunk-to-execute-next in an object. That stored *"what runs after the pause"* is called the **continuation**. Both worlds use continuations, then. The whole difference is about **who holds the continuation, and who decides when, where and if it ever runs**:

```text
THE TASK PATTERN - your code is PUSHED forward by the runtime

  you call LoadLevelAsync()
         |
         v
  the code runs until `await DownloadAsync()`, then stops.
  the REST OF THE METHOD (the continuation) is handed over:
  "resume me when the download is done"
         |
         v
  the download runs on a thread chosen by the runtime
  (a ThreadPool worker: not a thread you picked)
         |
         v
  download finished -> the runtime grabs the continuation
  and resumes it however IT decides
         |
         v
  BuildScene(data) finally runs. Same story again
  for every other `await` in the method

  NOTE: can you choose WHERE the code resumes? Not by
  default: it lands on the ThreadPool. Two levers exist:
  - a CUSTOM AWAITER receives the continuation at every
    await and can resume it wherever IT wants - this is
    what our .RunOn(runner) awaiter does (example 16)
  - an installed SYNCHRONIZATIONCONTEXT catches instead
    every default await of the method - this is what our
    experimental TaskSynchronizationContext exploits
    to host whole async methods on a runner (example 21)
```

Read that diagram again and notice what is missing: *you*. Once the method started, your hands are off. The continuation travels with the awaited operation and comes back to life on a thread, at a moment, chosen by the infrastructure.

```text
THE ITERATOR PATTERN - your code is PULLED forward by your runner

  your game loop calls runner.Step()             <+
         |                                        |
         v                                        |
  every live task advances up to its next         |
  `yield`, then freezes exactly there             | repeat next
         |                                        | frame/tick
         v                                        |
  the yield hands control BACK to the runner      |
  with an instruction: "wait a frame", "run my    |
  child task", "stop me"... The continuation of   |
  the task just sits there, completely inert      |
         |                                        |
         v                                        |
  the RUNNER reads the instruction and decides:   |
  resume it now? skip it this tick? stop it?      |
         |                                        |
         +----------------------------------------+

  NOTE: which context resumes your continuation?
  The one of the runner YOU chose with RunOn():
    main-loop runner -> the game thread
    MultiThreadRunner -> its own background thread
  Same task code, different context: just change
  the runner passed to RunOn()
```

Here the continuation never goes anywhere: it belongs to the task, and the task belongs to your runner. Since stepping always originates from your own `Step()` call, tasks can only ever execute on the thread that owns the runner — deterministic threading for free. Stop stepping, or `Dispose()` the runner, and every continuation out there is simply frozen forever. And because the runner inspects each yield before acting on it, pacing strategies like **StaggeredFlow** or **TimeBoundFlow** become possible: try telling an already-started `Task` "at most three of you this frame" — once pushed, it answers to nobody.

In short: `async`/`await` hides the continuation from you and pushes it around for convenience; Svelto.Tasks keeps the continuation in plain sight and lets your loop pull it.

## Ticking or handing over?

Before criticizing the push model, let me be fair: handing a continuation over is *efficient*. While you wait, your method costs literally nothing, and the moment the awaited event fires your code resumes — no loop, no polling. Here is my honest balance sheet of the two approaches:

|                          | **hand-over (`async`/`await`) — push**   | **ticking (iterators + runners) — pull**       |
|--------------------------|-----------------------------------------|------------------------------------------------|
| cost while waiting       | none: the method sleeps till the event  | parked parents cost zero; time/signal waiters poll once per tick |
| reaction latency         | immediate, event-driven                 | at best, the next `Step()`                     |
| who chooses the context  | the runtime (or awaiter/context author) | you, via the runner passed to `RunOn()`        |
| stopping mid-flight      | cooperative: tokens must be honored     | absolute: stop stepping, or dispose the runner |
| pacing / budgets         | cannot be imposed centrally             | flow modifiers see every step                  |
| profiling                | scattered wherever the runtime ran you  | one uniform hook inside the runner             |
| ecosystem                | the whole .NET speaks its language      | interop bridges needed                         |

So ticking has a price, but a smaller one than it seems, because suspension is *structural* rather than bookkeeping: when a task waits for a child spawned on the same runner, the runner parks it and the child literally takes over its slot in the running list — the parent costs nothing until the child finishes. What still ticks is what asked to tick: tasks pacing themselves on frames (`Yield.It`) and waiters polling wall-clock time or external signals — one cheap call each per tick. Why do I accept even that? Because **games are not servers**. A server holds thousands of genuinely idle waiters, woken by unpredictable external events — exactly the shape the hand-over model was designed for, and for that shape it is unbeatable. Gameplay has the opposite shape: a few dozen to a few hundred short-lived behaviours that must march in lockstep with a frame loop that ticks anyway.

Inside a world that already ticks 60 times a second, my coroutines' overhead is a handful of extra calls per frame — noise compared to what gameplay code does — and in exchange I get things the hand-over model cannot give me:

- **someone is always watching**: the runner sees every step, so pacing, budgeting and profiling are features of the design, not bolted-on hacks
- **stopping stays absolute**: no token to pass down, no orphaned continuation that can resurrect after its context died
- **costs stay visible**: N tasks times one cheap call per tick — the infrastructure never surprises me by resuming four hundred continuations within the same frame
- **one mental model everywhere**: console tool, editor, dedicated server, background thread — identical runner semantics

That trade — a little per-tick overhead in exchange for total supervision — is the deal Svelto.Tasks offers, and for gameplay code I take it every time.

## Lean or ExtraLean?

Tasks come in two weights:

- **ExtraLean** tasks are plain `IEnumerator`s. They can only yield "wait" signals, which makes them extremely lean — ideal for the vast majority of gameplay coroutines.
- **Lean** tasks yield the **TaskContract**, a discriminated union that can also carry return values, continuations, break directives and nested enumerators. They power composition: waiting for children, returning results, cancelling chains.

```csharp
//ExtraLean: the whole contract is "keep me waiting"
IEnumerator Countdown()
{
    for (int i = 3; i > 0; i--) { Console.Write(i); yield return null; }
}

//Lean: the yield point becomes a rich instruction
IEnumerator<TaskContract> LoadLevel()
{
    yield return Download().Continue();          //wait for a child task
    var cfg   = loader.Current.ToRef<GameConfig>(); //collect its result
    yield return WaitForSecondsEnumerator(1f).Continue(); //then wait a second
}
```

My opinionated take: I don't believe games actually need Lean tasks. What does a gameplay coroutine ever do? Wait some frames, wait some seconds, maybe run a small sequence — ExtraLean covers all of it with less overhead per step. Lean tasks are there to cover every possible case: returning values to callers, building task hierarchies, interoperating with other paradigms. When you need them, they're indispensable; when you don't, don't pay for them.

## Why not simply async/await for gameplay?

Fair question! `async`/`await` is great infrastructure, and Svelto.Tasks 2.0 interoperates with it (examples 16 and 21 show how). But gameplay code usually wants the opposite trade-off: to know *exactly* when and where things run, and to be able to stop them. With Tasks-based code, once you fire an async operation, you mostly hope it completes and you juggle cancellation tokens everywhere.

With runners, lifecycle is explicit and absolute. The feature I value most: **runners can be stopped at any time**, which lets you tie a set of tasks to an isolated context and be sure that none of its tasks is still running once the context is gone:

```csharp
//a match owns every coroutine of that session
_matchRunner = new MultiThreadRunner("MatchSession");
SpawnEffects().RunOn(_matchRunner);
UpdateAI().RunOn(_matchRunner);
UploadTelemetry().Forget(); //even fire-and-forget work belongs to the match

//match over: no orphan coroutine can outlive it. guaranteed.
_matchRunner.Dispose();
```

A level owns its runner → unloading the level cannot leave coroutines ticking into destroyed objects. A UI screen owns its runner → closing the screen kills its animations without unwinding dozens of tokens. `Pause()`/`Resume()` give you the same control temporarily (freeze a background worker mid-state without tearing it down), and `Flush()`/`Stop()` let a runner be reused after cleanup. Try expressing "these hundred coroutines belong to this context and must not outlive it" with plain Tasks. You'll end up rebuilding a poor man's runner anyway 🙂

## Taking over coroutines, Tasks and Unity Jobs patterns

One way to look at 2.0: whatever concurrency pattern you use today, Svelto.Tasks has a counterpart designed to absorb it.

- **Unity coroutines**: iterator tasks *are* Unity-coroutine-shaped, minus the engine lock. Yield `Yield.It` instead of `null`, run them on a runner instead of a `MonoBehaviour`, and they keep working outside the editor, on dedicated servers, in tests. Unity-specific glue (like yield-instruction interop) exists behind defines for when you need it.
- **Tasks / async-await**: await a Svelto runner directly so continuations resume on *your* thread, not the ThreadPool; or go the other way around and host entire `async` methods on a runner through the experimental `TaskSynchronizationContext`. Both directions are shown in the examples.
- **Unity Jobs**: the data-parallel pattern maps onto `ISveltoJob` + `MultiThreadedParallelJobCollection<T>`, which splits N iterations across M worker threads exactly like an `IJobParallelFor` would. On Unity these job structs can be Burst-compiled — that path is marked experimental.

Now the honest part: **Svelto.Tasks is not a true replacement for Unity Jobs**, and I am not going to pretend otherwise. Jobs+Burst is a compiler pipeline: your job code gets transformed into highly vectorized native code, backed by a safety system and deep player-loop integration that plain C# cannot replicate. If you need maximum raw throughput on tightly packed numeric loops inside *Unity*, use Jobs. Where Svelto.Tasks shines is everything around those hot kernels: orchestrating serial pipelines, staggering work across frames, bounding frame budgets, coordinating threads — with one consistent API on every platform.

## Performance and zero allocations

Performance was a first-class design constraint since 1.x. My benchmarks show Svelto.Tasks trading blows with *UniTask* and *Unity Jobs*: comparable throughput on coroutine stepping, comparable scaling on multi-threaded fan-out. I will publish the profiling numbers and captures separately, so you can judge for yourself rather than trusting my word for it.

*(benchmark tables and profiler screenshots to be added here)*

Zero-allocation usage is a set of patterns rather than magic, and 2.0 makes them easy:

- **ExtraLean tasks + struct iterators**: runners accept struct enumerator types (`SteppableRunner<T>`), so stepping a task touches zero heap objects.
- **Pooled iterator blocks**: `IteratorBlockPool<T>` recycles `while(true)` state machines via `Break.It`; blocks and their data survive across uses (examples 07 and 15 prove reuse with reference equality).
- **Pooled continuations**: every `.Continue()` hands back a pooled handle; nothing is allocated per wait.
- **Preallocated collections**: `SerialTaskCollection`/`ParallelTaskCollection` own their storage upfront.
- The usual discipline applies: avoid closures and LINQ in per-frame tasks, and remember that `yield return 42` boxes — extract values through `ToInt()`/`ToRef<T>()` consciously.

## The examples, one by one

Documentation was historically my libraries' weak spot, so this time every feature ships as a minimal runnable demo with a README explaining scenario, API and gotchas. Each folder under `Examples/` is independent:

```bash
cd Examples/01_GameLoop
dotnet run
```

Here is the most important extract of each one.

### 01 — GameLoop: Lean task + SteppableRunner

A simulated game loop ticks a runner once per frame while a task counts frames, yielding between counts. Teaches the fundamental cycle: `RunOn`, `Step()`, `hasTasks`. If you understand this example, you understand half of Svelto.Tasks — the full loop is shown at the top of this article.

### 02 — SimpleCoroutine: ExtraLean task

Same countdown, written as a plain `IEnumerator` on an ExtraLean runner. No `TaskContract` processing at all — the cheapest possible coroutine.

```csharp
IEnumerator Countdown() //plain IEnumerator: only null/Yield/Break allowed
{
    int count = 3;
    while (count > 0)
    {
        Console.Write(count--);
        yield return null; //wait exactly one step
    }
}
countdown.RunOn(extraLeanRunner);
```

### 03 — LoadingPipeline: TaskContract return values

A child task downloads and parses a config, then hands it to its parent. Values flow upward without globals or callbacks.

```csharp
IEnumerator<TaskContract> DownloadAndParse()
{
    ...download + parse...
    yield return TaskContract.FromReference(cfg); //hand the result up
}

IEnumerator<TaskContract> Parent()
{
    var child = DownloadAndParse();
    yield return child.Continue();                 //wait for the child

    GameConfig cfg = child.Current.ToRef<GameConfig>(); //extract the result
}
```

### 04 — ContinueChildTask: `.Continue()`

The simplest composition primitive: a parent delegates to a child **on the same runner** and parks until the child completes. Distinct from `.RunOn(runner)`, which targets a specific runner and returns a pollable continuation instead.

```csharp
yield return Child().Continue(); //same runner: parent suspends until done
parentResult = childCounter * 10; //resumes only after Child finished
```

### 05 — BackgroundComputation: RunOn + Continuation

Heavy math runs on a `MultiThreadRunner` (one dedicated background thread per runner) while the main thread polls. Real cross-thread parallelism with results published through volatile fields.

```csharp
using (var bgRunner = new MultiThreadRunner("BgCompute"))
{
    Continuation cont = HeavyComputation().RunOn(bgRunner);

    while (cont.isRunning) //main thread stays free meanwhile
        DrawProgress();

}   //Dispose() stops the background thread cleanly
```

### 06 — FireAndForgetLogging: `.Forget()`

Telemetry fired without waiting: the parent continues immediately and the child interleaves afterwards on the same runner. The recorded order `[1] [4] [2] [3]` is captured while tasks run, proving the scheduling instead of asserting it.

```csharp
IEnumerator<TaskContract> Parent()
{
    Step(1);                        //gameplay keeps going...
    yield return Child().Forget();  //...child is queued but NOT awaited
    Step(4);                        //this runs BEFORE the child finishes
}
```

### 07 — ReusableSpawnLoop: Break.It + pooling

`yield return Break.It` ends a cycle **without** destroying the state machine. Combined with `IteratorBlockPool<T>`, spawn loops become reusable objects: the demo proves the very same block and data instances come back from the pool, reference-equal.

```csharp
IEnumerator<TaskContract> SpawnLoop(EntityData data)
{
    while (true) //the state machine never dies
    {
        Spawn(data);
        yield return TaskContract.Break.It; //end cycle, stay alive, back to pool
    }
}

var (data, block) = pool.Get(); //second Get returns the SAME instances
data.kind = "Orc";              //re-initialize, then run again
```

### 08 — CancellableChain: forwarding failures

Load → Validate → Process, launched by a parent. Validation fails and the failure must cancel everything above it. The example teaches a subtle truth: `Break.AndStop` propagates **exactly one level up** — a killed task cannot forward its own break — so the chain forwards deliberately. Process is skipped, Parent is cancelled, and the summary derives from flags written while tasks ran.

```csharp
//leaf: stop only myself, let my caller decide
validationFailed = true;
yield return TaskContract.Break.It;

//middle level: forward the failure so EVERYTHING above cancels too
yield return ValidateStep().Continue();
if (validationFailed)
    yield return TaskContract.Break.AndStop; //Process skipped AND Parent cancelled

yield return ProcessStep().Continue(); //never reached
```

### 09 — OrderedLoading: SerialTaskCollection

Download → parse → initialize with strict ordering guarantees: the collection won't touch the next task before the current one finished. Ideal for loaders whose stages depend on each other.

```csharp
var serial = new SerialTaskCollection("LevelLoader");
serial.Add(DownloadStage());
serial.Add(ParseStage());
serial.Add(InitializeStage());

serial.Complete(10000); //strictly one after another
```

### 10 — ConcurrentAnimations: ParallelTaskCollection

Three UI bars progress together: each pass advances every still-running task once, round-robin, cooperatively on one thread. Concurrency here means *interleaving*, and that's usually exactly what game logic wants.

```csharp
var parallel = new ParallelTaskCollection("UIAnimations");
parallel.Add(HealthBar());
parallel.Add(ManaBar());
parallel.Add(XpBar());

while (parallel.MoveNext()) //one MoveNext advances ALL bars by one tick
    DrawFrame();
```

### 11 — AIBudgetStaggered: StaggeredFlow

Ten AI units, at most three thinking per tick. One line caps CPU spikes. The demo honestly documents the flip side: excess tasks starve until slots free up.

```csharp
runner.UseFlowModifier(new StaggeredFlow(3)); //max 3 AI tasks processed per tick
```

### 12 — FrameBudgetTimeBound: TimeBoundFlow

Instead of counting tasks, count milliseconds: process background work for at most 20ms per tick, wall-clock measured. The demo shows who ran and who starved each tick, and explains when to prefer completing tasks versus switching to fairer strategies.

```csharp
runner.UseFlowModifier(new TimeBoundFlow(20f)); //at most 20ms per tick
```

### 13 — BatchPathfinding: ParallelJobCollection

1000 pathfinding iterations split across four threads via `ISveltoJob` — the Svelto counterpart of `IJobParallelFor`. Every unit records which thread processed it, and the final report verifies the fan-out. On Unity, job structs like these are candidates for Burst compilation (experimental).

```csharp
struct PathfindingJob : ISveltoJob
{
    public int[] results;
    public int[] threadAssign;

    public void Update(int index) //runs on the worker threads
    {
        results[index]     = index;
        threadAssign[index] = Thread.CurrentThread.ManagedThreadId;
    }

    public void Dispose() { }
}

collection.Add(job, TotalUnits); //1000 iterations split across 4 threads
collection.Complete();           //wait for all slices to finish
```

### 14 — ParallelDownloads: MultiThreadedParallelTaskCollection

Four downloads on four real threads simultaneously, with a monitor thread redrawing progress bars while workers advance. Overlap is visible, not claimed.

```csharp
using var downloads = new MultiThreadedParallelTaskCollection("Downloads", 4, false);
downloads.Add(new DownloadTask("File_1.zip", progress1));
downloads.Add(new DownloadTask("File_2.zip", progress2));
downloads.Add(new DownloadTask("File_3.zip", progress3));
downloads.Add(new DownloadTask("File_4.zip", progress4));

downloads.Complete(); //four OS threads downloading simultaneously
```

### 15 — EntitySpawnPool: IteratorBlockPool visualized

Spawns entities from a pool, runs their lifecycles, recycles them via `Break.It`, and shows pool occupancy live. Ends with a verification box proving the recycled block and data are the *same objects* obtained again.

```csharp
_pool = new IteratorBlockPool<EntityData>(EntityLifecycle, "EntityPool");

var (data, block) = _pool.Get();      //take from the pool (or allocate once)
data.EntityId = ++_totalSpawned;
...step block.MoveNext() each tick...

//lifecycle yields Break.It at despawn:
//block auto-returns to the pool, state machine kept alive
int available = _pool.count;          //live pool occupancy
```

### 16 — AsyncHttpAwaiter: awaiting a runner

Bridges `async`/`await` into the library: the continuation after the `await` resumes on the runner's thread, interleaved with its other tasks. Experimental .NET Tasks integration, part one.

```csharp
async Task SimulateHttpRequest()
{
    SendRequest();
    await Task.Delay(800).RunOn(runner); //resume ON the runner, not the ThreadPool
    ReceiveResponse();                   //interleaved with the runner's other tasks
}
```

### 17 — PauseMenu: Pause/Resume

Opening the pause menu freezes a `MultiThreadRunner` dead in its tracks; closing it resumes every task exactly where it stopped. Snapshot checks in the demo prove nothing advanced while paused.

```csharp
runner.Pause();
Debug.Assert(counterBeforePause == ReadCounter()); //frozen solid
runner.Resume();
```

### 18 — RecursiveTreeTraversal: deep continuation chains

Walks a scene-graph tree depth-first through recursive `.Continue()` calls — parents suspend on children that suspend on grandchildren. Doubles as a stress case showing continuation chains surviving internal list resizes.

```csharp
IEnumerator<TaskContract> Traverse(TreeNode node)
{
    Visit(node);

    foreach (var child in node.Children)
        yield return Traverse(child).Continue(); //recursion as continuations
}
```

### 19 — DelayedSpawn: WaitForSecondsEnumerator

An enemy spawns two seconds after level start, gated by a reusable seconds-waiter. Wall-clock based, resettable, and available in a struct variant for allocation-sensitive loops.

```csharp
var wait = new WaitForSecondsEnumerator(2f); //wall clock, Reset()-able

while (wait.MoveNext())                      //poll until the wait is over
    yield return TaskContract.Yield.It;

SpawnEnemy();                                //exactly 2s later
```

### 20 — CrossThreadSignal: WaitForSignal

A background thread computes and signals; the main-thread task waits through the same signal. A tiny typed subclass gives a clean producer/consumer handshake between runners.

```csharp
class DataReadySignal : WaitForSignal<DataReadySignal> {}

//background thread, when the data is ready:
_dataReady.Signal();

//main-thread task:
yield return _dataReady.Wait(); //suspend until Signalled()
Consume(computedData);
```

### 21 — StopRunnerNetTask: hosting async methods on a runner

The most experimental piece: `TaskSynchronizationContext` hosts whole `async` methods on any Lean runner — every `await` continuation resumes on the runner's thread, ordered with your other coroutines. Disposing the runner mid-await abandons hosted work deterministically: frozen forever, then collectable. Isolated-context semantics, now for async code too.

```csharp
var context = new TaskSynchronizationContext(runner);

context.Run(async () =>
{
    var page = await http.GetStringAsync(url); //any suspension
    Parse(page);                               //back on the RUNNER thread
});

//runner.Dispose() mid-await: hosted work abandoned deterministically
```

## What's next

Being a beta, there are open points where I would value your help:

1. **Feedback on the TaskContract API** — it is the heart of Lean tasks and the community will stress it better than my tests did.
2. **Stabilizing the experimental parts** — the .NET Tasks interop and the Burst-oriented job path need real-world usage before I freeze them.
3. **Docs and schedulers for other platforms** — if you are a happy user, reviewing the example READMEs or porting a scheduler to your favourite framework is the most valuable contribution.

## Conclusions

Svelto.Tasks 2.0 stays true to the idea the library was born with — iterators as tasks, runners as schedulers, zero surprises — while finally getting the packaging, the test suite and the 21 runnable examples it always deserved. Use it wherever C# compiles; reach for ExtraLean in hot gameplay paths; treat Lean tasks, the Tasks interop and the job path as tools for the cases that genuinely need them.

Everything is on GitHub in the [sebas77/Svelto.Tasks-Repo](https://github.com/sebas77/Svelto.Tasks-Repo) repository. If you have questions or spot problems, leave a comment here or join our populated [Discord server](https://discord.gg/JTUZuJcME5). Feedback on the beta is not only welcome, it is necessary!
