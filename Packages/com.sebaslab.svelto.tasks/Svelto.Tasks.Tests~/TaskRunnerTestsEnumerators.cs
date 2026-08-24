using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Tasks.Enumerators;
using Svelto.Tasks.FlowModifiers;
using Svelto.Tasks.Lean;
using Svelto.Tasks.Parallelism;

#if !NETFX_CORE
namespace Svelto.Tasks.Tests
{
    /// <summary>
    ///  Shows how to use Svelto.Tasks with plain Enumerator. While convenient, using just enumerators
    ///  would enable a subset of the Svelto.Task potential.
    ///  TaskRoutines should be exploited to enable advanced features. 
    /// </summary>
    [TestFixture]
    public class TaskRunnerTests
    {
        [SetUp]
        public void Setup()
        {
            _iterable1 = new LeanEnumerator(100);
            
            LocalSyncRunners.Reset();
        }

#if PROFILE_SVELTO        
        [Test]
        public void TestPooledTaskMemoryUsage()
        {
            IEnumerator<TaskContract> enumerator = new WaitForSecondsEnumerator(0.1f);

            var runner = new MultiThreadRunner("test");
            enumerator.RunOn(runner);
            Assert.That(runner.WaitForTasksDone(2000), Is.True);

            /*Assert.That(() =>
            {
                var continuationWrapper = enumerator.RunOn(runner);
                while ((continuationWrapper).isRunning == true) ;
            }, Is.Not.AllocatingGCMemory());*/

            runner.Dispose();
        }
#endif
        [Test]
        public void TestTaskReturningTheContinuationOfOtherTasks()
        {
            int index = 0;

            //careful, runners can be garbage collected if they are not referenced somewhere and the
            //framework does not keep a reference
            using (var updateMonoRunner = new SteppableRunner("update"))
            {
                var cont = TestIt().RunOn(updateMonoRunner);

                updateMonoRunner.WaitForTasksDoneRelaxed(1000);
                
                Assert.That(index, Is.EqualTo(7));
            }

            IEnumerator<TaskContract> TestIt()
            {
                for (int i = 0; i < 3; i++)
                    yield return TestContinuation(i);
            }            
            
            TaskContract TestContinuation(int i)
            {
                switch (i)
                {
                    case 0:
                        return TestEnum().Continue();
                    case 1:
                        Assert.That(index, Is.EqualTo(3));
                        index++;
                        return TaskContract.Continue.It;
                    case 2:
                        Assert.That(index, Is.EqualTo(4));
                        return TaskContractReturningIEnumerator().Continue();
                    default:
                        throw new Exception();
                }
            }
            
            IEnumerator<TaskContract> TestEnum()
            {
                yield return TaskContract.Yield.It;
                yield return TaskContractReturningIEnumerator().Continue();
                yield return TaskContract.Yield.It;
            }            
            
            IEnumerator TaskContractReturningIEnumerator()
            {
                for (int i = 0; i < 3; i++)
                {
                    yield return TaskContract.Yield.It;
                    index++;
                }
            }
        }
        
        /// <summary>
        /// a Task Contract can return among the other things an IEnumerator.
        /// the IEnumerator must complete correctly before returning to the caller
        /// </summary>
        [Test]
        public void TestTaskContractReturningIEnumerator()
        {
            int index = 0;

            //careful, runners can be garbage collected if they are not referenced somewhere and the
            //framework does not keep a reference
            using (var updateMonoRunner = new SteppableRunner("update"))
            {
                var cont = TestEnum().RunOn(updateMonoRunner);

                updateMonoRunner.WaitForTasksDoneRelaxed(1000);
                
                Assert.That(index, Is.EqualTo(3));
            }
            
            IEnumerator<TaskContract> TestEnum()
            {
                yield return TaskContract.Yield.It;
                yield return TaskContractReturningIEnumerator().Continue();
                yield return TaskContract.Yield.It;
            }            
            
            IEnumerator TaskContractReturningIEnumerator()
            {
                while (index < 3)
                {
                    yield return TaskContract.Yield.It;
                    index++;
                }
            }
        }
        
