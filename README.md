# Svelto.Tasks 2.0: a multithreaded, allocation-free tasks runner for C# — massive parallelism, async/await interoperable, built for games

## Introduction

*Svelto.Tasks* is the platform-agnostic C# library that runs serial and parallel coroutines, even on other threads. It has been the quiet library behind many of my games for years, shipping in real products (*Robocraft*, *Cardlife*), yet it never got the attention it deserved. For version **2.0**, I decided to get help from AI to organize a proper package usable on every C# platform, add good test coverage, and provide several self-contained console examples.

The API has settled through years of production use, and the AI-made test suite covers the core semantics properly, but I obviously cannot promise there are no bugs left. Two areas are explicitly **experimental**: the .NET `Tasks` integration (`SveltoAwaiter` and the new `TaskSynchronizationContext`) and the Burst-oriented job path when used inside *Unity*. They work, the examples demonstrate them, but consider their APIs more fluid than the rest.

Svelto.Tasks has no dependency on any engine. If you can compile C#, you can run Svelto.Tasks. The few Unity specializations (yield-instruction interop and the Unity-dedicated schedulers) live behind compiler defines and are strictly optional add-ons to an otherwise engine-agnostic core.

## Why I keep coming back to Svelto.Tasks from .NET Tasks

My take is that .NET Tasks were (obviously) not designed for games, and two problems especially make me come back to Svelto.Tasks: being able to **profile** tasks and being sure they are **stopped** when I need them to be. For example: when I leave a match and go back to the main menu, I want to be sure that every task belonging to that match is stopped.

That is where I find the `CancellationToken` pattern awkward and impractical: tokens must be created, passed down through every layer, checked at every step, and remembered at every spawn site. One forgotten call quietly leaves an orphan behind. A runner inverts the responsibility: tasks belong to their context, so stopping the context stops everything, all at once, by construction. `Pause()`, `Stop()` and `Dispose()` give me the certainty that cancellation tokens never could.

## The mental model: tasks are iterators, runners are schedulers

A task is any **iterator block** (in Unity you can call them co-routines). A **runner** ticks them according to a strategy called a **flow modifier**. That's the whole architecture, and it gives you something `async`/`await` fundamentally cannot: complete control over *when* and *where* your code executes.

This is (a trimmed version of) example number 1:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; FrameCounterTask() //this is the iterator block
{
    for (int i = 1; i &lt;= 10; i++)
    {
        frameCount = i;
        yield return TaskContract.Yield.It; //this doesn't handle the continuator to the context, but suspend the task until the next runner.Step()
    }
}

using (var runner = new SteppableRunner("GameLoopRunner")) //this is the runner!
{
    FrameCounterTask().RunOn(runner); //enqueue the task. It doesn't run yet!

    while (runner.hasTasks)           //your loop decides everything
        runner.Step();
}
</pre>

You can decide when to step any `SteppableRunner` and thus, when not to step them.

## How is this different from the Task pattern?

Before comparing them, one fact makes everything simpler: `async` methods and iterator blocks are both compiled into a **state machine**. The compiler chops your method into chunks around every `await` or `yield` and stores the chunk-to-execute-next in an object. That stored *"what runs after the pause"* is called the **continuation**. Both worlds use continuations, with a big difference: co-routines cannot yield to other coroutine out of the box. Svelto.Tasks introduces an API to let coroutine run exactly like .Net tasks.

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
  default: it lands on the ThreadPool. However, in .NET
  two levers exist:
  - a CUSTOM AWAITER receives the continuation at every
    await and can resume it wherever IT wants
  - an installed SYNCHRONIZATIONCONTEXT catches instead
    every default await of the method
