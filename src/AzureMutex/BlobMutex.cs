using System;
using System.Net;
using System.Threading.Tasks;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace AzureMutex;

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
            return await DoAcquire();
        }
        catch (RequestFailedException e) when (e.ErrorCode == BlobErrorCode.BlobNotFound ||
                                               e.ErrorCode == BlobErrorCode.ContainerNotFound)
        {
            await Init();
            return await DoAcquire();
        }
        catch (RequestFailedException e) when (e.Status == (int)HttpStatusCode.Conflict)
        {
            throw new ConcurrentAccessException("Failed to acquire lock. It's already taken");
        }
    }

    public async Task Renew(Lease lease) => await blob.GetBlobLeaseClient(lease.Id).RenewAsync();
    public async Task Release(Lease lease) => await blob.GetBlobLeaseClient(lease.Id).ReleaseAsync();

    async Task<Lease> DoAcquire()
    {
        var leaseId = (await blob.GetBlobLeaseClient().AcquireAsync(TimeSpan.FromSeconds(60))).Value.LeaseId;
        return new Lease(leaseId, this);
    }

    async Task Init()
    {
        await container.CreateIfNotExistsAsync();
        try
        {
            await blob.UploadAsync(BinaryData.FromString(""),
                new BlobUploadOptions { Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All } });
        }
        catch (RequestFailedException ignore) when (Leased(ignore)) { }

        static bool Leased(RequestFailedException ex) =>
            ex.ErrorCode == BlobErrorCode.ConditionNotMet ||
            ex.ErrorCode == BlobErrorCode.LeaseIdMissing;
    }
}
