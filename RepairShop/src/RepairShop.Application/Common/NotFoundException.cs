namespace RepairShop.Application.Common;

public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
