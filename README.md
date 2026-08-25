# Introducing Svelto.Tasks 2.0 beta: coroutines for every C# platform

## Introduction

*Svelto.Tasks* is the platform agnostic C# library that runs serial and parallel coroutines, even on other threads. It has been the quiet library behind many of my games for years, shipping in real products (*Robocraft*, *Cardlife*), yet it never got the attention it deserved. With the version **2.0** finished a long time ago, I decided to get help from AI to organize a proper package usable on every C# platform, and add a good test coverage plus several self-contained console examples. 

The API has settled through years of production use, the AI made test suite covers the core semantics properly, but I obviously cannot promise there are no bugs left. Two areas are explicitly **experimental**: the .NET `Tasks` integration (`SveltoAwaiter` and the new `TaskSynchronizationContext`) and the Burst-oriented job path when used inside *Unity*. They work, the examples demonstrate them, but consider their APIs more fluid than the rest.

One important thing to understand from the start: the core of Svelto.Tasks has no dependency on any engine. If you can compile C#, you can run Svelto.Tasks. The few Unity specializations (yield-instruction interop and the Unity-dedicated schedulers) live behind compiler defines and are strictly optional add-ons to an otherwise engine-agnostic core.

## Why I keep coming back to Svelto.Tasks from .net Tasks

.Net tasks has not been designed for games and especially two problems make me come back to Svelto.Tasks: being able to **profile** the tasks and being sure that tasks are **stopped** when I need to. When I leave a match and go back to the main menu, I want to be sure that every task belonging to that match is stopped.

That is where I find the `CancellationToken` pattern awkward and impractical: tokens must be created, passed down through every layer, checked at every step and remembered at every spawn site, and one forgotten call quietly leaves an orphan behind. A runner inverts the responsibility: tasks belong to their context, so stopping the context stops everything, all at once, by construction. `Pause()`, `Stop()` and `Dispose()` give me the certainty that cancellation tokens never could.

## The mental model: tasks are iterators, runners are schedulers

A task is any **iterator block** (in Unity you can call them co-routines). A **runner** ticks them according to a strategy called a **flow modifier**. That's the whole architecture, and it gives you something `async`/`await` fundamentally cannot: complete control over *when* and *where* your code executes.

This is (a trimmed version of) example number 1:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; FrameCounterTask() //this is the iterator block
{
    for (int i = 1; i &lt;= 10; i++)
    {
        frameCount = i;
        yield return TaskContract.Yield.It; //suspend until the next runner.Step()
    }
}

using (var runner = new SteppableRunner("GameLoopRunner")) //this is the runner!
{
    FrameCounterTask().RunOn(runner); //enqueue the task. It doesn't run yet!

    while (runner.hasTasks)           //your loop decides everything
        runner.Step();
}
</pre>

The user can decide when to step any SteppableRunner, while Multithreaded Runners handle the ticking themselves.

## How is this different from the Task pattern?

Before comparing them, one fact makes everything simpler: `async` methods and iterator blocks are both compiled into a **state machine**. The compiler chops your method into chunks around every `await` or `yield` and stores the chunk-to-execute-next in an object. That stored *"what runs after the pause"* is called the **continuation**. Both worlds use continuations, then. The whole difference is about **who holds the continuation, and who decides when, where and if it ever runs**:

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
         |
         v
  download finished -> the runtime grabs the continuation
  and resumes it 
         |
         v
  BuildScene(data) runs. Same story again
  for every other `await` in the method

  NOTE: can you choose WHERE the code resumes? Not by
  default: it lands on the ThreadPool, however in .net 
  two levers exist:
  - a CUSTOM AWAITER receives the continuation at every
    await and can resume it wherever IT wants
  - an installed SYNCHRONIZATIONCONTEXT catches instead
    every default await of the method
```

In .net, once the method started, your hands are off. The continuation travels with the awaited operation and comes back to life on a thread, at a moment, chosen by the infrastructure.

```text
THE SVELTO.TASKS PATTERN - your code is PULLED forward by your runner

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

