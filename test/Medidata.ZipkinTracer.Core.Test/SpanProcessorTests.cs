using System;
using System.Collections.Concurrent;
using Medidata.ZipkinTracer.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Threading.Tasks;
using Medidata.ZipkinTracer.Core.Test;

namespace Medidata.ZipkinTracer.Core.Collector.Test
{
    [TestClass]
    public class SpanProcessorTests
    {
        private SpanProcessor spanProcessor;
        private SpanProcessorTaskFactory taskFactory;
        private BlockingCollection<Span> queue;
        private uint testMaxBatchSize;
        private ILogger<SpanProcessorTaskFactory> logger;

        [TestInitialize]
        public void Init()
        {
            logger = Substitute.For<ILogger<SpanProcessorTaskFactory>>();
            queue = new BlockingCollection<Span>();
            testMaxBatchSize = 10;
            spanProcessor = Substitute.ForPartsOf<SpanProcessor>(new Uri("http://localhost"), queue, testMaxBatchSize);
            spanProcessor.WhenForAnyArgs(x => x.SendSpansToZipkin(Arg.Any<string>()).IgnoreAwait()).Do(s => { });
            taskFactory = Substitute.For<SpanProcessorTaskFactory>(logger, null);
            spanProcessor.spanProcessorTaskFactory = taskFactory;
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullSpanQueue()
        {
            new SpanProcessor(new Uri("http://localhost"), null, 10);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullZipkinServer()
        {
            new SpanProcessor(null, queue, 10);
        }

        [TestMethod]
        public void Start()
        {
            taskFactory.CreateAndStart(Arg.Is<Func<Task>>(y => ValidateStartTask(y(), spanProcessor)));

            spanProcessor.Start();
        }

        [TestMethod]
        public async Task Stop()
        {
            await spanProcessor.Stop();

            //assert
            taskFactory.Received().StopTask();
        }

        [TestMethod]
        public async Task Stop_RemainingGetLoggedIfCancelled()
        {
            spanProcessor.When(x => x.SendSpansToZipkin(Arg.Any<string>()).IgnoreAwait()).DoNotCallBase();
            taskFactory.IsTaskCancelled().Returns(true);

            spanProcessor.spanQueue.Add(GenerateNewSpan());
            await spanProcessor.Stop();

            //assert
            await spanProcessor.Received().SendSpansToZipkin(Arg.Any<string>());
        }

        [TestMethod]
        public async Task LogSubmittedSpans_DoNotIncrementSubsequentPollCountIfSpanQueueIsEmpty()
        {
            await spanProcessor.LogSubmittedSpans();
            Assert.AreEqual(0, spanProcessor.subsequentPollCount);
        }

        [TestMethod]
        public async Task LogSubmittedSpans_IncrementSubsequentPollCountIfSpanQueueHasAnItemLessThanMax()
        {
            //put item in queue
            spanProcessor.spanQueue.Add(GenerateNewSpan());
            await spanProcessor.LogSubmittedSpans();

            //Proces Log with no new items
            await spanProcessor.LogSubmittedSpans();

            //Subsquent count has incremented
            Assert.AreEqual(1, spanProcessor.subsequentPollCount);
        }

        [TestMethod]
        public async Task LogSubmittedSpans_WhenQueueIsSubsequentlyLessThanTheMaxBatchCountMaxTimes()
        {
            spanProcessor.spanQueue.Add(GenerateNewSpan());
            await spanProcessor.LogSubmittedSpans();
            spanProcessor.subsequentPollCount = SpanProcessor.MAX_NUMBER_OF_POLLS + 1;
            await spanProcessor.LogSubmittedSpans();

            //assert
            await spanProcessor.Received().SendSpansToZipkin(Arg.Any<string>());
        }

        [TestMethod]
        public async Task LogSubmittedSpans_WhenLogEntriesReachMaxBatchSize()
        {
            AddLogEntriesToMaxBatchSize();
            await spanProcessor.LogSubmittedSpans();

            //assert
            await spanProcessor.Received().SendSpansToZipkin(Arg.Any<string>());
        }

        private bool ValidateStartTask(Task y, SpanProcessor spanProcessor)
        {
            Assert.AreEqual(spanProcessor.LogSubmittedSpans(), y);
            return true;
        }

        private void AddLogEntriesToMaxBatchSize()
        {
            for (int i = 0; i < testMaxBatchSize + 1; i++)
            {
                spanProcessor.spanQueue.Add(GenerateNewSpan());
            }
        }

        private Span GenerateNewSpan()
        {
            return new Span
            {
                Id = "id",
                Name = "name",
                ParentId = "parentId",
                TraceId = "traceId",
            };
        }
    }
}