```

In .NET, once the method starts, your hands are off. The continuation travels with the awaited operation and comes back to life on a thread, at a moment, chosen by the infrastructure.

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

## Ticking or handing over?

.NET Tasks hand a continuation over to another thread once the previous slice is done; Svelto.Tasks runs the next slice on the next tick:

|                          | **hand-over (`async`/`await`) — push**  | **ticking (iterators + runners) — pull**       |
|--------------------------|-----------------------------------------|------------------------------------------------|
| cost while waiting       | none: the method sleeps till the event  | parked parents cost zero;                      |
| reaction latency         | cost of resuming a thread, event-driven | at best, the next `Step()`, polling            |
| who chooses the context  | the runtime (or awaiter/context author) | you, via the runner passed to `RunOn()`        |
| stopping mid-flight      | cooperative: tokens must be honored     | absolute: stop stepping, or dispose the runner |
| pacing / budgets         | cannot be imposed centrally             | flow modifiers see every step                  |
| profiling                | scattered wherever the runtime ran you  | one uniform hook inside the runner             |

Inside a world that already ticks 60 times a second, my coroutines' overhead is a handful of extra calls per frame — noise compared to what gameplay code does — and in exchange I get things the hand-over model cannot give me:

- **someone is always watching**: the runner sees every step, so pacing, budgeting and profiling are features of the design
- **stopping stays absolute**: no token to pass down, no orphaned continuation that can resurrect after its context died
- **costs stay visible**: N tasks times one cheap call per tick, easy to profile

## When continuations make sense in a game

Continuations were introduced to eliminate the so called **callback hell**, and they deliver. Instead of nested callbacks reading upside-down:

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

When an async sequence needs to run in a linear fashion, both Svelto.Tasks and .NET Tasks make sense, with the difference that Svelto.Tasks has been designed around game architecture needs.

## .Net Tasks are fine for services

Tasks are still fine for service-layer async operations: HTTP requests, telemetry, cloud saves, asset downloads, and login flows, as long as you can still control their flow.

Where I would reach for Svelto.Tasks instead is whenever I want **complete control over the execution**: which context resumes the code, when it may proceed, whether it can outlive its context, how much of it runs per tick. The moment a service's consumption becomes frame-sensitive — say, downloaded assets materializing progressively in the world — that control starts to matter, and runners are designed exactly for it.

## Lean or ExtraLean?

Svelto.Tasks comes in two weights:

- **ExtraLean** tasks are plain co-routines. They can only yield "wait" signals, which makes them extremely lean — ideal for the vast majority of gameplay coroutines.
- **Lean** tasks yield the **TaskContract**, a discriminated union that can also carry return values, continuations, break directives and nested enumerators. They power the Svelto.Tasks composition logic: waiting for children, returning results, cancelling chains.

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
    yield return Download().Continue();           //suspend until this child task completes, immediately execute the task until the first yield
    yield return SideEffect().Forget();           //queue the child, keep going right away
    yield return TaskContract.Continue.It;        //advance again WITHIN the same runner step
    yield return 42;                              //hand a value upward, no boxing involved
    yield return TaskContract.FromReference(cfg); //hand any reference upward
    yield return TaskContract.Break.It;           //end my cycle; my iterator stays reusable
    yield return TaskContract.Break.AndStop;      //end me AND every parent waiting on me
}
</pre>

The caller reads results through explicit extraction: primitives with `ToInt()`, `ToFloat()`, `ToBool()`, references with `ToRef<T>()`:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var child = Produce();
yield return child.Continue();              //wait for the child

int        answer = child.Current.ToInt();
GameConfig cfg    = child.Current.ToRef&lt;GameConfig&gt;();
</pre>

Three TaskContract members are unique to this design. **`Continue.It`** tells the wrapper to call `MoveNext()` again immediately instead of waiting for the next runner step: instant instructions chain within one step without paying a tick each. **`Break.It`** and **`Break.AndStop`**, instead, play a trick on the C# language itself, and get their own article subsections.

### Break.It: the state machine that refuses to die

Svelto.Tasks relies on special signals to add more semantics to the state machine:

- `yield return TaskContract.Yield.It` (equivalent to `yield return null`) means return here on the next step.
- `yield return TaskContract.Break.It` (which is NOT the equivalent of `yield break`) means the task is now over.
- `yield return TaskContract.Break.AndStop` means the task is now over, *and* every parent waiting on it through `.Continue()` is over too.

A compiler-generated iterator dies when `MoveNext()` returns false, which happens at sequence end or through `yield break`. Every yield instead parks the machine at that exact point. **`Break.It`** marks that point as the end of one reusable cycle: runner-side bookkeeping treats the task as completed, while the pooled state machine remains suspended just after its `yield return Break.It` line. On the next use, `MoveNext()` resumes after that yield, reaches the end of the enclosing loop, and starts its next iteration.

Svelto.Tasks exploits this mechanism through `IteratorBlockPool<T>` to achieve zero allocations at runtime, preventing new iterator blocks from becoming the only allocations left in recurring tasks inside a gameplay loop.
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

Reusable blocks resume; they do not restart. Put `Break.It` only at a deliberate cycle boundary inside the infinite loop, and make sure everything after it safely leads back to the top. Locals captured by the iterator survive between cycles, so reset all per-run state at the start of the loop or keep it in the pooled data object and re-initialize that object after every `Get()`. Do not retain references or resources from the previous borrower across the break. A block that reaches `yield break`, falls off the end, or is stopped before reaching its cycle boundary cannot safely be reused.

### Break.AndStop: stopping the entire chain

`Break.It` is polite: it stops the task that yields it and lets the parent continue as if the child had simply completed. Sometimes that is not what you want. When a deep child hits a fatal condition, you do not want every ancestor to poll a flag and unwind politely one per frame — you want the whole pipeline gone. **`Break.AndStop`** is that kill switch: the runner disposes the task that yields it together with its entire `.Continue()` ancestry, in one pass:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; LoadPipeline()
{
    yield return DownloadAsset().Continue(); //wait for the deep child
    SpawnLevel();                            //never reached if the checksum fails
}

IEnumerator&lt;TaskContract&gt; DownloadAsset()
{
    if (ChecksumFails())
        yield return TaskContract.Break.AndStop; //kill me, LoadPipeline and whoever waits on it

    ...                                          //download, verify, hand the bytes upward
}
</pre>

The chain is disposed, not parked: unlike `Break.It`, nothing here is meant to run again. There is one boundary to remember: `Break.AndStop` travels along `.Continue()` chains on the same runner only. A task handed to another runner with `.RunOn()` is a root on its own — `Break.AndStop` inside it cannot cross over, and the parent waiting through `RunOn` simply resumes when the child completes. The same rule holds inside task collections, which unwind themselves and propagate the stop to whoever was waiting on them.

### Errors and exceptions

Exceptions deserve their own paragraph because C# forces a constraint on us: a `yield return` cannot sit inside a `try`/`catch` block (only a `try`/`finally` block). Fallible work must live between yields, with any error stored until the iterator reaches its next yield point.

If an exception escapes a task uncaught, the runner catches it, marks the task as faulted, disposes it, and keeps ticking its siblings. It reports the exception through the global **`TaskExceptionStrategy`**. By default, `LogTaskExceptionStrategy` sends it to `Svelto.Console`, but applications can replace the strategy to forward failures to their own reporting system. A caller waiting through `.Continue()` simply resumes at its next step: from the caller's perspective, a faulted child looks like a completed one.

That reporting is a deliberate plugin point, not a hard-wired behavior: implementing **`ITaskExceptionStrategy`** and assigning it to `TaskExceptionStrategy.Current` routes uncaught faults to any external system, from a custom logger to a cloud crash reporter. Implementations must be thread-safe, since several multithreaded runners can report concurrently, and a strategy that itself throws while reporting is caught and logged on its own: a broken reporting pipeline must never be able to alter how runners dispose faulted tasks and tick their siblings.

For deliberate error handling, the contract itself can carry an exception upward. An exception-valued `TaskContract` completes the task while handing its payload to whoever was waiting:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
IEnumerator&lt;TaskContract&gt; RiskyStep()
{
    Result r;
    Exception error = null;
    try   { r = Parse(DownloadedBytes()); }        //fallible work between yields
    catch (Exception e)
    {
        error = e;                                  //a catch block cannot yield
    }

    if (error != null)
        yield return new TaskContract(error);       //complete here, hand the error to my caller
    else
        yield return Process(r).Continue();         //normal path
}

IEnumerator&lt;TaskContract&gt; Caller()
{
    var risky = RiskyStep().Continue();
    yield return risky;

    if (risky.Current.ToRef&lt;Exception&gt;() is Exception error)
        Recover(error);                             //the caller decides what to do
}
</pre>

Between runner-level fault reporting and hand-carried exceptions, you can pick per task how explicit failure handling should be. What the runner never does is let one broken task take down the others: isolation is part of the deal.

## Customising runners and FlowModifiers

Flow modifiers decide how tasks advance during one `Step()`:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.UseFlowModifier(new StandardFlow());      //every task advances once
runner.UseFlowModifier(new SerialFlow());        //one task advances until complete
runner.UseFlowModifier(new StaggeredFlow(3));    //at most three tasks advance
runner.UseFlowModifier(new TimeBoundFlow(5f));   //advance tasks for about 5 ms
runner.UseFlowModifier(new TimeSlicedFlow(5f));  //keep cycling through tasks for about 5 ms
</pre>

- **StandardFlow** is the default: every live task advances once per `Step()`.
- **SerialFlow** advances one task until it completes, then moves to the next.
- **StaggeredFlow(n)** advances at most `n` tasks per `Step()`; the others wait for the next one. The budget always restarts from the first task, so tasks that never complete starve the ones behind them — it is a cap, not a round-robin.
- **TimeBoundFlow(milliseconds)** advances tasks until the time budget expires; the remaining tasks wait for the next `Step()`. The budget is cooperative: elapsed time is checked between task steps, so one long `MoveNext()` can overshoot it.
- **TimeSlicedFlow(milliseconds)** does the same, but wraps to the first task when it reaches the end, so tasks can advance more than once in the same `Step()`.

You can create your own, pluggable, flor modifier if you ever need to.

## Controlling runner lifetime

Tasks belong to their runner, so runner lifetime controls task lifetime:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.Pause();   //freeze every task where it is
runner.Resume();  //continue from the same yield points

runner.Stop();    //asynchronously stop running tasks; runner stays reusable
runner.Flush();   //synchronously dispose running and queued tasks; runner stays reusable
runner.Dispose(); //dispose all tasks, terminate the worker, and reject new work
</pre>

- **Pause** freezes running and queued tasks without disposing anything. New tasks may still be queued and start after `Resume()`. Pause takes effect on the next pass: a `MoveNext()` already in flight when `Pause()` is called finishes first.
- **Stop** is asynchronous. It stops tasks already running on the next processing pass. Tasks queued while it stops wait, then run after the runner automatically unstops.
- **Flush** disposes both running and queued tasks and leaves the runner ready for new work. On a `MultiThreadRunner`, it blocks until cleanup completes and rejects submissions while cleanup is in progress; the same worker thread remains alive for reuse.
- **Dispose** is terminal. It rejects further scheduling, disposes every task, signals the `MultiThreadRunner` worker to exit, and waits for that worker to terminate.

Runner shutdown is cooperative: a task must return from its current `MoveNext()` call before a `MultiThreadRunner` can process `Flush()` or `Dispose()`. Both operations reject calls made from the worker itself, and throw `MultiThreadRunnerException` if cleanup or termination exceeds the two-second safety timeout. They cannot forcibly abort an infinite loop or blocking call inside a task.

### Thread safety, the whole library in one place

The boundaries are thread-safe by construction: submitting a task to any runner from any thread is safe — admission is serialized and queued through a concurrent structure. So are the iterator pools, the continuation handles you may poll from a foreign thread, and the `WaitForSignal<T>` handshake.

Task bodies are not magic: a task runs on the thread of its runner, and if it touches data shared with other threads, synchronizing that data is your job, not the library's — `volatile`, `Interlocked` and locks as usual. In return the library guarantees the useful half: a task's state machine is only ever touched by its owning runner, so a task that keeps its state to itself is thread-safe without a single lock. On the steppable runners the same rule takes a specific form: submit from anywhere, but step from one thread only — the owner of the loop.

# .Net Tasks and Svelto.Tasks interop

The .NET Tasks interop works in both directions. The natural one brings existing async code *into* a runner: `await someDotNetTask.RunOn(runner)` wraps the real `TaskAwaiter` (or `ValueTaskAwaiter`) so that when the task completes, the continuation after the `await` is enqueued on the runner instead of the ThreadPool. The other direction pushes an iterator *out* to async land: `await enumerator.ToTask<T>(runner)` returns a `ValueTask` that completes with the task result — `T` must be a reference type, since the contract carries references, not generic values. For whole async methods that should *live* on a runner, the experimental `TaskSynchronizationContext` hosts them through the standard `SynchronizationContext` mechanism: every internal `await` continuation resumes on the runner's thread, interleaved with the other tasks.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//existing async code, kept on the runner thread where it matters:
async Task&lt;PlayerProfile&gt; DownloadProfile(SteppableRunner runner)
{
    //standard await: this continuation resumes on the ThreadPool, as always
    var response = await _httpClient.GetAsync(profileUrl);

    response.EnsureSuccessStatusCode();

    //RunOn: this continuation is enqueued on the runner instead
    var json = await response.Content.ReadAsStringAsync().RunOn(runner);

    //deserialization runs on the runner thread: game data is safe to touch here
    return JsonSerializer.Deserialize&lt;PlayerProfile&gt;(json);
}
</pre>

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//or host whole async methods on a runner, through the standard SynchronizationContext mechanism:
var context = new TaskSynchronizationContext(runner);

context.Run(async () =&gt;
{
    var data = await ComputeOnJob();  //every internal await resumes on the runner thread,
    Publish(data);                    //interleaved with the runner's other tasks
});
</pre>

One semantic is deliberate everywhere: if the runner is disposed before a bridged task completes, the pending continuation is never run — the abandoned async method stays frozen, exactly like any other task the runner was carrying. The whole interop surface is marked experimental: solid enough for the demos, but I want real-world feedback before I freeze it.

## The multithreaded runners

Everything I described about lifetime controls applies to the **`MultiThreadRunner`** family, the runners that own their own background thread. Tasks on the same `MultiThreadRunner` can never run simultaneously — there is only one worker — but yielding tasks are cooperatively interleaved on it, each advancing one step per pass, not run-to-completion in submission order. Want two things to actually run in parallel? Two runners.

The Lean `MultiThreadRunner` comes in a few flavors. The default constructor spins up a reactive worker that wakes essentially instantly when a task is queued. `relaxed: true` trades some wake-up latency for a quieter thread, and the `intervalInMs` constructor builds the low-CPU variant: the worker ticks at fixed intervals and sleeps in between. On the other axis, `tightTasks: true` tells the worker to never volunteer a pause — ideal for cache-friendly loops you know will occupy the thread — while the default behavior yields periodically so other threads can breathe. `initialNumberOfTasks` pre-sizes the internal containers so even submitting a burst of tasks stays allocation-free. The same machinery exists in the ExtraLean flavors — `ExtraLean.MultiThreadRunner` and its struct-typed variant — for when the task itself must be as lean as the scheduler:

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var mainRunner = new SteppableRunner("MainLoop");  //single-threaded: ticks from my game loop
var worker1    = new MultiThreadRunner("Worker1"); //each runner owns one background thread
var worker2    = new MultiThreadRunner("Worker2");

var chunkA = new float[500_000]; //three chunks of data
var chunkB = new float[500_000]; //to be processed on
var chunkC = new float[500_000]; //the workers

IEnumerator&lt;TaskContract&gt; ComputeChunk(float[] buffer)
{
    for (int i = 0; i &lt; buffer.Length; i++)
        buffer[i] = DoHeavyMath(i);

    yield break; //one-shot task: everything above runs inside the first MoveNext, on the worker
}

IEnumerator&lt;TaskContract&gt; MainTask()
{
    //dispatch one batch and wait for it: worker2 sits idle the whole time
    yield return ComputeChunk(chunkA).RunOn(worker1);

    //dispatch BOTH before waiting: a RunOn starts the task when it is called,
    //a yield only waits for it — the two chunks now compute side by side
    Continuation left  = ComputeChunk(chunkB).RunOn(worker1);
    Continuation right = ComputeChunk(chunkC).RunOn(worker2);
	
    yield return left;     //suspend the loop task — the main thread never blocks
    yield return right;    //however long the slower chunk takes, not their sum

    Merge(chunkA, chunkB, chunkC); //back on the main thread, results are ready
}

MainTask().RunOn(mainRunner); //my game loop just calls mainRunner.Step() every frame
</pre>

The first batch kept `worker2` idle — dispatch and wait, one at a time. The next two were dispatched together, so they computed side by side, and the wait lasted as long as the slower chunk, not their sum. Dispatch before you yield, zero locks, and the main loop never stopped ticking: a main-thread task fanning work out to multithreaded runners and suspending on their continuations.

When one thread is not enough but a runner-per-producer is too blunt, **`MultiThreadRunnerPool`** dispatches each scheduled root task round-robin to one of N inner runners: a fixed set of worker threads shared by independent jobs. The pool is designed for independently scheduled root tasks and the sub-tasks can still run on other runners.

### The MultiThreadedParallelTaskCollection implementations

When I need a bounded burst of work to actually run in parallel, I reach for the **`MultiThreadedParallelTaskCollection`** implementations (plain or struct-typed, in the `Svelto.Tasks.Parallelism.ExtraLean` namespace): a collection owns N internal `MultiThreadRunner`s and behaves as a single reusable unit of work. I fill it with tasks — each one implements `IParallelTask`, so it is an iterator that disposes its own state — then drive it like any other task: poll `MoveNext()`, call `Complete()`, or `yield return collection.Run()`. The tasks wait in a concurrent queue and each runner claims the next one the moment it frees up, so uneven durations self-balance instead of matching a static split. The lifecycle is deliberately strict: `Add()` throws once the batch is running (compose first, run second); `onComplete` fires exactly once, when the last task finishes; `Stop()` ends the run cooperatively but keeps the tasks, so the collection can run again as-is; `Reset()` empties it; and `Dispose()` disposes every task — including the ones no thread ever claimed — together with its runners. The Burst sibling, `MultiThreadedBurstParallelTaskCollection<T>`, scales the same batch idea to data-parallel range tasks: `Add(prototype, iterations, elementsPerTask)` stores one immutable prototype and splits the range into fixed-size chunks that one reusable dispatcher per thread claims atomically — no per-chunk wrappers to allocate, a cooperative `Stop()` whose latency is bounded to one chunk, and Burst code that stays hot on the workers.
Tasks consumed by these collections understand only the ExtraLean wait semantics (`null`, `Break.It`, `Break.AndStop`): the workers step them as plain `IEnumerator`s, so complex Lean tasks have no place here — continuations, return values and `Continue.It` would have nowhere to go on a worker thread.

### The work stealing behind MultiThreadedParallelTaskCollection

The collection owns N internal ExtraLean `MultiThreadRunner`s, each with its own worker thread, and the balancing trick lives in how they are fed. The tasks you `Add()` sit in a plain list — the source of truth, kept so the same batch can run again — while a concurrent queue fed from that list is what the runners actually consume. When a run starts, the queue is refilled and just one task is scheduled per runner: everything else stays queued. The moment a worker runs out of work, before parking its thread, it fires an idle callback, and that callback claims the next task from the shared queue. So this is not a static split computed upfront, and not classic work-stealing either (there are no per-worker deques to steal from): it is one shared queue that the first runner to free up pulls from. The effect is what matters — uneven task durations self-balance, and the wave finishes in roughly total-work / threads time instead of matching the worst static partition. Once claimed, a task runs to completion on its runner: no preemption, no migration. If a runner is being stopped exactly when a task is handed over, the task is returned to the queue instead of lost, and `Dispose()` drains whatever comes back after every in-flight claim has exited. The workers step every task as a plain `IEnumerator` and understand only ExtraLean wait semantics — `null`, `Break.It`, `Break.AndStop` — because that is all a worker can make sense of: continuations, return values and `Continue.It` have nowhere to go on a worker thread. A compiler-generated `IEnumerator<TaskContract>` even fails the workers' checks, since every `TaskContract` yield boxes through the non-generic `IEnumerator.Current`: write tasks as hand-written `IParallelTask` implementations that yield only wait signals, as the tests do.

```text
ADD:  task0 task1 task2 ... taskM   ->   list  (source of truth, survives re-runs)
                                          |
