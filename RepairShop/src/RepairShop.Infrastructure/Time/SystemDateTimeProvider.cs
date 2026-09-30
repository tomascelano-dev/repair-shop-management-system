using RepairShop.Application.Abstractions;

namespace RepairShop.Infrastructure.Time;

public sealed class SystemDateTimeProvider : IDateTimeProvider
{
    public DateTime UtcNow => DateTime.UtcNow;
}