Here the continuation never goes anywhere: it belongs to the task, and the task belongs to your runner. Since stepping always originates from your own `Step()` call, svelto tasks can only ever execute on the thread that owns the runner. Stop stepping, or `Dispose()` the runner, and every continuation out there is simply frozen forever. And because the runner inspects each yield before acting on it, these runners can also implement pacing strategies like **StaggeredFlow** or **TimeBoundFlow** to run tasks according to predefined rules.

## Ticking or handing over?

.Net tasks hands a continuation over to another thread once the previous slice is done, Svelto.Tasks runs the next slice on the next tick:

|                          | **hand-over (`async`/`await`) — push**  | **ticking (iterators + runners) — pull**       |
|--------------------------|-----------------------------------------|------------------------------------------------|
| cost while waiting       | none: the method sleeps till the event  | parked parents cost zero;                      |
| reaction latency         | immediate, event-driven                 | at best, the next `Step()`                     |
| who chooses the context  | the runtime (or awaiter/context author) | you, via the runner passed to `RunOn()`        |
| stopping mid-flight      | cooperative: tokens must be honored     | absolute: stop stepping, or dispose the runner |
| pacing / budgets         | cannot be imposed centrally             | flow modifiers see every step                  |
| profiling                | scattered wherever the runtime ran you  | one uniform hook inside the runner             |
| ecosystem                | the whole .NET speaks its language      | interop bridges needed                         |

Inside a world that already ticks 60 times a second, my coroutines' overhead is a handful of extra calls per frame — noise compared to what gameplay code does — and in exchange I get things the hand-over model cannot give me:

- **someone is always watching**: the runner sees every step, so pacing, budgeting and profiling are features of the design
- **stopping stays absolute**: no token to pass down, no orphaned continuation that can resurrect after its context died
- **costs stay visible**: N tasks times one cheap call per tick, easy to profile

## When continuations make sense in a game

Continuations were introduced to kill **callback hell**, and they deliver. Instead of nested callbacks reading upside-down:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//callback hell
Download(url, data =&gt; Parse(data, cfg =&gt;
          ShowPopup(cfg, choice =&gt; ApplyChoice(choice))));
</pre>

each step resumes where the previous stopped, so the code reads top-down like the execution it describes:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//same flow, readable again
GameConfig cfg  = Parse(await DownloadAsync(url));
PopupResult choice = await ShowPopup(cfg); //the rest of the method IS the continuation
ApplyChoice(choice);
</pre>

When an async sequence needs to run in a linear fashion, both Svelto.Tasks and .Net Tasks make sense to use, with the difference that Svelto.Tasks has been designed around game architecture needs.

## Plain Tasks are fine for services

Tasks are still fine to use for service layer async coroutines: HTTP requests, telemetry, cloud saves, asset downloads, login flows, as long as you can still control their flow.

Where I would reach for Svelto.Tasks instead is whenever I want **complete control over the execution**: which context resumes the code, when it may proceed, whether it can outlive its context, how much of it runs per tick. The moment a service's consumption becomes frame-sensitive — say, downloaded assets materializing progressively in the world — that control starts to matter, and runners are designed exactly for it.

## Lean or ExtraLean?

Svelto.Tasks come in two weights:

- **ExtraLean** tasks are plain `IEnumerator`s. They can only yield "wait" signals, which makes them extremely lean — ideal for the vast majority of gameplay coroutines.
- **Lean** tasks yield the **TaskContract**, a discriminated union that can also carry return values, continuations, break directives and nested enumerators. They power composition: waiting for children, returning results, cancelling chains.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//ExtraLean: the whole contract is "keep me waiting"
IEnumerator Countdown()
{
    for (int i = 3; i &gt; 0; i--) { Console.Write(i); yield return null; }
}

