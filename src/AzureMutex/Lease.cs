using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AzureMutex;

public sealed class Lease : IAsyncDisposable
{
    public readonly string Id;
    readonly BlobMutex mutex;
    bool disposed;

    public Lease(string id, BlobMutex mutex)
    {
        Id = id;
        this.mutex = mutex;
    }

    public async Task Renew() => await mutex.Renew(this);

    public async ValueTask DisposeAsync()
    {
        if (!disposed)
        {
            try
            {
                await mutex.Release(this);
            }
            catch (Exception e)
            {
                // Swallow exception. We could not do much here. But throwing an
                // exception could result in an exception within a finally block
                // of using statement. Which could result in a lost exception
                // from client code.
                Trace.TraceError("Failed to release lease. Error details:\n{0}", e);
            }

            disposed = true;
        }
    }
}
