#if !NETFX_CORE

using System;
using System.Collections;
using System.Threading;
using NUnit.Framework;
using Svelto.Tasks;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.Experimental;
using UnityEngine;
using UnityEngine.TestTools;

//Note: RunSync is used only for testing purposes
//Real scenarios should use Run or RunManaged
namespace Test
{
    [TestFixture]
    public class TaskRunnerTests
    {
        [SetUp]
        public void Setup ()
        {
            _vo = new ValueObject();

            _serialTasks1 = new SerialTaskCollection<ValueObject>(_vo);
            _parallelTasks1 = new ParallelTaskCollection<ValueObject>(_vo);
            _serialTasks2 = new SerialTaskCollection<ValueObject>(_vo);
            _parallelTasks2 = new ParallelTaskCollection<ValueObject>(_vo);

            _task1 = new Task();
            _task2 = new Task();

            _taskChain1 = new TaskChain();
            _taskChain2 = new TaskChain();
            
            _iterable1 = new Enumerable(10000);
            _iterable2 = new Enumerable(10000);
            _iterableWithException = new Enumerable(-5);
            
            _taskRunner = TaskRunner.Instance;
            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine()
                .SetScheduler(StandardSchedulers.syncScheduler); //the taskroutine will stall the thread because it runs on the SyncScheduler
        }

