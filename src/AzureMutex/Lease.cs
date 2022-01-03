using System;
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
            await mutex.Release(this);
            disposed = true;
        }
    }
}