//Lean: the yield point becomes a rich instruction
IEnumerator&lt;TaskContract&gt; LoadLevel()
{
    yield return Download().Continue();          //wait for a child task
    var cfg   = loader.Current.ToRef&lt;GameConfig&gt;(); //collect its result
    yield return WaitForSecondsEnumerator(1f).Continue(); //then wait a second
}
</pre>

## Inside Lean Tasks: the TaskContract

Everything a Lean task can say to its runner passes through one type: the **TaskContract**. It is a `readonly struct` working as a **discriminated union**: an internal tag decides which of its overlapping fields is meaningful, and a set of implicit conversions lets you write what comes naturally while nothing allocates behind your back. A whole task vocabulary lives in that single struct:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; Vocabulary()
{
    yield return TaskContract.Yield.It;           //suspend until the runner steps me again
    yield return Download().Continue();           //suspend until this child task completes
    yield return SideEffect().Forget();           //queue the child, keep going right away
    yield return TaskContract.Continue.It;        //advance again WITHIN the same runner step
    yield return 42;                              //hand a value upward, no boxing involved
    yield return TaskContract.FromReference(cfg); //hand any reference upward
    yield return TaskContract.Break.It;           //end my cycle; my iterator stays reusable
}
</pre>

The caller reads results through explicit extraction: primitives with `ToInt()`, `ToFloat()`, `ToBool()`, references with `ToRef<T>()`:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var child = Produce();
yield return child.Continue();              //wait for the child

int        answer = child.Current.ToInt();
GameConfig cfg    = child.Current.ToRef&lt;GameConfig&gt;();
</pre>

Two TaskContract members deserve special mention because they are unique to this design. **`Continue.It`** tells the wrapper to call `MoveNext()` again immediately instead of returning to the runner: instant instructions chain within one step without paying a tick each. **`Break.It`**, instead, plays a trick on the C# language itself, and deserves its own subsection.

### Break.It: the state machine that refuses to die

Svelto.Tasks relies on special signals to add more semantic to the state machine:

- yield return TaskContract.Yield.it (equivalent to yield return null), means return here next step
- yield return TaskContract.Break.it (which is NOT the equivalent of yield break), means the task is now over.

A compiler-generated iterator dies only one way: `MoveNext()` returning false, which happens at sequence end or through `yield break`. Every other yield simply parks the machine where it stands. **`Break.It`** exploits exactly this gap: the runner-side bookkeeping treats the task as completed — the task is disposed and removed from the runner — but nobody drives the state machine to exhaustion. The `finally` blocks run, the object stays alive, frozen just after its `yield return Break.It` line. Call `MoveNext()` again and it wakes up at the top of the enclosing loop, good as new.

Svelto.Tasks exploits this mechanism, through the IteratorBlockPool<T> to achieve 0 allocations at run time, as preventing the allocation of new iterator blocks the only allocations still happening in recurring tasks inside a gameplay loop.
The pool pairs an immortal `while(true)` machine with a plain data-holder class, and recycles both forever:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; SpawnLoop(SpawnData data)
{
    while (true) //the state machine never dies
    {
        Spawn(data.kind);
        yield return TaskContract.Yield.It;  //advance one tick per spawn...

        yield return TaskContract.Break.It;  //...then end the cycle: task removed, machine kept
    }
}

var pool = new IteratorBlockPool&lt;SpawnData&gt;(SpawnLoop, "Spawns");

var (data, block) = pool.Get(); //allocates once, recycles forever after
data.kind = "Orc";              //re-initialize the data, run again
</pre>

### Errors and exceptions

Exceptions deserve their own paragraph because C# forces a constraint on us: a `yield return` cannot sit inside a `try`/`catch` block (only `try`/`finally`). Fallible code must live between yields, wrapped manually. What happens to exceptions then? Three layers, from implicit to deliberate.

If an exception escapes a task uncaught, the runner catches it, logs it through `Svelto.Console`, marks the task as faulted and removes it. Sibling tasks keep running untouched, and a caller waiting through `.Continue()` simply resumes at its next step: from the caller's perspective a faulted child looks like a completed one, so check logs or handle errors explicitly when correctness depends on it.

