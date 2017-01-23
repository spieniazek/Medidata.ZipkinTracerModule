using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Threading;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core.Collector.Test
{
    [TestClass]
    public class SpanProcesssorTaskFactoryTests
    {
        private SpanProcessorTaskFactory spanProcessorTaskFactory;
        private CancellationTokenSource cancellationTokenSource;
        private bool taskCalled;
        private ILogger<SpanProcessorTaskFactory> logger;

        [TestInitialize]
        public void Init()
        {
            logger = Substitute.For<ILogger<SpanProcessorTaskFactory>>();
            cancellationTokenSource = new CancellationTokenSource();
            spanProcessorTaskFactory = new SpanProcessorTaskFactory(logger, cancellationTokenSource);
            taskCalled = false;
        }

        [TestMethod]
        public void StopTask()
        {
            Assert.IsFalse(cancellationTokenSource.IsCancellationRequested);

            spanProcessorTaskFactory.StopTask();

            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
        }

        [TestMethod]
        public void IsTaskCancelled()
        {
            Assert.IsFalse(cancellationTokenSource.IsCancellationRequested);
            Assert.IsFalse(spanProcessorTaskFactory.IsTaskCancelled());

            cancellationTokenSource.Cancel();
            Assert.IsTrue(cancellationTokenSource.IsCancellationRequested);
            Assert.IsTrue(spanProcessorTaskFactory.IsTaskCancelled());
        }

        [TestMethod]
        public async Task TaskWrapper()
        {
            Assert.IsFalse(taskCalled);

            await spanProcessorTaskFactory.TaskWrapper(() => Task.Run(() => { taskCalled = true; cancellationTokenSource.Cancel(); }));

            Assert.IsTrue(taskCalled);
        }

        [TestMethod]
        public async Task TaskWrapper_Exception()
        {
            Exception ex = new Exception("Exception!");
            var myTask = new Task(() => { taskCalled = true; throw ex; });
            Assert.IsFalse(taskCalled);

            try
            {
                await spanProcessorTaskFactory.TaskWrapper(() => Task.Run(() => { taskCalled = true; cancellationTokenSource.Cancel(); throw ex; }));
                Assert.IsTrue(taskCalled);
            }
            catch (Exception e)
            {
                Assert.Fail("Expected no expections: {0}", e.GetType());
            }
        }

        [TestMethod]
        public async Task TaskWrapper_NotCalledIfCancelled()
        {
            var myTask = new Task(() => { taskCalled = true; });
            Assert.IsFalse(taskCalled);

            cancellationTokenSource.Cancel();
            await spanProcessorTaskFactory.TaskWrapper(() => Task.Run(() => taskCalled = true));
            Assert.IsFalse(taskCalled);
        }
    }
}
