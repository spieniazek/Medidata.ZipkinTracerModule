using System;
using System.Runtime.CompilerServices;
using Medidata.ZipkinTracer.Models;
using Medidata.ZipkinTracer.Core.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core
{
    public class ZipkinClient: ITracerClient
    {
        internal SpanCollector spanCollector;
        internal SpanTracer spanTracer;

        private ILogger<ZipkinClient> logger;

        public bool IsTraceOn { get; set; }

        public ITraceProvider TraceProvider { get; }

        public IZipkinConfig ZipkinConfig { get; }

        private static SpanCollector instance;
        private static readonly object syncObj = new object();

        static SpanCollector GetInstance(Uri uri, uint maxProcessorBatchSize)
        {
            SyncHelper.ExecuteSafely(syncObj, () => instance == null,
                () =>
                    {
                        instance = new SpanCollector(uri, maxProcessorBatchSize);
                    });

            return instance;
        }

        public ZipkinClient(IZipkinConfig zipkinConfig, HttpContext context, SpanCollector collector = null)
        {
            if (zipkinConfig == null) throw new ArgumentNullException(nameof(zipkinConfig));
            if (context == null) throw new ArgumentNullException(nameof(context));
            var traceProvider = new TraceProvider(zipkinConfig, context);
            IsTraceOn = !zipkinConfig.Bypass(context.Request) && IsTraceProviderSamplingOn(traceProvider);

            if (!IsTraceOn)
                return;

            zipkinConfig.Validate();
            ZipkinConfig = zipkinConfig;
            logger = new LoggerFactory().CreateLogger<ZipkinClient>();

            try
            {
                spanCollector = collector ?? GetInstance(
                    zipkinConfig.ZipkinBaseUri,
                    zipkinConfig.SpanProcessorBatchSize);

                spanCollector.Start();

                spanTracer = new SpanTracer(
                    spanCollector,
                    new ServiceEndpoint(),
                    zipkinConfig.NotToBeDisplayedDomainList,
                    zipkinConfig.Domain(context.Request));

                TraceProvider = traceProvider;
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, "Error Building Zipkin Client Provider");
                IsTraceOn = false;
            }
        }

        public async Task<Span> StartClientTrace(Uri remoteUri, string methodName, ITraceProvider trace)
        {
            if (!IsTraceOn)
                return null;

            try
            {
                return await spanTracer.SendClientSpan(
                    methodName.ToLower(),
                    trace.TraceId,
                    trace.ParentSpanId,
                    trace.SpanId,
                    remoteUri);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, "Error Starting Client Trace");
                return null;
            }
        }

        public void EndClientTrace(Span clientSpan, int statusCode)
        {
            if (!IsTraceOn)
                return;

            try
            {
                spanTracer.ReceiveClientSpan(clientSpan, statusCode);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, "Error Ending Client Trace");
            }
        }

        public async Task<Span> StartServerTrace(Uri requestUri, string methodName)
        {
            if (!IsTraceOn)
                return null;

            try
            {
                return await spanTracer.ReceiveServerSpan(
                    methodName.ToLower(),
                    TraceProvider.TraceId,
                    TraceProvider.ParentSpanId,
                    TraceProvider.SpanId,
                    requestUri);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, "Error Starting Server Trace");
                return null;
            }
        }

        public void EndServerTrace(Span serverSpan)
        {
            if (!IsTraceOn)
                return;

            try
            {
                spanTracer.SendServerSpan(serverSpan);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, "Error Ending Server Trace");
            }
        }

        /// <summary>
        /// Records an annotation with the current timestamp and the provided value in the span.
        /// </summary>
        /// <param name="span">The span where the annotation will be recorded.</param>
        /// <param name="value">The value of the annotation to be recorded. If this parameter is omitted
        /// (or its value set to null), the method caller member name will be automatically passed.</param>
        public async Task Record(Span span, [CallerMemberName] string value = null)
        {
            if (!IsTraceOn)
                return;

            try
            {
                await spanTracer.Record(span, value);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, "Error recording the annotation");
            }
        }

        /// <summary>
        /// Records a key-value pair as a binary annotiation in the span.
        /// </summary>
        /// <typeparam name="T">The type of the value to be recorded. See remarks for the currently supported types.</typeparam>
        /// <param name="span">The span where the annotation will be recorded.</param>
        /// <param name="key">The key which is a reference to the recorded value.</param>
        /// <param name="value">The value of the annotation to be recorded.</param>
        /// <remarks>The RecordBinary will record a key-value pair which can be used to tag some additional information
        /// in the trace without any timestamps. The currently supported value types are <see cref="bool"/>,
        /// <see cref="byte[]"/>, <see cref="short"/>, <see cref="int"/>, <see cref="long"/>, <see cref="double"/> and
        /// <see cref="string"/>. Any other types will be passed as string annotation types.
        /// 
        /// Please note, that although the values have types, they will be recorded and sent by calling their
        /// respective ToString() method.</remarks>
        public async Task RecordBinary<T>(Span span, string key, T value)
        {
            if (!IsTraceOn)
                return;

            try
            {
                await spanTracer.RecordBinary(span, key, value);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, $"Error recording a binary annotation (key: {key})");
            }
        }

        /// <summary>
        /// Records a local component annotation in the span.
        /// </summary>
        /// <param name="span">The span where the annotation will be recorded.</param>
        /// <param name="value">The value of the local trace to be recorder.</param>
        public async Task RecordLocalComponent(Span span, string value)
        {
            if (!IsTraceOn)
                return;

            try
            {
                await spanTracer.RecordBinary(span, ZipkinConstants.LocalComponent, value);
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(0), ex, $"Error recording local trace (value: {value})");
            }
        }

        public void ShutDown()
        {
            if (spanCollector != null)
            {
                spanCollector.Stop();
            }
        }

        private bool IsTraceProviderSamplingOn(ITraceProvider traceProvider)
        {
            return !string.IsNullOrEmpty(traceProvider.TraceId) && traceProvider.IsSampled;
        }
    }
}
