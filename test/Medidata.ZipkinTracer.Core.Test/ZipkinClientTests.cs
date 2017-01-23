using System;
using System.Collections.Generic;
using System.Linq;
using Medidata.ZipkinTracer.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.Http;
using NSubstitute;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Medidata.ZipkinTracer.Core.Test
{
    [TestClass]
    public class ZipkinClientTests
    {
        private SpanCollector spanCollectorStub;
        private SpanTracer spanTracerStub;
        private ITraceProvider traceProvider;
        private HttpContext httpContext;
        private Dictionary<string, Microsoft.Extensions.Primitives.StringValues> headers;

        [TestInitialize]
        public void Init()
        {
            traceProvider = Substitute.For<ITraceProvider>();
            httpContext = Substitute.ForPartsOf<HttpContext>();
            httpContext.Items.Returns(new Dictionary<object, object>());
            var request = Substitute.ForPartsOf<HttpRequest>();
            httpContext.Request.Returns(request);
            headers = new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>();
            request.Headers.Returns(new HeaderDictionary(headers));
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullConfig()
        {
            new ZipkinClient(null, httpContext);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CTOR_WithNullContext()
        {
            new ZipkinClient(new ZipkinConfig(), null);
        }

        [TestMethod]
        public void CTOR_WithNullCollector_create_default_collector()
        {
            var zipkinConfigStub = CreateZipkinConfigWithDefaultValues(sampleRate: 1);

            var client = new ZipkinClient(zipkinConfigStub, httpContext, null);

            Assert.IsNotNull(client.spanCollector);
        }

        [TestMethod]
        public void multiple_Client_WithNullCTORCollector_share_same_collector()
        {
            var zipkinConfigStub = CreateZipkinConfigWithDefaultValues(sampleRate: 1);

            var client1 = new ZipkinClient(zipkinConfigStub, httpContext, null);
            var client2 = new ZipkinClient(zipkinConfigStub, httpContext, null);

            Assert.IsNotNull(client1.spanCollector);
            Assert.ReferenceEquals(client1.spanCollector, client2.spanCollector);
        }

        [TestMethod]
        public void CTOR_WithTraceIdNullOrEmpty()
        {
            var zipkinConfigStub = CreateZipkinConfigWithDefaultValues();

            AddTraceId(string.Empty);
            AddSampled(false);

            spanCollectorStub = Substitute.For<SpanCollector>(new Uri("http://localhost"), (uint)0);
            var zipkinClient = new ZipkinClient(zipkinConfigStub, httpContext, spanCollectorStub);
            Assert.IsFalse(zipkinClient.IsTraceOn);
        }

        [TestMethod]
        public void CTOR_WithIsSampledFalse()
        {
            var zipkinConfigStub = CreateZipkinConfigWithDefaultValues();

            AddTraceId("traceId");
            AddSampled(false);

            spanCollectorStub = Substitute.For<SpanCollector>(new Uri("http://localhost"), (uint)0);
            var zipkinClient = new ZipkinClient(zipkinConfigStub, httpContext, spanCollectorStub);
            Assert.IsFalse(zipkinClient.IsTraceOn);
        }

        [TestMethod]
        public void CTOR_StartCollector()
        {
            var zipkinClient = (ZipkinClient)SetupZipkinClient();
            Assert.IsNotNull(zipkinClient.spanCollector);
            Assert.IsNotNull(zipkinClient.spanTracer);
        }

        [TestMethod]
        public void Shutdown_StopCollector()
        {
            var zipkinClient = (ZipkinClient)SetupZipkinClient();

            zipkinClient.ShutDown();

            //assert
            spanCollectorStub.Received().Stop();
        }

        [TestMethod]
        public void Shutdown_CollectorNullDoesntThrow()
        {
            var zipkinClient = (ZipkinClient)SetupZipkinClient();
            zipkinClient.spanCollector = null;

            zipkinClient.ShutDown();
        }

        [TestMethod]
        public async Task StartServerSpan()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var uriHost = "https://www.x@y.com";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;
            var requestUri = new Uri(uriHost + uriAbsolutePath);

            var expectedSpan = new Span();
            spanTracerStub.ReceiveServerSpan(
                Arg.Is(spanName.ToLower()),
                Arg.Is(traceProvider.TraceId),
                Arg.Is(traceProvider.ParentSpanId),
                Arg.Is(traceProvider.SpanId),
                Arg.Is(requestUri))
                .Returns(expectedSpan);

            var result = await tracerClient.StartServerTrace(requestUri, methodName);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public async Task StartServerSpan_Exception()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var uriHost = "https://www.x@y.com";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;
            var requestUri = new Uri(uriHost + uriAbsolutePath);

            spanTracerStub.When(x => x.ReceiveServerSpan(
                Arg.Is(spanName.ToLower()),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Is(requestUri)).IgnoreAwait()).Do(y => { throw new Exception(); });

            var result = await tracerClient.StartServerTrace(requestUri, methodName);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task StartServerSpan_IsTraceOnIsFalse()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;
            var uriHost = "https://www.x@y.com";
            var uriAbsolutePath = "/object";
            var methodName = "GET";

            var result = await tracerClient.StartServerTrace(new Uri(uriHost + uriAbsolutePath), methodName);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void EndServerSpan()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var serverSpan = new Span();

            tracerClient.EndServerTrace(serverSpan);

            //assert
            spanTracerStub.Received().SendServerSpan(Arg.Is(serverSpan));
        }

        [TestMethod]
        public void EndServerSpan_Exception()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var serverSpan = new Span();

            spanTracerStub.When(x => x.SendServerSpan(Arg.Is(serverSpan))).Do(y => { throw new Exception(); });

            tracerClient.EndServerTrace(serverSpan);
        }

        [TestMethod]
        public void EndServerSpan_IsTraceOnIsFalse_DoesntThrow()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;
            var serverSpan = new Span();

            tracerClient.EndServerTrace(serverSpan);
        }

        [TestMethod]
        public void EndServerSpan_NullServerSpan_DoesntThrow()
        {
            var tracerClient = SetupZipkinClient();

            tracerClient.EndServerTrace(null);
        }

        [TestMethod]
        public async Task StartClientSpan()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "abc-sandbox";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            var expectedSpan = new Span();
            spanTracerStub.SendClientSpan(
                Arg.Is(spanName.ToLower()),
                Arg.Is(traceProvider.TraceId),
                Arg.Is(traceProvider.ParentSpanId),
                Arg.Is(traceProvider.SpanId),
                Arg.Any<Uri>()).Returns(expectedSpan);

            var result = await tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public async Task StartClientSpan_UsingIpAddress()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "192.168.178.178";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            var expectedSpan = new Span();

            spanTracerStub.SendClientSpan(
                Arg.Is(spanName.ToLower()),
                Arg.Is(traceProvider.TraceId),
                Arg.Is(traceProvider.ParentSpanId),
                Arg.Is(traceProvider.SpanId),
                Arg.Any<Uri>()).Returns(expectedSpan);

            var result = await tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public async Task StartClientSpan_MultipleDomainList()
        {
            var zipkinConfig = CreateZipkinConfigWithDefaultValues();
            zipkinConfig.NotToBeDisplayedDomainList = new List<string> { ".abc.net", ".xyz.net" };
            var tracerClient = SetupZipkinClient(zipkinConfig);
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "abc-sandbox";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            var expectedSpan = new Span();
            spanTracerStub.SendClientSpan(
                Arg.Is(spanName.ToLower()),
                Arg.Is(traceProvider.TraceId),
                Arg.Is(traceProvider.ParentSpanId),
                Arg.Is(traceProvider.SpanId),
                Arg.Any<Uri>()).Returns(expectedSpan);

            var result = await tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.AreEqual(expectedSpan, result);
        }

        [TestMethod]
        public async Task StartClientSpan_Exception()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientServiceName = "abc-sandbox";
            var uriAbsolutePath = "/object";
            var methodName = "GET";
            var spanName = methodName;

            spanTracerStub.When(x => x.SendClientSpan(
                Arg.Is(spanName.ToLower()),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Uri>()).IgnoreAwait()).Do(y => { throw new Exception(); });

            var result = await tracerClient.StartClientTrace(new Uri("https://" + clientServiceName + ".xyz.net:8000" + uriAbsolutePath), methodName, traceProvider);

            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task StartClientSpan_IsTraceOnIsFalse()
        {
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;
            var clientServiceName = "abc-sandbox";
            var clientServiceUri = new Uri("https://" + clientServiceName + ".xyz.net:8000");
            var methodName = "GET";

            var result = await tracerClient.StartClientTrace(clientServiceUri, methodName, traceProvider);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void EndClientSpan()
        {
            short returnCode = 123;
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientSpan = new Span();

            tracerClient.EndClientTrace(clientSpan, returnCode);

            //assert
            spanTracerStub.Received().ReceiveClientSpan(clientSpan, returnCode);
        }

        [TestMethod]
        public void EndClientSpan_Exception()
        {
            short returnCode = 123;
            var tracerClient = SetupZipkinClient();
            var zipkinClient = (ZipkinClient)tracerClient;
            spanTracerStub = GetSpanTracerStub();
            zipkinClient.spanTracer = spanTracerStub;
            var clientSpan = new Span();

            spanTracerStub.When(x => x.ReceiveClientSpan(Arg.Is(clientSpan), Arg.Is(returnCode))).Do(y => { throw new Exception(); });

            tracerClient.EndClientTrace(clientSpan, returnCode);
        }

        [TestMethod]
        public void EndClientSpan_NullClientTrace_DoesntThrow()
        {
            short returnCode = 123;
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();

            var called = false;
            spanTracerStub.When(x => x.ReceiveClientSpan(Arg.Any<Span>(), Arg.Is(returnCode))).Do(y => called = true);

            tracerClient.EndClientTrace(null, returnCode);

            Assert.IsFalse(called);
        }

        [TestMethod]
        public void EndClientSpan_IsTraceOnIsFalse_DoesntThrow()
        {
            short returnCode = 123;
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var called = false;
            spanTracerStub.When(x => x.ReceiveClientSpan(Arg.Any<Span>(), Arg.Is(returnCode))).Do(y => called = true);

            tracerClient.EndClientTrace(new Span(), returnCode);

            Assert.IsFalse(called);
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public async Task Record_IsTraceOnIsFalse_DoesNotAddAnnotation()
        {
            // Arrange
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var testSpan = new Span();

            // Act
            await tracerClient.Record(testSpan, "irrelevant");

            // Assert
            Assert.IsFalse(testSpan.Annotations.Any(), "There are annotations but the trace is off.");
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public async Task Record_WithoutValue_AddsAnnotationWithCallerName()
        {
            // Arrange
            var callerMemberName = GetCallerName();
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = true;

            var testSpan = new Span();

            // Act
            await tracerClient.Record(testSpan);

            // Assert
            Assert.AreEqual(1, testSpan.Annotations.Count, "There is not exactly one annotation added.");
            Assert.IsNotNull(
                testSpan.GetAnnotationsByType<Annotation>().SingleOrDefault(a => (string)a.Value == callerMemberName),
                "The record with the caller name is not found in the Annotations."
            );
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public void RecordBinary_IsTraceOnIsFalse_DoesNotAddBinaryAnnotation()
        {
            // Arrange
            var keyName = "TestKey";
            var testValue = "Some Value";
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var testSpan = new Span();

            // Act
            tracerClient.RecordBinary(testSpan, keyName, testValue);

            // Assert
            Assert.IsFalse(testSpan.GetAnnotationsByType<Annotation>().Any(), "There are annotations but the trace is off.");
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public async Task RecordLocalComponent_WithNotNullValue_AddsLocalComponentAnnotation()
        {
            // Arrange
            var testValue = "Some Value";
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = true;

            var testSpan = new Span();

            // Act
            await tracerClient.RecordLocalComponent(testSpan, testValue);

            // Assert
            var annotation = testSpan.GetAnnotationsByType<BinaryAnnotation>().SingleOrDefault(a => a.Key == ZipkinConstants.LocalComponent);
            Assert.IsNotNull(annotation, "There is no local trace annotation in the binary annotations.");
            Assert.AreEqual(testValue, annotation.Value, "The local component annotation value is not correct.");
        }

        [TestMethod]
        [TestCategory("TraceRecordTests")]
        public void RecordLocalComponent_IsTraceOnIsFalse_DoesNotAddLocalComponentAnnotation()
        {
            // Arrange
            var testValue = "Some Value";
            var tracerClient = SetupZipkinClient();
            spanTracerStub = GetSpanTracerStub();
            var zipkinClient = (ZipkinClient)tracerClient;
            zipkinClient.IsTraceOn = false;

            var testSpan = new Span();

            // Act
            tracerClient.RecordBinary(testSpan, ZipkinConstants.LocalComponent, testValue);

            // Assert
            Assert.IsFalse(testSpan.GetAnnotationsByType<BinaryAnnotation>().Any(), "There are annotations but the trace is off.");
        }

        private ITracerClient SetupZipkinClient(IZipkinConfig zipkinConfig = null)
        {
            spanCollectorStub = Substitute.For<SpanCollector>(new Uri("http://localhost"), (uint)0);

            traceProvider.TraceId.Returns("traceId");
            traceProvider.SpanId.Returns("spanId");
            traceProvider.ParentSpanId.Returns("parentSpanId");
            traceProvider.IsSampled.Returns(true);

            var context = Substitute.ForPartsOf<HttpContext>();
            var request = Substitute.ForPartsOf<HttpRequest>();
            context.Request.Returns(request);
            context.Items.Returns(new Dictionary<object, object> { { TraceProvider.Key, traceProvider } });

            IZipkinConfig zipkinConfigSetup = zipkinConfig;
            if (zipkinConfig == null)
            {
                zipkinConfigSetup = CreateZipkinConfigWithDefaultValues();
            }

            return new ZipkinClient(zipkinConfigSetup, context, spanCollectorStub);
        }

        static readonly char[] separators = new[] { ',', ';' };
        static readonly Func<string, IList<string>> SplitFunc = s => s.Split(separators).Select(e => e.Trim()).ToList();

        private IZipkinConfig CreateZipkinConfigWithDefaultValues(string uriSt = "http://zipkin.com", string domainSt = "http://server.com",
            uint spanProcessorBatchSize = 123, string excludedPathList = "/foo, /bar, /baz", double sampleRate = 0.5, string notToBeDisplayedDomainList = ".xyz.net")
        {
            return new ZipkinConfig
            {
                ZipkinBaseUri = new Uri(uriSt),
                Domain = r => new Uri(domainSt),
                SpanProcessorBatchSize = spanProcessorBatchSize,
                ExcludedPathList = SplitFunc(excludedPathList),
                SampleRate = sampleRate,
                NotToBeDisplayedDomainList = SplitFunc(notToBeDisplayedDomainList),
            };
        }

        private SpanTracer GetSpanTracerStub()
        {
            return Substitute.For<SpanTracer>(
                spanCollectorStub,
                Substitute.For<ServiceEndpoint>(),
                new List<string>(),
                new Uri("http://server.com"));
        }

        private void AddTraceId(string traceId)
        {
            headers.Add(TraceProvider.TraceIdHeaderName, new[] { traceId });
        }

        private void AddSampled(bool sampled)
        {
            headers.Add(TraceProvider.SampledHeaderName, new[] { sampled.ToString() });
        }

        private string GetCallerName([CallerMemberName] string name = null)
        {
            return name;
        }
    }
}
