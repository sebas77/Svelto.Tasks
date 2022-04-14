using System;
using Svelto.Common;
using Svelto.Common.DataStructures;
using Svelto.DataStructures;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    /// <summary>
    /// Remember, unless you are using the StandardSchedulers, nothing hold your runners. Be careful that if you
    /// don't hold a reference, they will be garbage collected even if tasks are still running
    /// </summary>
    public class SteppableRunner<T> : ISteppableRunner, IRunner<T> where T : ISveltoTask
    {
        public bool isStopping => _flushingOperation.stopping;

        //        public bool isKilled   => _flushingOperation.kill;
        public bool hasTasks => numberOfProcessingTasks != 0;

        public uint   numberOfRunningTasks    => (uint)_runningCoroutines.count + (uint)_spawnedCoroutines.count;
        public uint   numberOfQueuedTasks     => _newTaskRoutines.count;
        public uint   numberOfProcessingTasks => numberOfRunningTasks + numberOfQueuedTasks;
        public string name                    => _name;

        public SteppableRunner(string name, int size = NUMBER_OF_INITIAL_COROUTINE)
        {
            _name              = name;
            _flushingOperation = new SveltoTaskRunner<T>.FlushingOperation();
            _newTaskRoutines   = new ThreadSafeQueue<T>(size);
            _runningCoroutines = new FasterList<T>(size);
            _spawnedCoroutines = new FasterList<T>(size);

            UseFlowModifier(new StandardFlow
            {
                runnerName = name
            });
        }

        ~SteppableRunner()
        {
            Console.LogWarning(_name.FastConcat(" has been garbage collected, this could have serious" +
                "consequences, are you sure you want this? "));

            _flushingOperation.Kill(_name);
        }

        public void Pause()
        {
            _flushingOperation.Pause(_name);
        }

        public void Resume()
        {
            _flushingOperation.Resume(_name);
        }

        public bool Step()
        {
            using (_platformProfiler.Sample(_name))
            {
                return _processEnumerator.MoveNext(_platformProfiler);
            }
        }

        public void StartTask(in T task)
        {
            DBC.Tasks.Check.Require(_flushingOperation.kill == false,
                $"can't schedule new routines on a killed scheduler {_name}");

            _newTaskRoutines.Enqueue(task);
        }

        public void SpawnContinuingTask(T task)
        {
            DBC.Tasks.Check.Require(_flushingOperation.kill == false,
                $"can't schedule new routines on a killed scheduler {_name}");

            _spawnedCoroutines.Add(task);
        }
        
        public virtual void Stop() 
        {
            //even if there are 0 coroutines, this must marked as stopping as during the stopping phase I don't want
            //new task to be put in the processing queue. So in the situation of 0 processing tasks but N 
            //waiting tasks, the waiting tasks must stay in the waiting list.
            //a Stopped scheduler is not meant to stop ticking MoveNext, it's just not executing tasks
            _flushingOperation.Stop(_name);
        }

        /// <summary>
        /// Stop the scheduler and Step once to clean up the tasks
        /// </summary>
        public virtual void Flush()
        {
            _flushingOperation.StopAndFlush();
            Step();
        }

        /// <summary>
        /// a Disposed scheduler is not meant to stop ticking MoveNext, it's just not executing tasks
        /// </summary>
        public virtual void Dispose()
        {
            if (_flushingOperation.kill == true)
            {
                Console.LogDebugWarning($"disposing an already disposed runner?! {_name}");

                return;
            }

            _flushingOperation.Kill(_name);

            GC.SuppressFinalize(this);
        }
        
        protected void UseFlowModifier<TFlowModifier>(TFlowModifier modifier) where TFlowModifier : IFlowModifier
        {
            _processEnumerator = new SveltoTaskRunner<T>.Process<TFlowModifier>(_newTaskRoutines, _runningCoroutines,
                _spawnedCoroutines, _flushingOperation, modifier);
        }

        protected IProcessSveltoTasks _processEnumerator;

        readonly ThreadSafeQueue<T> _newTaskRoutines;
        readonly FasterList<T>      _runningCoroutines;
        readonly FasterList<T>      _spawnedCoroutines;

        readonly SveltoTaskRunner<T>.FlushingOperation _flushingOperation;

        readonly string           _name;
        readonly PlatformProfiler _platformProfiler;

        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}