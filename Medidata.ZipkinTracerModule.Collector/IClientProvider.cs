﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracerModule.Collector
{
    public interface IClientProvider
    {
        void Setup();
        void Close();
    }
}