        [Test]
        public void TestTaskContractReturningATaskContractReturningATaskContract()
        {
            using (var updateMonoRunner = new SteppableRunner("update"))
            {
                var testEnum = FirstEnum();
                var cont = testEnum.RunOn(updateMonoRunner);

                updateMonoRunner.WaitForTasksDoneRelaxed(1000);
                
                if (cont.isRunning)
                    Assert.Fail("timeout");

                Assert.That(testEnum.Current.ToInt(), Is.EqualTo(10));
            }
            
            IEnumerator<TaskContract> FirstEnum()
            {
                yield return TaskContract.Yield.It;

                var testEnum1 = SecondEnum();
                yield return testEnum1.Continue();
                
                yield return testEnum1.Current;
            }            
            
            IEnumerator<TaskContract> SecondEnum()
            {
                var subEnumerator = SubEnumerator(0, 10);
                yield return subEnumerator.Continue();
                yield return subEnumerator.Current;
            }
        }
        
        [Test]
        public void TaskYieldingAnotherTaskRunningOnAnotherRunner()
        {
            int index = 0;

            //careful, runners can be garbage collected if they are not referenced somewhere and the
            //framework does not keep a reference
            using var updateMonoRunner2 = new SteppableRunner("update2");
            
            using (var updateMonoRunner = new SteppableRunner("update"))
            {
                var cont = TestEnum().RunOn(updateMonoRunner);

                DateTime dateTime = DateTime.Now.AddSeconds(1);
                while (cont.isRunning && DateTime.Now < dateTime)
                {
                    updateMonoRunner.Step();
                    updateMonoRunner2.Step();
                }

                Assert.That(index, Is.EqualTo(3));
            }
            
            IEnumerator<TaskContract> TestEnum()
            {
                yield return TaskContract.Yield.It;
                yield return TaskContractReturningIEnumerator().RunOn(updateMonoRunner2);
                yield return TaskContract.Yield.It;
            }            
            
            IEnumerator<TaskContract> TaskContractReturningIEnumerator()
            {
                while (index < 3)
                {
                    yield return TaskContract.Yield.It;
                    index++;
                }
            }
        }

        [Test]
        public void TestTaskContractJustYielding()
        {
            //careful, runners can be garbage collected if they are not referenced somewhere and the
            //framework does not keep a reference
            using (var updateMonoRunner = new SteppableRunner("update"))
            {
                var cont = new LeanEnumerator(1).RunOn(updateMonoRunner);

                Assert.That(cont.isRunning, Is.True);

                updateMonoRunner.Step();

                Assert.That(cont.isRunning, Is.True);

                updateMonoRunner.Step();

                Assert.That(cont.isRunning, Is.False);
            }
        }

        class ItsAllRight
        { 
        
            public bool AllRight = false;
        }
        /// <summary>
        /// Test if Complete() works correctly with a simple IEnumerator
        /// </summary>
        /// <returns></returns>
        [Test]
        public void TestCompleteWithIEnumerator()
        {
            IEnumerator SimpleIEnumerator(ItsAllRight itsAllRight)
            {
                int i = 0;
                while (i < 10)
                {
                    i++;
                    yield return null;
                }
                
                itsAllRight.AllRight = true;
            }

            var allRight = new ItsAllRight();
            var simpleIEnumerator = SimpleIEnumerator(allRight);
            simpleIEnumerator.Complete( 1000); //ms

            Assert.That(allRight.AllRight, Is.True);
        }
        
        /// <summary>
        /// Test if Complete() works correctly with a struct IEnumerator
        /// </summary>
        /// <returns></returns>
        [Test]
        public void TestCompleteWithStructEnumerator()
        {
            var extraLeanEnumerator = new ExtraLeanEnumerator(10);
            extraLeanEnumerator.Complete( 1000); //ms

            Assert.That(extraLeanEnumerator.AllRight, Is.True);
        }

        /// <summary>
        /// Test if Complete() works correctly on a simple TaskContract
        /// must return correct value in Current
        /// </summary>
        /// <returns></returns>
        [Test]
        public void TestCompleteWithTaskContract()
        {
            var subEnumerator = SubEnumerator(0, 10);
            subEnumerator.Complete(1000); //ms

            Assert.That(subEnumerator.Current.ToInt(), Is.EqualTo(10));
        }

        /// <summary>
        /// Svelto tasks allows to yield enumerators from inside other enumerators allowing more complex
        /// sequence of actions
        /// </summary>
        /// <returns></returns>
        [Test]
        public void TestFancyEnumerator()
        {
            //the assertion runs inside a task body: without this guard a failure would be
            //swallowed by the runner and the test would silently pass
            using (new FailOnSwallowedTaskExceptions())
            {
                ComplexEnumerator((i) => Assert.That(i, Is.EqualTo(100))).Complete(1000); //ms
            }
        }

