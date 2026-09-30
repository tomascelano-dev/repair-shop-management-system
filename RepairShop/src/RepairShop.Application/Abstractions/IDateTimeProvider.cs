namespace RepairShop.Application.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