        [Test]
        public void TestEnumerablesAreExecutedInSerial()
        {
            _serialTasks1.onComplete += () => Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime), Is.True);

            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,_serialTasks1);
        }

        [Test]
        public void TestSerialBreakIt()
        {
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(BreakIt());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, _serialTasks1);

            Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }

        IEnumerator BreakIt()
        {
            yield return Break.It;
        }

        [Test]
        public void TestParallelBreakIt()
        {
            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.Add(BreakIt());
            _parallelTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, _parallelTasks1);

            Assert.That(_iterable1.AllRight == false && _iterable1.iterations == 1 && 
                _iterable2.AllRight == false && _iterable2.iterations == 0);
        }

        [Test]
        public void TestBreakIt()
        {
             _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,SeveralTasks());

             Assert.That(_iterable1.AllRight == true && _iterable2.AllRight == false);
        }

        IEnumerator SeveralTasks()
        {
            yield return _iterable1.GetEnumerator();

            yield return Break.It;

            yield return _iterable2.GetEnumerator();
        }

        [Test]
        public void TestEnumerablesAreExecutedInSerialWithReusableTask()
        {
            _reusableTaskRoutine.SetEnumerator(TestSerialTwice()).Start();
        }

        IEnumerator TestSerialTwice()
        {
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime), Is.True);

            _iterable1.Reset(); _iterable2.Reset();
            _serialTasks1.Add(_iterable1.GetEnumerator());
            _serialTasks1.Add(_iterable2.GetEnumerator());

            yield return _serialTasks1;

            Assert.That(_iterable1.AllRight && _iterable2.AllRight && (_iterable1.endOfExecutionTime <= _iterable2.endOfExecutionTime), Is.True);
        }

        [Test]
        public void TestEnumerableAreExecutedInParallel()
        {
            _parallelTasks1.onComplete += () => { Assert.That(_iterable1.AllRight && _iterable2.AllRight); };

            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.Add(_iterable2.GetEnumerator());

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,_parallelTasks1);
        }

        [Test]
        public void TestPromisesExceptionHandler()
        {
            bool allDone = false;

            _serialTasks1.onComplete += () => { allDone = true; Assert.That(false);};

            _serialTasks1.Add (_iterable1.GetEnumerator());
            _serialTasks1.Add (_iterableWithException.GetEnumerator()); //will throw an exception

            _reusableTaskRoutine.SetEnumerator(_serialTasks1).Start
                (e => Assert.That(allDone, Is.False)); //will catch the exception
        }

        [Test]
        public void TestPromisesCancellation()
        {
            bool allDone = false;
            bool testDone = false;

            _serialTasks1.onComplete += () => { allDone = true; Assert.That(false); };

            _serialTasks1.Add (_iterable1.GetEnumerator());
            _serialTasks1.Add (_iterable2.GetEnumerator());
            
            //this time we will make the task run on another thread
            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner("TestPromisesCancellation")).
                SetEnumerator(_serialTasks1).Start
                (null, () => { testDone = true; Assert.That(allDone, Is.False); });
            _reusableTaskRoutine.Stop();

            while (testDone == false);
        }

        // Test ITask implementations

        [Test]
        public void TestSingleITaskExecution()
        {
            _task1.Execute();
            
            while (_task1.isDone == false);

            Assert.That(_task1.isDone);
        }

        [Test]
        public void TestSingleTaskExecutionCallsOnComplete()
        {
            _task1.OnComplete(() => Assert.That(_task1.isDone, Is.True) );
            
            _task1.Execute();

            while (_task1.isDone == false);
        }

        //test parallel and serial tasks

        [Test]
        public void TestSerializedTasksAreExecutedInSerial()
        {
            _serialTasks1.onComplete += () => Assert.That(_task1.isDone && _task2.isDone, Is.True); 
            
            _serialTasks1.Add (_task1);
            _serialTasks1.Add (_task2);
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,_serialTasks1);
        }

        [Test]
        public void TestTask1IsExecutedBeforeTask2()
        {
            bool test1Done = false;
            
            _task1.OnComplete(() => { test1Done = true; });
            _task2.OnComplete(() => { Assert.That (test1Done); });
            
            _serialTasks1.Add (_task1);
            _serialTasks1.Add (_task2);
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, _serialTasks1);
        }

        [Test]
        public void TestTasksAreExecutedInParallel()
        {
            _parallelTasks1.onComplete += () => Assert.That(_task1.isDone && _task2.isDone, Is.True); 
                
            _parallelTasks1.Add (_task1);
            _parallelTasks1.Add (_task2);
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, _parallelTasks1);
        }

        //test parallel/serial tasks combinations

        [Test]
        public void TestParallelTasks1IsExecutedBeforeParallelTask2 ()
        {
            TaskRunner.Instance.RunOnSchedule(StandardSchedulers.syncScheduler, SerialContinuation());
        }

        [Test]
        public void TestExtension()
        {
            SerialContinuation().RunOnSchedule(StandardSchedulers.syncScheduler);
        }

        IEnumerator SerialContinuation()
        {
            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(_task1);
            _parallelTasks1.Add(_iterable1.GetEnumerator());

            yield return _parallelTasks1;
            
            Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true;

            _parallelTasks1.Add(_task2);
            _parallelTasks1.Add(_iterable2.GetEnumerator());

            yield return _parallelTasks1;

            Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true;
            Assert.That(parallelTasks1Done && parallelTasks2Done);
        }

        [Test]
        public void TestParallelTasksAreExecutedInSerial()
        {
            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            _parallelTasks1.Add(_task1);
            _parallelTasks1.Add(_iterable1.GetEnumerator());
            _parallelTasks1.onComplete += () => { Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true; };

            _parallelTasks2.Add(_task2);
            _parallelTasks2.Add(_iterable2.GetEnumerator());
            _parallelTasks2.onComplete += () => { Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true; };

            _serialTasks1.Add(_parallelTasks1);
            _serialTasks1.Add(_parallelTasks2);
            _serialTasks1.onComplete += () => { Assert.That(parallelTasks1Done && parallelTasks2Done); };
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, _serialTasks1);
        }

        //test passage of token between tasks 

        [Test]
        public void TestSerialTasks1ExecutedInParallelWithToken ()
        {
            _serialTasks1.Add(_taskChain1);
            _serialTasks1.Add(_taskChain1);
            _serialTasks2.Add(_taskChain2);
            _serialTasks2.Add(_taskChain2);

            _parallelTasks1.Add(_serialTasks1);
            _parallelTasks1.Add(_serialTasks2);

            _parallelTasks1.onComplete += 
                () => Assert.That(_vo.counter, Is.EqualTo(4));

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, _parallelTasks1);
        }

        [Test]
        public void TestSerialTasksAreExecutedInParallel ()
        {
            int test1 = 0;
            int test2 = 0;

            _serialTasks1.Add (_iterable1.GetEnumerator());
            _serialTasks1.Add (_iterable2.GetEnumerator());
            _serialTasks1.onComplete += () => { test1++; test2++; }; 

            _serialTasks2.Add (_task1);
            _serialTasks2.Add (_task2);
            _serialTasks2.onComplete += () => { test2++; };

            _parallelTasks1.Add (_serialTasks1);
            _parallelTasks1.Add (_serialTasks2);
            _parallelTasks1.onComplete += () => Assert.That((test1 == 1) && (test2 == 2), Is.True);

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, _parallelTasks1);
        }

        [Test]
        public void TestStopStartTaskRoutine()
        {
            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner("TestStopStartTaskRoutine"));
            _reusableTaskRoutine.SetEnumerator(TestWithThrow()).Start();

            _reusableTaskRoutine.Stop();

            _reusableTaskRoutine.SetScheduler(StandardSchedulers.syncScheduler);
            var enumerator = TestWithoutThrow();
            _reusableTaskRoutine.SetEnumerator(enumerator).Start();
            Assert.That((int)enumerator.Current, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStopStart()
        {
            ValueObject result = new ValueObject();

            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner("TestSimpleTaskRoutineStopStart")).SetEnumerator(SimpleEnumerator(result)).Start();
            _reusableTaskRoutine.Stop();
            var continuator = _reusableTaskRoutine.SetEnumerator(SimpleEnumerator(result)).Start();
            
            while (continuator.MoveNext()) yield return null;

            Assert.That(result.counter == 2);
        }

        [UnityTest]
        public IEnumerator TestSimpleTaskRoutineStopStartWithProvider()
        {
            ValueObject result = new ValueObject();

            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner("TestSimpleTaskRoutineStopStartWithProvider")).SetEnumerator(SimpleEnumerator(result)).Start();
            _reusableTaskRoutine.SetEnumeratorProvider(() => SimpleEnumerator(result)).Start();
            _reusableTaskRoutine.Stop();
            var continuator = _reusableTaskRoutine.Start();

            while (continuator.MoveNext()) yield return null;

            Assert.That(result.counter == 2);
        }

        IEnumerator SimpleEnumerator(ValueObject result)
        {
            result.counter++;

            yield return new WaitForSecondsEnumerator(1);

            result.counter++;
        }

        IEnumerator TestWithThrow()
        {
            yield return null;

            throw new Exception();
        }

        IEnumerator TestWithoutThrow()
        {
            yield return 1;
        }

        [Test]
        public void TestComplexCoroutine()
        {
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,
                ComplexEnumerator((i) => Assert.That(i == 100, Is.True)));
        }

        [Test]
        public void TestMultithread()
        {
            using (var runner = new MultiThreadRunner("TestMultithread"))
            {
                _iterable1.Reset();

                var continuator = _iterable1.GetEnumerator().ThreadSafeRunOnSchedule(runner);

                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);

                _iterable1.Reset();

                continuator = _iterable1.GetEnumerator().ThreadSafeRunOnSchedule(runner);

                while (continuator.MoveNext());

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [Test]
        public void TestMultithreadQuick()
        {
            using (var runner = new MultiThreadRunner("TestMultithreadQuick", false))
            {
                var task = _iterable1.GetEnumerator().ThreadSafeRunOnSchedule(runner);

                while (task.MoveNext());

                Assert.That(_iterable1.AllRight == true);

                //do it again to test if starting another task works

                _iterable1.Reset();

                task = _iterable1.GetEnumerator().ThreadSafeRunOnSchedule(runner);

                while (task.MoveNext());

                Assert.That(_iterable1.AllRight == true);
            }
        }

        [Test]
        public void TestMultithreadIntervaled()
        {
            using (var runner = new MultiThreadRunner("TestMultithreadIntervaled", 1))
            {
                DateTime now = DateTime.Now;

                var task = _iterable1.GetEnumerator().ThreadSafeRunOnSchedule(runner);

                while (task.MoveNext());

                var seconds = (DateTime.Now - now).Seconds;

                //10000 iteration * 1ms = 10 seconds

                Assert.That(_iterable1.AllRight == true && seconds == 10);
            }
        }

        IEnumerator ComplexEnumerator(Action<int> callback)
        {
            int i = 0;
            int j = 0;
            while (j < 10)
            {
                j++;

                var enumerator = SubEnumerator(i);
                yield return enumerator;  //it will be executed on the same frame
                i = (int)enumerator.Current; //carefull it will be unboxed
            }

            callback(i);
        }

        IEnumerator SubEnumerator(int i)
        {
            int count = i + 10;
            while (++i < count)
                yield return null; //enable asynchronous execution

            yield return i; //careful it will be boxed;
        }

        TaskRunner _taskRunner;
        ITaskRoutine _reusableTaskRoutine;

        SerialTaskCollection<ValueObject> _serialTasks1;
        SerialTaskCollection<ValueObject> _serialTasks2;
        ParallelTaskCollection<ValueObject> _parallelTasks1;
        ParallelTaskCollection<ValueObject> _parallelTasks2;

        Task _task1;
        Task _task2;

        Enumerable _iterable1;
        Enumerable _iterable2;

        Enumerable _iterableWithException;
        TaskChain _taskChain1;
        TaskChain _taskChain2;
        ValueObject _vo;

        class Task : ITask
        {
            //ITask Implementation
            public bool  isDone { get; private set; }

            public Task()
            {
                isDone = false;
            }

            //ITask Implementation
            public void Execute() 
            {
                _delayTimer = new System.Timers.Timer
                {
                    Interval = 1000,
                    Enabled = true
                };
                _delayTimer.Elapsed += _delayTimer_Elapsed;
                _delayTimer.Start();
            }

            public void	OnComplete(Action action)
            {
                _onComplete += action;
            }

            void _delayTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            {
                isDone = true;
                if (_onComplete != null)
                    _onComplete();

                _delayTimer.Stop();
                _delayTimer = null;
            }

            System.Timers.Timer _delayTimer;
            Action _onComplete;
        }

        class TaskChain: ITaskChain<ValueObject>
        {
            public bool  isDone { get; private set; }
            
            public TaskChain()
            {
                isDone = false;
            }

            public void Execute(ValueObject token)
            {
                token.counter++;

                isDone = true;
            }
        }

        class ValueObject
        {
            public int counter;
        }

        class Enumerable : IEnumerable
        {
            public long endOfExecutionTime {get; private set;}

            public bool AllRight { get 
            {
                return iterations == totalIterations; 
            }}

            public Enumerable(int niterations)
            {
                iterations = 0; 
                totalIterations = niterations;
            }

            public void Reset()
            {
                iterations = 0;
            }

            public IEnumerator GetEnumerator()
            {
                if (totalIterations < 0)
                    throw new Exception("can't handle this");

                while (iterations < totalIterations)
                {
                    iterations++;
                    
                    yield return null;
                }
                
                endOfExecutionTime = DateTime.Now.Ticks;
            }

            int totalIterations;
            public int iterations;
        }
    }
}
#endif