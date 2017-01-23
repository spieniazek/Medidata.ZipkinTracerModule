using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Medidata.ZipkinTracer.Core.Test
{
    public static class TestHelper
    {
        public static void IgnoreAwait(this Task task)
        {
        }
    }
}
