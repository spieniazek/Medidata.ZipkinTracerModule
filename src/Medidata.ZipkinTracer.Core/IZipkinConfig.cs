using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;

namespace Medidata.ZipkinTracer.Core
{
    public interface IZipkinConfig
    {
        Predicate<HttpRequest> Bypass { get; set; }

        Uri ZipkinBaseUri { get; set; }

        Func<HttpRequest, Uri> Domain { get; set; }

        uint SpanProcessorBatchSize { get; set; }

        IList<string> ExcludedPathList { get; set; }

        double SampleRate { get; set; }

        IList<string> NotToBeDisplayedDomainList { get; set; }

        bool Create128BitTraceId { get; set; }

        bool ShouldBeSampled(string sampled, string requestPath);

        void Validate();
    }
}