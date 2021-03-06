﻿using WkHtmlToPdfDotNet.Contracts;
using System;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace WkHtmlToPdfDotNet
{
    public class SynchronizedConverter : BasicConverter, IDisposable
    {
        private readonly BlockingCollection<Task> conversions = new BlockingCollection<Task>();
        private readonly Task runningTask;
        private readonly CancellationTokenSource cts = new CancellationTokenSource();

        public SynchronizedConverter(ITools tools) : base(tools)
        {
            this.runningTask = Task.Run(() =>
            {
                while (!this.cts.IsCancellationRequested)
                {
                    try
                    {
                        var task = this.conversions.Take(this.cts.Token);

                        task.RunSynchronously();
                    }
                    catch
                    {
                    }
                }
            });
        }

        public override byte[] Convert(IDocument document)
        {
            return Invoke(() => base.Convert(document));
        }

        public TResult Invoke<TResult>(Func<TResult> @delegate)
        {
            var task = new Task<TResult>(@delegate);

            this.conversions.Add(task);

            task.Wait();

            if (task.Exception != null)
            {
                throw task.Exception;
            }

            return task.Result;
        }

        public void Dispose()
        {
            this.cts.Cancel();
            this.runningTask.Wait();
            this.runningTask.Dispose();
            this.conversions.Dispose();
        }
    }
}
