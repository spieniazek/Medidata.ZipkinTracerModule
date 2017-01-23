using System;
using System.Collections.Concurrent;
using Medidata.ZipkinTracer.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core.Test
{
    [TestClass]
    public class SpanCollectorTests
    {
        private SpanCollector spanCollector;
        private SpanProcessor spanProcessorStub;
        private ILogger<SpanCollector> logger;

        [TestInitialize]
        public void Init()
        {
            logger = Substitute.For<ILogger<SpanCollector>>();
        }

        [TestMethod]
        public void CTOR_initializesSpanCollector()
        {
            SpanCollector.spanQueue = null;

            spanCollector = new SpanCollector(new Uri("http://localhost"), 0);

            Assert.IsNotNull(SpanCollector.spanQueue);
        }

        [TestMethod]
        public void CTOR_doesntReinitializeSpanCollector()
        {
            var spanQueue = new BlockingCollection<Span>();
            SpanCollector.spanQueue = spanQueue;

            spanCollector = new SpanCollector(new Uri("http://localhost"), 0);

            Assert.IsTrue(ReferenceEquals(SpanCollector.spanQueue, spanQueue));
        }

        [TestMethod]
        public void CollectSpans()
        {
            SetupSpanCollector();

            var testSpanId = "spanId";
            var testTraceId = "traceId";
            var testParentSpanId = "parentSpanId";
            var testName = "name";

            Span span = new Span();
            span.Id = testSpanId;
            span.TraceId = testTraceId;
            span.ParentId = testParentSpanId;
            span.Name = testName;

            spanCollector.Collect(span);

            Assert.AreEqual(1, SpanCollector.spanQueue.Count);

            Span queuedSpan;
            var spanInQueue = SpanCollector.spanQueue.TryTake(out queuedSpan);

            Assert.AreEqual(span, queuedSpan);
        }

        [TestMethod]
        public void StartProcessingSpans()
        {
            SetupSpanCollector();

            spanCollector.Start();

            spanProcessorStub.Received().Start();
            Assert.IsTrue(spanCollector.IsStarted);
        }

        [TestMethod]
        public async Task StopProcessingSpansWithoutStartFirst()
        {
            SetupSpanCollector();

            spanCollector.Stop();

            await spanProcessorStub.DidNotReceive().Stop();
            Assert.IsFalse(spanCollector.IsStarted);
        }

        [TestMethod]
        public async Task StopProcessingSpans()
        {
            SetupSpanCollector();

            spanCollector.Start();
            spanCollector.Stop();

            await spanProcessorStub.Received().Stop();
            Assert.IsFalse(spanCollector.IsStarted);
        }

        private void SetupSpanCollector()
        {
            spanCollector = new SpanCollector(new Uri("http://localhost"), 0);

            SpanCollector.spanQueue = Substitute.For<BlockingCollection<Span>>();
            spanProcessorStub = Substitute.For<SpanProcessor>(
                new Uri("http://localhost"),
                SpanCollector.spanQueue,
                (uint)0);
            spanCollector.spanProcessor = spanProcessorStub;
        }
    }
}
