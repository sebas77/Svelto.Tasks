using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Svelto.Tasks.Lean
{
    /// <summary>
    /// Hosts .NET async methods on an existing Svelto Lean runner.
    ///
    /// Capturing awaits in a hosted async method post their continuations to this context. A permanent pump task,
    /// scheduled on the runner supplied to the constructor, executes those continuations at a later runner tick.
    /// This confines continuation code to the runner thread without requiring <c>RunOn</c> at every await:
    ///
    ///     var runner  = new MultiThreadRunner("BgWorker");
    ///     var context = new TaskSynchronizationContext(runner);
    ///     context.Run(async () => { await something; DoWork(); }); // DoWork runs on BgWorker
    ///
    /// Semantics:
    /// - Code before the first await executes synchronously on the caller thread (standard .NET).
    /// - Only incomplete awaits that capture the current context are posted here. Completed awaits execute inline,
    ///   and <c>ConfigureAwait(false)</c> deliberately bypasses this context.
    /// - A posted continuation runs no earlier than the next pump tick. Work posted while the pump is draining waits
    ///   for the following tick, preventing recursive continuation execution in a single drain.
    /// - Disposing the runner kills the pump: queued continuations are never executed anymore, so
    ///   all hosted tasks freeze forever and become garbage collected once unreferenced. This is
    ///   intentional: stopping a runner means abandoning its work.
    /// </summary>
    public sealed class TaskSynchronizationContext : SynchronizationContext
    {
        public TaskSynchronizationContext(IGenericLeanRunner runner)
        {
            _runner = runner ?? throw new ArgumentNullException(nameof(runner));

            _wait    = new ConcurrentQueue<(SendOrPostCallback callback, object state)>();
            _execute = new ConcurrentQueue<(SendOrPostCallback callback, object state)>();

            // The pump owns the queues for this context and intentionally runs until the runner is disposed.
            // Its yielded ticks give the runner control over when posted .NET continuations are allowed to resume.
            Pump().RunOn(runner);
        }

        /// <summary>
        /// Hosts an async method on this context. The returned Task can be observed or awaited
        /// from anywhere else; awaiting it from another thread will not bring it back here.
        /// </summary>
        public Task Run(Func<Task> asyncMethod)
        {
            var prevContext = Current;

            // SynchronizationContext is ambient and thread-local. Install this one only while starting the state
            // machine so its first incomplete, context-capturing await posts to this runner's pump.
            SetSynchronizationContext(this);
            try
            {
                return asyncMethod();
            }
            finally
            {
                // Do not leak this runner's scheduling policy to code invoked after Run returns on this thread.
                // Nested Run calls restore this context because prevContext then refers to this same instance.
                SetSynchronizationContext(prevContext);
            }
        }

        /// <inheritdoc cref="Run(Func{Task})"/>
        public Task<T> Run<T>(Func<Task<T>> asyncMethod)
        {
            var prevContext = Current;

            // SynchronizationContext is ambient and thread-local. Install this one only while starting the state
            // machine so its first incomplete, context-capturing await posts to this runner's pump.
            SetSynchronizationContext(this);
            try
            {
                return asyncMethod();
            }
            finally
            {
                // Do not leak this runner's scheduling policy to code invoked after Run returns on this thread.
                // Nested Run calls restore this context because prevContext then refers to this same instance.
                SetSynchronizationContext(prevContext);
            }
        }

        /// <summary>
        /// Queues a captured await continuation. It is never invoked inline because doing so would run it on the
        /// completion thread rather than on the runner thread.
        /// </summary>
        public override void Post(SendOrPostCallback d, object state)
        {
            _wait.Enqueue((d, state));
        }

        /// <summary>
        /// Executes inline on the calling thread. Send has synchronous SynchronizationContext semantics and cannot
        /// be marshalled through the asynchronous pump without blocking the caller; it therefore breaks confinement
        /// unless the caller is already executing on the runner thread.
        /// </summary>
        public override void Send(SendOrPostCallback d, object state)
        {
            d(state);
        }

        // The context holds no per-operation mutable state beyond its shared queues, so copies must share it.
        public override SynchronizationContext CreateCopy() => this;

        IEnumerator<TaskContract> Pump()
        {
            while (true)
            {
                // Move the current batch before executing it. Post can run concurrently, and new items remain in
                // _wait until the next tick instead of recursively extending this drain indefinitely.
                while (_wait.TryDequeue(out var work))
                    _execute.Enqueue(work);

                while (_execute.TryDequeue(out (SendOrPostCallback callback, object state) work))
                {
                    var prevContext = Current;

                    // A continuation runs on the runner thread, whose ambient context is otherwise unrelated.
                    // Reinstall this context so nested awaits capture this pump as well.
                    SetSynchronizationContext(this);
                    try
                    {
                        work.callback(work.state);
                    }
                    catch (Exception e)
                    {
                        Console.LogException(e);
                    }
                    finally
                    {
                        // Preserve the runner thread's pre-existing context for code that executes after this callback.
                        SetSynchronizationContext(prevContext);
                    }
                }

                // Yielding returns control to the Svelto runner; it decides when this context may process another batch.
                yield return TaskContract.Yield.It;
            }
        }

        readonly IGenericLeanRunner _runner;
        readonly ConcurrentQueue<(SendOrPostCallback callback, object state)> _wait;
        readonly ConcurrentQueue<(SendOrPostCallback callback, object state)> _execute;
    }
}
