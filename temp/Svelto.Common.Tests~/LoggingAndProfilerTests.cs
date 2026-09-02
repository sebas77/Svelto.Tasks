using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Svelto.Common;
using Svelto.Utilities;
using SveltoConsole = Svelto.Console;

namespace Svelto.Common.Tests
{
    [TestFixture]
    [NonParallelizable]
    public class LoggingAndProfilerTests
    {
        sealed class CapturingLogger : ILogger
        {
            public readonly List<(string text, LogType type, Exception exception)> entries = new();
            public int added;
            public string compressedName;

            public void Log(string txt, LogType type = LogType.Log, bool showLogStack = true,
                Exception e = null, Dictionary<string, string> data = null)
            {
                entries.Add((txt, type, e));
            }

            public void OnLoggerAdded() => added++;
            public void CompressLogsToZipAndShow(string zipName) => compressedName = zipName;
        }

        [Test]
        public void Console_RoutesMessagesEventsAndCompressionToRegisteredLogger()
        {
            var logger = new CapturingLogger();
            var duplicate = new CapturingLogger();
            var eventEntries = new List<(string text, LogType type)>();
            Exception observedException = null;
            string observedExceptionText = null;
            void OnLog(string text, LogType type, Exception exception) => eventEntries.Add((text, type));
            void OnException(Exception exception, string text)
            {
                observedException = exception;
                observedExceptionText = text;
            }

            SveltoConsole.AddLogger(logger);
            SveltoConsole.AddLogger(duplicate);
            SveltoConsole.logMessage += OnLog;
            SveltoConsole.onException += OnException;
            try
            {
                SveltoConsole.Log("log");
                SveltoConsole.LogWarning("warning");
                SveltoConsole.LogError("error", new Dictionary<string, string> { ["id"] = "7" });
                var exception = new InvalidOperationException("failure");
                SveltoConsole.LogException(exception, "context");
                SveltoConsole.CompressLogsToZipAndShow("logs.zip");

                Assert.That(logger.added, Is.EqualTo(1));
                Assert.That(duplicate.added, Is.Zero);
                Assert.That(logger.entries, Has.Count.EqualTo(4));
                Assert.That(logger.entries[0].text, Is.EqualTo("log"));
                Assert.That(logger.entries[0].type, Is.EqualTo(LogType.Log));
                Assert.That(logger.entries[0].exception, Is.Null);
                Assert.That(logger.entries[1].text, Does.StartWith("------> warning"));
                Assert.That(logger.entries[2].text, Does.StartWith("-!!!!!!-> error"));
                Assert.That(logger.entries[3].text, Does.Contain("failure -- context"));
                Assert.That(eventEntries, Has.Count.EqualTo(4));
                Assert.That(observedException, Is.SameAs(exception));
                Assert.That(observedExceptionText, Does.Contain("failure"));
                Assert.That(logger.compressedName, Is.EqualTo("logs.zip"));
            }
            finally
            {
                SveltoConsole.logMessage -= OnLog;
                SveltoConsole.onException -= OnException;
            }
        }

        [Test]
        public void SimpleLogger_FormatsDetailedErrorsToSystemConsole()
        {
            var previous = System.Console.Out;
            using var output = new StringWriter();
            System.Console.SetOut(output);
            try
            {
                var logger = new SimpleLogger();
                logger.Log("failure", LogType.Error, e: new InvalidOperationException("reason"),
                    data: new Dictionary<string, string> { ["id"] = "7" });
                logger.OnLoggerAdded();
                logger.CompressLogsToZipAndShow("ignored.zip");

                Assert.That(output.ToString(), Does.Contain("failure"));
                Assert.That(output.ToString(), Does.Contain("\"id\":\"7\""));
            }
            finally
            {
                System.Console.SetOut(previous);
            }
        }

        [Test]
        public void PlatformProfiler_NoOpImplementationSupportsCompleteLifecycle()
        {
            var profiler = new PlatformProfiler("test");
            var mtProfiler = new PlatformProfilerMT("test");

            profiler.Sample().Dispose();
            profiler.Sample("sample").Yield().Dispose();
            profiler.Sample(42).Dispose();
            profiler.Yield().Dispose();
            PlatformProfiler.PreCreate("test").Sample().Dispose();
            mtProfiler.Sample("sample").Dispose();
            mtProfiler.Sample(42).Dispose();
        }

        [Test]
        public void StandardProfiler_SamplerHolderReportsElapsedTimeBeforeAndAfterDispose()
        {
            var profiler = new StandardProfiler("test");
            var sampler = profiler.Sample();

            Assert.That(sampler.ElapsedMs, Is.GreaterThanOrEqualTo(0));
            Assert.That(sampler.ElapsedNano, Is.GreaterThanOrEqualTo(0));

            sampler.Dispose();

            Assert.That(sampler.ElapsedMs, Is.GreaterThanOrEqualTo(0));
            Assert.That(sampler.ElapsedNano, Is.GreaterThanOrEqualTo(0));
        }

        [Test]
        public void StandardProfiler_SampleAndLogPublishesResult()
        {
            string observed = null;
            void OnLog(string text, LogType type, Exception exception) => observed = text;
            SveltoConsole.logMessage += OnLog;
            try
            {
                var sampler = new StandardProfiler("profiler").SampleAndLog("sample");
                sampler.Dispose();

                Assert.That(observed, Does.StartWith("profiler -> sample -> "));
                Assert.That(observed, Does.EndWith(" ms"));
            }
            finally
            {
                SveltoConsole.logMessage -= OnLog;
            }
        }

        [Test]
        public void ThreadUtility_ExposesThreadMetadataAndWaitOperations()
        {
            Assert.That(ThreadUtility.processorNumber, Is.GreaterThan(0));
            Assert.That(ThreadUtility.currentThreadId, Is.EqualTo(Environment.CurrentManagedThreadId));

            var iterations = 0;
            ThreadUtility.Wait(ref iterations, 2);
            ThreadUtility.Wait(ref iterations, 2);
            Assert.That(iterations, Is.EqualTo(2));

            var watch = Stopwatch.StartNew();
            ThreadUtility.LongestWaitLeft(0, ref iterations, watch, 2);
            ThreadUtility.LongWaitLeft(0, ref iterations, watch, 2);
            ThreadUtility.LongWait(ref iterations, watch, 2);
            ThreadUtility.SleepWithOneEyeOpen(0, watch, SyncStrategy.Balanced);
            ThreadUtility.SleepWithOneEyeOpen(0, watch, SyncStrategy.SpinAggressive);
        }
    }
}
