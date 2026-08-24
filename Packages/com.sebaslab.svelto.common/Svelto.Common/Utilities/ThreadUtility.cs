#if (UNITY_IOS || UNITY_ANDROID || UNITY_WEBGL)
//On mobile all the assumptions in this code are wrong. The code is not going to work as expected
#define MOBILE_SPIN
#endif
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Svelto.Utilities
{
    /// <summary>
    /// Note this code is not that dissimilar to what SpinWait.cs does in .net. I just decided to use my tailored solution instead
    /// </summary>
    public static class ThreadUtility
    {
        public static uint processorNumber => (uint)Environment.ProcessorCount;
        public static string currentThreadName => Thread.CurrentThread.Name;
        public static int currentThreadId => Thread.CurrentThread.ManagedThreadId;
        
        /// <summary>
        /// The main difference between Yield and Sleep(0) is that Yield doesn't allow a switch of context
        /// but allows the core to be given to a thread that is already running on that core. Sleep(0) may cause
        /// a context switch, yielding the core to a thread from ANY process. Thread.Yield only yields
        /// the core to any thread associated with the current core.
        /// Remember that sleep(1) FORCES a context switch instead, while sleep(0) does it only if required.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Yield()
        {
#if MOBILE_SPIN
            Thread.Sleep(0);
#else            
            Thread.Yield();
#endif
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void TakeItEasy()
        {
            Thread.Sleep(1);
        }

        //In C#, Thread.Sleep(0) triggers the thread scheduler to switch the context. Basically,
        //it hints the scheduler to check if there's another thread ready to run.
        //If none, the current thread continues execution. It's like saying, “Hey, anyone else need the CPU?
        //No? Okay, I'll keep going.” Useful for giving other threads a chance to execute without pausing the
        //current thread's execution.
        //Sleep(1) instead definitely causes a context switch.
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Relax()
        {
            Thread.Sleep(0);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Spin()
        {
#if MOBILE_SPIN
            Thread.Yield();
#else            
            Thread.SpinWait(4);
#endif
        }

        /// <summary>
        /// Yield the thread every so often. Remember, I don't do Spin because
        /// Spin is the equivalent of while(); and I don't see the point of it in most
        /// of the expected scenarios. I don't do Sleep(0) because it can still
        /// cause a context switch;
        /// </summary>
        /// <param name="quickIterations">will be incremented by 1</param>
        /// <param name="powerOf2Frequency">must be power of 2</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Wait(ref int quickIterations, int powerOf2Frequency = 256)
        {
            if ((quickIterations++ & (powerOf2Frequency - 1)) == 0)
                Yield();
            else
                Spin();
        }
        
        //note because I just realised this: when you use ticks with DateTime they are already converted to follow the .net contract
        //that 1ms = 10000 ticks. However, when you use stopwatch ticks this conversion is not done and number of ticks per ms can change
        //between machines. using StopWatch.Elapsed.Ticks just remove any kind of doubt (as the conversion happens in the TimeSpan)  
        static readonly long _16MSInTicks = 160_000;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LongestWaitLeft(long timeToSleepInTicks, ref int quickIterations, in Stopwatch watch, int powerOf2Frequency = 256)
        {
            var elapsedTicks = watch.Elapsed.Ticks;
            if (timeToSleepInTicks - elapsedTicks <= _16MSInTicks)
            {
                if ((quickIterations++ & (powerOf2Frequency - 1)) == 0) 
                    Yield();
                else
                    Spin();                
            }
            else
                TakeItEasy();
        }

        /// DO NOT TOUCH THE NUMBERS, THEY ARE THE BEST BALANCE BETWEEN CPU OCCUPATION AND RESUME SPEED
        /// DO NOT ADD THREAD.SLEEP(1) it KILLS THE RESUME
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LongWaitLeft(long timeToSleepInTicks, ref int quickIterations, in Stopwatch watch, int powerOf2Frequency = 256)
        {
            var elapsedTicks = watch.Elapsed.Ticks;
            if (timeToSleepInTicks - elapsedTicks <= _16MSInTicks)
            {
                if ((quickIterations++ & (powerOf2Frequency - 1)) == 0) //spinwait, yield every so often
                    Yield();
                else
                    Spin();
            }
            else
            {
                if ((quickIterations++ & ((powerOf2Frequency << 3) - 1)) == 0) //yield, Sleep(0) every so often
                    Relax();
                else
                    Yield();
            }
        }
        
        /// DO NOT TOUCH THE NUMBERS, THEY ARE THE BEST BALANCE BETWEEN CPU OCCUPATION AND RESUME SPEED
        /// DO NOT ADD THREAD.SLEEP(1) it KILLS THE RESUME
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void LongWait(ref int quickIterations, in Stopwatch watch, int frequency = 256)
        {
            var elapsedTicks = watch.Elapsed.Ticks;
            if (elapsedTicks < _16MSInTicks)
            {
                if ((quickIterations++ & (frequency - 1)) == 0)
                    Yield();
                else
                    Spin();
            }
            else
            {
                if ((quickIterations++ & ((frequency << 3) - 1)) == 0)
                    Relax();
                else
                    Yield();
            }
        }
        
        public static void SleepWithOneEyeOpen(float waitTimeMs, in Stopwatch stopwatch, SyncStrategy strategy, int yieldFrequency = 64)
        {
            stopwatch.Restart();
            
            if (waitTimeMs <= 0f) // nothing to wait
                return;
            
#if MOBILE_SPIN || UNITY_STANDALONE_LINUX
            //On Mobile any small amount of spin wait can cause throttling, however it seems that SpinWait is able to cope with it
            //NEVER NEVER USE SPINNING ON MOBILE, SPIN WAIT IS FORBIDDEN!!!
            //Sleep is anyway precise enough on mobile (different schedulers)
            
            Thread.Sleep((int)waitTimeMs);
#else
            int quickIterations = 0;
            var timeInTicks = TimeSpan.FromMilliseconds(waitTimeMs).Ticks;
            
            var elapsedTicks = stopwatch.Elapsed.Ticks;
            switch (strategy)
            {
                case SyncStrategy.Balanced:
                    while (elapsedTicks < timeInTicks)
                    {
                        LongestWaitLeft(timeInTicks, ref quickIterations, stopwatch, yieldFrequency);
                        elapsedTicks = stopwatch.Elapsed.Ticks;
                    }

                    break;
                case SyncStrategy.SpinAggressive:
                    while (elapsedTicks < timeInTicks)
                    {
                        LongWaitLeft(timeInTicks, ref quickIterations, stopwatch, yieldFrequency);
                        elapsedTicks = stopwatch.Elapsed.Ticks;
                    }

                    break;
            }
#endif
        }
    }

    public enum SyncStrategy
    {
        Balanced,
        SpinAggressive
    }
}