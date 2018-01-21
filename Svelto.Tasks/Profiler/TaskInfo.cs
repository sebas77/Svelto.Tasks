using System;
using System.Collections;
using System.Collections.Generic;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public struct TaskInfo
    {
        const int NUM_UPDATE_TYPES = 3;
        const int NUM_FRAMES_TO_AVERAGE = 10;

        public string taskName { get { return _threadInfo.FastConcat(_taskName); } }
        public double lastUpdateDuration { get { return _lastUpdateDuration; } }
        public double minUpdateDuration { get { return _minUpdateDuration; } }
        public double maxUpdateDuration { get { return _maxUpdateDuration; } }
        public double averageUpdateDuration { get { return _updateFrameTimes.Count == 0 ? 0 : _accumulatedUpdateDuration / _updateFrameTimes.Count; } }

        public TaskInfo(IEnumerator task) : this()
        {
            _taskName = " ".FastConcat(task.ToString());

            _updateFrameTimes = new Queue<double>();

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

        void ResetDurations()
        {
            for (var i = 0; i < NUM_UPDATE_TYPES; i++)
            {
                _accumulatedUpdateDuration = 0;
                _minUpdateDuration = 0;
                _maxUpdateDuration = 0;
                _updateFrameTimes.Clear();
            }
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

        string _threadInfo;

        //use a queue to averave out the last 30 frames
        Queue<double> _updateFrameTimes;
    }
}
