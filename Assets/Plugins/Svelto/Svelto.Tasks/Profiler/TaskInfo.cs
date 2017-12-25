using System;
using System.Collections;
using Svelto.DataStructures;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public sealed class TaskInfo
    {
        const int NUM_UPDATE_TYPES = 3;
        const int NUM_FRAMES_TO_AVERAGE = 10;

        public string taskName { get { return _threadInfo.FastConcat(_taskName); } }
        public double lastUpdateDuration { get { return _lastUpdateDuration; } }
        public double minUpdateDuration { get { return _minUpdateDuration; } }
        public double maxUpdateDuration { get { return _maxUpdateDuration; } }
        public double averageUpdateDuration { get { return _updateFrameTimes.Count == 0 ? 0 : _accumulatedUpdateDuration / _updateFrameTimes.Count; } }

        public TaskInfo(IEnumerator task)
        {
            _taskName = " ".FastConcat(task.ToString());

            _updateFrameTimes = new ThreadSafeQueue<double>();

            ResetDurations();
        }

        public void AddUpdateDuration(double updateDuration)
        {
            AddUpdateDurationForType(updateDuration);
        }

        public void AddThreadInfo(string threadInfo)
        {
            _threadInfo = threadInfo;
        }

        public void AddAddDuration(double duration)
        {
            if ((duration < _minAddDuration) || (Math.Abs(_minAddDuration) < double.Epsilon))
                _minAddDuration = duration;
            if (duration > _maxAddDuration)
                _maxAddDuration = duration;
        }

        public void AddRemoveDuration(double duration)
        {
            if ((duration < _minRemoveDuration) || (Math.Abs(_minRemoveDuration) < double.Epsilon))
                _minRemoveDuration = duration;
            if (duration > _maxRemoveDuration)
                _maxRemoveDuration = duration;
        }

        public void ResetDurations()
        {
            for (var i = 0; i < NUM_UPDATE_TYPES; i++)
            {
                _accumulatedUpdateDuration = 0;
                _minUpdateDuration = 0;
                _maxUpdateDuration = 0;
                _updateFrameTimes.Clear();
            }

            _minAddDuration = 0;
            _maxAddDuration = 0;

            _minRemoveDuration = 0;
            _maxRemoveDuration = 0;
        }

        void AddUpdateDurationForType(double updateDuration)
        {
            if ((updateDuration < _minUpdateDuration) || (Math.Abs(_minUpdateDuration) < double.Epsilon))
                _minUpdateDuration = updateDuration;
            if (updateDuration > _maxUpdateDuration)
                _maxUpdateDuration = updateDuration;

            if (_updateFrameTimes.Count == NUM_FRAMES_TO_AVERAGE)
                _accumulatedUpdateDuration -= _updateFrameTimes.Dequeue();

            _accumulatedUpdateDuration += updateDuration;
            _updateFrameTimes.Enqueue(updateDuration);
            _lastUpdateDuration = updateDuration;
        }

        double _accumulatedUpdateDuration;
        double _lastUpdateDuration;
        double _maxUpdateDuration;
        double _minUpdateDuration;

        readonly string _taskName;

        double _maxAddDuration;
        double _maxRemoveDuration;
        double _minAddDuration;
        double _minRemoveDuration;

        string _threadInfo;

        //use a queue to averave out the last 30 frames
        ThreadSafeQueue<double> _updateFrameTimes;
    }
}
