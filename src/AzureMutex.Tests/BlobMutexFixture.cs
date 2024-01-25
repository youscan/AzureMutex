using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using NUnit.Framework;

namespace AzureMutex.Tests;

public class BlobMutexFixture
{
    [Test]
    public async Task Creates_underlying_blob_automatically()
    {
        var name = Guid.NewGuid().ToString();
        var mutex = new BlobMutex(Container(), name);
        var blob = Container().GetBlobClient(name);

        try
        {
            await using var lease = await mutex.Acquire();
            await Assert.ThatAsync( () => blob.ExistsAsync(), Is.True);
        }
        finally
        {
            await blob.DeleteIfExistsAsync();
        }
    }

    [Test]
    public async Task When_acquiring_concurrent_lock()
    {
        await using var lease = await Mutex().Acquire();

        var concurrent = Mutex();
        Assert.ThrowsAsync<ConcurrentAccessException>(() => concurrent.Acquire());
    }

    [Test]
    public async Task When_releasing()
    {
        var lease = await Mutex().Acquire();
        await lease.DisposeAsync();
        Assert.DoesNotThrowAsync(async () => await lease.DisposeAsync());

        await using var concurrentLease = await Mutex().TryAcquire();
        Assert.That(concurrentLease, Is.Not.Null);
    }

    [Test]
    public async Task When_renewing()
    {
        await using var lease = await Mutex().Acquire();
        await lease.Renew();

        Assert.ThrowsAsync<ConcurrentAccessException>(() => Mutex().Acquire());
    }

    static BlobMutex Mutex() => new(Container(), "mutex");
    static BlobContainerClient Container() => new("UseDevelopmentStorage=true", "mutex-container");
}

