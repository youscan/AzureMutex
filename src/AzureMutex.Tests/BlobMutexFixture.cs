using System;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using NUnit.Framework;

namespace AzureMutex.Tests
{
    public class BlobMutexFixture
    {
        [Test]
        public async Task Creates_underlying_blob_automatically()
        {
            var container = new BlobContainerClient(AzureStorageConnectionString(), "mutex");
            var name = Guid.NewGuid().ToString();
            var mutex = new BlobMutex(container, name);
            var blob = container.GetBlobClient(name);

            try
            {
                await using var lease = await mutex.Acquire();
                Assert.True(await blob.ExistsAsync());
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

            Assert.Null(await Mutex().TryAcquireAutoRenewed());
        }

        [Test]
        public async Task When_releasing()
        {
            var lease = await Mutex().Acquire();
            await lease.DisposeAsync();
            Assert.DoesNotThrowAsync(async () => await lease.DisposeAsync());

            await using var concurrentLease = await Mutex().TryAcquireAutoRenewed();
            Assert.NotNull(concurrentLease);
        }

        [Test]
        public async Task When_renewing()
        {
            await using var lease = await Mutex().Acquire();
            await lease.Renew();

            Assert.ThrowsAsync<ConcurrentAccessException>(() => Mutex().Acquire());
        }

        [Test]
        [Category("Slow")]
        public async Task When_acquiring_auto_renewed()
        {
            await using var _ = await Mutex().TryAcquireAutoRenewed();

            await Task.Delay(TimeSpan.FromSeconds(60)); // default lease time

            await using var concurrentLease = await Mutex().TryAcquireAutoRenewed();
            Assert.Null(concurrentLease);
        }

        static BlobMutex Mutex()
        {
            var container = new BlobContainerClient(AzureStorageConnectionString(), "mutex");
            return new BlobMutex(container, "mutex");
        }

        static string AzureStorageConnectionString() => "UseDevelopmentStorage=true";
    }
}