        /// <summary>
        /// shows how simple is to concatenate parallel and serial sequence of naive enumerators
        /// </summary>
        /// <returns></returns>
        [Test]
        public void TestEvenFancierEnumerator()
        {
            using (new FailOnSwallowedTaskExceptions())
            {
                var multiThreadRunner = new MultiThreadRunner("test");
                MoreComplexEnumerator((i) => Assert.That(i, Is.EqualTo(20)), multiThreadRunner).Complete(1000); //ms
                multiThreadRunner.Dispose();
            }
        }

        /// <summary>
        /// this is the most common use of the Enumerators running on tasks. Svelto.Tasks is the core of
        /// every looped routine and nothing else should be used to create loops. (This is how loops
        /// are create in Svelto.ECS engines for examples)
        /// </summary>
        /// <returns></returns>
        [Test]
        public void StandardUseOfEnumerators()
        {
            //you would normally write GameLoop().Run() to run on the standard scheduler, which changes
            //according the platform (on Unity the standard scheduler is the CoroutineMonoRunner, which
            //cannot run during these tests)
            //GameLoop waits twice on a reusable WaitForSecondsEnumerator through a continuation: those
            //waits are executed by the runner, so completing the loop must really take two seconds.
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            GameLoop().Complete(5000); //ms

            Assert.That(watch.ElapsedMilliseconds, Is.GreaterThanOrEqualTo(2000),
                "the WaitForSecondsEnumerator waits were not actually processed");
        }


        [Test]
        public void RunSeparateCoroutinesInParallelAndWaitForThem()
        {
            using (var runner = new SyncRunner("test"))
            {
                bool composeCompleted = false;

                IEnumerator<TaskContract> InitCompose()
                {
                    var smartFunctionEnumerator = new SmartFunctionEnumerator<int>(ExitTest, 0);

                    var wait1 = smartFunctionEnumerator.RunOn(runner);
                    var wait2 = smartFunctionEnumerator.RunOn(runner);
                    var wait3 = smartFunctionEnumerator.RunOn(runner);
                    var wait4 = smartFunctionEnumerator.RunOn(runner);

                    while (wait1.isRunning == true || wait2.isRunning == true || wait3.isRunning == true
                     || wait4.isRunning == true)
                        yield return TaskContract.Yield.It;

                    composeCompleted = true;
                }

                var task = InitCompose().RunOn(runner);

                Assert.That(runner.WaitForTasksDoneRelaxed(1000), Is.True, "the tasks did not complete within the deadline");
                Assert.That(composeCompleted, Is.True, "the waiting task never observed its continuations completing");
                Assert.That(task.isRunning, Is.False, "the waiting task is still running after the continuations completed");
            }
        }

        [Test]
        public void StandardUseOfEnumerators2()
        {
            /// <summary>
            /// since in order to avoid allocations is needed to preallocate enumerators, the SmartFunctionEnumerator
            /// can avoid some boilerplate 
            /// </summary>
            static IEnumerator<TaskContract> GameLoop2()
            {
                //initialization phase, for example you can precreate reusable enumerators or taskroutines
                //to avoid runtime allocations
                var smartFunctionEnumerator = new SmartFunctionEnumerator<int>(ExitTest, 0);

                //start a loop, you can actually start multiple loops with different conditions so that
                //you can wait for specific states to be valid before to start the real loop 
                yield return smartFunctionEnumerator.Continue();
                yield return smartFunctionEnumerator.Continue(); //it can be reused in opposition to a compiler generated iterator block
                yield return smartFunctionEnumerator.value;
            }

            var gameLoop2 = GameLoop2();
            gameLoop2.Complete(1000); //ms

            Assert.That(gameLoop2.Current.ToInt(), Is.EqualTo(2));
        }
        
