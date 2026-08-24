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
    /// A SynchronizationContext whose continuations are executed by a pump task running on the
    /// runner passed to the constructor. Every await suspension of a hosted async method is posted
    /// back onto the context (Post) and resumed by the pump at the next tick, so hosted code runs
    /// on the runner's thread without sprinkling RunOn anywhere:
    ///
    ///     var runner  = new MultiThreadRunner("BgWorker");
    ///     var context = new TaskSynchronizationContext(runner);
    ///     context.Run(async () => { await something; DoWork(); }); // DoWork runs on BgWorker
    ///
    /// Semantics:
    /// - Code before the first await executes synchronously on the caller thread (standard .NET).
    /// - Every await costs one pump tick: work posted during a drain is executed at the next tick.
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

            //the pump never completes: it dies together with the runner
            Pump().RunOn(runner);
        }

        /// <summary>
        /// Hosts an async method on this context. The returned Task can be observed or awaited
        /// from anywhere else; awaiting it from another thread will not bring it back here.
        /// </summary>
        public Task Run(Func<Task> asyncMethod)
        {
            var prevContext = Current;

            // The delegate's first await captures this context, routing its continuation to the runner pump.
            SetSynchronizationContext(this);
            try
            {
                return asyncMethod();
            }
            finally
            {
                // Keep this context scoped to the hosted delegate; callers retain their own scheduling policy.
                SetSynchronizationContext(prevContext);
            }
        }

        /// <inheritdoc cref="Run(Func{Task})"/>
        public Task<T> Run<T>(Func<Task<T>> asyncMethod)
        {
            var prevContext = Current;

            // The delegate's first await captures this context, routing its continuation to the runner pump.
            SetSynchronizationContext(this);
            try
            {
                return asyncMethod();
            }
            finally
            {
                // Keep this context scoped to the hosted delegate; callers retain their own scheduling policy.
                SetSynchronizationContext(prevContext);
            }
        }

        /// <summary>Queues the continuation to be executed by the pump. Never inline.</summary>
        public override void Post(SendOrPostCallback d, object state)
        {
            _wait.Enqueue((d, state));
        }

        /// <summary>
        /// Executes inline on the calling thread. This breaks the confinement of the context,
        /// use only if you know you are already on the right thread.
        /// </summary>
        public override void Send(SendOrPostCallback d, object state)
        {
            d(state);
        }

        public override SynchronizationContext CreateCopy() => this;

        IEnumerator<TaskContract> Pump()
        {
            while (true)
            {
                //snapshot first: continuations posted during the drain are executed next tick
                while (_wait.TryDequeue(out var work))
                    _execute.Enqueue(work);

                while (_execute.TryDequeue(out (SendOrPostCallback callback, object state) work))
                {
                    var prevContext = Current;

                    SetSynchronizationContext(this); //nested awaits must recapture this context
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
                        SetSynchronizationContext(prevContext);
                    }
                }

                yield return TaskContract.Yield.It;
            }
        }

        readonly IGenericLeanRunner _runner;
        readonly ConcurrentQueue<(SendOrPostCallback callback, object state)> _wait;
        readonly ConcurrentQueue<(SendOrPostCallback callback, object state)> _execute;
    }
}
