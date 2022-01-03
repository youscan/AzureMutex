using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace AzureMutex;

public sealed class AutoRenewedLease : IAsyncDisposable
{
    readonly Lease lease;
    readonly PeriodicTimer timer;
    readonly Task renew;
    readonly CancellationTokenSource leaseLostSource;

    public AutoRenewedLease(Lease lease)
    {
        this.lease = lease;
        timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        renew = Renew();
        leaseLostSource = new CancellationTokenSource();
    }

    public CancellationToken LeaseLost => leaseLostSource.Token;

    async Task Renew()
    {
        while (await timer.WaitForNextTickAsync())
        {
            try
            {
                await lease.Renew();
            }
            catch (Exception e)
            {
                leaseLostSource.Cancel(); // Notify caller code that the lease was lost
                timer.Dispose(); // Stop trying to renew

                // There is no much sense in throwing an exception here as it would
                // only be handled by a Dispose. Instead we notify client with with
                // a `LeaseLost` cancellation token.
                Trace.TraceError("Failed to auto renew lease. Error details:\n{0}", e);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        timer.Dispose();
        await renew;
        try
        {
            await lease.DisposeAsync();
        }
        catch (Exception e)
        {
            // Swallow exception. We could not do much here. But throwing an
            // exception would likely result in an exception within a finally
            // block of using statement. Which could result in a lost exception
            // from client code.
            Trace.TraceError("Failed to release lease. Error details:\n{0}", e);
        }
    }
}