RUN:  queue := list, one task per runner, the rest stay queued
                                          |
          +---------------+---------------+
          v               v               v
      runner#0        runner#1        runner#2        (N worker threads)
       task A          task B          task C
          |               |               |
        done            done            done
          |               |               |
          +---------------+---------------+
                          |
     a runner with no work fires its idle callback BEFORE parking:
     claim the next queued task  <------ first free runner wins
                          |
     every completion atomically decrements the wave counter;
     counter == 0 -> the next MoveNext() returns false,
     onComplete fires exactly once, on the polling thread
```

No signals and no blocking waits drive any of this: the host picks the cadence — polling `MoveNext()`, calling `Complete()` or yielding `collection.Run()` — and completion is simply the counter reaching zero between two polls.

### Taking advantage of work stealing

Taking advantage of it means shaping the batch so the claim mechanism can do its job:

- **Many tasks, more than threads.** Tasks are the unit of balancing: the more of them per wave, the finer the self-balancing. Example 14 runs 400 deliberately uneven downloads on four threads; the MillionPoints demo splits one million iterations into 8192-particle chunks claimed by `ProcessorCount - 1` workers. With tasks ≤ threads there is nothing to balance and the leaner `MultiThreadRunnerPool` is the better tool (Example 22).
- **Chunky, not microscopic.** Every claim pays a queue hop and a task start, and `Stop()` is cooperative: it waits for the tasks in flight, so its latency is bounded by the longest one currently running. Hundreds of meaty tasks balance perfectly; thousands of micro-tasks just move the time from the work to the scheduling.
- **Keep tasks self-contained.** They implement `IParallelTask` — an iterator that disposes its own state — and must never assume which runner's thread claims them, because any of them can. Data shared between genuinely parallel tasks needs your synchronization, like everywhere else in the library.
- **Compose first, run second.** `Add()` throws once the batch is running; the list survives `Stop()`, so the same collection runs again as-is. `Reset()` empties it and `Dispose()` disposes every task, including the ones no thread ever claimed.

## Taking over coroutines, Tasks and Unity Jobs patterns

One way to look at 2.0: whatever concurrency pattern you use today, Svelto.Tasks has a proposed counterpart designed to mimic it.

- **Unity coroutines**: iterator tasks *are* Unity-coroutine-shaped, minus the engine lock. Yield `Yield.It` or `null`, run them on a runner instead of a `MonoBehaviour`, and they keep working outside the editor, on dedicated servers, in tests. Unity-specific glue (like yield-instruction interop) exists behind defines for when you need it.
- **Tasks / async-await**: await a Svelto runner directly so continuations resume on *your* thread, not the ThreadPool; or go the other way around and host entire `async` methods on a runner through the experimental `TaskSynchronizationContext`. Both directions are shown in the examples.
- **Unity Jobs**: the data-parallel pattern maps onto `ISveltoJob` + `MultiThreadedParallelJobCollection<T>`, which splits N iterations across M worker threads exactly like an `IJobParallelFor` would. On Unity these job structs can be Burst-compiled — that path is marked experimental.

**Svelto.Tasks is not a true replacement for Unity Jobs**, and I am not going to pretend otherwise. Jobs has a ton of controls that I didn't mirror in Svelto.Tasks, but for some specific algorithms, Svelto.Tasks runners can be used. They support burstification too.

## The MillionPoints Unity demo: how Svelto.Tasks can be used for massive parallelism

The companion *Unity* project [`Svelto.Tasks.Examples`](https://github.com/sebas77/Svelto.Tasks.Examples) (https://github.com/sebas77/Svelto.Tasks.Examples) contains my favourite stress case: animating **one million rotating points** at full framerate. Every implementation lives in the `Assets/MillionPoints` folder of the repo and renders identically — a one-vertex point mesh drawn through `Graphics.DrawMeshInstancedIndirect`, with positions streamed every frame into a mapped (`SubUpdates`) `ComputeBuffer`. Uploading buffers from the CPU to the GPU is a tricky task because of the asynchronous nature of the operation: the various examples show different forms of synchronization achievable with Svelto.Tasks and compare their performance against the fully native version (everything running on the GPU through compute shaders) and the Unity Jobs version. All the Svelto.Tasks examples use Unity Burst to vectorise the code and write straight into the driver-mapped `ComputeBuffer` memory exposed by Unity's modern `ComputeBuffer.BeginWrite` API.

![The MillionPoints demo: one million points animated at full framerate](https://raw.githubusercontent.com/sebas77/Svelto.Tasks.Examples/master/Captures/Screenshot.png)

### The comparison baselines

`MillionPointsGPU` moves the whole simulation to the GPU through a compute shader — the best tool for this job, and the quality reference. `MillionPointsCPUUnityJobs` is the classic Unity `IJobParallelFor` path, `Schedule()`/`Complete()` once per frame, which I keep around as the baseline to beat. Both matter for measurements, neither teaches anything about Svelto.Tasks, so let me jump straight to the interesting part: the three Svelto strategies and their synchronization. All three drive `MultiThreadedBurstParallelTaskCollection<T>` identically — one prototype range task, the million iterations split into 8192-particle chunks, `ProcessorCount - 1` workers claiming chunks through the collection's idle callbacks — so the only thing that changes from one strategy to the next is *what waits for what*.

**Compute Shader version profiling capture:**
![Profiler capture of the GPU compute shader version](https://raw.githubusercontent.com/sebas77/Svelto.Tasks.Examples/master/Captures/GPUCS.png)

**Unity Jobs version profiling capture:**
![Profiler capture of the Unity Jobs version](https://raw.githubusercontent.com/sebas77/Svelto.Tasks.Examples/master/Captures/CPUJOBS.png)

### Svelto Burst — BurstSync: double-buffered frames

`MillionPointsCPU_BurstSync` is the direct Svelto.Tasks counterpart of the Unity Jobs version, with the simplest possible contract. A main-thread runner hosts two roots: an upload task and a render task. The upload task claims a slot of the double buffer, waits until that slot's newest graphics fence proves the GPU finished reading it, maps the upload region (`BeginWrite`) and publishes the region — together with the frame time — through a native frame-data struct the Burst tasks read. Then it just `yield return`s the collection's `Run()` continuation: the Burst pass fills the mapped GPU-bound memory on the worker threads while the main loop keeps ticking and the render root keeps drawing the last published slot. When the pass completes, the upload task closes the write (`EndWrite`) and publishes the slot. No `Complete()` barrier exists in the steady state: while the GPU reads slot A, the CPU is already filling slot B, and the fences — not a blocking call — tell the CPU when a slot is safe to map again.

```text
BURSTSYNC - double buffered, cooperative wait: no hard barrier per frame

