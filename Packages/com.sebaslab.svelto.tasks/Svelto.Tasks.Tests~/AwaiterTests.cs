using System.Threading.Tasks;
using Svelto.Tasks.Lean;
using Svelto.Tasks.Enumerators;

namespace Svelto.Tasks.Tests
{
    [TestFixture]
    public class AwaiterTests
    {
        class testClass
        {
            public bool continued = false;
        }
        [Test]
        public void TestThatSveltoAwaiterContinuationDoesNotRunWhenRunnerStops()
        {
            // arrange
            var runner = new SteppableRunner("SveltoAwaiterTest");
            
            var continued = new testClass();

            // obtain the custom awaiter that posts continuation into the runner
            var task = SomeAsyncOperation(continued, runner);
            
            while (continued.continued == false)
                runner.Step();
            
            // give some frames/time to potentially run (it shouldn't)
            new WaitForSecondsEnumerator(0.2f).Complete();

            Assert.That(task.IsCompleted, Is.False, "Continuation should not run when the runner is stopped");
        }

        async Task SomeAsyncOperation(testClass continued, SteppableRunner runner)
        {
            await Task.Delay(10).RunOn(runner);
            
            continued.continued = true;
            runner.Stop();
            await Task.Delay(10).RunOn(runner);
        }
    }
}

