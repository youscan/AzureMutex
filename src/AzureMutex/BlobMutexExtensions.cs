using System;
using System.Threading;
using System.Threading.Tasks;

namespace AzureMutex;

public static class BlobMutexExtensions
{
    public static async Task<Lease?> TryAcquire(this BlobMutex mutex)
    {
        try
        {
            return await mutex.Acquire();
        }
        catch (ConcurrentAccessException)
        {
            return null;
        }
    }

    public static async Task<Lease> AcquireOrWait(this BlobMutex mutex,
        CancellationToken cancellation, TimeSpan? acquireAttemptInterval = null)
    {
        using var timer = new PeriodicTimer(acquireAttemptInterval ?? TimeSpan.FromMinutes(1));
        do
        {
            var lease = await mutex.TryAcquire();
            if (lease != null)
                return lease;
        } while (!cancellation.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellation));

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
    public static Func<CancellationToken, Task> EnsureSingleInstance(
        this Func<CancellationToken, Task> job,
        BlobMutex mutex, TimeSpan? checkInterval = null) =>
        c => mutex.RunSingleInstance(job, c, checkInterval ?? TimeSpan.FromMinutes(1));

    public static async Task RunSingleInstance(
        this BlobMutex mutex, Func<CancellationToken, Task> func, CancellationToken cancellation, TimeSpan? checkInterval = null)
    {
        await using var lease = await mutex.AcquireOrWait(cancellation, checkInterval);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var main = func(cts.Token);
        var renew = AutoRenew(lease, cts.Token);

        await RunUntilAnyCompletes(main, renew, cts);

        static async Task RunUntilAnyCompletes(Task main, Task renew, CancellationTokenSource leaseCts)
        {
            // Run until any finishes
            await Task.WhenAny(main, renew);

            // Cancel main or renew lease task
            leaseCts.Cancel();

            try
            {
                // Ensure all completed
                await Task.WhenAll(main, renew);
            }
            catch (Exception)
            {
                try
                {
                    // Either throws LeaseLostException which caused the trouble.
                    // Or ignores OperationCanceledException to not lost possible
                    // exception thrown by main func
                    await renew;
                }
                catch (OperationCanceledException) { }

                // We're most interested in the exception of the main func. Ensure it iss not lost
                await main;
            }
        }

        static async Task AutoRenew(Lease lease, CancellationToken cancellation)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            while (!cancellation.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellation))
            {
                try
                {
                    await lease.Renew();
                }
                catch (Exception e)
                {
                    throw new LeaseLostException("Lease was lost", e);
                }
            }
        }
    }
}
