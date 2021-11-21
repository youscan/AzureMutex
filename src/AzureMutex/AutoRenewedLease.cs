using System;
using System.Threading;
using System.Threading.Tasks;

namespace AzureMutex
{
    sealed class AutoRenewedLease : IAsyncDisposable
    {
        readonly CancellationTokenSource cts = new();
        readonly Lease lease;
        readonly PeriodicTimer timer;
        readonly Task renew;

        public AutoRenewedLease(Lease lease)
        {
            this.lease = lease;
            timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
            renew = Renew();
        }

        async Task Renew()
        {
            try
            {
                while (await timer.WaitForNextTickAsync(cts.Token))
                    await lease.Renew();
            }
            catch (OperationCanceledException) { }
        }

        public async ValueTask DisposeAsync()
        {
            cts.Cancel();
            timer.Dispose();
            try
            {
                await renew;
            }
            finally
            {
                await lease.DisposeAsync();
                cts.Dispose();
            }
        }
    }
}
