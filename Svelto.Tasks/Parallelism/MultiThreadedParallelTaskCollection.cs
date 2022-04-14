// using System;
// using System.Collections;
// using System.Collections.Generic;
// using System.Threading;
// using Svelto.Tasks.Internal;
// using Svelto.Tasks.Lean;
// using Svelto.Tasks.Parallelism.Internal;
// using Svelto.Utilities;
//
// namespace Svelto.Tasks.Parallelism
// {
//     /// <summary>
//     /// a ParallelTaskCollection ran by MultiThreadRunner will run the tasks in a single thread
//     /// MultiThreadParallelTaskCollection enables parallel tasks to run on different threads
//     /// </summary>
//     ///
//     public class
//         MultiThreadedParallelJobCollection<TJob> : MultiThreadedParallelTaskCollection<ParallelRunEnumerator<TJob>>
//         where TJob : struct, ISveltoJob
//     {
//         public void Add(ref TJob job, int iterations)
//         {
//             if (isRunning == true)
//                 throw new MultiThreadedParallelTaskCollectionException(
//                     "can't add tasks on a started MultiThreadedParallelTaskCollection");
//
//             var runnersLength      = _runners.Length;
//             int particlesPerThread = (int)Math.Floor((double)iterations / runnersLength);
//             int reminder           = iterations % runnersLength;
//
//             for (int i = 0; i < runnersLength; i++)
//                 Add(new ParallelRunEnumerator<TJob>(ref job, particlesPerThread * i, particlesPerThread));
//
//             if (reminder > 0)
//                 Add(new ParallelRunEnumerator<TJob>(ref job, particlesPerThread * runnersLength, reminder));
//         }
//
//         public MultiThreadedParallelJobCollection(string name, uint numberOfThreads, bool tightTasks) : base(name,
//             numberOfThreads, tightTasks)
//         {
//         }
//     }
//
//     public class MultiThreadedParallelTaskCollection<TTask> : IEnumerator<TaskContract>
//         where TTask : IEnumerator<TaskContract>
//     {
//         public event Action onComplete;
//
//         public TaskContract Current
//         {
//             get { return Yield.It; }
//         }
//
//         object IEnumerator.Current
//         {
//             get { return null; }
//         }
//
//         public bool isRunning { private set; get; }
//
//         /// <summary>
//         ///  
//         /// </summary>
//         /// <param name="numberOfThreads"></param>
//         /// <param name="tightTasks">
//         /// if several cache friendly and optimized tasks run in parallel, using tightTasks may improve parallelism
//         /// as gives the chance to other threads to run.
//         /// </param>
//         public MultiThreadedParallelTaskCollection(string name, uint numberOfThreads, bool tightTasks)
//         {
//             _decrementRunningThread = DecrementRunningThread;
//             DBC.Tasks.Check.Require(numberOfThreads > 1, "doesn't make much sense to use this with just 1 thread");
//
//             _name = name;
//
//             InitializeThreadsAndData(numberOfThreads, tightTasks);
//         }
//         
//         public MultiThreadedParallelTaskCollection(string name, bool tightTasks)
//         {
//             _decrementRunningThread = DecrementRunningThread;
//             
//             _name = name;
//
//             InitializeThreadsAndData(ThreadUtility.ProcessorNumber - 2, tightTasks);
//         }
//
//         /// <summary>
//         /// Add can be called by another thread, so if the collection is already running
//         /// I can't allow adding more tasks.
//         /// </summary>
//         /// <param name="enumerator"></param>
//         /// <exception cref="MultiThreadedParallelTaskCollectionException"></exception>
//         public void Add(TTask enumerator)
//         {
//             if (isRunning == true)
//                 throw new MultiThreadedParallelTaskCollectionException(
//                     "can't add tasks on a started MultiThreadedParallelTaskCollection");
//
//             _numberOfConcurrentOperationsToRun = Math.Min(_parallelTasks.Length, _numberOfTasksAdded);
//         }
//
//         public bool MoveNext()
//         {
//             if (Volatile.Read(ref _isDisposed)) return false;
//
//             if (RunMultiThreadParallelTasks()) return true;
//
//             if (onComplete != null)
//                 onComplete();
//
//             isRunning = false;
//
//             return false;
//         }
//
//         public void Stop()
//         {
//             _stoppingThreads = _taskRoutines.Length;
//
//             for (int i = 0; i < _runners.Length; i++)
//                 _runners[i].Stop();
//
//             while (_stoppingThreads > 0)
//                 ThreadUtility.TakeItEasy();
//
//             isRunning = false;
//
//             Interlocked.MemoryBarrier();
//         }
//
//         public void Dispose()
//         {
//             if (Volatile.Read(ref _isDisposed) == true) return;
//
//             Volatile.Write(ref _isDisposed, true);
//             _disposingThreads = _runners.Length;
//
//             for (int i = 0; i < _runners.Length; i++)
//                 _runners[i].Kill(_decrementRunningThread);
//
//             while (_disposingThreads > 0)
//                 ThreadUtility.TakeItEasy();
//
//             for (int i = 0; i < _runners.Length; i++)
//                 _runners[i].Dispose();
//
//             _runners            = null;
//             onComplete          = null;
//             _numberOfTasksAdded = 0;
//             isRunning           = false;
//
//             Interlocked.MemoryBarrier();
//
//             GC.SuppressFinalize(this);
//         }
//
//         public override string ToString()
//         {
//             return _name;
//         }
//
//         ~MultiThreadedParallelTaskCollection()
//         {
//             Dispose();
//         }
//
//         void InitializeThreadsAndData(uint numberOfThreads, bool tightTasks)
//         {
//             _runners       = new MultiThreadRunner<LeanSveltoTask<TTask>>[numberOfThreads];
//             _taskRoutines  = new LeanSveltoTask<TTask>[numberOfThreads];
//
//             //prepare a single multithread runner for each group of fiber like task collections
//             //number of threads can be less than the number of tasks to run
//             for (int i = 0; i < numberOfThreads; i++)
//                 _runners[i] = new MultiThreadRunner<LeanSveltoTask<TTask>>(
//                     "MultiThreadedParallelRunner ".FastConcat(_name, " #").FastConcat(i), false, tightTasks);
//
//             Action ptcOnOnComplete = DecrementConcurrentOperationsCounter;
//             Func<Exception, bool> ptcOnOnException = (e) =>
//             {
//                 DecrementConcurrentOperationsCounter();
//                 return false;
//             };
//
//             //prepare the fiber-like paralleltasks
//             for (int i = 0; i < numberOfThreads; i++)
//             {
//                 _taskRoutines[i] = taskRoutine;
//             }
//         }
//
//         bool RunMultiThreadParallelTasks()
//         {
//             if (_taskRoutines == null)
//                 throw new MultiThreadedParallelTaskCollectionException(
//                     "can't run a MultiThreadedParallelTaskCollection once killed");
//
//             if (isRunning == false)
//             {
//                 _counter = _numberOfConcurrentOperationsToRun;
//                 Interlocked.MemoryBarrier();
//
//                 Action ptcOnOnComplete = DecrementStoppingThread;
//                 //start them
//                 for (int i = 0; i < _numberOfConcurrentOperationsToRun; i++)
// //                    _taskRoutines[i].Run(onStop: ptcOnOnComplete);
//             }
//
//             //wait for completition, I am not using signaling as this Collection could be yielded by a main thread runner
//             Interlocked.MemoryBarrier();
//             isRunning = _counter > 0;
//
//             return isRunning;
//         }
//
//         void DecrementRunningThread()
//         {
//             Interlocked.Decrement(ref _disposingThreads);
//         }
//
//         void DecrementStoppingThread()
//         {
//             Interlocked.Decrement(ref _stoppingThreads);
//         }
//
//         void DecrementConcurrentOperationsCounter()
//         {
//             Interlocked.Decrement(ref _counter);
//         }
//
//         protected MultiThreadRunner<LeanSveltoTask<TTask>>[] _runners;
//         LeanSveltoTask<TTask>[]                              _taskRoutines;
//
//         int  _numberOfTasksAdded;
//         int  _numberOfConcurrentOperationsToRun;
//         int  _counter;
//         int  _disposingThreads;
//         int  _stoppingThreads;
//         bool _isDisposed;
//
//         readonly string _name;
//         readonly Action _decrementRunningThread;
//     }
//
//     public class MultiThreadedParallelTaskCollectionException : Exception
//     {
//         public MultiThreadedParallelTaskCollectionException(
//             string canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection) : base(
//             canTAddEnumeratorsOnAStartedMultithreadedparalleltaskcollection)
//         {
//         }
//     }
// }