using System;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace AzureMutex
{
    public class BlobMutex
    {
        readonly BlobContainerClient container;
        readonly BlobClient blob;

        public BlobMutex(string connectionString, string containerName, string key) :
            this(new BlobContainerClient(connectionString, containerName), key) { }

        public BlobMutex(BlobContainerClient container, string key)
        {
            this.container = container;
            blob = container.GetBlobClient(key);
        }

        public async Task<Lease> Acquire()
        {
            try
            {
                var leaseId = (await blob.GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(60))).Value.LeaseId;
                return new Lease(leaseId, this);
            }
            catch (RequestFailedException e) when (e.ErrorCode == BlobErrorCode.BlobNotFound || e.ErrorCode == BlobErrorCode.ContainerNotFound)
            {
                await Init();
                return await Acquire();
            }
            catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.Conflict)
            {
                throw new ConcurrentAccessException("Failed to acquire lock. It's already taken");
            }
        }

        public async Task Renew(Lease lease) => await blob.GetBlobLeaseClient(lease.Id).RenewAsync();
        public async Task Release(Lease lease) => await blob.GetBlobLeaseClient(lease.Id).ReleaseAsync();

        async Task Init()
        {
            await container.CreateIfNotExistsAsync();
            try
            {
                await blob.UploadAsync(BinaryData.FromString(""), new BlobUploadOptions
                {
                    Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
                });
            }
            catch (RequestFailedException ignore) when (Leased(ignore)) { }

            static bool Leased(RequestFailedException ex) =>
                ex.ErrorCode == BlobErrorCode.ConditionNotMet ||
                ex.ErrorCode == BlobErrorCode.LeaseIdMissing;
        }
    }

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

    [Serializable]
    public class ConcurrentAccessException : Exception
    {
        public ConcurrentAccessException() { }
        public ConcurrentAccessException(string message) : base(message) { }
        public ConcurrentAccessException(string message, Exception innerException) : base(message, innerException) { }
    }
}
