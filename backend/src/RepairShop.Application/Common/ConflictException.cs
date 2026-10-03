namespace RepairShop.Application.Common;

/// <summary>409: the operation conflicts with the current state (concurrency, duplicates, in-use records).</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
    public ConflictException(string message, Exception inner) : base(message, inner) { }
}