For deliberate error handling, the contract itself can carry an exception upward. Yielding one ends the task while handing the payload to whoever was waiting:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; RiskyStep()
{
    Result r;
    try   { r = Parse(DownloadedBytes()); }        //fallible work between yields
    catch (Exception e)
    {
        yield return new TaskContract(e);           //end here, hand the error to my caller
        yield break;                                //never reached
    }

    yield return Process(r).Continue();             //normal path
}

IEnumerator&lt;TaskContract&gt; Caller()
{
    var risky = RiskyStep().Continue();
    yield return risky;

    if (risky.Current.ToRef&lt;Exception&gt;() is Exception error)
        Recover(error);                             //the caller decides what to do
}
</pre>

Between the two extremes — silent faults and hand-carried exceptions — you can pick per task how loud failures should be. What the runner never does is let one broken task take down the others: isolation is part of the deal.

## Customising runners

Runners offer two natural seams for specialisation, and interestingly they sit at opposite ends of the abstraction: the *task type* and the *iteration strategy*.

The first seam is the generic parameter itself. `GenericSteppableRunner<TTask>` accepts any `ISveltoTask`, and since generics devirtualise calls and keep structs inline, T can be a **hand-written struct state machine**: no iterator block, no heap object, no pooling needed — stepping such a task touches zero heap allocations:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//a struct implementing IEnumerator&lt;TaskContract&gt;: the JIT never boxes it through the runner
struct Blink : IEnumerator&lt;TaskContract&gt;
{
    readonly float _duration;
    float          _elapsed;

    public Blink(float duration) : this() => _duration = duration;

    public TaskContract Current =>
        _elapsed &lt; _duration ? TaskContract.Yield.It : TaskContract.Break.It;

    public bool MoveNext()
    {
        _elapsed += UnityEngine.Time.deltaTime;
        return true; //completion arrives through the Break.It contract above
    }

    public void Reset() => _elapsed = 0f;
    public void Dispose() { }
}
</pre>

However, in years of shipping games I have never used this feature in practice. Hand-writing state machines is exactly the kind of work iterator blocks exist to avoid, and pooled blocks already reach zero steady-state allocations for everything cyclical. The struct path remains there for the cases where even the pool feels like too much machinery, but I would start from iterator blocks and let profiling tell me otherwise.

The second seam is the one I actually exercise: the **flow modifier**. A runner does not hardcode who gets processed each tick; it asks an `IFlowModifier` three questions — may this index run now, may we advance to the next task, and reset for the new frame. Every pacing strategy shipped with the library is just an answer to those questions:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//the shape of a custom pacing policy (sketch)
class PriorityFlow : IFlowModifier
{
    public bool CanProcessThis(ref int index) =&gt;
        index &lt; _budget; //only the highest-priority tasks fit this tick

    public bool CanMoveNext<T>(ref int nextIndex, int coroutinesCount, bool hasCoroutineCompleted)
        where T : ISveltoTask =>
        ++nextIndex < coroutinesCount;

    public void Reset() { }
}

runner.UseFlowModifier(new PriorityFlow());
</pre>

**StaggeredFlow**, **TimeBoundFlow** and **TimeSlicedFlow** are little more than different answers to those same three questions, which is why they cost a handful of lines each. In my experience, "writing your own runner" almost always means writing your own flow instead. Truly new runners are only justified when the driving loop itself changes shape — a `SyncRunner` that drains tasks synchronously until completion, or a `MultiThreadRunner` that owns its dedicated thread — and even those are thin shells around the shared task-processing core.

## Why not simply async/await for gameplay?

Fair question! `async`/`await` is great infrastructure, and Svelto.Tasks 2.0 interoperates with it (examples 16 and 21 show how). But gameplay code usually wants the opposite trade-off: to know *exactly* when and where things run, and to be able to stop them. With Tasks-based code, once you fire an async operation, you mostly hope it completes and you juggle cancellation tokens everywhere.

