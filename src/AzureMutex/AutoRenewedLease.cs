using System;
using System.Threading;
using System.Threading.Tasks;

namespace AzureMutex
{
    sealed class AutoRenewedLease : IAsyncDisposable
    {
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
            while (await timer.WaitForNextTickAsync())
                await lease.Renew();
        }

        public async ValueTask DisposeAsync()
        {
            timer.Dispose();
            try
            {
                await renew;
            }
            finally
            {
                await lease.DisposeAsync();
            }
        }
    }
}
