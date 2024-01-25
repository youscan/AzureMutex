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
    public async Task When_auto_renewing_lease()
    {
        var cts = new CancellationTokenSource();
        var task = Mutex().RunSingleInstance(c => Task.Delay(TimeSpan.FromSeconds(100), c), cts.Token);

        await Task.Delay(TimeSpan.FromSeconds(65)); // 60 sec is a default lease time

        Assert.That( task.IsCompleted, Is.False);

        await using var concurrentLease = await Mutex().TryAcquire();
        Assert.That(concurrentLease, Is.Null, "Lease is held due to auto renew");

        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(() => task);
    }

    [Test]
    public async Task When_operation_completes()
    {
        var completes = Mutex().RunSingleInstance(_ => Task.CompletedTask, CancellationToken.None);
        Assert.DoesNotThrowAsync(() => completes, "should not throw");

        await AssertLeaseReleased();
    }

    [Test]
    public async Task When_operation_fails()
    {
        var fails = Mutex().RunSingleInstance(_ => throw new DivideByZeroException(), CancellationToken.None);
        Assert.ThrowsAsync<DivideByZeroException>(() => fails, "should propagate original exception");

        await AssertLeaseReleased();
    }

    [Test]
    public async Task When_operation_canceled()
    {
        var cts = new CancellationTokenSource();
        var canceled = Mutex().RunSingleInstance(c => Task.Delay(TimeSpan.FromSeconds(2), c), cts.Token);

        cts.Cancel();
        Assert.ThrowsAsync<TaskCanceledException>(() => canceled, "should propagate original exception");

        await AssertLeaseReleased();
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

    static async Task AssertLeaseReleased()
    {
        await using var lease = await Mutex().Acquire();
        Assert.That(lease, Is.Not.Null, "should release lease");
    }

    static BlobMutex Mutex() => new(Container(), "mutex");
    static BlobContainerClient Container() => new("UseDevelopmentStorage=true", "mutex-container");
}
