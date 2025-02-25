using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Svelto.Utilities;

namespace Svelto.Tasks
{
    static internal class SharedCode
    {
        public static void Complete(ISteppableRunner syncRunner, int timeout)
        {
            var quickIterations = 0;

            if (timeout > 0)
            {
                var then  = DateTime.Now.AddMilliseconds(timeout);
                var valid = true;

                syncRunner.Step();

                while (syncRunner.hasTasks && (valid = DateTime.Now < then))
                {
                    ThreadUtility.Wait(ref quickIterations);
                    syncRunner.Step();
                }

                if (valid == false)
                    throw new SveltoTaskException("synchronous task timed out, increase time out or check if it got stuck");
            }
            else
            {
                if (timeout == 0)
                    while (syncRunner.hasTasks)
                    {
                        syncRunner.Step();
                        ThreadUtility.Wait(ref quickIterations);
                    }
                else
                    while (syncRunner.hasTasks)
                    {
                        syncRunner.Step();
                    }
            }
        }
    }

    namespace Lean
    {
        public class SyncRunner : SteppableRunner
        {
            public SyncRunner(string name) : base(name)   { }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ForceComplete(int timeout)
            {
                SharedCode.Complete(this, timeout);
            }
        }
        
        public class SyncRunner<T> : SteppableRunner<T> where T : IEnumerator<TaskContract>
        {
            public SyncRunner(string name) : base(name) { }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void ForceComplete(int timeout)
            {
                SharedCode.Complete(this, timeout);
            }
        }
        
        public static class LocalSyncRunners<T> where T : IEnumerator<TaskContract>
        {
            public static  ThreadLocal<SyncRunner<T>> syncRunner  { get; private set; }
        
            static LocalSyncRunners() 
            {
                Reset();
            }

            public static void Reset()
            {
                syncRunner = new ThreadLocal<SyncRunner<T>>(() => new SyncRunner<T>(ThreadUtility.currentThreadName + " SyncRunner"));
            }
        }
    }
}