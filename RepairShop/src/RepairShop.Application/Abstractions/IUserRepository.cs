using RepairShop.Domain.Users;

namespace RepairShop.Application.Abstractions;

public interface IUserRepository
{
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct);
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(AppUser user, CancellationToken ct);
    Task<bool> AnyAsync(CancellationToken ct);
}
