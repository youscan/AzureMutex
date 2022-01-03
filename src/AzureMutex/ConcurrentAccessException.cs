using System;

namespace AzureMutex;

[Serializable]
public class ConcurrentAccessException : Exception
{
    public ConcurrentAccessException() { }
    public ConcurrentAccessException(string message) : base(message) { }
    public ConcurrentAccessException(string message, Exception innerException) : base(message, innerException) { }
}
