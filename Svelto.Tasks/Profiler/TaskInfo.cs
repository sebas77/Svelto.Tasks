#if TASKS_PROFILER_ENABLED
using System;

//This profiler is based on the Entitas Visual Debugging tool 
//https://github.com/sschmid/Entitas-CSharp

namespace Svelto.Tasks.Profiler
{
    public struct TaskInfo
    {
        public string taskName => _taskName;
        public double minUpdateDuration => _minUpdateDuration;
        public double maxUpdateDuration => _maxUpdateDuration;
        public float currentUpdateDuration => _currentUpdateDuration;
        public float averageUpdateDuration
        {
            get
            {
                float sum = 0;
                var length = ITERATIONS >> 2;
                for (int i = 0; i < length; i++)
                {
                    var index = i << 2;
                    
                    sum += times[index];
                    sum += times[index + 1];
                    sum += times[index + 2];
                    sum += times[index + 3];
                }

                return sum / ITERATIONS;
            }
        }
        public uint deltaCalls => _deltaCalls;
        
        public TaskInfo(string name, string runnerName) : this()
        {
            _taskName = " ".FastConcat(name, ":", runnerName);
            times = new float[ITERATIONS];
        }

        public void AddUpdateDuration(float updateDuration)
        {
            _currentUpdateDuration += updateDuration;

            _deltaCalls++;
        }

        public void MarkNextFrame()
        {
            if (_frame == 0)
            {
                _minUpdateDuration = float.MaxValue;
                _maxUpdateDuration = float.MinValue;
            }
            
            if (_currentUpdateDuration < _minUpdateDuration) _minUpdateDuration = _currentUpdateDuration;
            if (_currentUpdateDuration > _maxUpdateDuration) _maxUpdateDuration = _currentUpdateDuration;
            
            times[_frame] = _currentUpdateDuration;
            _frame = (_frame + 1) & (ITERATIONS - 1);
            
            _deltaCalls = 0;
            _currentUpdateDuration = 0;
        }

        float _currentUpdateDuration; float _maxUpdateDuration; float _minUpdateDuration;
        uint _deltaCalls; uint _frame;
        readonly string _taskName;
        readonly float[] times;

        const int ITERATIONS = 32;
    }
}
#endif