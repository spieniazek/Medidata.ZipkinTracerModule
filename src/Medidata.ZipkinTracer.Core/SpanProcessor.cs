using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Medidata.ZipkinTracer.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core
{
    public class SpanProcessor
    {
        //send contents of queue if it has pending items but less than max batch size after doing max number of polls
        internal const int MAX_NUMBER_OF_POLLS = 5;
        internal const string ZIPKIN_SPAN_POST_PATH = "/api/v1/spans";

        private readonly Uri uri;
        internal BlockingCollection<Span> spanQueue;

        //using a queue because even as we pop items to send to zipkin, another 
        //thread can be adding spans if someone shares the span processor accross threads
        internal ConcurrentQueue<JsonSpan> serializableSpans;
        internal SpanProcessorTaskFactory spanProcessorTaskFactory;

        internal int subsequentPollCount;
        internal uint maxBatchSize;
        private readonly ILogger<SpanProcessor> logger;

        public SpanProcessor(Uri uri, BlockingCollection<Span> spanQueue, uint maxBatchSize)
        {
            if (spanQueue == null)
            {
                throw new ArgumentNullException("spanQueue");
            }

            if (uri == null)
            {
                throw new ArgumentNullException("uri");
            }

            this.uri = uri;
            this.spanQueue = spanQueue;
            this.serializableSpans = new ConcurrentQueue<JsonSpan>();
            this.maxBatchSize = maxBatchSize;
            this.logger = new LoggerFactory().CreateLogger<SpanProcessor>();
            spanProcessorTaskFactory = new SpanProcessorTaskFactory();
        }

        public virtual async Task Stop()
        {
            spanProcessorTaskFactory.StopTask();
            await LogSubmittedSpans();
        }

        public virtual void Start()
        {
            spanProcessorTaskFactory.CreateAndStart(() => LogSubmittedSpans());
        }

        internal virtual async Task LogSubmittedSpans()
        {
            var anyNewSpans = ProcessQueuedSpans();

            if (anyNewSpans) subsequentPollCount = 0;
            else if (serializableSpans.Count > 0) subsequentPollCount++;

            if (ShouldSendQueuedSpansOverWire())
            {
                await SendSpansOverHttp();
            }
        }

        public virtual async Task SendSpansToZipkin(string spans)
        {
            if (spans == null) throw new ArgumentNullException("spans");
            using (var client = new HttpClient())
            {
                try
                {
                    client.BaseAddress = uri;
                    var response = await client.PostAsync(ZIPKIN_SPAN_POST_PATH, new StringContent(spans));

                    if (!response.IsSuccessStatusCode)
                    {
                        logger.LogError("Failed to send spans to Zipkin server (HTTP status code returned: {0}). Response from server: {1}",
                            response.StatusCode, await response.Content.ReadAsStringAsync());
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(new EventId(0), ex, ex.Message);
                }
            }
        }

        private bool ShouldSendQueuedSpansOverWire()
        {
            return serializableSpans.Any() &&
                   (serializableSpans.Count() >= maxBatchSize
                   || spanProcessorTaskFactory.IsTaskCancelled()
                   || subsequentPollCount > MAX_NUMBER_OF_POLLS);
        }

        private bool ProcessQueuedSpans()
        {
            Span span;
            var anyNewSpansQueued = false;
            while (spanQueue.TryTake(out span))
            {
                serializableSpans.Enqueue(new JsonSpan(span));
                anyNewSpansQueued = true;
            }
            return anyNewSpansQueued;
        }

        private async Task SendSpansOverHttp()
        {
            var spansJsonRepresentation = GetSpansJSONRepresentation();
            await SendSpansToZipkin(spansJsonRepresentation);
            subsequentPollCount = 0;
        }

        private string GetSpansJSONRepresentation()
        {
            JsonSpan span;
            var spanList = new List<JsonSpan>();
            //using Dequeue into a list so that the span is removed from the queue as we add it to list
            while (serializableSpans.TryDequeue(out span))
            {
                spanList.Add(span);
            }
            var spansJsonRepresentation = JsonConvert.SerializeObject(spanList);
            return spansJsonRepresentation;
        }
    }
}
