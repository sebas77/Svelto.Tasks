#if TASKS_PROFILER_ENABLED
using System;
using System.Collections.Generic;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public struct TaskInfo
    {
        const int NUM_FRAMES_TO_AVERAGE = 100;

        public string taskName => _threadInfo.FastConcat(_taskName);
        public double lastUpdateDuration => _lastUpdateDuration;
        public double minUpdateDuration => _minUpdateDuration;
        public double maxUpdateDuration => _maxUpdateDuration;
        public double currentUpdateDuration => _updateFrameTimes.Count == 0 ? 0 : _currentUpdateDuration / _updateFrameTimes.Count;
        public double averageUpdateDuration => _averageUpdateDuration / _totaleFrames;

        public TaskInfo(string name) : this()
        {
            _taskName = " ".FastConcat(name);

            _updateFrameTimes = new Queue<double>();

            _currentUpdateDuration = 0;
            _averageUpdateDuration = 0;
            _minUpdateDuration     = 0;
            _maxUpdateDuration     = 0;
            _totaleFrames          = 0;
            _updateFrameTimes.Clear();
        }

        public void AddUpdateDuration(double updateDuration)
        {
            AddUpdateDurationForType(updateDuration);
        }

        public void AddThreadInfo(string threadInfo)
        {
            _threadInfo = threadInfo;
        }

        void AddUpdateDurationForType(double updateDuration)
        {
            if ((updateDuration < _minUpdateDuration) || (Math.Abs(_minUpdateDuration) < double.Epsilon))
                _minUpdateDuration = updateDuration;
            if (updateDuration > _maxUpdateDuration)
                _maxUpdateDuration = updateDuration;

            if (_updateFrameTimes.Count == NUM_FRAMES_TO_AVERAGE)
                _currentUpdateDuration -= _updateFrameTimes.Dequeue();

            _currentUpdateDuration += updateDuration;
            _averageUpdateDuration += updateDuration;
            _updateFrameTimes.Enqueue(updateDuration);
            _lastUpdateDuration = updateDuration;
            _totaleFrames++;
        }

        double _currentUpdateDuration;
        double _averageUpdateDuration;
        double _lastUpdateDuration;
        double _maxUpdateDuration;
        double _minUpdateDuration;

        double _totaleFrames;

        readonly string _taskName;

        string _threadInfo;

        //use a queue to averave out the last 30 frames
        readonly Queue<double> _updateFrameTimes;
    }
}
#endif