upload root:   wait fence(slot) -> BeginWrite -> run Burst pass  (yield on Run().Continue())
                     ^                                                             |
                     |                                                             v
               draw published slot (render root, every step)            EndWrite -> publish slot
workers (xM):              8192-particle chunks write the mapped region directly

while the GPU reads slot A, the CPU already fills slot B:
compute and rendering overlap, and the main thread never blocks
```

![Profiler capture of the Svelto.Tasks Burst Sync strategy](https://raw.githubusercontent.com/sebas77/Svelto.Tasks.Examples/master/Captures/CPUSveltoBurstSync.png)

### Svelto Burst — AdvancedSync: pipelined by handshake

`MillionPointsCPU_AdvancedSync` makes the division of responsibilities explicit with three independent roots: the upload loop and the render loop live on the main-thread runner, the compute loop on its own `MultiThreadRunner` coordinator. `BeginWrite`/`EndWrite` are main-thread-only APIs, so two typed `WaitForSignal<T>` subclasses move exactly those boundaries across the thread: the main thread maps a fence-safe slot, publishes the region together with the snapshot frame time and signals "region mapped"; the coordinator runs one complete Burst pass into it and signals "compute done"; the main thread closes the write and the render loop draws. Two GPU buffers alternate every pass so a region is only ever re-mapped after the draw reading it has retired, and the render loop draws unconditionally every Update — slow compute never gates the render cadence. The price is lockstep: one handshake round-trip per frame.

```text
ADVANCEDSYNC - three roots, two signals: BeginWrite/EndWrite stay on the main thread

