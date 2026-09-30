namespace RepairShop.Application.Common;

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