With runners, lifecycle is explicit and absolute. The feature I value most: **runners can be stopped at any time**, which lets you tie a set of tasks to an isolated context and be sure that none of its tasks is still running once the context is gone:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//a match owns every coroutine of that session
_matchRunner = new MultiThreadRunner("MatchSession");
SpawnEffects().RunOn(_matchRunner);
UpdateAI().RunOn(_matchRunner);
UploadTelemetry().Forget(); //even fire-and-forget work belongs to the match

//match over: no orphan coroutine can outlive it. guaranteed.
_matchRunner.Dispose();
</pre>

A level owns its runner → unloading the level cannot leave coroutines ticking into destroyed objects. A UI screen owns its runner → closing the screen kills its animations without unwinding dozens of tokens. `Pause()`/`Resume()` give you the same control temporarily (freeze a background worker mid-state without tearing it down), and `Flush()`/`Stop()` let a runner be reused after cleanup. Try expressing "these hundred coroutines belong to this context and must not outlive it" with plain Tasks. You'll end up rebuilding a poor man's runner anyway 🙂

## Taking over coroutines, Tasks and Unity Jobs patterns

One way to look at 2.0: whatever concurrency pattern you use today, Svelto.Tasks has a counterpart designed to absorb it.

- **Unity coroutines**: iterator tasks *are* Unity-coroutine-shaped, minus the engine lock. Yield `Yield.It` instead of `null`, run them on a runner instead of a `MonoBehaviour`, and they keep working outside the editor, on dedicated servers, in tests. Unity-specific glue (like yield-instruction interop) exists behind defines for when you need it.
- **Tasks / async-await**: await a Svelto runner directly so continuations resume on *your* thread, not the ThreadPool; or go the other way around and host entire `async` methods on a runner through the experimental `TaskSynchronizationContext`. Both directions are shown in the examples.
- **Unity Jobs**: the data-parallel pattern maps onto `ISveltoJob` + `MultiThreadedParallelJobCollection<T>`, which splits N iterations across M worker threads exactly like an `IJobParallelFor` would. On Unity these job structs can be Burst-compiled — that path is marked experimental.

**Svelto.Tasks is not a true replacement for Unity Jobs**, and I am not going to pretend otherwise. Jobs+Burst is a compiler pipeline: your job code gets transformed into highly vectorized native code, backed by a safety system and deep player-loop integration that plain C# cannot replicate. If you need maximum raw throughput on tightly packed numeric loops inside *Unity*, use Jobs. Where Svelto.Tasks shines is everything around those hot kernels: orchestrating serial pipelines, staggering work across frames, bounding frame budgets, coordinating threads — with one consistent API on every platform.

## Performance and zero allocations

Performance was a first-class design constraint since 1.x. My benchmarks show Svelto.Tasks trading blows with *UniTask* and *Unity Jobs*: comparable throughput on coroutine stepping, comparable scaling on multi-threaded fan-out. I will publish the profiling numbers and captures separately, so you can judge for yourself rather than trusting my word for it.

*(benchmark tables and profiler screenshots to be added here)*

Zero-allocation usage is a set of patterns rather than magic, and 2.0 makes them easy:

- **ExtraLean tasks + struct iterators**: runners accept struct enumerator types (`SteppableRunner<T>`), so stepping a task touches zero heap objects.
- **Pooled iterator blocks**: `IteratorBlockPool<T>` recycles `while(true)` state machines via `Break.It`; blocks and their data survive across uses (examples 07 and 15 prove reuse with reference equality).
- **Pooled continuations**: every `.Continue()` hands back a pooled handle; nothing is allocated per wait.
- **Preallocated collections**: `SerialTaskCollection`/`ParallelTaskCollection` own their storage upfront.
- The usual discipline applies: avoid closures and LINQ in per-frame tasks. Value extraction through `ToInt()`/`ToRef<T>()` stays explicit, but since 2.0 nothing boxes: typed constructors store primitives in an inline union.