upload root:    [wait fence] -> BeginWrite -> signal "mapped" -> wait "done" -> EndWrite
coordinator:                                     wait "mapped" -> Burst pass -> signal "done"
render root:                     draws every Update, whichever slot the upload loop published

compute of pass N+1 overlaps the rendering of pass N,
but both sides still advance in lockstep: one handshake per frame
```

![Profiler capture of the Svelto.Tasks Advanced Sync strategy](https://raw.githubusercontent.com/sebas77/Svelto.Tasks.Examples/master/Captures/CPUSveltoAdvanced.png)

### Svelto Burst — IndependentThreads: latest wins

`MillionPointsCPU_IndependentThreads` deletes the lockstep altogether. The coordinator computes passes back-to-back, forever, and the only synchronization is a tiny three-state ownership machine (`closed -> computing -> ready-to-close`) plus per-slot graphics fences — no `WaitForSignal<T>` handshakes at all: fence polls, not signals, tell the CPU when a slot is safe to touch. Since `BeginWrite` must run on the main thread, the coordinator snapshots the pass time, then requests the mapping through a reusable `BeginWriteTask` scheduled on the main runner: a cross-thread continuation that completes the moment the main thread opens the slot. The Burst pass fills the region, the coordinator marks the write ready to close, and the main-thread root closes it, publishes the slot and draws — as many frames as it likes, always the slot published last. Back-pressure is structural instead of counted: the coordinator cannot even start pass N before pass N-1's mapping is closed and the target slot's newest draw fence has passed, so with two slots in rotation the pipeline can never run ahead of the renderer by more than one in-flight pass.

```text
INDEPENDENTTHREADS - decoupled rates, latest-wins handoff

