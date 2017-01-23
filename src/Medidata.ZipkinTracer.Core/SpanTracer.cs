﻿using System;
using System.Collections.Generic;
using System.Linq;
using Medidata.ZipkinTracer.Models;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core
{
    public class SpanTracer
    {
        private SpanCollector spanCollector;
        private string serviceName;
        private ushort servicePort;
        private ServiceEndpoint zipkinEndpoint;
        private IEnumerable<string> zipkinNotToBeDisplayedDomainList;

        public SpanTracer(SpanCollector spanCollector, ServiceEndpoint zipkinEndpoint, IEnumerable<string> zipkinNotToBeDisplayedDomainList, Uri domain)
        {
            if (spanCollector == null) throw new ArgumentNullException(nameof(spanCollector));
            if (zipkinEndpoint == null) throw new ArgumentNullException(nameof(zipkinEndpoint));
            if (zipkinNotToBeDisplayedDomainList == null) throw new ArgumentNullException(nameof(zipkinNotToBeDisplayedDomainList));
            if (domain == null) throw new ArgumentNullException(nameof(domain));

            this.spanCollector = spanCollector;
            this.zipkinEndpoint = zipkinEndpoint;
            this.zipkinNotToBeDisplayedDomainList = zipkinNotToBeDisplayedDomainList;
            var domainHost = domain.Host;
            this.serviceName = CleanServiceName(domainHost);
            this.servicePort = (ushort)domain.Port;
        }

        public virtual async Task<Span> ReceiveServerSpan(string spanName, string traceId, string parentSpanId, string spanId, Uri requestUri)
        {
            var newSpan = CreateNewSpan(spanName, traceId, parentSpanId, spanId);
            var serviceEndpoint = await zipkinEndpoint.GetLocalEndpoint(serviceName, (ushort)requestUri.Port);

            var annotation = new Annotation()
            {
                Host = serviceEndpoint,
                Value = ZipkinConstants.ServerReceive
            };

            newSpan.Annotations.Add(annotation);

            AddBinaryAnnotation("http.path", requestUri.AbsolutePath, newSpan, serviceEndpoint);

            return newSpan;
        }

        public virtual void SendServerSpan(Span span)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            if (span.Annotations == null || !span.Annotations.Any())
            {
                throw new ArgumentException("Invalid server span: Annotations list is invalid.");
            }

            var annotation = new Annotation()
            {
                Host = span.Annotations.First().Host,
                Value = ZipkinConstants.ServerSend
            };

            span.Annotations.Add(annotation);

            spanCollector.Collect(span);
        }

        public virtual async Task<Span> SendClientSpan(string spanName, string traceId, string parentSpanId, string spanId, Uri remoteUri)
        {
            var newSpan = CreateNewSpan(spanName, traceId, parentSpanId, spanId);
            var serviceEndpoint = await zipkinEndpoint.GetLocalEndpoint(serviceName, (ushort)remoteUri.Port);
            var clientServiceName = CleanServiceName(remoteUri.Host);

            var annotation = new Annotation
            {
                Host = serviceEndpoint,
                Value = ZipkinConstants.ClientSend
            };

            newSpan.Annotations.Add(annotation);
            AddBinaryAnnotation("http.path", remoteUri.AbsolutePath, newSpan, serviceEndpoint);
            AddBinaryAnnotation("sa", "1", newSpan, await zipkinEndpoint.GetRemoteEndpoint(remoteUri, clientServiceName));

            return newSpan;
        }

        private string CleanServiceName(string host)
        {
            foreach (var domain in zipkinNotToBeDisplayedDomainList)
            {
                if (host.Contains(domain))
                {
                    return host.Replace(domain, string.Empty);
                }
            }

            return host;
        }

        public virtual void ReceiveClientSpan(Span span, int statusCode)
        {
            if (span == null)
            {
                throw new ArgumentNullException(nameof(span));
            }

            if (span.Annotations == null || !span.Annotations.Any())
            {
                throw new ArgumentException("Invalid client span: Annotations list is invalid.");
            }

            var annotation = new Annotation()
            {
                Host = span.Annotations.First().Host,
                Value = ZipkinConstants.ClientReceive
            };

            span.Annotations.Add(annotation);
            AddBinaryAnnotation("http.status", statusCode.ToString(), span, span.Annotations.First().Host);

            spanCollector.Collect(span);
        }

        public virtual async Task Record(Span span, string value)
        {
            if (span == null)
                throw new ArgumentNullException(nameof(span), "In order to record an annotation, the span must be not null.");

            span.Annotations.Add(new Annotation()
            {
                Host = await zipkinEndpoint.GetLocalEndpoint(serviceName, servicePort),
                Value = value
            });
        }

        public async Task RecordBinary<T>(Span span, string key, T value)
        {
            if (span == null)
                throw new ArgumentNullException(nameof(span), "In order to record a binary annotation, the span must be not null.");

            var host = await zipkinEndpoint.GetLocalEndpoint(serviceName, servicePort);
            span.Annotations.Add(new BinaryAnnotation()
            {
                Host = host,
                Key = key,
                Value = value
            });
        }

        internal static Span CreateNewSpan(string spanName, string traceId, string parentSpanId, string spanId)
        {
            return new Span
            {
                Name = spanName,
                TraceId = traceId,
                ParentId = parentSpanId,
                Id = spanId
            };
        }

        private void AddBinaryAnnotation<T>(string key, T value, Span span, Endpoint endpoint)
        {
            var binaryAnnotation = new BinaryAnnotation()
            {
                Host = endpoint,
                Key = key,
                Value = value
            };

            span.Annotations.Add(binaryAnnotation);
        }
    }
}
