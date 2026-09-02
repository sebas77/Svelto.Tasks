using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.DataStructures;
using Svelto.Tasks.ExtraLean;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class MultiThreadRunnerPoolTests
    {
        [Test]
        public void Pool_InvalidThreadCount_ThrowsPoolException()
        {
            Assert.Throws<MultiThreadRunnerPoolException>(() =>
                new MultiThreadRunnerPool("Pool_InvalidThreadCount", 0));
        }

        [Test]
        public void Pool_RunsTasksOnEveryRunner()
        {
            var threadIds = new ThreadSet<int>();
            using var completed = new CountdownEvent(64);

            IEnumerator Task()
            {
                threadIds.Add(Thread.CurrentThread.ManagedThreadId);
                completed.Signal();
                yield break;
            }

            using (var pool = new MultiThreadRunnerPool("Pool_RunsTasksOnEveryRunner", 4))
            {
                for (int i = 0; i < 64; i++)
                    Task().RunOn(pool);

                Assert.That(completed.Wait(2000), Is.True);
            }

            Assert.That(threadIds.Count, Is.EqualTo(4));
        }

        [Test]
        public void Pool_AllowsTasksToScheduleMoreRootTasks()
        {
            using var childrenCompleted = new CountdownEvent(16);
            using var pool = new MultiThreadRunnerPool("Pool_AllowsTasksToScheduleMoreRootTasks", 4);

            IEnumerator Child()
            {
                childrenCompleted.Signal();
                yield break;
            }

            IEnumerator Parent()
            {
                for (int i = 0; i < 16; i++)
                    Child().RunOn(pool);
                yield break;
            }

            Parent().RunOn(pool);

            Assert.That(childrenCompleted.Wait(2000), Is.True);
        }

        [Test]
        public void Pool_StopAndDispose_DisposesTasksOnEveryRunner()
        {
            using var started = new CountdownEvent(4);
            var disposed = new Counter();
            using var pool = new MultiThreadRunnerPool("Pool_StopAndDispose_DisposesTasksOnEveryRunner", 4);

            for (int i = 0; i < 4; i++)
                new DisposableSpinEnumerator(started, disposed).RunOn(pool);

            Assert.That(started.Wait(2000), Is.True);

            pool.Stop();
            pool.Dispose();

            Assert.That(disposed.value, Is.EqualTo(4));
        }

        [Test]
        public void Pool_DisposeIsIdempotent()
        {
            var pool = new MultiThreadRunnerPool("Pool_DisposeIsIdempotent", 2);

            pool.Dispose();
            Assert.DoesNotThrow(() => pool.Dispose());
        }

        [Test]
        public void Pool_AddTaskAfterDispose_Throws()
        {
            var pool = new MultiThreadRunnerPool("Pool_AddTaskAfterDispose", 1);

            pool.Dispose();

            IEnumerator Task()
            {
                yield break;
            }

            Assert.Throws<MultiThreadRunnerPoolException>(() => Task().RunOn(pool));
        }

        [Test]
        public void Pool_AddTaskAfterStop_RunsAfterRunnerUnstops()
        {
            using var pool = new MultiThreadRunnerPool("Pool_AddTaskAfterStop", 1);
            pool.Stop();
            using var completed = new ManualResetEventSlim(false);
            var task = new TrackingEnumerator(completed);

            task.RunOn(pool);

            Assert.That(completed.Wait(2000), Is.True);
            Assert.That(task.wasExecuted, Is.True);
            Assert.That(task.wasDisposed, Is.True);
        }

        [Test]
        public void Pool_AddTaskRacingWithStop_IsThreadSafeAndDisposesEveryTask()
        {
            const int taskCount = 1000;
            using var start = new ManualResetEventSlim(false);
            var disposed = new Counter();
            var pool = new MultiThreadRunnerPool("Pool_AddTaskRacingWithStop", 4);
            Exception schedulingException = null;

            var addThread = new Thread(() =>
            {
                start.Wait();
                try
                {
                    for (int i = 0; i < taskCount; i++)
                        new CountingEnumerator(disposed).RunOn(pool);
                }
                catch (Exception e)
                {
                    schedulingException = e;
                }
            });
            var stopThread = new Thread(() =>
            {
                start.Wait();
                pool.Stop();
            });

            addThread.Start();
            stopThread.Start();
            start.Set();
            addThread.Join();
            stopThread.Join();
            pool.Dispose();

            Assert.That(schedulingException, Is.Null);
            Assert.That(pool.isDisposed, Is.True);
            Assert.That(disposed.value, Is.EqualTo(taskCount));
        }

        [Test]
        public void Pool_RejectsRunnerLocalContinuationIndices()
        {
            using var pool = new MultiThreadRunnerPool("Pool_RejectsRunnerLocalContinuationIndices", 2);
            ExtraLeanSveltoTask<IEnumerator> task = default;

            Assert.Throws<MultiThreadRunnerPoolException>(() =>
                pool.AddTask(task, (0, new TombstoneHandle(0))));
        }

        [Test]
        public void TypedPool_AllowsStructTasksToScheduleChildrenAtRuntime()
        {
            using var completed = new CountdownEvent(64);
            using var pool = new MultiThreadRunnerPool<DynamicStructTask>(
                "TypedPool_AllowsStructTasksToScheduleChildrenAtRuntime", 4, true);

            new DynamicStructTask(pool, completed, 64).RunOn(pool);

            Assert.That(completed.Wait(2000), Is.True);
        }

        [Test]
        public void TypedPool_PreservesStructStateAcrossSteps()
        {
            var steps = new Counter();
            using var disposed = new ManualResetEventSlim(false);
            using var pool = new MultiThreadRunnerPool<StatefulStructTask>(
                "TypedPool_PreservesStructStateAcrossSteps", 1, true);

            new StatefulStructTask(steps, disposed, 64).RunOn(pool);

            Assert.That(disposed.Wait(2000), Is.True);
            Assert.That(steps.value, Is.EqualTo(64));
        }

        sealed class ThreadSet<T>
        {
            readonly HashSet<T> _set = new HashSet<T>();
            readonly object _lock = new object();

            public void Add(T item)
            {
                lock (_lock)
                    _set.Add(item);
            }

            public int Count
            {
                get
                {
                    lock (_lock)
                        return _set.Count;
                }
            }
        }

        sealed class DisposableSpinEnumerator : IEnumerator, IDisposable
        {
            public DisposableSpinEnumerator(CountdownEvent started, Counter disposed)
            {
                _started = started;
                _disposed = disposed;
            }

            public object Current => null;

            public bool MoveNext()
            {
                if (Interlocked.Exchange(ref _hasStarted, 1) == 0)
                    _started.Signal();

                Thread.SpinWait(128);
                return true;
            }

            public void Reset() { }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _hasDisposed, 1) == 0)
                    Interlocked.Increment(ref _disposed.value);
            }

            readonly CountdownEvent _started;
            readonly Counter _disposed;
            int _hasStarted;
            int _hasDisposed;
        }

        sealed class TrackingEnumerator : IEnumerator, IDisposable
        {
            public TrackingEnumerator(ManualResetEventSlim completed)
            {
                _completed = completed;
            }

            public object Current => null;
            public bool wasExecuted { get; private set; }
            public bool wasDisposed { get; private set; }

            public bool MoveNext()
            {
                wasExecuted = true;
                return false;
            }

            public void Reset() { }
            public void Dispose()
            {
                wasDisposed = true;
                _completed.Set();
            }

            readonly ManualResetEventSlim _completed;
        }

        sealed class CountingEnumerator : IEnumerator, IDisposable
        {
            public CountingEnumerator(Counter disposed)
            {
                _disposed = disposed;
            }

            public object Current => null;
            public bool MoveNext() => false;
            public void Reset() { }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _hasDisposed, 1) == 0)
                    Interlocked.Increment(ref _disposed.value);
            }

            readonly Counter _disposed;
            int _hasDisposed;
        }

        struct DynamicStructTask : IEnumerator, IDisposable
        {
            public DynamicStructTask(MultiThreadRunnerPool<DynamicStructTask> pool,
                                     CountdownEvent completed, int children)
            {
                _pool = pool;
                _completed = completed;
                _children = children;
            }

            public object Current => null;

            public bool MoveNext()
            {
                if (_children == 0)
                {
                    _completed.Signal();
                    return false;
                }

                for (int i = 0; i < _children; i++)
                    new DynamicStructTask(_pool, _completed, 0).RunOn(_pool);

                return false;
            }

            public void Reset() { }
            public void Dispose() { }

            readonly MultiThreadRunnerPool<DynamicStructTask> _pool;
            readonly CountdownEvent _completed;
            readonly int _children;
        }

        struct StatefulStructTask : IEnumerator, IDisposable
        {
            public StatefulStructTask(Counter steps, ManualResetEventSlim disposed, int remainingSteps)
            {
                _steps = steps;
                _disposed = disposed;
                _remainingSteps = remainingSteps;
            }

            public object Current => null;

            public bool MoveNext()
            {
                Interlocked.Increment(ref _steps.value);
                return --_remainingSteps > 0;
            }

            public void Reset() { }
            public void Dispose() => _disposed.Set();

            readonly Counter _steps;
            readonly ManualResetEventSlim _disposed;
            int _remainingSteps;
        }

        sealed class Counter
        {
            public int value;
        }
    }
}