## The examples, one by one

Documentation was historically my libraries' weak spot, so this time every feature ships as a minimal runnable demo with a README explaining scenario, API and gotchas. Each folder under `Examples/` is independent:

<pre class="EnlighterJSRAW" data-enlighter-language="bash">
cd Examples/01_GameLoop
dotnet run
</pre>

The most important extract of each one:

### 01 — GameLoop: Lean task + SteppableRunner

A simulated game loop ticks a runner once per frame while a task counts frames, yielding between counts. Teaches the fundamental cycle: `RunOn`, `Step()`, `hasTasks`. If you understand this example, you understand half of Svelto.Tasks — the full loop is shown at the top of this article.

### 02 — SimpleCoroutine: ExtraLean task

Same countdown, written as a plain `IEnumerator` on an ExtraLean runner. No `TaskContract` processing at all — the cheapest possible coroutine.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator Countdown() //plain IEnumerator: only null/Yield/Break allowed
{
    int count = 3;
    while (count &gt; 0)
    {
        Console.Write(count--);
        yield return null; //wait exactly one step
    }
}
countdown.RunOn(extraLeanRunner);
</pre>

### 03 — LoadingPipeline: TaskContract return values

A child task downloads and parses a config, then hands it to its parent. Values flow upward without globals or callbacks.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; DownloadAndParse()
{
    ...download + parse...
    yield return TaskContract.FromReference(cfg); //hand the result up
}

IEnumerator&lt;TaskContract&gt; Parent()
{
    var child = DownloadAndParse();
    yield return child.Continue();                 //wait for the child

    GameConfig cfg = child.Current.ToRef&lt;GameConfig&gt;(); //extract the result
}
</pre>

### 04 — ContinueChildTask: `.Continue()`

The simplest composition primitive: a parent delegates to a child **on the same runner** and parks until the child completes. Distinct from `.RunOn(runner)`, which targets a specific runner and returns a pollable continuation instead.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
yield return Child().Continue(); //same runner: parent suspends until done
parentResult = childCounter * 10; //resumes only after Child finished
</pre>

### 05 — BackgroundComputation: RunOn + Continuation

Heavy math runs on a `MultiThreadRunner` (one dedicated background thread per runner) while the main thread polls. Real cross-thread parallelism with results published through volatile fields.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
using (var bgRunner = new MultiThreadRunner("BgCompute"))
{
    Continuation cont = HeavyComputation().RunOn(bgRunner);

    while (cont.isRunning) //main thread stays free meanwhile
        DrawProgress();

}   //Dispose() stops the background thread cleanly
</pre>

### 06 — FireAndForgetLogging: `.Forget()`

Telemetry fired without waiting: the parent continues immediately and the child interleaves afterwards on the same runner. The recorded order `[1] [4] [2] [3]` is captured while tasks run, proving the scheduling instead of asserting it.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; Parent()
{
    Step(1);                        //gameplay keeps going...
    yield return Child().Forget();  //...child is queued but NOT awaited
    Step(4);                        //this runs BEFORE the child finishes
}
</pre>

### 07 — ReusableSpawnLoop: Break.It + pooling

`yield return Break.It` ends a cycle **without** destroying the state machine. Combined with `IteratorBlockPool<T>`, spawn loops become reusable objects: the demo proves the very same block and data instances come back from the pool, reference-equal.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; SpawnLoop(EntityData data)
{
    while (true) //the state machine never dies
    {
        Spawn(data);
        yield return TaskContract.Break.It; //end cycle, stay alive, back to pool
    }
}

var (data, block) = pool.Get(); //second Get returns the SAME instances
data.kind = "Orc";              //re-initialize, then run again
</pre>

### 08 — CancellableChain: forwarding failures

