namespace RepairShop.Application.Common;

/// <summary>403: authenticated but not allowed (role/shop restrictions).</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
