#if UNITY_5 || UNITY_5_3_OR_NEWER
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Unity.Internal
{
    static class UnityCoroutineRunner<T> where T:IEnumerator
    {
        static RunnerBehaviourUpdate _runnerBehaviour;

        static UnityCoroutineRunner()
        {
            GameObject go = new GameObject("Svelto.Tasks.UnityScheduler");
            _runnerBehaviour = go.AddComponent<RunnerBehaviourUpdate>();

            if (Application.isPlaying)
                Object.DontDestroyOnLoad(go);
        }
        
        public static void StartUpdateCoroutine(IEnumerator process)
        {
            _runnerBehaviour.StartUpdateCoroutine(process);
        }
        
        public static void StartEarlyUpdateCoroutine(IEnumerator process)
        {
            _runnerBehaviour.StartEarlyUpdateCoroutine(process);
        }
        
        public static void StartEndOfFrameCoroutine(IEnumerator process)
        {
            _runnerBehaviour.StartEndOfFrameCoroutine(process);
        }
        
        public static void StartLateCoroutine(IEnumerator process)
        {
            _runnerBehaviour.StartLateCoroutine(process);
        }

        public static void StartCoroutine(IEnumerator process)
        {
            _runnerBehaviour.StartCoroutine(process);
        }

        public static void StartPhysicCoroutine(IEnumerator process)
        {
            _runnerBehaviour.StartPhysicCoroutine(process);
        }
        
        public static void StopRoutines(FlushingOperation
            flushingOperation)
        {
            //note: _coroutines will be cleaned by the single tasks stopping silently. in this way they will be put
            //back to the pool. Let's be sure that the runner had the time to stop and recycle the previous tasks
            flushingOperation.stopped = true;
        }
        
        internal class Process<RunningInfo> : IEnumerator where RunningInfo: IRunningTasksInfo<T>
        {
            public Process( ThreadSafeQueue<ISveltoTask<T>> newTaskRoutines,
                            FasterList<ISveltoTask<T>>      coroutines, 
                            FlushingOperation              flushingOperation,
                            RunningInfo                    info)
            {
                _newTaskRoutines = newTaskRoutines;
                _coroutines = coroutines;
                _flushingOperation = flushingOperation;
                _info = info;
            }    

            public bool MoveNext()
            {
                if (_flushingOperation.kill) return false;
#if ENABLE_PLATFORM_PROFILER
                var _platformProfiler = new Svelto.Common.PlatformProfiler();
                using (_platformProfiler.StartNewSession(_info.runnerName))
#endif
                {
                    //don't start anything while flushing
                    if (_newTaskRoutines.Count > 0 && false == _flushingOperation.stopped) 
                        _newTaskRoutines.DequeueAllInto(_coroutines); 
                    
                    if (_coroutines.Count == 0 || _flushingOperation.paused == true) return true;

                    _info.Reset();
                    
                    int index = _flushingOperation.immediate == true ? _coroutines.Count - 1 : 0;

                    bool mustExit;
                    do
                    {
                        if (_info.CanProcessThis(ref index) == false) break;
                        
                        var coroutines = _coroutines.ToArrayFast();

                        bool result;
                        
                        if (_flushingOperation.stopped) coroutines[index].Stop();

#if ENABLE_PLATFORM_PROFILER
                        using (_platformProfiler.Sample(coroutines[index].ToString()))
#endif
                        {
#if TASKS_PROFILER_ENABLED
                            result =
                            Profiler.TaskProfiler.MonitorUpdateDuration(coroutines[index], _info.runnerName);
#else
                            result = coroutines[index].MoveNext();
#endif
                        }
                        
                        var current = coroutines[index].Current;

                        if (result == false)
                            _coroutines.UnorderedRemoveAt(index);
                        else
                            index++;

                        var coroutinesCount = _coroutines.Count;
                        mustExit = (coroutinesCount == 0 ||
                             _info.CanMoveNext(ref index, current) == false || index >= coroutinesCount);
                    } 
                    while (!mustExit);
                }

                if (_flushingOperation.stopped == true && _coroutines.Count == 0)
                {   //once all the coroutines are flushed the loop can return accepting new tasks
                    _flushingOperation.stopped = false;
                }

                return true;
            }

            public void Reset()
            {}

            public object Current { get; private set; }
            
            readonly ThreadSafeQueue<ISveltoTask<T>> _newTaskRoutines;
            readonly FasterList<ISveltoTask<T>>      _coroutines;
            readonly FlushingOperation              _flushingOperation;
            
            RunningInfo _info;
        }

        public struct RunningTasksInfo:IRunningTasksInfo<T>
        {
            public bool CanMoveNext(ref int nextIndex, TaskCollection<T>.CollectionTask currentResult)
            {
                return true;
            }

            public bool CanProcessThis(ref int index)
            {
                return true;
            }

            public void Reset()
            {}

            public string runnerName { get; set; }
        }

        public class FlushingOperation
        {
            public bool paused;
            public bool stopped;
            public bool immediate;
            public bool kill;
        }

        const string GAMEOBJECT_ALREADY_EXISTING_ERROR = "A MonoRunner GameObject with the same name was already been used, did you forget to dispose the old one?";
    }
}
#endif