        public class SerialSteppableRunner : SyncRunner
        {
            public SerialSteppableRunner(string name):base(name)
            {
                UseFlowModifier(new SerialFlow());
            }
        }
        class RefHolder
        {
            public uint value;
        }   

        
        [Test]
        public void SerialFlowModifierRunsTasksInSerial()
        {
            IEnumerator<TaskContract> TestEnum(RefHolder value, uint testvalue)
            {
                Assert.That(value.value, Is.EqualTo(testvalue));
                
                yield return TaskContract.Yield.It;
                value.value++;
                yield return TaskContract.Yield.It;
                value.value++;
                yield return TaskContract.Yield.It;
                value.value++;
                
                Assert.That(value.value, Is.EqualTo(testvalue + 3));
            }
            
            IEnumerator<TaskContract> TestEnum2(RefHolder value, uint testvalue)
            {
                Assert.That(value.value, Is.EqualTo(testvalue));

                yield return TestEnum(value, testvalue).Continue();
                yield return TestEnum(value, testvalue + 3).Continue();
                yield return TestEnum(value, testvalue + 6).Continue();
                
                Assert.That(value.value, Is.EqualTo(testvalue + 9));
            }

            var                   refHolder = new RefHolder();
            SerialSteppableRunner runner    = new SerialSteppableRunner("test");

            //although the tasks run in serial, they order of execution is not guaranteed (that's why it's 0, 18, 9)
            //task removed are shuffled
            TestEnum2(refHolder, 0).RunOn(runner);
            TestEnum2(refHolder, 18).RunOn(runner); //Serial flow doesn't guarantee the order of execution
            TestEnum2(refHolder, 9).RunOn(runner);
            
            runner.WaitForTasksDoneRelaxed(10000);
            
            Assert.That(refHolder.value, Is.EqualTo(27));
        }
        
        [Test]
        public void TestEnumeratorContinuingEnumeratorReturningValue()
        {
            //the assertion on the returned value runs inside the task body: without this guard
            //a failure would be swallowed by the runner and the test would silently pass
            using (new FailOnSwallowedTaskExceptions())
            using (var runner = new MultiThreadRunner("test"))
            {
                NestedEnumerator().RunOn(runner);

                Assert.That(runner.WaitForTasksDone(2000), Is.True);
            }
        }

        static IEnumerator<TaskContract> NestedEnumerator()
        {
            var simpleEnumerator = SimpleEnumerator();
            yield return simpleEnumerator.Continue();
            Assert.That(simpleEnumerator.Current.ToInt(), Is.EqualTo(10));
        }

        static IEnumerator<TaskContract> SimpleEnumerator()
        {
            yield return 10;
        }

        static IEnumerator<TaskContract> GameLoop()
        {
            //initialization phase, for example you can precreate reusable enumerators or taskroutines
            //to avoid runtime allocations
            var reusableEnumerator = new WaitForSecondsEnumerator(1);
            int i                  = 0;

            //start a loop, you can actually start multiple loops with different conditions so that
            //you can wait for specific states to be valid before to start the real loop 
            while (true //usually last as long as the application run
            )
            {
                if (i++ > 1)
                    yield break;

                yield return reusableEnumerator.Continue();

                reusableEnumerator.Reset();

                yield return TaskContract.Yield.It; //yield one iteration, if you forget this, will enter in an infinite loop!
                //it's not mandatory but there must be at least one yield in a loop
            }
        }

        /// <summary>
        /// it will be called until i >= 2
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        static bool ExitTest(ref int i)
        {
            if (i < 2)
            {
                i++;
                return true;
            }

            return false;
        }

        /// <summary>
        /// very naive implementation, it's boxing and allocation madness. Just for testing purposes
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        IEnumerator<TaskContract> ComplexEnumerator(Action<int> callback)
        {
            int i = 0;
            int j = 0;
            while (j < 5) //do it five times
            {
                j++;

                var enumerator = SubEnumerator(i, 10); //naive enumerator! it allocates
                yield return enumerator.Continue(); //yield until is done 
                enumerator = SubEnumerator((int)enumerator.Current.ToInt(), 10); //naive enumerator! it allocates
                yield return enumerator.Continue(); //yield until is done 
                i = (int)enumerator.Current.ToInt(); //careful it will be unboxed
            }

            callback(i);
        }

        /// <summary>
        /// very naive implementation, it's boxing and allocation madness. Just for testing purposes
        /// this give a first glimpse to the powerful concept of Svelto Tasks continuation (running
        /// tasks on other runners and white their completion on the current runner)
        /// </summary>
        /// <param name="callback"></param>
        /// <returns></returns>
        IEnumerator<TaskContract> MoreComplexEnumerator(Action<int> callback, MultiThreadRunner runner)
        {
            int i = 0;
            {
                var enumerator1 = SubEnumerator(0, 10); //naive enumerator! it allocates
                var enumerator2 = SubEnumerator(0, 10); //naive enumerator! it allocates

                //the two enumerator will run in "parallel" on the multithread runner
                //I need to use a multithread runner as these tests won't allow using the normal
                //coroutine while the syncrunner is not suitable as it forces the current task to be finished first 
                //defeating the point of testing the parallelism
                //Running an enumerator from inside another enumerator is different than yielding. A yield
                //enumerator is yield on the same runner, while in this way the enumerator starts on another
                //runner. The generated continuator is a new enumerator, used to spin the current runner
                //untile the enumerators are done.
                //In a real scenario, any compatible runner can be used.
                yield return enumerator1.RunOn(runner);
                yield return enumerator2.RunOn(runner);

                i = (int)enumerator1.Current.ToInt() + (int)enumerator2.Current.ToInt();
            }

            callback(i);
        }

