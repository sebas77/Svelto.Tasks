using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Svelto.Utilities;

namespace Svelto.Tasks
{
   namespace Lean
    {
        public class SyncRunner : SteppableRunner
        {
            public SyncRunner(string name) : base(name)   { }
        }
        
        public class SyncRunner<T> : SteppableRunner<T> where T : struct, IEnumerator<TaskContract>
        {
            public SyncRunner(string name) : base(name) { }
        }
        
        public static class LocalSyncRunners 
        {
            public static  ThreadLocal<SyncRunner> syncRunner  { get; private set; }
        
            static LocalSyncRunners() 
            {
                Reset();
            }

            public static void Reset()
            {
                syncRunner = new ThreadLocal<SyncRunner>(() => new SyncRunner(ThreadUtility.currentThreadName + "Lean SyncRunner"));
            }
        }
        
        public static class LocalSyncRunners<T> where T : struct, IEnumerator<TaskContract>
        {
            public static  ThreadLocal<SyncRunner<T>> syncRunner  { get; private set; }
        
            static LocalSyncRunners() 
            {
                Reset();
            }

            public static void Reset()
            {
                syncRunner = new ThreadLocal<SyncRunner<T>>(() => new SyncRunner<T>(ThreadUtility.currentThreadName + $"Lean SyncRunner {typeof(T).Name}"));
            }
        }
    }
   
    namespace ExtraLean
    {
        public class SyncRunner : SteppableRunner
        {
            public SyncRunner(string name) : base(name)   { }
        }
        
        public class SyncRunner<T> : SteppableRunner<T> where T : struct, IEnumerator
        {
            public SyncRunner(string name) : base(name) { }
        }
        
        public static class LocalSyncRunners 
        {
            public static  ThreadLocal<SyncRunner> syncRunner  { get; private set; }
        
            static LocalSyncRunners() 
            {
                Reset();
            }

            public static void Reset()
            {
                syncRunner = new ThreadLocal<SyncRunner>(() => new SyncRunner(ThreadUtility.currentThreadName + " Extra lean SyncRunner"));
            }
        }
        
        public static class LocalSyncRunners<T> where T : struct, IEnumerator
        {
            public static  ThreadLocal<SyncRunner<T>> syncRunner  { get; private set; }
        
            static LocalSyncRunners() 
            {
                Reset();
            }

            public static void Reset()
            {
                syncRunner = new ThreadLocal<SyncRunner<T>>(() => new SyncRunner<T>(ThreadUtility.currentThreadName + $" Extra lean SyncRunner {typeof(T).Name}"));
            }
        }
    }
}