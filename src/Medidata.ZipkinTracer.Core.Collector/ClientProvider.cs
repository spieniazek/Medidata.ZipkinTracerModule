﻿using System;
using System.Collections.Generic;
using Thrift.Protocol;
using Thrift.Transport;

namespace Medidata.ZipkinTracer.Core.Collector
{
    public class ClientProvider : IClientProvider
    {
        private readonly string host;
        private readonly int port;
        private TTransport transport;
        internal ZipkinCollector.Client Client;

        internal static ClientProvider instance = null;

        private ClientProvider(string host, int port) 
        {
            this.host = host;
            this.port = port;
        }

        public static ClientProvider GetInstance(string host, int port)
        {
            if (instance == null)
            {
                instance = new ClientProvider(host, port);
                try
                {
                    instance.Setup();
                }
                catch (Exception ex)
                {
                    instance.Close();
                    instance = null;
                    throw ex;
                }
            }
            return instance;
        }

        public void Setup()
        {
            var socket = new TSocket(host, port);
            transport = new TFramedTransport(socket);
            var protocol = new TBinaryProtocol(transport);
            Client = new ZipkinCollector.Client(protocol);
            transport.Open();
        }

        public void Close()
        {
            if (transport != null)
            {
                transport.Close();
                transport.Dispose();
            }
        }

        public void Log(List<LogEntry> logEntries)
        {
            Client.Log(logEntries);
        }
    }
}
