using System;
using System.Threading;
using System.Threading.Tasks;

namespace AzureMutex
{
    public static class BlobMutexExtensions
    {
        public static async Task<IAsyncDisposable?> TryAcquireAutoRenewed(this BlobMutex mutex)
        {
            try
            {
                return new AutoRenewedLease(await mutex.Acquire());
            }
            catch (ConcurrentAccessException)
            {
                return null;
            }
        }

        static async Task<IAsyncDisposable> AcquireAutoRenewed(this BlobMutex mutex, TimeSpan checkInterval, CancellationToken cancellation)
        {
            using var timer = new PeriodicTimer(checkInterval);
            do
            {
                var lease = await mutex.TryAcquireAutoRenewed();
                if (lease != null)
                    return lease;
            }
            while (await timer.WaitForNextTickAsync(cancellation));

            throw new OperationCanceledException(cancellation);
        }

        /// <summary>
        /// Ensures <see cref="job"/> is run on single node by acquiring a distributed mutex lease.
        /// It tries to acquire lease forever, until its available.
        /// </summary>
        /// <param name="job">Function to run</param>
        /// <param name="mutex">Distributed mutex to lock the execution on a single node.</param>
        /// <param name="checkInterval">How often to check that lease is available. Defaults to 1 minute interval</param>
        /// <returns>A wrapped function that runs on a single node.</returns>
        public static Func<CancellationToken, Task> EnsureSingleInstance(this Func<CancellationToken, Task> job,
            BlobMutex mutex, TimeSpan? checkInterval = null) =>
            c => mutex.RunSingleInstance(job, checkInterval ?? TimeSpan.FromMinutes(1), c);

        public static async Task RunSingleInstance(this BlobMutex mutex, Func<CancellationToken, Task> func, TimeSpan checkInterval, CancellationToken cancellation)
        {
            await using var lease = await mutex.AcquireAutoRenewed(checkInterval, cancellation);
            await func(cancellation);
        }
    }
}
