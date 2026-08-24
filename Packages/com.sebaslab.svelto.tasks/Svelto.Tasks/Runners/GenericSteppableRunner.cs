using System;
using System.Runtime.CompilerServices;
using Svelto.Common;
using Svelto.DataStructures;
using Svelto.Tasks.Internal;

namespace Svelto.Tasks
{
    /// <summary>
    /// Remember, unless you are using the StandardSchedulers, nothing holds your runners. Be careful that if you
    /// don't hold a reference, they will be garbage collected even if tasks are still running
    /// </summary>
    public class GenericSteppableRunner<TTask> : ISteppableRunner, IRunner<TTask> where TTask : ISveltoTask
    {
        public bool isStopping => _flushingOperation.stopping;
        public bool hasTasks => numberOfTasks != 0;

        public uint   numberOfRunningTasks  => _processor.numberOfRunningTasks;
        public uint   numberOfQueuedTasks   => _processor.numberOfQueuedTasks;
        public uint   numberOfTasks         => _processor.numberOfTasks;
        public string name                  => _name;

        public GenericSteppableRunner(string name)
        {
            _name              = name;
            _flushingOperation = new SveltoTaskRunner<TTask>.FlushingOperation();
        }

        ~GenericSteppableRunner()
        {
            Console.LogWarning(_name.FastConcat(" has been garbage collected, this could have serious" +
                "consequences, are you sure you want this? "));

            Kill();
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
                return _processor.MoveNext(_platformProfiler);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AddTask(in TTask task,
            (int runningTaskIndexToReplace, TombstoneHandle parentSpawnedTaskIndex) index)
        {
            if (_flushingOperation.kill == true)
                throw new DBC.Tasks.PreconditionException($"cannot add a task to a killed runner {_name}");
            
            if (_processor == null)
                throw new DBC.Tasks.PreconditionException($"cannot add a task to a disposed runner {_name}");

            _processor.AddTask(task, index);
        }
        
        public void Stop() 
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
        public void Flush()
        {
            _flushingOperation.StopAndReset(_name);
            Step();
        }

        /// <summary>
        /// a Disposed scheduler is not meant to stop ticking MoveNext, it's just not executing tasks
        /// </summary>
        public virtual void Dispose()
        {
            Kill();

            GC.SuppressFinalize(this);
        }

        void Kill()
        {
            if (_flushingOperation.kill == true)
            {
                Console.LogDebugWarning($"disposing an already disposed runner?! {_name}");

                return;
            }

            _flushingOperation.Kill(_name);
            Step(); //one last step to clean up the tasks
            
            _processor = null;
        }

        public void UseFlowModifier<TFlowModifier>(TFlowModifier modifier) where TFlowModifier : IFlowModifier
        {
            _processor = new SveltoTaskRunner<TTask>.Process<TFlowModifier>(_flushingOperation, modifier, NUMBER_OF_INITIAL_COROUTINE, _name);
        }

        readonly SveltoTaskRunner<TTask>.FlushingOperation      _flushingOperation;
        IProcessSveltoTasks<TTask> _processor;

        readonly string           _name;
        readonly PlatformProfiler _platformProfiler;

        const int NUMBER_OF_INITIAL_COROUTINE = 3;
    }
}