        IEnumerator<TaskContract> SubEnumerator(int i, int total)
        {
            int count = i + total;
            do
            {
                yield return TaskContract.Yield.It; //enable asynchronous execution
            } while (++i < count);

            yield return i; //careful it will be boxed;
        }

        [Test]
        public void TestTaskDisposeCalledOnCompletion()
        {
            var disposableTask = new DisposableEnumerator();
            using (var runner = new SteppableRunner("test"))
            {
                disposableTask.RunOn(runner);
                // Run until completion
                runner.WaitForTasksDoneRelaxed(1000);
                Assert.That(disposableTask.disposed, Is.True);
            }
        }

        [Test]
        public void TestTaskDisposeCalledOnRunnerDispose()
        {
            var disposableTask = new DisposableEnumerator();
            var runner = new SteppableRunner("test");
            disposableTask.RunOn(runner);
            // Don't complete, just dispose runner
            runner.Dispose();
            Assert.That(disposableTask.disposed, Is.True);
        }

        [Test]
        public void TestTaskDisposeCalledOnMultiThreadRunnerDispose()
        {
            var started = new ManualResetEventSlim(false);
            var disposableTask = new StartedDisposableEnumerator(started);
            var runner         = new MultiThreadRunner("test");

            disposableTask.RunOn(runner);

            Assert.That(started.Wait(2000), Is.True);

            runner.Dispose();

            Assert.That(disposableTask.disposed, Is.True);
        }

        [Test]
        public void TestJobDisposeCalledOnMultiThreadRunnerDispose()
        {
            var started = new ManualResetEventSlim(false);
            var disposableJobTask = new StartedDisposableJobEnumerator(started);
            var runner            = new MultiThreadRunner("test");

            disposableJobTask.RunOn(runner);

            Assert.That(started.Wait(2000), Is.True);

            runner.Dispose();

            Assert.That(disposableJobTask.disposed, Is.True);
        }

        class StartedDisposableJobEnumerator : IEnumerator<TaskContract>, ISveltoJob
        {
            public StartedDisposableJobEnumerator(ManualResetEventSlim started)
            {
                _started = started;
            }

            public TaskContract Current => TaskContract.Yield.It;
            object IEnumerator.Current  => Current;

            public bool MoveNext()
            {
                _started.Set();
                return ++count < 3;
            }

            public void Reset() { count = 0; }

            public void Update(int jobIndex) { }

            public void Dispose() 
            {
                Volatile.Write(ref _disposed, true); 
            }
            
            public bool disposed
            {
                get => Volatile.Read(ref _disposed);
            }

            bool _disposed = false;

            readonly ManualResetEventSlim _started;
            int count = 0;
        }

        class DisposableJobEnumerator : IEnumerator<TaskContract>, ISveltoJob
        {
            public TaskContract Current => TaskContract.Yield.It;
            object IEnumerator.Current  => Current;

            public bool MoveNext() => ++count < 3;

            public void Reset() { count = 0; }

            public void Update(int jobIndex) { }

            public void Dispose() 
            {
                Volatile.Write(ref _disposed, true); 
            }
            
            public bool disposed
            {
                get => Volatile.Read(ref _disposed);
            }

            bool _disposed = false;

            int count = 0;
        }

        class StartedDisposableEnumerator : IEnumerator<TaskContract>
        {
            public StartedDisposableEnumerator(ManualResetEventSlim started)
            {
                _started = started;
            }

            public TaskContract Current => TaskContract.Yield.It;
            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                _started.Set();
                return ++count < 3;
            }

            public void Reset() { count = 0; }

            public void Dispose() { disposed = true; }

            public volatile bool disposed = false;

            readonly ManualResetEventSlim _started;
            int count = 0;
        }

        class DisposableEnumerator : IEnumerator<TaskContract>
        {
            public TaskContract Current => TaskContract.Yield.It;
            object IEnumerator.Current => Current;
            public bool MoveNext() => ++count < 3;
            public void Reset() { count = 0; }
            public void Dispose() { disposed = true; }
            public volatile bool disposed = false;
            int count = 0;
        }

        [TearDown]
        public void TearDown()
        {
            _iterable1.Dispose();
        }

        LeanEnumerator _iterable1;
    }
}
#endif