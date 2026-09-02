using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks.Lean
{
    //When a .Net task is awaited on a Svelto runner, we need to make sure that the continuation is posted on the same runner instead of the default
    //synchronization context.
    //The syntax is await task.RunOn(runner) instead of await task. RunOn will then use these custom awaiters to schedule the continuation on the
    //runner instead of the default synchronization context.
    public readonly struct ValueTaskRunnerAwaiter : ICriticalNotifyCompletion
    {
        readonly IGenericLeanRunner _runner;
        // Keep the wrapped operation's real awaiter: it owns the completion check, result propagation,
        // and the race-safe mechanism for registering a callback with this specific ValueTask.
        readonly ValueTaskAwaiter _taskAwaiter;

        public ValueTaskRunnerAwaiter(ValueTask task, IGenericLeanRunner runner)
        {
            _taskAwaiter = task.GetAwaiter();
            _runner = runner;
        }

        public bool IsCompleted => _taskAwaiter.IsCompleted;
        public void GetResult() => _taskAwaiter.GetResult();
        public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
        {
            // The compiler calls this after IsCompleted was observed false, not after completion.
            // The ValueTask may still complete between that check and this call; its awaiter handles that race.
            var runner = _runner;
            // Register with the wrapped ValueTask first. Only after it completes do we enqueue the async
            // state-machine continuation on the Svelto runner; enqueueing it now would make GetResult block.
            _taskAwaiter.UnsafeOnCompleted(() => SveltoAwaiterExtensions.ScheduleContinuation(runner, continuation));
        }

        public ValueTaskRunnerAwaiter GetAwaiter() => this;
    }
    
    public readonly struct TaskRunnerAwaiter : ICriticalNotifyCompletion
    {
        readonly IGenericLeanRunner _runner;
        // Keep the wrapped operation's real awaiter: it owns the completion check, result propagation,
        // and the race-safe mechanism for registering a callback with this specific Task.
        readonly TaskAwaiter _taskAwaiter;

        public TaskRunnerAwaiter(Task task, IGenericLeanRunner runner)
        {
            _taskAwaiter = task.GetAwaiter();
            _runner = runner;
        }

        public bool IsCompleted => _taskAwaiter.IsCompleted;
        public void GetResult() => _taskAwaiter.GetResult();
        public void OnCompleted(Action continuation) => UnsafeOnCompleted(continuation);

        public void UnsafeOnCompleted(Action continuation)
        {
            // UnsafeOnComplete is called by the task if the task has any await in it (otherwise it's immediately completed). We register the custom
            // TaskAwaiter
            // which will be called directly on the await, so we register the actual main task completed callback to run the continuation on the S
            // velto runner.
         //   async Task A()
         //   {
          //      await B(); UnsafeOnCompleted is called once for B (if it has await inside), to register the continuation at this point
          //      await C();UnsafeOnCompleted is called once for C (if it has await inside), to register the continuation at this point
         //   }
//
         //   await A().RunOn(runner); UnsafeOnCompleted is called once for A using this custom awaiter, to register the continuation at this point,
         // but it cannot be executed until B and C are completed. 
         //  The continuation of B() and C() will be scheduled on the default synchronization context, but the continuation of A() will be scheduled
         // on the runner.
            var runner = _runner;
            // Register with the wrapped Task first. Only after it completes do we enqueue the async
            // state-machine continuation on the Svelto runner; enqueueing it now would make GetResult block.
            _taskAwaiter.UnsafeOnCompleted(() => SveltoAwaiterExtensions.ScheduleContinuation(runner, continuation)); //we cannot add the continuation immediately in the runner otherwise it will run immediately. We need to add it only when the task is completed
        }

        public TaskRunnerAwaiter GetAwaiter() => this;
    }

// extension method to get our awaiter
    public static class SveltoAwaiterExtensions
    {
        internal static void ScheduleContinuation(IGenericLeanRunner runner, Action continuation)
        {
            if (runner.isValid)
            {
                var continuationEnumerator = ContinuationEnumeratorPool.RetrieveFromPool();
                continuationEnumerator.SetContinuation(continuation);

                new LeanSveltoTask<IEnumerator<TaskContract>>().Run(runner, continuationEnumerator);
            }
        }

        /// <summary>
        /// Await a standard .NET Task/ValueTask with the async continuation posted onto the given
        /// Lean runner instead of the default synchronization context. Works with any runner of
        /// LeanSveltoTask (SteppableRunner, MultiThreadRunner, ...).
        /// </summary>
        public static ValueTaskRunnerAwaiter RunOn(this ValueTask task, IGenericLeanRunner runner)
        {
            return new ValueTaskRunnerAwaiter(task, runner);
        }

        public static TaskRunnerAwaiter RunOn(this Task task, IGenericLeanRunner runner)
        {
            return new TaskRunnerAwaiter(task, runner);
        }
    }
}
