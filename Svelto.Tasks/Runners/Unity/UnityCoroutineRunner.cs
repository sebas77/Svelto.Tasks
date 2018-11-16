#if UNITY_5 || UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Unity.Internal
{
    public static class UnityCoroutineRunner
    {
        public static void StopRoutines(FlushingOperation
            flushingOperation)
        {
            //note: _coroutines will be cleaned by the single tasks stopping silently.
            //in this way they will be put back to the pool.
            //let's be sure that the runner had the time to stop and recycle the previous tasks
            flushingOperation.stopped = true;
        }

        internal static void InitializeGameObject(string name, ref GameObject go, bool mustSurvive)
        {
            var taskRunnerName = "TaskRunner.".FastConcat(name);

            DBC.Tasks.Check.Require(GameObject.Find(taskRunnerName) == null, GAMEOBJECT_ALREADY_EXISTING_ERROR);

            go = new GameObject(taskRunnerName);

            if (mustSurvive && Application.isPlaying)
                Object.DontDestroyOnLoad(go);
        }

        internal class Process<RunningInfo> : IEnumerator where RunningInfo: IRunningTasksInfo
        {
            readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines;
            readonly FasterList<IPausableTask>      _coroutines;
            readonly FlushingOperation              _flushingOperation;
            readonly RunningInfo                    _info;

            public Process( ThreadSafeQueue<IPausableTask> newTaskRoutines,
                            FasterList<IPausableTask>      coroutines, 
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
#if ENABLE_PLATFORM_PROFILER                
                using (var _platformProfiler = new Svelto.Common.PlatformProfiler(_info.runnerName))
#endif
                {
                    if (_newTaskRoutines.Count > 0 
                     && false == _flushingOperation.stopped) //don't start anything while flushing
                        _newTaskRoutines.DequeueAllInto(_coroutines); 
                    
                    _info.Reset();
                    
                    int index = 0;

                    if (_coroutines.Count == 0) return true;

                    bool result;
                    
                    while (true)
                    {
                        if (_info.CanProcessThis(ref index) == false) break;
                        
                        var pausableTask = _coroutines[index];
                        
#if ENABLE_PLATFORM_PROFILER                        
                        using (_platformProfiler.Sample(_coroutines[index].ToString()))
#endif
                        {
#if TASKS_PROFILER_ENABLED
                                result =
 Svelto.Tasks.Profiler.TaskProfiler.MonitorUpdateDuration(pausableTask, _info.runnerName);
#else
                                result = pausableTask.MoveNext();
#endif
                        }
                        
                        if (result == false)
                        {
                            var disposable = pausableTask as IDisposable;
                            if (disposable != null)
                                disposable.Dispose();

                            _coroutines.UnorderedRemoveAt(index);
                        }
                        else
                            index++;

                        if (_coroutines.Count == 0 ||
                            _info.CanMoveNext(ref index, pausableTask.Current) == false || index >= _coroutines.Count) break;
                    }
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
        }

        sealed public class RunningTasksInfo:IRunningTasksInfo
        {
            public bool CanMoveNext(ref int nextIndex, object currentResult)
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
            public bool stopped;
        }

        const string GAMEOBJECT_ALREADY_EXISTING_ERROR = "A MonoRunner GameObject with the same name was already been used, did you forget to dispose the old one?";
    }

    public interface IRunningTasksInfo
    {
        bool CanMoveNext(ref int nextIndex, object currentResult);
        bool CanProcessThis(ref int index);
        void Reset();
        string runnerName { get; }
    }
}
#endif