coordinator:   P0   P1   P2   P3   P4   P5   P6 ...    computes passes back-to-back,
               |    |    |    |    |    |    |         one in-flight pass at most
published:     0    1    2    3    4    5    6          each pass closes into a slot

frames:            F0         F1         F2              display rate, independent
                   |          |          |
                draws gen 2  draws gen 5  draws gen 6      gens 3,4 skipped, nobody waits

back-pressure is structural: pass N cannot start before pass N-1 is closed
and the target slot's newest draw fence has passed — nobody spins
```

![Profiler capture of the Svelto.Tasks Independent Threads strategy](https://raw.githubusercontent.com/sebas77/Svelto.Tasks.Examples/master/Captures/CPUSveltoInd.png)

Notice what none of the three demos contain: no locks, no event handles, no busy-wait loops. The yield mechanism *is* the synchronization. Every cross-thread wait — the workers finishing a pass, the `WaitForSignal<T>` handshake, the coordinator suspending until the main thread maps a slot — is expressed as `yield return something`, and the runner parks the coroutine at zero cost until the wait resolves. The same statement that suspends a task for one frame suspends it for a million-particle compute pass, which is why the parallel code reads top-down like the serial one.

## Performance and zero allocations

Performance was a first-class design constraint since 1.x. My benchmarks show Svelto.Tasks trading blows with *UniTask* and *Unity Jobs*: comparable throughput on coroutine stepping, comparable scaling on multi-threaded fan-out. Zero allocation is guaranteed (except for buffer resizes) in release mode. 

### Profiling with PROFILE_SVELTO

When profiling a Debug build, define **`PROFILE_SVELTO`**. It removes Svelto's debug checks, diagnostic runner references, and debug logging, so they do not pollute the measurements. It does not enable the task profiler itself: that is the separate **`TASKS_PROFILER_ENABLED`** define, available in *Unity* through `Tasks/Enable Profiler`.

The task profiler is designed as a plugin as well. When **`TASKS_PROFILER_ENABLED`** is compiled in, **`TaskProfiler`** wraps every task step and every runner processing pass: it measures durations through thread-local stopwatches, accumulates the per-task timing data the editor views consume, and forwards balanced begin/end scopes to an optional **`ITaskProfilerDriver`**. That four-method interface — `BeginRunner`/`EndRunner`, `BeginTask`/`EndTask` — is the extension point toward any profiling backend, which keeps platform-specific instrumentation out of the scheduler entirely.

*Unity* ships a driver ready-made: **`UnityTaskProfilerDriver`** installs itself automatically at startup and emits dynamic **`ProfilerMarker`s** for every runner (`Runner/<name>`) and normalized task name inside a dedicated *Svelto.Tasks* profiler category, plus two per-frame native counters, *Task Time* (nanoseconds) and *Task Steps*, synchronized across threads so background runners are counted correctly too. The editor-only `SveltoTasksProfilerModule` picks them up and renders them CPU-module-style: a busiest-first runner picker, a case-insensitive name filter, and an expandable Object/Total/Self/Calls/GC Alloc hierarchy pruned to show only Svelto subtrees, with dominant branches red-tinted.

![The Svelto.Tasks profiler module rendering per-runner timings and the Object/Total/Self/Calls/GC Alloc task hierarchy in the Unity Profiler](https://raw.githubusercontent.com/sebas77/Svelto.Tasks.Examples/master/Captures/sveltoprofiler.png)

Zero-allocation usage is a set of patterns rather than magic, and 2.0 makes them easy:

- **ExtraLean tasks + struct iterators**: runners accept struct enumerator types (`SteppableRunner<T>`), so stepping a task touches zero heap objects.
- **Pooled iterator blocks**: `IteratorBlockPool<T>` recycles `while(true)` state machines via `Break.It`; blocks and their data survive across uses (examples 07 and 15 prove reuse with reference equality).
- **Pooled continuations**: every `.Continue()` hands back a pooled handle; nothing is allocated per wait.
- **Preallocated collections**: `SerialTaskCollection`/`ParallelTaskCollection` own their storage upfront.
- The usual discipline applies: avoid closures and LINQ in per-frame tasks. Value extraction through `ToInt()`/`ToRef<T>()` stays explicit. Supported primitive payload conversions do not box: typed constructors store them in an inline union. Boxing can still occur when a `TaskContract` is exposed through non-generic `IEnumerator.Current`, or when a struct task enters a non-generic runner that stores an interface reference.

Release-only zero-allocation tests cover preallocated Lean, ExtraLean, synchronous, multithreaded, pooled and parallel runner paths.

## The examples, one by one

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

### 04 — PreallocatedRunner: capacity + struct task path

When the peak number of simultaneous tasks is known, construct a `SteppableRunner<TTask>` with that capacity. Its internal containers avoid their initial growth during the first busy wave, while the matching struct task stays on the concrete path instead of being boxed as an interface. The demo compares first-wave allocations with a default runner, then confirms the warmed-up runner's steady-state allocation behavior.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
const int tasksPerWave = 100;
using var runner = new SteppableRunner&lt;WorkTask&gt;("PreallocRunner", tasksPerWave);

for (int i = 0; i &lt; tasksPerWave; i++)
    new WorkTask(i).RunOn(runner); //concrete struct path, no boxing

while (runner.hasTasks)
    runner.Step();
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

Load → Validate → Process, launched by a parent. Validation fails and `Break.AndStop` cancels the failing task plus every waiting `.Continue()` ancestor. Process is skipped, Parent is cancelled, and the summary derives from flags written while tasks ran.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
//leaf: stop myself and the complete .Continue() parent chain
validationFailed = true;
yield return TaskContract.Break.AndStop;

//neither Chain nor Parent resumes after the child stops
yield return ValidateStep().Continue();
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

Ten AI units, at most three thinking per tick. One line caps CPU spikes. The demo honestly documents the flip side: the budget restarts from the first task every tick, so with never-ending tasks the same first three win and the rest starve indefinitely — a cap, not a rotation.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.UseFlowModifier(new StaggeredFlow(3)); //max 3 AI tasks processed per tick
</pre>

### 12 — FrameBudgetTimeBound: TimeBoundFlow

Instead of counting tasks, count milliseconds: process background work within a ~20ms wall-clock budget per tick. The budget is cooperative — checked between task steps, so a single long step can overshoot. The demo shows who ran and who starved each tick, and explains when to prefer completing tasks versus switching to fairer strategies.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.UseFlowModifier(new TimeBoundFlow(20f)); //~20ms cooperative budget per tick
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

Four hundred downloads on four real threads, with a monitor thread redrawing aggregate progress while workers advance. Download sizes are deliberately uneven and tasks far outnumber threads — the whole point: a runner that finishes a download immediately steals the next queued one, so per-thread totals stay balanced and the wave finishes in roughly total-work / threads time. With tasks ≤ threads, a bare `MultiThreadRunnerPool` is the leaner tool (Example 22).

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
using var downloads = new MultiThreadedParallelTaskCollection("Downloads", 4, false);
downloads.onComplete += () =&gt; Console.WriteLine("wave done");

foreach (var file in files)
    downloads.Add(new DownloadTask(file)); //400 tasks queued

downloads.Complete(); //4 runners, idle ones steal queued downloads
</pre>

### 15 — EntitySpawnPool: IteratorBlockPool visualized

Spawns entities from a pool, runs their lifecycles, recycles them via `Break.It`, and shows pool occupancy live. Ends with a verification box proving the recycled block and data are the *same objects* obtained again.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
_pool = new IteratorBlockPool&lt;EntityData&gt;(EntityLifecycle, "EntityPool");

var (data, block) = _pool.Get();      //take from the pool (or allocate once)
data.EntityId = ++_totalSpawned;
...step block.MoveNext() each tick...

//lifecycle yields Break.It at despawn:
//block is flagged for release; the runner's Dispose returns it, machine kept alive
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

Opening the pause menu freezes a `MultiThreadRunner`; closing it resumes every task exactly where it stopped. Pause stops new passes but does not preempt a step already in flight, so the demo lets the worker settle before snapshotting — then proves nothing advanced while paused.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
runner.Pause();
...give the worker a moment to settle, then:
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

A background thread computes and signals; the main-thread task waits through the same signal. A tiny typed subclass gives a clean producer/consumer handshake between runners. The wait auto-times-out (default 1000ms): when the deadline expires, `MoveNext()` throws and faults the waiting task, so pick a timeout that fits the producer.

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

### 22 — RunnerPoolDispatch: MultiThreadRunnerPool

The lean alternative for independent work when tasks don't outnumber threads: 16 requests dispatched round-robin to 4 pooled runners (4 per runner, all in flight). No feed queue, no wrapper, no wave counter — `AddTask` is one atomic increment and a direct hand-off, and the host counts completions itself. The demo prints the deterministic dispatch table and the honest trade: dispatch never rebalances, so a slow request lags its own runner while others idle. That is exactly why Example 14 exists for the tasks > threads case.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
var pool = new MultiThreadRunnerPool("request-pool", 4);

foreach (var request in requests)
    request.RunOn(pool); //request i → runner i % 4, that's the whole dispatch

//no Complete(), no onComplete: poll your own completion counter
</pre>

### 23 — ProfilerPlugin: installing a profiler driver

Measurement as a plugin: implement `ITaskProfilerDriver` and assign it to `TaskProfiler.Driver`, and every task step on every runner — main thread or worker — flows through your backend with balanced `Begin/End` scopes. The demo ships a console driver that aggregates per-step avg/max per task and prints the table; Unity ships its own driver that bridges the same scopes into the Unity Profiler. The instrumentation is opt-in (`TASKS_PROFILER_ENABLED`, zero-cost when off) — on plain .NET that is the `EnableTasksProfiler` build flag.

<pre class="EnlighterJSRAW" data-enlighter-language="csharp">
TaskProfiler.Driver = new ConsoleProfilerDriver(); //install the plugin

//... run tasks on any runners; EndTask(runner, task, elapsedMs) arrives per step,
//    possibly from worker threads, so the driver must be thread-safe

TaskProfiler.CopyAndUpdate(ref infos); //built-in per-pass min/avg/max aggregate
</pre>

## Conclusions

Svelto.Tasks 2.0 stays true to the idea the library was born with — iterators as tasks, runners as schedulers, zero surprises — while finally getting the packaging, the test suite and a set of runnable examples it always deserved. Use it wherever C# compiles; reach for ExtraLean in hot gameplay paths; treat Lean tasks, the Tasks interop and the job path as tools for the cases that genuinely need them.

## FAQ

### Is Svelto.Tasks a replacement for Unity Jobs?

No, and I am not going to pretend otherwise. Jobs has a ton of controls I did not mirror. What Svelto.Tasks offers is the familiar shapes: `MultiThreadedParallelJobCollection<T>` splits an index range across threads like an `IJobParallelFor` would, Burst range tasks run through `MultiThreadedBurstParallelTaskCollection<T>`, and the MillionPoints demo shows the whole spectrum. For specific algorithms the runners are an excellent tool; as a general Jobs replacement, they are not.

### Why shouldn't I just use async/await and .NET Tasks?

Because they give you no control over where and when your code runs. An awaited continuation resumes on the ThreadPool, stopping mid-flight means passing `CancellationToken`s through every layer, pacing and profiling cannot be imposed centrally. Svelto.Tasks inverts that: you choose the runner, so you choose the thread; stopping the runner stops everything by construction; and flow modifiers see every step, so budgets and profiling are features of the design. .NET Tasks remain fine for service-layer work like HTTP calls and cloud saves.

### How do I stop all the tasks of a match when the player leaves?

Stop their context, not each task: `runner.Stop()` ends running tasks asynchronously, `runner.Flush()` disposes running and queued ones, `runner.Dispose()` is terminal. No token to create, pass down and remember at every spawn site — one forgotten call cannot leave an orphan behind.

### Lean or ExtraLean?

ExtraLean for the vast majority of gameplay coroutines: plain `IEnumerator`, only wait signals, the cheapest possible task. Lean when you need the TaskContract: return values, waiting for child tasks, `Break.It`/`Break.AndStop` semantics and composition. ExtraLean struct tasks plus the pooling machinery are what make the zero-allocation paths possible.

### Is it really allocation-free?

Zero-allocation usage is a set of patterns rather than magic: ExtraLean tasks with struct iterators, `IteratorBlockPool<T>` recycling immortal state machines, pooled continuations, preallocated task collections. Release-only zero-allocation tests cover the Lean, ExtraLean, synchronous, multithreaded, pooled and parallel paths — run them with `dotnet test -c Release`. The usual discipline still applies: no closures or LINQ in per-frame tasks.

### Does it work outside Unity?

The core is engine-agnostic and targets `netstandard2.1`: if you can compile C#, you can run Svelto.Tasks. The console examples in this repo are plain .NET applications. The few Unity specializations (yield-instruction interop, the Unity profiler driver) live behind compiler defines and are strictly optional.

### Do I need locks when tasks run on other threads?

For the task itself, no: a task's state machine is only ever touched by its owning runner, so a task that keeps its state to itself is thread-safe without a single lock. Submitting tasks from any thread is safe by construction. If a task touches data shared with other threads, synchronizing that data is your job — `volatile`, `Interlocked` and locks as usual.

### What does "massive parallelism" mean in practice?

That one million rotating points can be simulated on the CPU every frame while the framerate stays full. The MillionPoints demo splits the million iterations into 8192-particle chunks that `ProcessorCount - 1` workers claim as they free up, with the Burst kernels writing straight into the memory the driver mapped for the GPU. Three synchronization strategies span the spectrum, from a double-buffered frame barrier to compute passes that run completely decoupled from rendering. None of that synchronization is written by hand: waiting for the worker chunks, for a handshake signal or for the main thread to map a slot is always the same `yield return` statement, and the runner parks the coroutine until the wait is over — the parallel code reads like the serial one. To be precise, it is batch and data parallelism — a bounded burst of work spread across all your cores — not a thread pool for unrelated async jobs.

### What is still experimental?

Two areas: the .NET Tasks interop (`SveltoAwaiter`, `TaskSynchronizationContext`) and the Burst-oriented job path inside Unity. They work and the examples demonstrate them, but consider their APIs more fluid than the rest until real-world feedback comes in.

Everything is on GitHub in the [sebas77/Svelto.Tasks-Repo](https://github.com/sebas77/Svelto.Tasks-Repo) repository. If you have questions or spot problems, leave a comment here or join our populated [Discord server](https://discord.gg/JTUZuJcME5). Feedback on the beta is not only welcome, it is necessary!

## Installation

Svelto.Tasks reaches you in three forms: the source code in this repository, NuGet packages for plain .NET projects, and OpenUPM packages for Unity. `Svelto.Tasks` is accompanied by its low-level dependency `Svelto.Common` in every channel.

### Unity, through OpenUPM

Add the packages to your project through the OpenUPM registry, either with the CLI:

```powershell
openupm add com.sebaslab.svelto.tasks
```

or by adding the scoped registry and the dependencies to `Packages/manifest.json`:

```json
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.sebaslab"]
    }
  ],
  "dependencies": {
    "com.sebaslab.svelto.common": "3.7.3",
    "com.sebaslab.svelto.tasks": "2.0.0-preview.5"
  }
}
```

### Plain .NET, through NuGet

```powershell
dotnet add package Svelto.Tasks --version 2.0.0-preview.5
```

`Svelto.Common` is restored automatically as a dependency. However, even on plain .NET I recommend consuming the source code directly whenever your setup allows it: both libraries are designed around conditional compilation (debug checks, profiling instrumentation, the Unity/Burst paths) and around being read, debugged and evolved together with your code. The packages are a convenience for quick integration — source-first is the workflow the libraries are built for.
