# Distributed mutex based on Azure Blobs

[![CI](https://github.com/youscan/AzureMutex/actions/workflows/workflow.yml/badge.svg)](https://github.com/youscan/AzureMutex/actions/workflows/workflow.yml) [![NuGet](https://img.shields.io/nuget/v/AzureMutex.svg?style=flat)](https://www.nuget.org/packages/AzureMutex/)

.Net implementation of a distributed lock on top of Azure Blobs. 

**Diclaimer**: if you think you need it, you might doing it wrong :) 

Jokes aside, we do use it for some cases. Typical example involves a process running on several nodes (for redundancy). E.g. periodic sync with a 3rd party system, a database migration, etc.

## Recommended usage

Recommended usage is via an extension method that takes a long-running task as a parameter and ensures it runs as a single instance (in a single process):

```csharp
var mutex = new BlobMutex("storage-account-connection", "mutex-container", "blob-name");

// 1. Blocks until lock is held or `CancellationToken` cancelled
// 2. Executes `LongRunningTask` while periodically renewing the lock taken. 
// 3. Runs either until operation is finished or `CancellationToken` canceled
// 4. Terminates operation in case the lock is lost (e.g. because of connectivity issues)
//    with a `LeaseLostException`
await mutex.RunSingleInstance(LongRunningTask, CancellationToken.None); 

async Task LongRunningTask(CancellationToken cancellation) {
    do {
        await Task.Delay(TimeSpan.FromMinutes(10), cancellation);
    }
    while (!cancellation.IsCancellationRequested)
}

```


## References

* [How to do distributed locking](https://martin.kleppmann.com/2016/02/08/how-to-do-distributed-locking.html) by Martin Kleppmann
* [Azure / Architecture / Cloud Design Patterns / Leader Election pattern](https://docs.microsoft.com/en-us/azure/architecture/patterns/leader-election)
