#if UNITY_5 || UNITY_5_3_OR_NEWER
using System;
using System.Collections;
using Svelto.DataStructures;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Svelto.Tasks.Unity.Internal
{
    public static partial class UnityCoroutineRunner
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

        internal class Process : IEnumerator
        {
            readonly ThreadSafeQueue<IPausableTask> _newTaskRoutines;
            readonly FasterList<IPausableTask>      _coroutines;
            readonly FlushingOperation              _flushingOperation;
            readonly RunningTasksInfo               _info;
            readonly Svelto.Common.PlatformProfiler _platformProfiler;

            public Process( ThreadSafeQueue<IPausableTask> newTaskRoutines,
                            FasterList<IPausableTask>      coroutines, 
                            FlushingOperation              flushingOperation,
                            RunningTasksInfo               info)
            {
                _newTaskRoutines = newTaskRoutines;
                _coroutines = coroutines;
                _flushingOperation = flushingOperation;
                _info = info;
                _platformProfiler = new Svelto.Common.PlatformProfiler(_info.runnerName);
            }    

            public bool MoveNext()
            {
                using (_platformProfiler.Sample(_info.runnerName))
                {
                    if (false == _flushingOperation.stopped) //don't start anything while flushing
                        _newTaskRoutines.DequeueAllInto(_coroutines);
                    
                    _info.Reset();
                    
                    for (int i = 0; i < _coroutines.Count && 
                                    _info.CanMoveNext(ref i, _coroutines.Count, _coroutines[i].Current); ++i)
                        using (_platformProfiler.Sample(_coroutines[i].ToString()))
                            StandardCoroutineProcess.StandardCoroutineIteration(ref i, _coroutines);
                }

                if (_flushingOperation.stopped == true && _coroutines.Count == 0)
                {   //once all the coroutines are flushed
                    //the loop can return accepting new tasks
                    _flushingOperation.stopped = false;
                }

                return true;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }

            public object Current { get; private set; }
        }

        public abstract class RunningTasksInfo
        {
            public string runnerName;

            public abstract bool CanMoveNext(ref int index, int count, object current);
            public abstract void Reset();
        }

        public sealed class StandardRunningTaskInfo : RunningTasksInfo
        {
            public override bool CanMoveNext(ref int index, int count, object current)
            {
                return true;
            }

            public override void Reset()
            {}
        }

        internal delegate void FlushTasksDel(ThreadSafeQueue<IPausableTask> 
            newTaskRoutines, FasterList<IPausableTask> coroutines, 
            FlushingOperation flushingOperation);

        public class FlushingOperation
        {
            public bool stopped;
        }

        const string GAMEOBJECT_ALREADY_EXISTING_ERROR = "A MonoRunner GameObject with the same name was already been used, did you forget to dispose the old one?";
    }
}
#endif