Load → Validate → Process, launched by a parent. Validation fails and the failure must cancel everything above it. The example teaches a subtle truth: `Break.AndStop` propagates **exactly one level up** — a killed task cannot forward its own break — so the chain forwards deliberately. Process is skipped, Parent is cancelled, and the summary derives from flags written while tasks ran.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//leaf: stop only myself, let my caller decide
validationFailed = true;
yield return TaskContract.Break.It;

//middle level: forward the failure so EVERYTHING above cancels too
yield return ValidateStep().Continue();
if (validationFailed)
    yield return TaskContract.Break.AndStop; //Process skipped AND Parent cancelled

yield return ProcessStep().Continue(); //never reached
</pre>

### 09 — OrderedLoading: SerialTaskCollection

Download → parse → initialize with strict ordering guarantees: the collection won't touch the next task before the current one finished. Ideal for loaders whose stages depend on each other.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var serial = new SerialTaskCollection("LevelLoader");
serial.Add(DownloadStage());
serial.Add(ParseStage());
serial.Add(InitializeStage());

serial.Complete(10000); //strictly one after another
</pre>

### 10 — ConcurrentAnimations: ParallelTaskCollection

Three UI bars progress together: each pass advances every still-running task once, round-robin, cooperatively on one thread. Concurrency here means *interleaving*, and that's usually exactly what game logic wants.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var parallel = new ParallelTaskCollection("UIAnimations");
parallel.Add(HealthBar());
parallel.Add(ManaBar());
parallel.Add(XpBar());

while (parallel.MoveNext()) //one MoveNext advances ALL bars by one tick
    DrawFrame();
</pre>

### 11 — AIBudgetStaggered: StaggeredFlow

Ten AI units, at most three thinking per tick. One line caps CPU spikes. The demo honestly documents the flip side: excess tasks starve until slots free up.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.UseFlowModifier(new StaggeredFlow(3)); //max 3 AI tasks processed per tick
</pre>

### 12 — FrameBudgetTimeBound: TimeBoundFlow

Instead of counting tasks, count milliseconds: process background work for at most 20ms per tick, wall-clock measured. The demo shows who ran and who starved each tick, and explains when to prefer completing tasks versus switching to fairer strategies.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.UseFlowModifier(new TimeBoundFlow(20f)); //at most 20ms per tick
</pre>

### 13 — BatchPathfinding: ParallelJobCollection

1000 pathfinding iterations split across four threads via `ISveltoJob` — the Svelto counterpart of `IJobParallelFor`. Every unit records which thread processed it, and the final report verifies the fan-out. On Unity, job structs like these are candidates for Burst compilation (experimental).

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
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
</pre>

### 14 — ParallelDownloads: MultiThreadedParallelTaskCollection

Four downloads on four real threads simultaneously, with a monitor thread redrawing progress bars while workers advance. Overlap is visible, not claimed.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
using var downloads = new MultiThreadedParallelTaskCollection("Downloads", 4, false);
downloads.Add(new DownloadTask("File_1.zip", progress1));
downloads.Add(new DownloadTask("File_2.zip", progress2));
downloads.Add(new DownloadTask("File_3.zip", progress3));
downloads.Add(new DownloadTask("File_4.zip", progress4));

downloads.Complete(); //four OS threads downloading simultaneously
</pre>

### 15 — EntitySpawnPool: IteratorBlockPool visualized

Spawns entities from a pool, runs their lifecycles, recycles them via `Break.It`, and shows pool occupancy live. Ends with a verification box proving the recycled block and data are the *same objects* obtained again.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
_pool = new IteratorBlockPool&lt;EntityData&gt;(EntityLifecycle, "EntityPool");

var (data, block) = _pool.Get();      //take from the pool (or allocate once)
data.EntityId = ++_totalSpawned;
...step block.MoveNext() each tick...

//lifecycle yields Break.It at despawn:
//block auto-returns to the pool, state machine kept alive
int available = _pool.count;          //live pool occupancy
</pre>

### 16 — AsyncHttpAwaiter: awaiting a runner

