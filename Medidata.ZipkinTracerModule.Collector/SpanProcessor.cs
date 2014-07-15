﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Thrift.Protocol;

namespace Medidata.ZipkinTracerModule.Collector
{
    public class SpanProcessor
    {
        private List<LogEntry> logEntries;
        private TBinaryProtocol.Factory protocolFactory;

        private int MAX_BATCH_SIZE = 20;
        private BlockingCollection<Span> spanQueue;
        private IClientProvider cliendProvider;

        public SpanProcessor(BlockingCollection<Span> spanQueue, IClientProvider cliendProvider)
        {
            this.spanQueue = spanQueue;
            this.cliendProvider = cliendProvider;
            logEntries = new List<LogEntry>(MAX_BATCH_SIZE);
            protocolFactory = new TBinaryProtocol.Factory();
        }

        public virtual void Start()
        {
        }

        public virtual void Stop()
        {
            throw new NotImplementedException();
        }
    }
}