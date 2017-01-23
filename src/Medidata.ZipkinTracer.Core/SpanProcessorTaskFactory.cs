using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Medidata.ZipkinTracer.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace Medidata.ZipkinTracer.Core
{
    public class SpanProcessorTaskFactory
    {
        private Task spanProcessorTaskInstance;
        private CancellationTokenSource cancellationTokenSource;
        private ILogger<SpanProcessorTaskFactory> _logger;
        private const int defaultDelayTime = 500;
        private const int encounteredAnErrorDelayTime = 30000;

        readonly object sync = new object();

        public SpanProcessorTaskFactory(ILogger<SpanProcessorTaskFactory> logger, CancellationTokenSource cancellationTokenSource)
        {
            _logger = logger ?? new LoggerFactory().CreateLogger<SpanProcessorTaskFactory>(); 
            this.cancellationTokenSource = cancellationTokenSource ?? new CancellationTokenSource();
        }

        public SpanProcessorTaskFactory()
            :this(new LoggerFactory().CreateLogger<SpanProcessorTaskFactory>() , new CancellationTokenSource())
        {
        }

        public virtual void CreateAndStart(Func<Task> taskFunc)
        {
            SyncHelper.ExecuteSafely(sync, () => spanProcessorTaskInstance == null || spanProcessorTaskInstance.Status == TaskStatus.Faulted,
                () =>
                {
                    spanProcessorTaskInstance = Task.Factory.StartNew(() => TaskWrapper(taskFunc), cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                });
        }

        public virtual void StopTask()
        {
            SyncHelper.ExecuteSafely(sync, () => cancellationTokenSource.Token.CanBeCanceled, () => cancellationTokenSource.Cancel());
        }

        internal async Task TaskWrapper(Func<Task> taskFunc)
        {
            while (!IsTaskCancelled())
            {
                int delayTime = defaultDelayTime;
                try
                {
                    await taskFunc();
                }
                catch (Exception ex)
                {
                    _logger.LogError(new EventId(0), ex, "Error in SpanProcessorTask");
                    delayTime = encounteredAnErrorDelayTime;
                }

                // stop loop if task is cancelled while delay is in process
                try
                {
                    await Task.Delay(delayTime, cancellationTokenSource.Token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                
            }
        }

        public virtual bool IsTaskCancelled()
        {
            return cancellationTokenSource.IsCancellationRequested;
        }
    }
}
