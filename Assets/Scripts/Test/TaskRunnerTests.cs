#region Usings

using System;
using System.Collections;
using NUnit.Framework;
using Svelto.Tasks;

#endregion

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
            vo = new ValueObject();

            serialTasks1 = new SerialTaskCollection<ValueObject>(vo);
            parallelTasks1 = new ParallelTaskCollection<ValueObject>(vo);
            serialTasks2 = new SerialTaskCollection<ValueObject>(vo);
            parallelTasks2 = new ParallelTaskCollection<ValueObject>(vo);

            task1 = new Task();
            task2 = new Task();

            taskChain1 = new TaskChain();
            taskChain2 = new TaskChain();
            
            iterable1 = new Enumerable(10000);
            iterable2 = new Enumerable(10000);
            iterableWithException = new Enumerable(-5);
            
            _taskRunner = TaskRunner.Instance;

            _reusableTaskRoutine = TaskRunner.Instance.AllocateNewTaskRoutine().SetScheduler(StandardSchedulers.syncScheduler); //the taskroutine will stall the thread because it runs on the SyncScheduler
        }

        [Test]
        public void TestEnumerablesAreExecutedInSerial()
        {
            serialTasks1.onComplete += () => Assert.That(iterable1.AllRight && iterable2.AllRight && (iterable1.endOfExecutionTime <= iterable2.endOfExecutionTime), Is.True);

            serialTasks1.Add(iterable1);
            serialTasks1.Add(iterable2);
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,serialTasks1);
        }

        [Test]
        public void TestEnumerablesAreExecutedInSerialWithReusableTask()
        {
            _reusableTaskRoutine.SetEnumerator(TestSerialTwice()).Start();
        }
        
        IEnumerator TestSerialTwice()
        {
            serialTasks1.Add(iterable1);
            serialTasks1.Add(iterable2);

            yield return serialTasks1;

            Assert.That(iterable1.AllRight && iterable2.AllRight && (iterable1.endOfExecutionTime <= iterable2.endOfExecutionTime), Is.True);

            iterable1.Reset(); iterable2.Reset();
            serialTasks1.Add(iterable1);
            serialTasks1.Add(iterable2);

            yield return serialTasks1;

            Assert.That(iterable1.AllRight && iterable2.AllRight && (iterable1.endOfExecutionTime <= iterable2.endOfExecutionTime), Is.True);
        }

        [Test]
        public void TestEnumerableAreExecutedInParallel()
        {
            parallelTasks1.onComplete += () => { Assert.That(iterable1.AllRight && iterable2.AllRight); };

            parallelTasks1.Add(iterable1);
            parallelTasks1.Add(iterable2);

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,parallelTasks1);
        }

        [Test]
        public void TestPromisesExceptionHandler()
        {
            bool allDone = false;

            serialTasks1.onComplete += () => { allDone = true; Assert.That(false);};

            serialTasks1.Add (iterable1);
            serialTasks1.Add (iterableWithException); //will throw an exception

            _reusableTaskRoutine.SetEnumerator(serialTasks1).Start
                (e => Assert.That(allDone, Is.False)); //will catch the exception
        }

        [Test]
        public void TestPromisesCancellation()
        {
            bool allDone = false;
            bool testDone = false;

            serialTasks1.onComplete += () => { allDone = true; Assert.That(false); };

            serialTasks1.Add (iterable1);
            serialTasks1.Add (iterable2);
            
            //this time we will make the task run on another thread
            _reusableTaskRoutine.SetScheduler(new MultiThreadRunner()).SetEnumerator(serialTasks1).Start(null, () => { testDone = true; Assert.That(allDone, Is.False); });
            _reusableTaskRoutine.Stop();

            while (testDone == false);
        }

        // Test ITask implementations

        [Test]
        public void TestSingleITaskExecution()
        {
            task1.Execute();
            
            while (task1.isDone == false);

            Assert.That(task1.isDone);
        }

        [Test]
        public void TestSingleTaskExecutionCallsOnComplete()
        {
            task1.OnComplete(() => Assert.That(task1.isDone, Is.True) );
            
            task1.Execute();

            while (task1.isDone == false);
        }

        //test parallel and serial tasks

        [Test]
        public void TestSerializedTasksAreExecutedInSerial()
        {
            serialTasks1.onComplete += () => Assert.That(task1.isDone && task2.isDone, Is.True); 
            
            serialTasks1.Add (task1);
            serialTasks1.Add (task2);
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,serialTasks1);
        }

        [Test]
        public void TestTask1IsExecutedBeforeTask2()
        {
            bool test1Done = false;
            
            task1.OnComplete(() => { test1Done = true; });
            task2.OnComplete(() => { Assert.That (test1Done); });
            
            serialTasks1.Add (task1);
            serialTasks1.Add (task2);
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, serialTasks1);
        }

        [Test]
        public void TestTasksAreExecutedInParallel()
        {
            parallelTasks1.onComplete += () => Assert.That(task1.isDone && task2.isDone, Is.True); 
                
            parallelTasks1.Add (task1);
            parallelTasks1.Add (task2);
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, parallelTasks1);
        }

        //test parallel/serial tasks combinations

        [Test]
        public void TestParallelTasks1IsExecutedBeforeParallelTask2 ()
        {
            TaskRunner.Instance.RunOnSchedule(StandardSchedulers.syncScheduler,SerialContinuation());
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

            parallelTasks1.Add(task1);
            parallelTasks1.Add(iterable1);

            yield return parallelTasks1;
            
            Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true;

            parallelTasks1.Add(task2);
            parallelTasks1.Add(iterable2);

            yield return parallelTasks1;

            Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true;

            Assert.That(parallelTasks1Done && parallelTasks2Done);
        }

        [Test]
        public void TestParallelTasksAreExecutedInSerial()
        {
            bool parallelTasks1Done = false;
            bool parallelTasks2Done = false;

            parallelTasks1.Add(task1);
            parallelTasks1.Add(iterable1);
            parallelTasks1.onComplete += () => { Assert.That(parallelTasks2Done, Is.False); parallelTasks1Done = true; };

            parallelTasks2.Add(task2);
            parallelTasks2.Add(iterable2);
            parallelTasks2.onComplete += () => { Assert.That(parallelTasks1Done, Is.True); parallelTasks2Done = true; };

            serialTasks1.Add(parallelTasks1);
            serialTasks1.Add(parallelTasks2);
            serialTasks1.onComplete += () => { Assert.That(parallelTasks1Done && parallelTasks2Done); };
            
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,serialTasks1);
        }

        //test passage of token between tasks 

        [Test]
        public void TestSerialTasks1ExecutedInParallelWithToken ()
        {
            serialTasks1.Add(taskChain1);
            serialTasks1.Add(taskChain1);
            serialTasks2.Add(taskChain2);
            serialTasks2.Add(taskChain2);

            parallelTasks1.Add(serialTasks1);
            parallelTasks1.Add(serialTasks2);

            parallelTasks1.onComplete += 
                () => Assert.That(vo.counter, Is.EqualTo(4));

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, parallelTasks1);
        }

        [Test]
        public void TestSerialTasksAreExecutedInParallel ()
        {
            int test1 = 0;
            int test2 = 0;

            serialTasks1.Add (iterable1);
            serialTasks1.Add (iterable2);
            serialTasks1.onComplete += () => { test1++; test2++; }; 

            serialTasks2.Add (task1);
            serialTasks2.Add (task2);
            serialTasks2.onComplete += () => { test2++; };

            parallelTasks1.Add (serialTasks1);
            parallelTasks1.Add (serialTasks2);
            parallelTasks1.onComplete += () => Assert.That((test1 == 1) && (test2 == 2), Is.True);

            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler, parallelTasks1);
        }

        [Test]
        public void TestComplexCoroutine()
        {
            _taskRunner.RunOnSchedule(StandardSchedulers.syncScheduler,ComplexEnumerator((i) => Assert.That(i == 100, Is.True)));
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

        SerialTaskCollection serialTasks1;
        SerialTaskCollection serialTasks2;
        ParallelTaskCollection parallelTasks1;
        ParallelTaskCollection parallelTasks2;

        Task task1;
        Task task2;

        Enumerable iterable1;
        Enumerable iterable2;

        Enumerable iterableWithException;
        TaskChain taskChain1;
        TaskChain taskChain2;
        ValueObject vo;

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
            int iterations;
        }
    }
}
