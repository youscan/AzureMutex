using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using NUnit.Framework;

namespace AzureMutex.Tests;

public class AutoRenewedLeaseFixture
{
    [OneTimeSetUp]
    public void OneTimeSetup() => Trace.Listeners.Add(new ConsoleTraceListener());

    [OneTimeSetUp]
    public void OneTimeTearDown() => Trace.Flush();

    [Test]
    [Category("Slow")]
    public async Task When_acquiring_auto_renewed()
    {
        await using var _ = await Mutex().TryAcquireAutoRenewed();

        await Task.Delay(TimeSpan.FromSeconds(60)); // default lease time

        await using var concurrentLease = await Mutex().TryAcquireAutoRenewed();
        Assert.Null(concurrentLease);
    }

    [Test]
    public async Task When_releasing_auto_renewed()
    {
        var lease = await Mutex().TryAcquireAutoRenewed();
        await lease!.DisposeAsync();
        Assert.DoesNotThrowAsync(async () => await lease.DisposeAsync());

        await using var concurrentLease = await Mutex().TryAcquireAutoRenewed();
        Assert.NotNull(concurrentLease);
    }

    [Test]
    public async Task When_loosing_lease()
    {
        var started = false;
        var task = Mutex().RunSingleInstance(LongRunningTask, CancellationToken.None);

        SpinWait.SpinUntil(() => started, TimeSpan.FromSeconds(2)); // Wait until long-running operation is started
        await Container().DeleteAsync(); // Simulate a trouble (e.g. network fails or storage is not available)

        Assert.ThrowsAsync<LeaseLostException>(() => task, "A dedicated exception, so that client code could decide what to do");

        async Task LongRunningTask(CancellationToken cancellation)
        {
            started = true;
            await Task.Delay(TimeSpan.FromSeconds(20), cancellation); // Auto-renew happens every 15 sec
        }
    }

    static BlobMutex Mutex() => new(Container(), "mutex");
    static BlobContainerClient Container() => new("UseDevelopmentStorage=true", "mutex-container");
}
