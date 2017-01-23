using Microsoft.AspNetCore.Http;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Medidata.ZipkinTracer.Core.Test
{
    [TestClass]
    public class TraceProviderTests
    {
        private const string regex128BitPattern = @"^[a-f0-9]{32}$";
        private const string regex64BitPattern = @"^[a-f0-9]{16}$";

        [TestMethod]
        public void Constructor_GeneratingNew64BitTraceId()
        {
            // Arrange
            var config = new ZipkinConfig
            {
                Create128BitTraceId = false
            };

            // Arrange & Act
            var traceProvider = new TraceProvider(config);

            // Assert
            Assert.IsTrue(Regex.IsMatch(traceProvider.TraceId, regex64BitPattern));
            Assert.IsTrue(Regex.IsMatch(traceProvider.SpanId, regex64BitPattern));
            Assert.AreEqual(string.Empty, traceProvider.ParentSpanId);
            Assert.AreEqual(false, traceProvider.IsSampled);
        }

        [TestMethod]
        public void Constructor_GeneratingNew128BitTraceId()
        {
            // Arrange
            var config = new ZipkinConfig
            {
                Create128BitTraceId = true
            };

            // Arrange & Act
            var traceProvider = new TraceProvider(config);

            // Assert
            Assert.IsTrue(Regex.IsMatch(traceProvider.TraceId, regex128BitPattern));
            Assert.IsTrue(Regex.IsMatch(traceProvider.SpanId, regex64BitPattern));
            Assert.AreEqual(string.Empty, traceProvider.ParentSpanId);
            Assert.AreEqual(false, traceProvider.IsSampled);
        }

        [TestMethod]
        public void Constructor_HavingTraceProviderInContext()
        {
            // Arrange
            var context = Substitute.ForPartsOf<HttpContext>();
            var providerInContext = Substitute.For<ITraceProvider>();
            var environment = new Dictionary<object, object>
            {
                { "Medidata.ZipkinTracer.Core.TraceProvider", providerInContext }
            };
            context.Items.Returns(environment);

            // Act
            var sut = new TraceProvider(new ZipkinConfig(), context);

            // Assert
            Assert.AreEqual(providerInContext.TraceId, sut.TraceId);
            Assert.AreEqual(providerInContext.SpanId, sut.SpanId);
            Assert.AreEqual(providerInContext.ParentSpanId, sut.ParentSpanId);
            Assert.AreEqual(providerInContext.IsSampled, sut.IsSampled);
        }

        [TestMethod]
        public void Constructor_AcceptingHeadersWith64BitTraceId()
        {
            // Arrange
            var traceId = Convert.ToString((long)123123123, 16);
            var spanId = Convert.ToString((long)123132123, 16);
            var parentSpanId = Convert.ToString((long)123123, 16);
            var isSampled = false;

            var context = GenerateContext(
                traceId,
                spanId,
                parentSpanId,
                isSampled.ToString());

            // Act
            var sut = new TraceProvider(new ZipkinConfig(), context);

            // Assert
            Assert.AreEqual(traceId, sut.TraceId);
            Assert.AreEqual(spanId, sut.SpanId);
            Assert.AreEqual(parentSpanId, sut.ParentSpanId);
            Assert.AreEqual(isSampled, sut.IsSampled);
        }

        [TestMethod]
        public void Constructor_AcceptingHeadersWithLessThan16HexCharacters()
        {
            // Arrange
            var traceId = Convert.ToString((long)12231, 16).Substring(1);
            var spanId = Convert.ToString((long)123212, 16);
            var parentSpanId = Convert.ToString((long)123213213, 16);
            var isSampled = false;

            var context = GenerateContext(
                traceId,
                spanId,
                parentSpanId,
                isSampled.ToString());

            // Act
            var sut = new TraceProvider(new ZipkinConfig(), context);

            // Assert
            Assert.AreEqual(traceId, sut.TraceId);
            Assert.AreEqual(spanId, sut.SpanId);
            Assert.AreEqual(parentSpanId, sut.ParentSpanId);
            Assert.AreEqual(isSampled, sut.IsSampled);
        }

        [TestMethod]
        public void Constructor_AcceptingHeadersWith128BitTraceId()
        {
            // Arrange
            var traceId = Guid.NewGuid().ToString("N");
            var spanId = Convert.ToString((long)231231, 16);
            var parentSpanId = Convert.ToString((long)123123, 16);
            var isSampled = false;

            var context = GenerateContext(
                traceId,
                spanId,
                parentSpanId,
                isSampled.ToString());

            // Act
            var sut = new TraceProvider(new ZipkinConfig(), context);

            // Assert
            Assert.AreEqual(traceId, sut.TraceId);
            Assert.AreEqual(spanId, sut.SpanId);
            Assert.AreEqual(parentSpanId, sut.ParentSpanId);
            Assert.AreEqual(isSampled, sut.IsSampled);
        }

        [TestMethod]
        public void Constructor_AcceptingHeadersWithOutIsSampled()
        {
            // Arrange
            var traceId = Convert.ToString((long)12123, 16);
            var spanId = Convert.ToString((long)12213, 16);
            var parentSpanId = Convert.ToString((long)213213213, 16);

            var context = Substitute.ForPartsOf<HttpContext>();
            var request = Substitute.ForPartsOf<HttpRequest>();
            var headers = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { TraceProvider.TraceIdHeaderName, new [] { traceId } },
                { TraceProvider.SpanIdHeaderName, new [] { spanId } },
                { TraceProvider.ParentSpanIdHeaderName, new [] { parentSpanId } }
            });
            var environment = new Dictionary<object, object>();

            request.Headers.Returns(headers);
            context.Request.Returns(request);
            context.Items.Returns(environment);

            var expectedIsSampled = false;
            var sampleFilter = Substitute.For<IZipkinConfig>();
            string sampleId = null;
            sampleFilter.ShouldBeSampled(Arg.Is(sampleId), Arg.Any<string>()).Returns(expectedIsSampled);

            // Act
            var sut = new TraceProvider(sampleFilter, context);

            // Assert
            Assert.AreEqual(traceId, sut.TraceId);
            Assert.AreEqual(spanId, sut.SpanId);
            Assert.AreEqual(parentSpanId, sut.ParentSpanId);
            Assert.AreEqual(expectedIsSampled, sut.IsSampled);
        }

        [TestMethod]
        public void Constructor_AcceptingHeadersWithInvalidIdValues()
        {
            // Arrange
            var traceId = Guid.NewGuid().ToString("N").Substring(1);
            var spanId = "spanId";
            var parentSpanId = "parent";
            var isSampled = "sampled";

            var context = GenerateContext(
                traceId,
                spanId,
                parentSpanId,
                isSampled);

            var expectedIsSampled = false;
            var sampleFilter = Substitute.For<IZipkinConfig>();
            sampleFilter.ShouldBeSampled(Arg.Is(isSampled), Arg.Any<string>()).Returns(expectedIsSampled);

            // Act
            var sut = new TraceProvider(sampleFilter, context);

            // Assert
            Assert.AreNotEqual(traceId, sut.TraceId);
            Assert.AreNotEqual(spanId, sut.SpanId);
            Assert.AreEqual(string.Empty, sut.ParentSpanId);
            Assert.AreEqual(expectedIsSampled, sut.IsSampled);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Constructor_AcceptingHeadersWithSpanAndParentSpan()
        {
            // Arrange
            var traceId = Convert.ToString((long)213213, 16);
            var spanId = Convert.ToString((long)21213231, 16);
            var parentSpanId = spanId;
            var isSampled = false;

            var context = GenerateContext(
                traceId,
                spanId,
                parentSpanId,
                isSampled.ToString());

            // Act
            new TraceProvider(new ZipkinConfig(), context);
        }

        [TestMethod]
        public void GetNext()
        {
            // Arrange
            var traceId = Convert.ToString((long)213213, 16);
            var spanId = Convert.ToString((long)2121, 16);
            var parentSpanId = Convert.ToString((long)212112, 16);
            var isSampled = false;

            var context = GenerateContext(
                traceId,
                spanId,
                parentSpanId,
                isSampled.ToString());

            var sut = new TraceProvider(new ZipkinConfig(), context);

            // Act
            var nextTraceProvider = sut.GetNext();

            // Assert
            Assert.AreEqual(sut.TraceId, nextTraceProvider.TraceId);
            Assert.IsTrue(Regex.IsMatch(nextTraceProvider.SpanId, regex64BitPattern));
            Assert.AreEqual(sut.SpanId, nextTraceProvider.ParentSpanId);
            Assert.AreEqual(sut.IsSampled, nextTraceProvider.IsSampled);
        }

        private HttpContext GenerateContext(string traceId, string spanId, string parentSpanId, string isSampled)
        {
            var context = Substitute.ForPartsOf<HttpContext>();
            var request = Substitute.ForPartsOf<HttpRequest>();
            var headers = new HeaderDictionary(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
            {
                { TraceProvider.TraceIdHeaderName, new [] { traceId } },
                { TraceProvider.SpanIdHeaderName, new [] { spanId } },
                { TraceProvider.ParentSpanIdHeaderName, new [] { parentSpanId } },
                { TraceProvider.SampledHeaderName, new [] { isSampled } }
            });
            var environment = new Dictionary<object, object>();

            request.Headers.Returns(headers);
            context.Request.Returns(request);
            context.Items.Returns(environment);

            return context;
        }
    }
}
