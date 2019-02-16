using System;
using System.Collections;
using System.Collections.Generic;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.Tasks.Internal;
using Svelto.Utilities;
using Unity.Jobs;

#if NETFX_CORE
using System.Threading.Tasks;
#endif

namespace Svelto.Tasks
{
    namespace Lean
    {
        public sealed class UnityJobRunner : UnityJobRunner<LeanSveltoTask<IEnumerator<TaskContract>>>
        {
            public UnityJobRunner(string name, bool relaxed = false, bool tightTasks = false) :
                base(name, relaxed, tightTasks)
            {
            }
        }
    }
    
    namespace ExtraLean
    {
        public sealed class UnityJobRunner : UnityJobRunner<ExtraLeanSveltoTask<IEnumerator>>
        {
            public UnityJobRunner(string name, bool relaxed = false, bool tightTasks = false) :
                base(name, relaxed, tightTasks)
            {
            }
        }
    }

    public class UnityJobRunner<TTask> : UnityJobRunner<TTask, StandardRunningTasksInfo> where TTask : ISveltoTask
    {
        public UnityJobRunner(string name, bool relaxed = false, bool tightTasks = false) : 
            base(name, new StandardRunningTasksInfo())
        {
        }
    }
    /// <summary>
    /// The multithread runner always uses just one thread to run all the couroutines. If you want to use a separate
    /// thread, you will need to create another MultiThreadRunner 
    /// </summary>
    /// <typeparam name="TTask"></typeparam>
    /// <typeparam name="TFlowModifier"></typeparam>
    public class UnityJobRunner<TTask, TFlowModifier> : IRunner, IInternalRunner<TTask> where TTask: ISveltoTask
           where TFlowModifier:IRunningTasksInfo
    {
        /// <summary>
        /// when the thread must run very tight and cache friendly tasks that won't allow the CPU to start new threads,
        /// passing the tightTasks as true would force the thread to yield every so often. Relaxed to true
        /// would let the runner be less reactive on new tasks added.  
        /// </summary>
        /// <param name="name"></param>
        public UnityJobRunner(string name, TFlowModifier modifier)
        {
            var runnerData = new RunnerData(name, modifier);

            Init(runnerData);
        }
        
        public void Pause()
        {
            _runnerData.isPaused = true;
        }

        public void Resume()
        {
            _runnerData.isPaused = false;
        }
        
        public bool paused
        {
            get
            {
                return _runnerData.isPaused;
            }
        }

        public bool isStopping
        {
            get
            {
                ThreadUtility.MemoryBarrier();
                return _runnerData.waitForFlush;
            }
        }

        public bool isKilled { get; set; }

        public int numberOfRunningTasks
        {
            get { return _runnerData.Count; }
        }
        
        public int numberOfQueuedTasks
        {
            get { return  _runnerData.newTaskRoutines.Count; }
        }

        public int numberOfProcessingTasks
        {
            get { return _runnerData.Count + _runnerData.newTaskRoutines.Count; }
        }

        public override string ToString()
        {
            return _runnerData.name;
        }

        ~UnityJobRunner()
        {
            Console.LogWarning("MultiThreadRunner has been garbage collected, this could have serious" +
                                                        "consequences, are you sure you want this? ".FastConcat(_runnerData.name));
                                                        
            Dispose();
        }

        public void Dispose()
        {
            if (isKilled == false)
                Kill();
            
            GC.SuppressFinalize(this);
        }

        void Init(RunnerData runnerData)
        {
            _runnerData = runnerData;

            _runnerData.Schedule();

        }

        public void StartCoroutine(ref TTask task, bool immediate)
        {
            if (isKilled == true)
                throw new JobRunnerException("Trying to start a task on a killed runner");
            
            _runnerData.newTaskRoutines.Enqueue(task);
            _runnerData.UnlockThread();
        }

        public void StopAllCoroutines()
        {
            if (isKilled == true)
                throw new JobRunnerException("Trying to stop tasks on a killed runner");
            
            _runnerData.newTaskRoutines.Clear();
            _runnerData.waitForFlush = true;

            ThreadUtility.MemoryBarrier();
        }

        public void Kill()
        {
            if (isKilled == true)
                throw new JobRunnerException("Trying to kill an already killed runner");
            
            _runnerData.Kill();
        }
        
        RunnerData _runnerData;

        struct RunnerData:IJob
        {
            public RunnerData(string name, TFlowModifier modifier): this()
            {
                _coroutines          = new FasterList<TTask>();
                newTaskRoutines      = new ThreadSafeQueue<TTask>();
                this.name            = name;
                _flushingOperation   = new CoroutineRunner<TTask>.FlushingOperation();
                modifier.runnerName  = name;
                _process             = new CoroutineRunner<TTask>.Process<TFlowModifier, PlatformProfilerMT>
                    (newTaskRoutines, _coroutines, _flushingOperation, modifier); 
            }

            public int Count
            {
                get
                {
                    ThreadUtility.MemoryBarrier();

                    return _coroutines.Count;
                }
            }

            internal void UnlockThread()
            {
                _stopRescheduling = false;

                ThreadUtility.MemoryBarrier();
            }

            public void Kill()
            {
                    _flushingOperation.kill = true;
                    ThreadUtility.MemoryBarrier();

                    UnlockThread();
                    
                    _stopRescheduling = true;
            }

            public void Execute()
            {
                ThreadUtility.MemoryBarrier();
                
                while (_process.MoveNext(false))
                {
                    if (_flushingOperation.kill == false)
                    {
                        if (_flushingOperation.paused)
                            _stopRescheduling = true;
                                
                        if (_coroutines.Count == 0)
                        {
                            if (newTaskRoutines.Count == 0)
                                _stopRescheduling = true;
                        }
                    }
                }

                if (_stopRescheduling == false)
                    this.Schedule();
            }
            
            internal bool isPaused
            {
                get { return _flushingOperation.paused; }
                set
                {
                    ThreadUtility.VolatileWrite(ref _flushingOperation.paused, value);
                    
                    if (value == false) UnlockThread();
                }
            }

            internal bool waitForFlush
            {
                get { return _flushingOperation.stopping; }
                set
                {
                    ThreadUtility.VolatileWrite(ref _flushingOperation.stopping, value);
                }
            }

            internal readonly ThreadSafeQueue<TTask> newTaskRoutines;
            internal readonly string                 name;

            readonly FasterList<TTask> _coroutines;
            bool _stopRescheduling;

            readonly CoroutineRunner<TTask>.FlushingOperation                          _flushingOperation;
            readonly CoroutineRunner<TTask>.Process<TFlowModifier, PlatformProfilerMT> _process;
        }
    }
   
    
    public class JobRunnerException : Exception
    {
        public JobRunnerException(string message): base(message)
        {}
    }
}