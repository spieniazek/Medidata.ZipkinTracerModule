using System;
using System.Runtime.CompilerServices;
using Medidata.ZipkinTracer.Models;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core
{
    public interface ITracerClient
    {
        bool IsTraceOn { get; }

        ITraceProvider TraceProvider { get; }

        Task<Span> StartServerTrace(Uri requestUri, string methodName);

        Task<Span> StartClientTrace(Uri remoteUri, string methodName, ITraceProvider trace);

        void EndServerTrace(Span serverSpan);

        void EndClientTrace(Span clientSpan, int statusCode);

        Task Record(Span span, [CallerMemberName] string value = null);

        Task RecordBinary<T>(Span span, string key, T value);

        Task RecordLocalComponent(Span span, string value);
    }
}