Bridges `async`/`await` into the library: the continuation after the `await` resumes on the runner's thread, interleaved with its other tasks. Experimental .NET Tasks integration, part one.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
async Task SimulateHttpRequest()
{
    SendRequest();
    await Task.Delay(800).RunOn(runner); //resume ON the runner, not the ThreadPool
    ReceiveResponse();                   //interleaved with the runner's other tasks
}
</pre>

### 17 — PauseMenu: Pause/Resume

Opening the pause menu freezes a `MultiThreadRunner` dead in its tracks; closing it resumes every task exactly where it stopped. Snapshot checks in the demo prove nothing advanced while paused.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.Pause();
Debug.Assert(counterBeforePause == ReadCounter()); //frozen solid
runner.Resume();
</pre>

### 18 — RecursiveTreeTraversal: deep continuation chains

Walks a scene-graph tree depth-first through recursive `.Continue()` calls — parents suspend on children that suspend on grandchildren. Doubles as a stress case showing continuation chains surviving internal list resizes.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; Traverse(TreeNode node)
{
    Visit(node);

    foreach (var child in node.Children)
        yield return Traverse(child).Continue(); //recursion as continuations
}
</pre>

### 19 — DelayedSpawn: WaitForSecondsEnumerator

An enemy spawns two seconds after level start, gated by a reusable seconds-waiter. Wall-clock based, resettable, and available in a struct variant for allocation-sensitive loops.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var wait = new WaitForSecondsEnumerator(2f); //wall clock, Reset()-able

while (wait.MoveNext())                      //poll until the wait is over
    yield return TaskContract.Yield.It;

SpawnEnemy();                                //exactly 2s later
</pre>

### 20 — CrossThreadSignal: WaitForSignal

A background thread computes and signals; the main-thread task waits through the same signal. A tiny typed subclass gives a clean producer/consumer handshake between runners.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
class DataReadySignal : WaitForSignal&lt;DataReadySignal&gt; {}

//background thread, when the data is ready:
_dataReady.Signal();

//main-thread task:
yield return _dataReady.Wait(); //suspend until Signalled()
Consume(computedData);
</pre>

### 21 — StopRunnerNetTask: hosting async methods on a runner

The most experimental piece: `TaskSynchronizationContext` hosts whole `async` methods on any Lean runner — every `await` continuation resumes on the runner's thread, ordered with your other coroutines. Disposing the runner mid-await abandons hosted work deterministically: frozen forever, then collectable. Isolated-context semantics, now for async code too.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var context = new TaskSynchronizationContext(runner);

context.Run(async () =&gt;
{
    var page = await http.GetStringAsync(url); //any suspension
    Parse(page);                               //back on the RUNNER thread
});

//runner.Dispose() mid-await: hosted work abandoned deterministically
</pre>

## What's next

Being a beta, there are open points where I would value your help:

1. **Feedback on the TaskContract API** — it is the heart of Lean tasks and the community will stress it better than my tests did.
2. **Stabilizing the experimental parts** — the .NET Tasks interop and the Burst-oriented job path need real-world usage before I freeze them.
3. **Docs and schedulers for other platforms** — if you are a happy user, reviewing the example READMEs or porting a scheduler to your favourite framework is the most valuable contribution.

## Conclusions

Svelto.Tasks 2.0 stays true to the idea the library was born with — iterators as tasks, runners as schedulers, zero surprises — while finally getting the packaging, the test suite and the 21 runnable examples it always deserved. Use it wherever C# compiles; reach for ExtraLean in hot gameplay paths; treat Lean tasks, the Tasks interop and the job path as tools for the cases that genuinely need them.

Everything is on GitHub in the [sebas77/Svelto.Tasks-Repo](https://github.com/sebas77/Svelto.Tasks-Repo) repository. If you have questions or spot problems, leave a comment here or join our populated [Discord server](https://discord.gg/JTUZuJcME5). Feedback on the beta is not only welcome, it is necessary!
