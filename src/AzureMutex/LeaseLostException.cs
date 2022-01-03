using System;

namespace AzureMutex;

[Serializable]
public class LeaseLostException : Exception
{
    public LeaseLostException() : this("Lease was lost") { }
    public LeaseLostException(string message) : base(message) { }
    public LeaseLostException(string message, Exception inner) : base(message, inner) { }
}
